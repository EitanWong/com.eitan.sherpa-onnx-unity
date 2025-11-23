namespace Eitan.SherpaONNXUnity.Samples
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Eitan.Sherpa.Onnx.Unity.Mono.Components;
    using Eitan.SherpaONNXUnity.Runtime;
    using UnityEngine;
    using UnityEngine.UI;
    using static UnityEngine.UI.Dropdown;
    using Stage = Eitan.SherpaONNXUnity.Samples.ModelLoadProgressTracker.Stage;

    /// <summary>
    /// Zero-shot speech synthesis demo with prompt selection and progress UI.
    /// 零样本语音合成示例，带提示选择与加载进度条。
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public sealed class ZeroShotSpeechSynthesisExample : MonoBehaviour
    {
        [Header("Sherpa Component")]
        [SerializeField] private ZeroShotSpeechSynthesisComponent ttsComponent;

        [Header("UI")]
        [SerializeField] private Dropdown modelDropdown;
        [SerializeField] private Button loadOrUnloadButton;
        [SerializeField] private Text progressMessageText;
        [SerializeField] private UI.EasyProgressBar progressBar;
        [SerializeField] private Text progressValueText;
        [SerializeField] private Text instructionText;
        [SerializeField] private Text statusText;

        [Header("Prompts")]
        [SerializeField] private RectTransform promptRoot;
        [SerializeField] private GameObject promptTemplate;
        [SerializeField] private Slider speedSlider;
        [SerializeField] private Text speedValueText;
        [SerializeField] private InputField contentInputField;
        [SerializeField] private Button generateButton;
        [SerializeField] private ZeroShotPrompt[] prompts;

        [Header("Defaults")]
        [SerializeField] private string defaultModelID = "sherpa-onnx-ced-base-audio-tagging-2024-04-19";

        private readonly List<PromptItem> promptItems = new List<PromptItem>(8);
        private int selectedPromptIndex;
        private AudioSource audioSource;
        private bool modelRequested;
        private Color defaultButtonColor = Color.white;
        private ModelLoadProgressTracker progressTracker;

        private void Awake()
        {
            audioSource = GetComponent<AudioSource>();
            if (ttsComponent == null)
            {
                ttsComponent = gameObject.AddComponent<ZeroShotSpeechSynthesisComponent>();
            }

            ttsComponent.OutputAudioSource = audioSource;
            ttsComponent.GenerationStartedEvent.AddListener(OnGenerationStarted);
            ttsComponent.ClipReadyEvent.AddListener(OnClipReady);
            ttsComponent.GenerationFailedEvent.AddListener(OnGenerationFailed);
            ttsComponent.InitializationStateChangedEvent.AddListener(OnInitializationChanged);
            ttsComponent.FeedbackMessages.AddListener(HandleFeedbackMessage);

            if (loadOrUnloadButton != null)
            {
                loadOrUnloadButton.onClick.AddListener(ToggleModelLoad);
                var image = loadOrUnloadButton.GetComponent<Image>();
                if (image != null)
                {
                    defaultButtonColor = image.color;
                }
            }

            if (generateButton != null)
            {
                generateButton.onClick.AddListener(HandleGenerateButtonClick);
            }

            if (speedSlider != null)
            {
                speedSlider.onValueChanged.AddListener(OnSpeedSliderChanged);
                OnSpeedSliderChanged(speedSlider.value);
            }

            progressTracker = new ModelLoadProgressTracker(progressBar, progressValueText, progressMessageText);
            progressTracker.SetVisible(false);
        }

        private void Start()
        {
            Application.runInBackground = true;
            Application.targetFrameRate = 30;

            progressBar.gameObject.SetActive(false);
            progressMessageText.gameObject.SetActive(false);
            statusText.text = "TTS Status: Model not loaded";
            instructionText.text = "Select a TTS model, pick a zero-shot prompt, then click Generate.";
            contentInputField.text = "Welcome to the zero-shot speech synthesis demo";

            RefreshPromptList();
            _ = PopulateDropdownAsync();
            UpdateLoadButtonUI();
        }

        private void OnDestroy()
        {
            if (ttsComponent != null)
            {
                ttsComponent.GenerationStartedEvent.RemoveListener(OnGenerationStarted);
                ttsComponent.ClipReadyEvent.RemoveListener(OnClipReady);
                ttsComponent.GenerationFailedEvent.RemoveListener(OnGenerationFailed);
                ttsComponent.InitializationStateChangedEvent.RemoveListener(OnInitializationChanged);
                ttsComponent.FeedbackMessages.RemoveListener(HandleFeedbackMessage);
            }

            if (loadOrUnloadButton != null)
            {
                loadOrUnloadButton.onClick.RemoveListener(ToggleModelLoad);
            }

            if (generateButton != null)
            {
                generateButton.onClick.RemoveListener(HandleGenerateButtonClick);
            }

            if (speedSlider != null)
            {
                speedSlider.onValueChanged.RemoveListener(OnSpeedSliderChanged);
            }
        }

        private async Task PopulateDropdownAsync()
        {
            if (modelDropdown == null)
            {
                return;
            }

            modelDropdown.options.Clear();
            modelDropdown.captionText.text = "Fetching model manifest from GitHub…";
            loadOrUnloadButton.gameObject.SetActive(false);
            var manifest = await SherpaONNXModelRegistry.Instance.GetManifestAsync(SherpaONNXModuleType.SpeechSynthesis);
            loadOrUnloadButton.gameObject.SetActive(true);

            modelDropdown.options.Clear();
            var options = new List<OptionData>();
            if (manifest.models != null && manifest.models.Count > 0)
            {
                var preferred = manifest.Filter(m => m.modelId.StartsWith("sherpa-onnx-zipvoice"));
                IEnumerable<SherpaONNXModelMetadata> source =
                    (preferred != null && preferred.Length > 0)
                        ? (IEnumerable<SherpaONNXModelMetadata>)preferred
                        : manifest.models;

                foreach (var model in source)
                {
                    if (!string.IsNullOrWhiteSpace(model.modelId))
                    {
                        options.Add(new OptionData(model.modelId));
                    }
                }
            }
            if (options.Count == 0)
            {
                modelDropdown.options.Add(new OptionData("<no models>"));
                modelDropdown.interactable = false;
            }
            else
            {
                modelDropdown.AddOptions(options);
                var defaultIndex = options.FindIndex(m => m.text == defaultModelID);
                modelDropdown.value = defaultIndex >= 0 ? defaultIndex : Mathf.Clamp(modelDropdown.value, 0, Mathf.Max(0, options.Count - 1));
                modelDropdown.interactable = options.Count > 0;
            }
        }

        private string SelectedModelId =>
            modelDropdown != null && modelDropdown.options.Count > 0
                ? modelDropdown.options[modelDropdown.value].text
                : string.Empty;

        private bool IsModelLoaded => ttsComponent != null && ttsComponent.IsInitialized;

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
            var modelId = SelectedModelId;
            if (string.IsNullOrWhiteSpace(modelId))
            {
                instructionText.text = "Select a model first.";
                return;
            }

            ttsComponent.ModelId = modelId.Trim();
            if (ttsComponent.TryLoadModule())
            {
                modelRequested = true;
                SetProgressVisible(true, $"Loading {modelId}…");
                progressTracker?.MarkStageComplete(Stage.Prepare, "Preparing model…");
                statusText.text = "TTS Status: Loading…";
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
            ttsComponent?.DisposeModule();
            statusText.text = "TTS Status: Model not loaded";
            instructionText.text = "Select a TTS model, pick a zero-shot prompt, then click Generate.";
            SetProgressVisible(false);
            UpdateLoadButtonUI();
        }

        private void HandleGenerateButtonClick()
        {
            if (!IsModelLoaded)
            {
                instructionText.text = "Load a model first.";
                return;
            }

            var prompt = GetSelectedPrompt();
            if (prompt == null)
            {
                instructionText.text = "Assign at least one prompt (audio + text).";
                return;
            }

            var promptText = prompt.PromptText != null ? prompt.PromptText.text : string.Empty;
            _ = ttsComponent.GenerateAsync(contentInputField.text, promptText, prompt.PromptAudio, speedSlider != null ? speedSlider.value : 1f);
        }

        private void OnGenerationStarted()
        {
            statusText.text = "TTS Status: Generating…";
            instructionText.text = "Synthesizing with the selected prompt.";
            SetProgressVisible(true, "Generating audio…");
        }

        private void OnClipReady(AudioClip clip)
        {
            SetProgressVisible(false);
            if (clip == null)
            {
                statusText.text = "TTS Status: Generation returned no audio.";
                return;
            }

            statusText.text = $"TTS Status: Ready ({clip.length:F1}s)";
            instructionText.text = "Playback started on the AudioSource.";
        }

        private void OnGenerationFailed(string message)
        {
            SetProgressVisible(false);
            statusText.text = $"TTS Status: Failed - {message}";
            instructionText.text = "Check prompt audio/text and model selection.";
        }

        private void HandleFeedbackMessage(string message)
        {
            if (progressMessageText != null)
            {
                progressMessageText.text = message;
            }
            else if (statusText != null)
            {
                statusText.text = message;
            }

            if (!string.IsNullOrEmpty(message))
            {
                progressTracker?.UpdateStage(Stage.Download, message, 0.35f);
            }
        }

        private void OnInitializationChanged(bool ready)
        {
            if (ready)
            {
                statusText.text = "TTS Status: Ready";
                instructionText.text = "Enter text and click Generate.";
                SetProgressVisible(false);
            }
            else if (modelRequested)
            {
                SetProgressVisible(true, "Initializing model…");
            }

            UpdateLoadButtonUI();
        }

        private void UpdateLoadButtonUI()
        {
            if (loadOrUnloadButton == null)
            {
                return;
            }

            var label = loadOrUnloadButton.GetComponentInChildren<Text>();
            if (label != null)
            {
                label.text = (IsModelLoaded || modelRequested) ? "Unload Model" : "Load Model";
            }

            var image = loadOrUnloadButton.GetComponent<Image>();
            if (image != null)
            {
                image.color = (IsModelLoaded || modelRequested) ? DemoUIShared.UnloadColor : defaultButtonColor;
            }

            modelDropdown.interactable = !(IsModelLoaded || modelRequested);
            if (generateButton != null)
            {
                generateButton.interactable = IsModelLoaded;
            }
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

        private void RefreshPromptList()
        {
            if (promptTemplate == null || promptRoot == null)
            {
                return;
            }

            // Hide template to keep UI clean. / 隐藏模板，保持界面整洁
            promptTemplate.SetActive(false);

            if (prompts == null || prompts.Length == 0)
            {
                return;
            }

            while (promptItems.Count < prompts.Length)
            {
                var go = Instantiate(promptTemplate, promptRoot);
                go.name = $"Prompt_{promptItems.Count}";
                var item = new PromptItem
                {
                    Go = go,
                    Image = go.GetComponentInChildren<RawImage>(true),
                    Label = go.GetComponentInChildren<Text>(true),
                    Button = go.GetComponent<Button>(),
                    Outline = go.GetComponent<Outline>() ?? go.AddComponent<Outline>()
                };
                promptItems.Add(item);
            }

            for (int i = 0; i < promptItems.Count; i++)
            {
                var active = i < prompts.Length;
                var item = promptItems[i];
                if (item.Go != null)
                {
                    item.Go.SetActive(active);
                }

                if (!active)
                {
                    continue;
                }

                item.Data = prompts[i];
                if (item.Image != null)
                {
                    item.Image.texture = item.Data.Icon;
                }

                if (item.Label != null)
                {
                    item.Label.text = item.Data.name;
                }

                if (item.Button != null)
                {
                    item.Button.onClick.RemoveAllListeners();
                    var capturedIndex = i;
                    item.Button.onClick.AddListener(() => SetSelectedPrompt(capturedIndex));
                }

                if (item.Outline != null)
                {
                    item.Outline.enabled = (i == selectedPromptIndex);
                }
            }
        }

        private void SetSelectedPrompt(int index)
        {
            if (prompts == null || index < 0 || index >= prompts.Length)
            {
                return;
            }

            selectedPromptIndex = index;
            for (int i = 0; i < promptItems.Count; i++)
            {
                if (promptItems[i].Outline != null)
                {
                    promptItems[i].Outline.enabled = (i == selectedPromptIndex);
                }
            }
        }

        private ZeroShotPrompt GetSelectedPrompt()
        {
            if (prompts == null || prompts.Length == 0)
            {
                return null;
            }

            selectedPromptIndex = Mathf.Clamp(selectedPromptIndex, 0, prompts.Length - 1);
            return prompts[selectedPromptIndex];
        }

        private void OnSpeedSliderChanged(float value)
        {
            if (speedValueText != null)
            {
                speedValueText.text = $"Speed: {value:0.00}x";
            }
        }


        public void OpenGithubRepo()
        {
            Application.OpenURL("https://github.com/EitanWong/com.eitan.sherpa-onnx-unity");
        }

        [System.Serializable]
        private sealed class PromptItem
        {
            public GameObject Go;
            public RawImage Image;
            public Text Label;
            public Button Button;
            public Outline Outline;
            public ZeroShotPrompt Data;
        }
    }
}
