
namespace Eitan.SherpaONNXUnity.Samples
{
    using System;

    using System.Linq;
    using System.Threading;

    using System.Threading.Tasks;
    using Eitan.Sherpa.Onnx.Unity.Mono.Components;
    using Eitan.Sherpa.Onnx.Unity.Mono.Inputs;
    using Eitan.SherpaONNXUnity.Runtime;
    using UnityEngine;
    using UnityEngine.UI;
    using Stage = Eitan.SherpaONNXUnity.Samples.ModelLoadProgressTracker.Stage;

    /// <summary>
    /// Streaming speech-to-text demo with clear UI feedback.
    /// 实时语音识别示例，提供清晰的加载与录音反馈。
    /// </summary>
    public sealed class RealtimeSpeechRecognitionExample : MonoBehaviour
    {
        [Header("Sherpa Components")]
        [SerializeField] private SpeechRecognizerComponent recognizer;
        [SerializeField] private SherpaMicrophoneInput microphone;

        [Header("UI")]
        [SerializeField] private Dropdown modelDropdown;
        [SerializeField] private Button loadOrUnloadButton;
        [SerializeField] private Text statusText;
        [SerializeField] private Text transcriptText;

        [Header("Loading UI / Progress")]
        [SerializeField] private UI.EasyProgressBar progressBar;
        [SerializeField] private Text progressValueText;
        [SerializeField] private Text progressMessageText;

        [SerializeField]
        [Tooltip("Optional message shown while fetching the manifest. / 拉取清单时的提示")]
        private string loadingMessage = "Fetching speech recognition manifest…";

        [Header("Defaults")]
        [SerializeField] private string defaultModelID = "sherpa-onnx-streaming-zipformer-bilingual-zh-en-2023-02-20";

        private bool moduleRequested;
        private bool modelReady;
        private ModelLoadProgressTracker progressTracker;
        private CancellationTokenSource manifestCts;

        private void Awake()
        {
            if (loadOrUnloadButton != null)
            {
                loadOrUnloadButton.onClick.AddListener(ToggleModel);
            }

            progressTracker = new ModelLoadProgressTracker(
                progressBar,
                progressValueText,
                progressMessageText != null ? progressMessageText : statusText);
            progressTracker.SetVisible(false);

            if (recognizer != null)
            {
                recognizer.TranscriptionReadyEvent.AddListener(HandleTranscriptReady);
                recognizer.InitializationStateChangedEvent.AddListener(HandleRecognizerReadyState);
                recognizer.FeedbackMessages.AddListener(HandleFeedbackMessage);
                recognizer.FeedbackReceived += HandleFeedback;
                if (microphone != null)
                {
                    recognizer.BindInput(microphone);
                }
            }
        }

        private void OnEnable()
        {
            manifestCts = new CancellationTokenSource();
            _ = PopulateModelDropdownAsync(manifestCts.Token);
            if (transcriptText != null)
            {
                transcriptText.text = "Tap Load Model to start streaming transcription.";
            }
            if (statusText != null)
            {
                statusText.text = "Pick a model to begin.";
            }
            modelReady = false;
            UpdateButtonVisuals();
        }

        private void OnDestroy()
        {
            if (loadOrUnloadButton != null)
            {
                loadOrUnloadButton.onClick.RemoveListener(ToggleModel);
            }

            if (recognizer != null)
            {
                recognizer.TranscriptionReadyEvent.RemoveListener(HandleTranscriptReady);
                recognizer.InitializationStateChangedEvent.RemoveListener(HandleRecognizerReadyState);
                recognizer.FeedbackMessages.RemoveListener(HandleFeedbackMessage);
                recognizer.FeedbackReceived -= HandleFeedback;
            }

            manifestCts?.Cancel();
            manifestCts?.Dispose();
        }

