namespace Eitan.SherpaONNXUnity.Samples
{
    using System.Threading.Tasks;
    using Eitan.Sherpa.Onnx.Unity.Mono.Components;
    using Eitan.Sherpa.Onnx.Unity.Mono.Inputs;
    using Eitan.SherpaONNXUnity.Runtime;
    using Eitan.SherpaONNXUnity.Runtime.Modules;

    using UnityEngine;
    using UnityEngine.UI;
    using static UnityEngine.UI.Dropdown;
    using Stage = Eitan.SherpaONNXUnity.Samples.ModelLoadProgressTracker.Stage;

    /// <summary>
    /// Audio tagging demo that highlights loading progress and live results.
    /// 音频标签示例，展示加载进度与实时结果。
    /// </summary>
    public sealed class AudioTaggingExample : MonoBehaviour
    {
        [Header("Sherpa Components")]
        [SerializeField] private AudioTaggingComponent taggingComponent;
        [SerializeField] private SherpaMicrophoneInput microphoneInput;

        [Header("UI Components")]
        [SerializeField] private Dropdown modelDropdown;
        [SerializeField] private Button modelLoadOrUnloadButton;
        [SerializeField] private Text statusText;
        [SerializeField] private Text tagResultText;
        [TextArea]

        [Header("Loading UI / Progress")]
        [SerializeField] private UI.EasyProgressBar progressBar;
        [SerializeField] private Text progressValueText;
        [SerializeField] private Text progressMessageText;

        [Header("Defaults")]
        [SerializeField] private string defaultModelID = "sherpa-onnx-ced-base-audio-tagging-2024-04-19";

        private bool modelRequested;
        private Color defaultButtonColor = Color.white;
        private ModelLoadProgressTracker progressTracker;

        private void Awake()
        {
            if (modelLoadOrUnloadButton != null)
            {
                modelLoadOrUnloadButton.onClick.AddListener(ToggleModelLoad);
                var image = modelLoadOrUnloadButton.GetComponent<Image>();
                if (image != null)
                {
                    defaultButtonColor = image.color;
                }
            }

            EnsureComponents();
            BindComponentEvents();

            progressTracker = new ModelLoadProgressTracker(progressBar, progressValueText, progressMessageText);
            progressTracker.SetVisible(false);
        }

        private void OnEnable()
        {
            _ = PopulateDropdownAsync();
            UpdateLoadButtonUI();
            SetProgressVisible(false);
            tagResultText.text = "Select an audio-tagging model, load it, then make some noise for live tags.";
            statusText.text = string.Empty;
        }

        private void OnDestroy()
        {
            if (modelLoadOrUnloadButton != null)
            {
                modelLoadOrUnloadButton.onClick.RemoveListener(ToggleModelLoad);
            }

            UnbindComponentEvents();
        }

        private void EnsureComponents()
        {
            if (taggingComponent == null)
            {
                taggingComponent = gameObject.AddComponent<AudioTaggingComponent>();
            }

            if (microphoneInput == null)
            {
                microphoneInput = gameObject.GetComponent<SherpaMicrophoneInput>();
                if (microphoneInput == null)
                {
                    microphoneInput = gameObject.AddComponent<SherpaMicrophoneInput>();
                }
            }

            taggingComponent.BindInput(microphoneInput);
        }

        private void BindComponentEvents()
        {
            if (taggingComponent == null)
            {
                return;
            }

            taggingComponent.TagsReadyEvent.AddListener(HandleTagsReady);
            taggingComponent.TaggingFailedEvent.AddListener(HandleTaggingFailed);
            taggingComponent.InitializationStateChangedEvent.AddListener(HandleInitializationChanged);
            taggingComponent.FeedbackMessages.AddListener(HandleFeedbackMessage);
            taggingComponent.FeedbackReceived += HandleFeedback;
        }

        private void UnbindComponentEvents()
        {
            if (taggingComponent == null)
            {
                return;
            }

            taggingComponent.TagsReadyEvent.RemoveListener(HandleTagsReady);
            taggingComponent.TaggingFailedEvent.RemoveListener(HandleTaggingFailed);
            taggingComponent.InitializationStateChangedEvent.RemoveListener(HandleInitializationChanged);
            taggingComponent.FeedbackMessages.RemoveListener(HandleFeedbackMessage);
            taggingComponent.FeedbackReceived -= HandleFeedback;
        }

