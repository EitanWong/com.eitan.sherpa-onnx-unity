
namespace Eitan.SherpaONNXUnity.Samples
{
    using System;

    using System.Linq;
    using System.Threading;

    using System.Threading.Tasks;
    using Eitan.Sherpa.Onnx.Unity.Mono.Components;
    using Eitan.Sherpa.Onnx.Unity.Mono.Inputs;
    using Eitan.SherpaONNXUnity.Runtime;
    using Eitan.SherpaONNXUnity.Runtime.Modules;
    using UnityEngine;
    using UnityEngine.UI;
    using Stage = Eitan.SherpaONNXUnity.Samples.ModelLoadProgressTracker.Stage;

    /// <summary>
    /// Realtime speech-to-text demo with clear UI feedback.
    /// 实时语音识别示例，提供清晰的加载与录音反馈。
    /// </summary>
    public sealed class RealtimeSpeechRecognitionExample : MonoBehaviour
    {
        [Header("Sherpa Components")]
        [SerializeField] private RealtimeSpeechRecognizerComponent realtimeRecognizer;
        [SerializeField] private SherpaMicrophoneInput microphone;

        [Header("UI")]
        [SerializeField] private Dropdown modelDropdown;
        [SerializeField] private Dropdown languageDropdown;
        [SerializeField] private Button loadOrUnloadButton;
        [SerializeField] private Text statusText;
        [SerializeField] private Text transcriptText;

        [Header("Loading UI / Progress")]
        [SerializeField] private UI.EasyProgressBar progressBar;
        [SerializeField] private Text progressValueText;
        [SerializeField] private Text progressMessageText;

        [SerializeField]
        [Tooltip("Optional message shown while fetching the manifest. / 拉取清单时的提示")]
        private string loadingMessage = "Fetching realtime speech models…";

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

            if (modelDropdown != null)
            {
                modelDropdown.onValueChanged.AddListener(HandleModelDropdownValueChanged);
            }

            progressTracker = new ModelLoadProgressTracker(
                progressBar,
                progressValueText,
                progressMessageText != null ? progressMessageText : statusText);
            progressTracker.SetVisible(false);

            if (realtimeRecognizer != null)
            {
                realtimeRecognizer.TranscriptionReadyEvent.AddListener(HandleTranscriptReady);
                realtimeRecognizer.InitializationStateChangedEvent.AddListener(HandleRecognizerReadyState);
                realtimeRecognizer.FeedbackMessages.AddListener(HandleFeedbackMessage);
                realtimeRecognizer.FeedbackReceived += HandleFeedback;
                if (microphone != null)
                {
                    realtimeRecognizer.BindInput(microphone);
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
                statusText.text = "Pick a streaming model to begin.";
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

            if (modelDropdown != null)
            {
                modelDropdown.onValueChanged.RemoveListener(HandleModelDropdownValueChanged);
            }

            if (realtimeRecognizer != null)
            {
                realtimeRecognizer.TranscriptionReadyEvent.RemoveListener(HandleTranscriptReady);
                realtimeRecognizer.InitializationStateChangedEvent.RemoveListener(HandleRecognizerReadyState);
                realtimeRecognizer.FeedbackMessages.RemoveListener(HandleFeedbackMessage);
                realtimeRecognizer.FeedbackReceived -= HandleFeedback;
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

            languageDropdown = DemoUIShared.EnsureLanguageDropdown(languageDropdown, modelDropdown);
            DemoUIShared.ConfigureSpeechLanguageDropdown(languageDropdown, string.Empty);

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
                    modelDropdown.options.Add(new Dropdown.OptionData("<no realtime models>"));
                    modelDropdown.interactable = false;
                    SetStatus("No realtime models available.");
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
                UpdateLanguageDropdown();
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

                UpdateLanguageDropdown();
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
            if (realtimeRecognizer == null)
            {
                SetStatus("RealtimeSpeechRecognizerComponent reference missing.");
                return;
            }

            if (!moduleRequested)
            {
                var modelId = SelectedModelId;
                if (string.IsNullOrWhiteSpace(modelId))
                {
                    SetStatus("Select a streaming ASR model first.");
                    return;
                }

                realtimeRecognizer.ModelId = modelId.Trim();
                realtimeRecognizer.RecognitionLanguage = DemoUIShared.GetSelectedSpeechLanguage(languageDropdown, modelId);
                if (realtimeRecognizer.TryLoadModule())
                {
                    moduleRequested = true;
                    modelReady = false;
                    BeginLoading($"Loading {realtimeRecognizer.ModelId}…");
                }
                else
                {
                    SetStatus("Model already loading or missing configuration.");
                }
            }
            else
            {
                realtimeRecognizer.DisposeModule();
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

            if (languageDropdown != null)
            {
                languageDropdown.interactable = !moduleRequested;
            }

            if (transcriptText != null)
            {
                transcriptText.color = modelReady ? Color.white : Color.grey;
            }
        }

        private void HandleModelDropdownValueChanged(int _)
        {
            UpdateLanguageDropdown();
        }

        private void UpdateLanguageDropdown()
        {
            DemoUIShared.ConfigureSpeechLanguageDropdown(languageDropdown, SelectedModelId);
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

        private void HandleTranscriptReady(SpeechRecognition.TranscriptionResult result)
        {
            var transcript = result.Text;
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
