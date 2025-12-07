namespace Eitan.SherpaONNXUnity.Samples
{
    using System;

    using System.Linq;
    using System.Threading;

    using System.Threading.Tasks;
    using Eitan.Sherpa.Onnx.Unity.Mono.Components;
    using Eitan.SherpaONNXUnity.Runtime;

    using UnityEngine;
    using UnityEngine.UI;
    using Stage = Eitan.SherpaONNXUnity.Samples.ModelLoadProgressTracker.Stage;

    /// <summary>
    /// Text-to-speech demo focused on clean UI and responsive feedback.
    /// 文字转语音示例，界面简洁、反馈及时。
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public sealed class SpeechSynthesisExample : MonoBehaviour
    {
        [Header("Sherpa Component")]
        [SerializeField] private SpeechSynthesizerComponent synthesizer;

        [Header("UI")]
        [SerializeField] private Dropdown modelDropdown;
        [SerializeField] private Button loadOrUnloadButton;
        [SerializeField] private InputField voiceIdInput;
        [SerializeField] private Slider speedSlider;
        [SerializeField] private Text speedLabel;
        [SerializeField] private InputField textInput;
        [SerializeField] private Button synthesizeButton;
        [SerializeField] private Text statusText;

        [Header("Loading UI / Progress")]
        [SerializeField] private UI.EasyProgressBar progressBar;
        [SerializeField] private Text progressValueText;
        [SerializeField] private Text progressMessageText;

        [SerializeField]
        [Tooltip("Placeholder text that can be inserted into the text field on Enable. / 默认展示文本")]
        private string defaultUtterance = "SherpaONNX makes neural speech easy.";


        [Header("AudioSource")]
        [SerializeField] private AudioSource audioSource;

        [Header("Defaults")]
        [SerializeField] private string defaultModelID = "vits-melo-tts-zh_en";

        private bool modelRequested;
        private bool modelReady;
        private ModelLoadProgressTracker progressTracker;
        private CancellationTokenSource manifestCts;

        private void Awake()
        {
            if (loadOrUnloadButton != null)
            {
                loadOrUnloadButton.onClick.AddListener(ToggleModel);
            }

            if (synthesizeButton != null)
            {
                synthesizeButton.onClick.AddListener(StartSynthesis);
            }

            if (speedSlider != null)
            {
                speedSlider.onValueChanged.AddListener(value => speedLabel.text = $"Speed: {value:F1}x");
            }

            progressTracker = new ModelLoadProgressTracker(
                progressBar,
                progressValueText,
                progressMessageText != null ? progressMessageText : statusText);
            progressTracker.SetVisible(false);

            if (synthesizer != null)
            {
                synthesizer.SynthesisStartedEvent.AddListener(() => statusText.text = "Generating audio…");
                synthesizer.ClipReadyEvent.AddListener(clip => statusText.text = clip != null ? $"Generated {clip.length:F1}s clip." : "Empty clip returned.");
                synthesizer.SynthesisFailedEvent.AddListener(message => statusText.text = message);
                synthesizer.InitializationStateChangedEvent.AddListener(HandleReadyState);
                synthesizer.FeedbackMessages.AddListener(HandleFeedbackMessage);
                synthesizer.FeedbackReceived += HandleFeedback;
            }

            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
                if (!audioSource)
                {
                    audioSource = gameObject.AddComponent<AudioSource>();
                }
            }

            audioSource.playOnAwake = false;
            audioSource.loop = false;
        }

        private void OnEnable()
        {
            manifestCts = new CancellationTokenSource();
            if (textInput != null && string.IsNullOrWhiteSpace(textInput.text))
            {
                textInput.text = defaultUtterance;
            }

            if (speedSlider != null)
            {
                speedLabel.text = $"Speed: {speedSlider.value:F1}x";
            }

            _ = PopulateDropdownAsync(manifestCts.Token);
            modelReady = false;
            UpdateButtonLabel();
            synthesizeButton.interactable = false;
            statusText.text = "Load a TTS model to begin.";
        }

        private void OnDestroy()
        {
            if (loadOrUnloadButton != null)
            {
                loadOrUnloadButton.onClick.RemoveListener(ToggleModel);
            }

            if (synthesizeButton != null)
            {
                synthesizeButton.onClick.RemoveListener(StartSynthesis);
            }

            if (speedSlider != null)
            {
                speedSlider.onValueChanged.RemoveAllListeners();
            }

            if (synthesizer != null)
            {
                synthesizer.SynthesisStartedEvent.RemoveAllListeners();
                synthesizer.ClipReadyEvent.RemoveAllListeners();
                synthesizer.SynthesisFailedEvent.RemoveAllListeners();
                synthesizer.InitializationStateChangedEvent.RemoveListener(HandleReadyState);
                synthesizer.FeedbackMessages.RemoveListener(HandleFeedbackMessage);
                synthesizer.FeedbackReceived -= HandleFeedback;
            }

            manifestCts?.Cancel();
            manifestCts?.Dispose();
        }

        private async Task PopulateDropdownAsync(CancellationToken cancellationToken)
        {
            if (modelDropdown == null)
            {
                return;
            }

            modelDropdown.options.Clear();
            modelDropdown.captionText.text = "Loading TTS models…";
            modelDropdown.interactable = false;

            try
            {
                var manifest = await SherpaONNXModelRegistry.Instance
                    .GetManifestAsync(SherpaONNXModuleType.SpeechSynthesis, cancellationToken)
                    .ConfigureAwait(true);

                if (manifest.models == null || manifest.models.Count == 0)
                {
                    modelDropdown.options.Add(new Dropdown.OptionData("<no models>"));
                    return;
                }

                var options = manifest.models
                    .Where(m => !string.IsNullOrWhiteSpace(m.modelId))
                    .Select(m => new Dropdown.OptionData(m.modelId))
                    .ToList();

                modelDropdown.AddOptions(options);
                var defaultIndex = options.FindIndex(m => m.text == defaultModelID);
                modelDropdown.value = defaultIndex >= 0 ? defaultIndex : 0;
                modelDropdown.interactable = options.Count > 0;
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                modelDropdown.options.Clear();
                modelDropdown.options.Add(new Dropdown.OptionData("<manifest unavailable>"));
                modelDropdown.interactable = false;
                statusText.text = $"Manifest fetch failed: {ex.Message}";
                if (loadOrUnloadButton != null)
                {
                    loadOrUnloadButton.interactable = false;
                }
            }
        }

        private string SelectedModelId =>
            modelDropdown != null && modelDropdown.options.Count > 0
                ? modelDropdown.options[modelDropdown.value].text
                : string.Empty;

        private void ToggleModel()
        {
            if (synthesizer == null)
            {
                statusText.text = "Assign the SpeechSynthesizerComponent.";
                return;
            }

            if (!modelRequested)
            {
                var modelId = SelectedModelId;
                if (string.IsNullOrWhiteSpace(modelId))
                {
                    statusText.text = "Select a model first.";
                    return;
                }

                synthesizer.ModelId = modelId.Trim();
                if (synthesizer.TryLoadModule())
                {
                    modelRequested = true;
                    modelReady = false;
                    BeginLoading($"Loading {synthesizer.ModelId}…");
                }
            }
            else
            {
                synthesizer.DisposeModule();
                modelRequested = false;
                modelReady = false;
                statusText.text = "Model disposed.";
                progressTracker?.Reset();
                progressTracker?.SetVisible(false);
            }

            UpdateButtonLabel();
            synthesizeButton.interactable = modelRequested && modelReady;
            modelDropdown.interactable = !modelRequested;
        }

        private void UpdateButtonLabel()
        {
            if (loadOrUnloadButton == null)
            {
                return;
            }

            var label = loadOrUnloadButton.GetComponentInChildren<Text>();
            if (label != null)
            {
                label.text = modelRequested ? "Unload Model" : "Load Model";
            }

            DemoUIShared.SetButtonColor(loadOrUnloadButton, modelRequested ? DemoUIShared.UnloadColor : DemoUIShared.LoadColor);
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

        private async void StartSynthesis()
        {
            if (!modelRequested || synthesizer == null || !modelReady)
            {
                statusText.text = "Load a model first.";
                return;
            }

            if (string.IsNullOrWhiteSpace(textInput?.text))
            {
                statusText.text = "Enter some text to synthesize.";
                return;
            }

            int? voiceId = null;
            if (!string.IsNullOrWhiteSpace(voiceIdInput?.text) && int.TryParse(voiceIdInput.text, out var parsedVoice))
            {
                voiceId = parsedVoice;
            }

            var clip = await synthesizer.GenerateClipAsync(
                textInput.text.Trim(),
                voiceId,
                speedSlider != null ? speedSlider.value : (float?)null).ConfigureAwait(true);

            if (clip == null)
            {
                statusText.text = "Generation cancelled or failed.";
            }
            else
            {
                //play generated audioclip.
                audioSource.Stop();
                audioSource.clip = clip;
                audioSource.Play();
            }
        }

        private void HandleReadyState(bool ready)
        {
            modelReady = ready && modelRequested;

            if (modelRequested)
            {
                if (ready)
                {
                    CompleteLoading($"Loaded {synthesizer.ModelId}. Enter text and synthesize.");
                }
                else
                {
                    BeginLoading("Loading model…");
                }
            }

            synthesizeButton.interactable = modelReady;
            modelDropdown.interactable = !modelRequested;
            UpdateButtonLabel();
        }

        private void BeginLoading(string message)
        {
            DemoUIShared.ShowLoading(progressTracker, statusText, message);
        }

        private void CompleteLoading(string message)
        {
            DemoUIShared.ShowLoadingComplete(progressTracker, statusText, message);
        }

        public void OpenGithubRepo()
        {
            Application.OpenURL("https://github.com/EitanWong/com.eitan.sherpa-onnx-unity");
        }
    }
}