        private async Task PopulateModelDropdownAsync(CancellationToken cancellationToken)
        {
            if (modelDropdown == null)
            {
                return;
            }

            modelDropdown.options.Clear();
            if (!string.IsNullOrEmpty(loadingMessage))
            {
                modelDropdown.captionText.text = loadingMessage;
            }

            if (loadOrUnloadButton != null)
            {
                loadOrUnloadButton.interactable = false;
            }
            try
            {
                var manifest = await SherpaONNXModelRegistry.Instance
                    .GetManifestAsync(SherpaONNXModuleType.SpeechRecognition, cancellationToken)
                    .ConfigureAwait(true);

                if (loadOrUnloadButton != null)
                {
                    loadOrUnloadButton.interactable = true;
                }

                modelDropdown.options.Clear();

                if (manifest.models == null || manifest.models.Count == 0)
                {
                    modelDropdown.options.Add(new Dropdown.OptionData("<no speech models>"));
                    modelDropdown.interactable = false;
                    SetStatus("No models available.");
                    return;
                }

                var options = manifest.models
                    .Where(m => !string.IsNullOrWhiteSpace(m.modelId) && SherpaONNXUnity.Runtime.Utilities.SherpaUtils.Model.IsOnlineModel(m.modelId))
                    .Select(m => new Dropdown.OptionData(m.modelId))
                    .ToList();

                modelDropdown.AddOptions(options);
                var defaultIndex = options.FindIndex(m => m.text == defaultModelID);
                modelDropdown.value = defaultIndex >= 0 ? defaultIndex : 0;
                modelDropdown.interactable = options.Count > 0;
            }
            catch (OperationCanceledException)
            {
                // ignore
            }
            catch (Exception ex)
            {
                modelDropdown.options.Clear();
                modelDropdown.options.Add(new Dropdown.OptionData("<manifest unavailable>"));
                modelDropdown.interactable = false;
                SetStatus($"Manifest fetch failed: {ex.Message}");
                if (loadOrUnloadButton != null)
                {
                    loadOrUnloadButton.interactable = false;
                }
            }
        }

        private string SelectedModelId =>
            modelDropdown != null &&
            modelDropdown.options != null &&
            modelDropdown.options.Count > 0
                ? modelDropdown.options[modelDropdown.value].text
                : string.Empty;

        private void ToggleModel()
        {
            if (recognizer == null)
            {
                SetStatus("SpeechRecognizerComponent reference missing.");
                return;
            }

            if (!moduleRequested)
            {
                var modelId = SelectedModelId;
                if (string.IsNullOrWhiteSpace(modelId))
                {
                    SetStatus("Select a model first.");
                    return;
                }

                recognizer.ModelId = modelId.Trim();
                if (recognizer.TryLoadModule())
                {
                    moduleRequested = true;
                    modelReady = false;
                    BeginLoading($"Loading {recognizer.ModelId}…");
                }
                else
                {
                    SetStatus("Model already loading or missing configuration.");
                }
            }
            else
            {
                recognizer.DisposeModule();
                moduleRequested = false;
                modelReady = false;
                if (transcriptText != null)
                {
                    transcriptText.text = string.Empty;
                }
                SetStatus("Model disposed.");
                progressTracker?.Reset();
                progressTracker?.SetVisible(false);
            }

            UpdateButtonVisuals();
        }

        private void UpdateButtonVisuals()
        {
            if (loadOrUnloadButton != null)
            {
                var label = loadOrUnloadButton.GetComponentInChildren<Text>();
                if (label != null)
                {
                    label.text = moduleRequested ? "Unload Model" : "Load Model";
                }

                DemoUIShared.SetButtonColor(loadOrUnloadButton, moduleRequested ? DemoUIShared.UnloadColor : DemoUIShared.LoadColor);
                loadOrUnloadButton.interactable = true;
            }

            if (modelDropdown != null)
            {
                modelDropdown.interactable = !moduleRequested;
            }

            if (transcriptText != null)
            {
                transcriptText.color = modelReady ? Color.white : Color.grey;
            }
        }

        private void HandleRecognizerReadyState(bool ready)
        {
            modelReady = ready && moduleRequested;

            if (!moduleRequested)
            {
                return;
            }

            if (ready)
            {
                DemoUIShared.ShowLoadingComplete(progressTracker, statusText, "Recognizer ready. Speak into the microphone.");
                if (transcriptText != null)
                {
                    transcriptText.text = "Awaiting speech…";
                }
            }
            else
            {
                DemoUIShared.ShowLoading(progressTracker, statusText, "Loading model…");
            }

            UpdateButtonVisuals();
        }

        private void HandleTranscriptReady(string transcript)
        {
            if (string.IsNullOrWhiteSpace(transcript))
            {
                return;
            }

            // 实时更新识别结果 / Update transcript in real time
            if (transcriptText != null)
            {
                transcriptText.text = transcript;
            }
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

        private void BeginLoading(string message)
        {
            DemoUIShared.ShowLoading(progressTracker, statusText, message);
            if (transcriptText != null)
            {
                transcriptText.text = "Preparing…";
            }
        }

        private void SetStatus(string message)
        {
            if (statusText != null)
            {
                statusText.text = message;
            }
        }

        public void OpenGithubRepo()
        {
            Application.OpenURL("https://github.com/EitanWong/com.eitan.sherpa-onnx-unity");
        }
    }
}