        private async Task PopulateDropdownAsync()
        {
            if (modelDropdown == null)
            {
                return;
            }

            modelDropdown.options.Clear();

            modelDropdown.captionText.text = "Loading audio tagging models…";
            modelLoadOrUnloadButton.gameObject.SetActive(false);

            var manifest = await SherpaONNXModelRegistry.Instance.GetManifestAsync(SherpaONNXModuleType.AudioTagging);

            modelLoadOrUnloadButton.gameObject.SetActive(true);
            modelDropdown.options.Clear();
            if (manifest.models != null && manifest.models.Count > 0)
            {
                var modelOptions = manifest.models.ConvertAll(m => new OptionData(m.modelId));
                modelDropdown.AddOptions(modelOptions);
                var defaultIndex = modelOptions.FindIndex(m => m.text == defaultModelID);
                modelDropdown.value = defaultIndex >= 0 ? defaultIndex : Mathf.Clamp(modelDropdown.value, 0, Mathf.Max(0, modelOptions.Count - 1));
                modelDropdown.interactable = modelOptions.Count > 0;
            }
            else
            {
                modelDropdown.options.Add(new OptionData("<no models>"));
                modelDropdown.interactable = false;
            }
        }

        private string SelectedModelId =>
            modelDropdown != null && modelDropdown.options.Count > 0
                ? modelDropdown.options[modelDropdown.value].text
                : string.Empty;

        private bool IsModelLoaded => taggingComponent != null && taggingComponent.IsInitialized;

        private void ToggleModelLoad()
        {
            if (IsModelLoaded || modelRequested)
            {
                UnloadModel();
            }
            else
            {
                LoadModel();
            }
        }

        private void LoadModel()
        {
            if (taggingComponent == null)
            {
                return;
            }

            var modelId = SelectedModelId;
            if (string.IsNullOrWhiteSpace(modelId))
            {
                statusText.text = "Select a model first.";
                tagResultText.text = "Pick a model from the dropdown, then tap Load.";
                return;
            }

            taggingComponent.ModelId = modelId.Trim();
            if (taggingComponent.TryLoadModule())
            {
                modelRequested = true;
                SetProgressVisible(true, "Loading audio-tagging model…");
                progressTracker?.MarkStageComplete(Stage.Prepare, "Preparing model…");
                statusText.text = "Loading model...";
            }
            else
            {
                progressMessageText.text = "Failed to start model load.";
            }

            UpdateLoadButtonUI();
        }

        private void UnloadModel()
        {
            modelRequested = false;
            taggingComponent?.ResetStreamingBuffer();
            taggingComponent?.DisposeModule();
            microphoneInput?.StopCapture();
            SetProgressVisible(false);
            statusText.text = "Load a model to begin.";
            tagResultText.text = "Model unloaded.";
            UpdateLoadButtonUI();
        }

        private void HandleInitializationChanged(bool ready)
        {
            if (ready)
            {
                SetProgressVisible(false);
                statusText.text = "Streaming microphone audio into the tagger.";
                microphoneInput?.TryStartCapture();
                tagResultText.text = "Speak or play audio near the mic to view predicted tags.";
            }
            else if (modelRequested)
            {
                SetProgressVisible(true, "Initializing…");
                tagResultText.text = "Initializing model… keep the microphone quiet.";
            }

            UpdateLoadButtonUI();
        }

        private void HandleTagsReady(AudioTagging.AudioTag[] tags)
        {
            if (tags == null || tags.Length == 0)
            {
                tagResultText.text = "No tags detected yet…";
                return;
            }

            // 直接展示标签结果 / Show detected tags directly
            tagResultText.text = AudioTagExtensions.ToString(tags, "\n");
        }

        private void HandleTaggingFailed(string message)
        {
            tagResultText.text = $"<color=red>Tagging failed:</color> {message}";
            statusText.text = "Check model selection and microphone permissions.";
        }

        private void HandleFeedbackMessage(string message)
        {
            if (progressMessageText != null)
            {
                progressMessageText.text = message;
            }
        }

        private void HandleFeedback(SherpaFeedback feedback)
        {
            DemoUIShared.UpdateProgressFromFeedback(progressTracker, progressMessageText, feedback);
        }

        private void UpdateLoadButtonUI()
        {
            if (modelLoadOrUnloadButton == null)
            {
                return;
            }

            var label = modelLoadOrUnloadButton.GetComponentInChildren<Text>();
            if (label != null)
            {
                label.text = (IsModelLoaded || modelRequested) ? "Unload Model" : "Load Model";
            }

            var buttonImage = modelLoadOrUnloadButton.GetComponent<Image>();
            if (buttonImage != null)
            {
                buttonImage.color = (IsModelLoaded || modelRequested) ? DemoUIShared.UnloadColor : defaultButtonColor;
            }

            modelDropdown.interactable = !(IsModelLoaded || modelRequested);
        }

        private void SetProgressVisible(bool visible, string message = "")
        {
            if (progressBar != null)
            {
                progressBar.gameObject.SetActive(visible);
            }

            if (progressMessageText != null)
            {
                progressMessageText.gameObject.SetActive(visible);
                progressMessageText.text = visible ? message : string.Empty;
            }

            if (progressValueText != null)
            {
                progressValueText.text = visible ? message : string.Empty;
            }

            if (visible)
            {
                DemoUIShared.ShowLoading(progressTracker, progressMessageText, message);
            }
        }


        public void OpenGithubRepo()
        {
            Application.OpenURL("https://github.com/EitanWong/com.eitan.sherpa-onnx-unity");
        }
    }
}
