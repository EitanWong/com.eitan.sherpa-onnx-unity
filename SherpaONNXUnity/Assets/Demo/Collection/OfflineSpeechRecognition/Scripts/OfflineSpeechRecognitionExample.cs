namespace Eitan.SherpaONNXUnity.Samples
{
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;
    using Eitan.Sherpa.Onnx.Unity.Mono.Components;
    using Eitan.Sherpa.Onnx.Unity.Mono.Inputs;
    using Eitan.SherpaONNXUnity.Runtime;
    using UnityEngine;
    using UnityEngine.UI;
    using UnityEngine.Events;
    using Stage = Eitan.SherpaONNXUnity.Samples.ModelLoadProgressTracker.Stage;
    using System.Threading;
    using System;



    /// <summary>
    /// Push-to-record offline transcription demo with progress UI.
    /// 离线语音识别示例，按下录音并带有加载进度显示。
    /// </summary>
    public sealed class OfflineSpeechRecognitionExample : MonoBehaviour
    {
        [Header("Sherpa Components")]
        [SerializeField] private OfflineSpeechRecognizerComponent offlineRecognizer;
        [SerializeField] private SherpaMicrophoneInput microphoneInput;

        [Header("UI")]
        [SerializeField] private Dropdown modelDropdown;
        [SerializeField] private Button loadOrUnloadButton;
        [SerializeField] private Button recordButton;
        [SerializeField] private Text statusText;
        [SerializeField] private Text transcriptText;
        [SerializeField]
        [Tooltip("Optional status while loading manifest. / 拉取清单时显示的提示")]
        private string loadingMessage = "Fetching offline speech models…";

        [Header("Loading UI / Progress")]
        [SerializeField] private UI.EasyProgressBar progressBar;
        [SerializeField] private Text progressValueText;
        [SerializeField] private Text progressMessageText;

        [Header("Defaults")]
        [SerializeField] private string defaultModelID = "sherpa-onnx-paraformer-zh-small-2024-03-09";

        private readonly StringBuilder rollingTranscript = new StringBuilder();
        private readonly List<float> recordedSamples = new List<float>(32768);
        private bool moduleRequested;
        private bool isRecording;
        private int recordingSampleRate = 16000;
        private CancellationTokenSource operationCts;
        private string lastTranscript;
        private UnityAction<string> failureHandler;
        private ModelLoadProgressTracker progressTracker;
        private CancellationTokenSource manifestCts;

        private void Awake()
        {
            if (loadOrUnloadButton != null)
            {
                loadOrUnloadButton.onClick.AddListener(ToggleModules);
            }

            failureHandler = HandleTranscriptionFailed;

            if (recordButton != null)
            {
                recordButton.onClick.AddListener(ToggleRecording);
            }

            progressTracker = new ModelLoadProgressTracker(
                progressBar,
                progressValueText,
                progressMessageText != null ? progressMessageText : statusText);
            progressTracker.SetVisible(false);

            if (offlineRecognizer != null)
            {
                offlineRecognizer.TranscriptReadyEvent.AddListener(HandleTranscriptReady);
                offlineRecognizer.TranscriptionFailedEvent.AddListener(failureHandler);
                offlineRecognizer.InitializationStateChangedEvent.AddListener(HandleRecognizerReadyState);
                offlineRecognizer.FeedbackMessages.AddListener(HandleFeedbackMessage);
                offlineRecognizer.FeedbackReceived += HandleFeedback;
            }
        }

        private void OnEnable()
        {
            operationCts = new CancellationTokenSource();
            manifestCts = new CancellationTokenSource();
            _ = PopulateSpeechModelsAsync(manifestCts.Token);
            ClearTranscript("Load a model, then tap Record to capture speech.");
            UpdateLoadButtonLabel();
            UpdateRecordButtonState(false);
        }

        private void OnDestroy()
        {
            if (loadOrUnloadButton != null)
            {
                loadOrUnloadButton.onClick.RemoveListener(ToggleModules);
            }

            if (recordButton != null)
            {
                recordButton.onClick.RemoveListener(ToggleRecording);
            }

            if (offlineRecognizer != null)
            {
                offlineRecognizer.TranscriptReadyEvent.RemoveListener(HandleTranscriptReady);
                offlineRecognizer.TranscriptionFailedEvent.RemoveListener(failureHandler);
                offlineRecognizer.InitializationStateChangedEvent.RemoveListener(HandleRecognizerReadyState);
                offlineRecognizer.FeedbackMessages.RemoveListener(HandleFeedbackMessage);
                offlineRecognizer.FeedbackReceived -= HandleFeedback;
            }

            UnsubscribeMicrophone();

            manifestCts?.Cancel();
            manifestCts?.Dispose();
            operationCts?.Cancel();
            operationCts?.Dispose();
        }

        private async Task PopulateSpeechModelsAsync(CancellationToken cancellationToken)
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
                    modelDropdown.options.Add(new Dropdown.OptionData("<no offline models>"));
                    modelDropdown.interactable = false;
                    statusText.text = "No offline models available.";
                    return;
                }

                var options = new List<Dropdown.OptionData>();
                foreach (var model in manifest.models)
                {
                    if (!SherpaONNXUnityAPI.IsOnlineModel(model.modelId))
                    {
                        options.Add(new Dropdown.OptionData(model.modelId));
                    }
                }

                modelDropdown.AddOptions(options);
                var defaultIndex = options.FindIndex(m => m.text == defaultModelID);
                modelDropdown.value = defaultIndex >= 0 ? defaultIndex : Mathf.Clamp(modelDropdown.value, 0, Mathf.Max(0, options.Count - 1));
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

        private string SelectedSpeechModelId =>
            modelDropdown != null && modelDropdown.options.Count > 0
                ? modelDropdown.options[modelDropdown.value].text
                : string.Empty;

        private void ToggleModules()
        {
            if (offlineRecognizer == null)
            {
                statusText.text = "Assign OfflineSpeechRecognizerComponent.";
                return;
            }

            if (!moduleRequested)
            {
                var asrModelId = SelectedSpeechModelId;
                if (string.IsNullOrWhiteSpace(asrModelId))
                {
                    statusText.text = "Select an offline ASR model first.";
                    return;
                }

                offlineRecognizer.ModelId = asrModelId.Trim();
                moduleRequested = offlineRecognizer.TryLoadModule();
                statusText.text = moduleRequested ? $"Loading {offlineRecognizer.ModelId}…" : "Unable to start module.";
                if (moduleRequested)
                {
                    BeginLoading(statusText.text);
                }
            }
            else
            {
                // Cancel any in-flight operations before disposing
                operationCts?.Cancel();
                offlineRecognizer.DisposeModule();
                moduleRequested = false;
                isRecording = false;
                recordedSamples.Clear();
                ClearTranscript();
                statusText.text = "Module disposed.";
                progressTracker?.Reset();
                progressTracker?.SetVisible(false);
                operationCts?.Dispose();
                operationCts = new CancellationTokenSource();
            }

            UpdateLoadButtonLabel();
            UpdateRecordButtonState(offlineRecognizer != null && offlineRecognizer.IsInitialized);
        }

        private void UpdateLoadButtonLabel()
        {
            if (loadOrUnloadButton == null)
            {
                return;
            }

            var label = loadOrUnloadButton.GetComponentInChildren<Text>();
            if (label != null)
            {
                label.text = moduleRequested ? "Unload Module" : "Load Module";
            }

            DemoUIShared.SetButtonColor(loadOrUnloadButton, moduleRequested ? DemoUIShared.UnloadColor : DemoUIShared.LoadColor);

            if (modelDropdown != null)
            {
                modelDropdown.interactable = !moduleRequested;
            }
        }

        private void HandleRecognizerReadyState(bool ready)
        {
            if (!moduleRequested)
            {
                return;
            }

            statusText.text = ready
                ? "Model ready. Tap Record to capture speech."
                : "Recognizer not ready.";

            if (ready)
            {
                CompleteLoading(statusText.text);
            }
            else
            {
                BeginLoading("Loading model…");
            }

            UpdateRecordButtonState(ready);
        }

        private void ToggleRecording()
        {
            if (!moduleRequested || offlineRecognizer == null || !offlineRecognizer.IsInitialized)
            {
                statusText.text = "Load the offline model before recording.";
                return;
            }

            if (isRecording)
            {
                _ = StopAndTranscribeAsync();
            }
            else
            {
                StartRecording();
            }
        }

        private void StartRecording()
        {
            if (microphoneInput == null)
            {
                statusText.text = "Assign SherpaMicrophoneInput.";
                return;
            }

            recordedSamples.Clear();
            recordingSampleRate = Mathf.Max(8000, microphoneInput.OutputSampleRate);

            ClearTranscript("Recording in progress…");

            SubscribeMicrophone();

            if (!microphoneInput.IsCapturing)
            {
                if (!microphoneInput.TryStartCapture())
                {
                    statusText.text = "Failed to start microphone capture.";
                    UnsubscribeMicrophone();
                    return;
                }
            }

            isRecording = true;
            statusText.text = "Recording… tap again to transcribe.";
            UpdateRecordButtonState(true);
        }

        private async Task StopAndTranscribeAsync()
        {
            if (!isRecording)
            {
                return;
            }

            isRecording = false;
            UnsubscribeMicrophone();
            microphoneInput?.StopCapture();
            UpdateRecordButtonState(offlineRecognizer != null && offlineRecognizer.IsInitialized);

            if (recordedSamples.Count == 0)
            {
                statusText.text = "Recording was empty.";
                return;
            }

            var samples = recordedSamples.ToArray();
            var clip = AudioClip.Create("RecordedClip", samples.Length, 1, recordingSampleRate, false);
            clip.SetData(samples, 0);

            statusText.text = "Transcribing…";

            try
            {
                var transcript = await offlineRecognizer.TranscribeClipAsync(clip, operationCts?.Token ?? CancellationToken.None).ConfigureAwait(true);
                if (string.IsNullOrWhiteSpace(transcript))
                {
                    statusText.text = "No transcript returned.";
                    return;
                }

                statusText.text = "Transcription complete.";
                HandleTranscriptReady(transcript);
            }
            catch (System.Exception ex)
            {
                statusText.text = $"Transcription failed: {ex.Message}";
            }
        }

        private void HandleTranscriptReady(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            var trimmed = text.Trim();
            if (string.Equals(trimmed, lastTranscript, System.StringComparison.Ordinal))
            {
                return;
            }

            // 追加识别结果，保持历史 / Append recognized text while keeping history
            ClearTranscript();
            rollingTranscript.Append(trimmed);
            lastTranscript = trimmed;
            transcriptText.text = rollingTranscript.ToString();
        }

        private void HandleTranscriptionFailed(string message)
        {
            statusText.text = message;
        }

        private void UpdateRecordButtonState(bool ready)
        {
            if (recordButton == null)
            {
                return;
            }

            recordButton.gameObject.SetActive(ready);

            var label = recordButton.GetComponentInChildren<Text>();
            if (label != null)
            {
                label.text = isRecording ? "Stop Recording" : "Record";
            }

            recordButton.interactable = ready;
            var color = !ready ? DemoUIShared.DisabledColor : (isRecording ? DemoUIShared.RecordStopColor : DemoUIShared.RecordIdleColor);
            DemoUIShared.SetButtonColor(recordButton, color);
        }

        private void SubscribeMicrophone()
        {
            if (microphoneInput != null)
            {
                microphoneInput.ChunkReady += HandleMicrophoneChunk;
            }
        }

        private void UnsubscribeMicrophone()
        {
            if (microphoneInput != null)
            {
                microphoneInput.ChunkReady -= HandleMicrophoneChunk;
            }
        }

        private void HandleMicrophoneChunk(float[] samples, int sampleRate)
        {
            if (!isRecording || samples == null || samples.Length == 0)
            {
                return;
            }

            if (recordingSampleRate != sampleRate)
            {
                recordingSampleRate = sampleRate;
            }

            recordedSamples.AddRange(samples);
        }

        private void ClearTranscript(string placeholder = "")
        {
            rollingTranscript.Clear();
            lastTranscript = string.Empty;
            if (transcriptText != null)
            {
                transcriptText.text = placeholder;
            }
        }

        private void BeginLoading(string message)
        {
            // 显示模型加载进度 / Show model loading progress bar
            DemoUIShared.ShowLoading(progressTracker, statusText, message);
        }

        private void CompleteLoading(string message)
        {
            DemoUIShared.ShowLoadingComplete(progressTracker, statusText, message);
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

        public void OpenGithubRepo()
        {
            Application.OpenURL("https://github.com/EitanWong/com.eitan.sherpa-onnx-unity");
        }
    }
}
