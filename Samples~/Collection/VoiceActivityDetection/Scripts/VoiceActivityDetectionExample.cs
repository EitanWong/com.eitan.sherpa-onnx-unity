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
    /// Voice activity detection demo with gated UI and visual feedback.
    /// 语音活动检测示例，包含加载进度与可视化反馈。
    /// </summary>
    public sealed class VoiceActivityDetectionExample : MonoBehaviour
    {
        [Header("Sherpa Components")]
        [SerializeField] private VoiceActivityDetectionComponent voiceActivity;
        [SerializeField] private SherpaMicrophoneInput microphone;

        [Header("UI")]
        [SerializeField] private Dropdown modelDropdown;
        [SerializeField] private Button loadOrUnloadButton;
        [SerializeField] private Text statusText;
        [SerializeField] private Text segmentText;

        [Header("Loading UI / Progress")]
        [SerializeField] private UI.EasyProgressBar progressBar;
        [SerializeField] private Text progressValueText;
        [SerializeField] private Text progressMessageText;

        [Header("Defaults")]
        [SerializeField] private string defaultModelID = "silero-vad";

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

            progressTracker = new ModelLoadProgressTracker(
                progressBar,
                progressValueText,
                progressMessageText != null ? progressMessageText : statusText);
            progressTracker.SetVisible(false);

            if (voiceActivity != null)
            {
                voiceActivity.SpeakingStateChanged += HandleSpeakingStateChanged;
                voiceActivity.SpeechSegmentReady += HandleSegmentReady;
                voiceActivity.InitializationStateChangedEvent.AddListener(HandleReadyStateChanged);
                if (microphone != null)
                {
                    voiceActivity.BindInput(microphone);
                }
            }
        }

        private void OnEnable()
        {
            manifestCts = new CancellationTokenSource();
            _ = PopulateDropdownAsync(manifestCts.Token);
            statusText.text = "Load a VAD model.";
            segmentText.text = "Tap Load Model to start voice activity detection.";
            modelReady = false;
            UpdateButtonLabel();
        }

        private void OnDestroy()
        {
            if (loadOrUnloadButton != null)
            {
                loadOrUnloadButton.onClick.RemoveListener(ToggleModel);
            }

            if (voiceActivity != null)
            {
                voiceActivity.SpeakingStateChanged -= HandleSpeakingStateChanged;
                voiceActivity.SpeechSegmentReady -= HandleSegmentReady;
                voiceActivity.InitializationStateChangedEvent.RemoveListener(HandleReadyStateChanged);
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
            modelDropdown.captionText.text = "Loading VAD models…";
            modelDropdown.interactable = false;

            try
            {
                var manifest = await SherpaONNXModelRegistry.Instance
                    .GetManifestAsync(SherpaONNXModuleType.VoiceActivityDetection, cancellationToken)
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

        private string SelectedModelId =>
            modelDropdown != null && modelDropdown.options.Count > 0
                ? modelDropdown.options[modelDropdown.value].text
                : string.Empty;

        private void ToggleModel()
        {
            if (voiceActivity == null)
            {
                statusText.text = "Assign the VoiceActivityDetectionComponent.";
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

                voiceActivity.ModelId = modelId.Trim();
                if (voiceActivity.TryLoadModule())
                {
                    modelRequested = true;
                    modelReady = false;
                    BeginLoading($"Loading {voiceActivity.ModelId}…");
                }
            }
            else
            {
                voiceActivity.DisposeModule();
                modelRequested = false;
                modelReady = false;
                statusText.text = "Model disposed.";
                segmentText.text = string.Empty;
                progressTracker?.Reset();
                progressTracker?.SetVisible(false);
            }

            UpdateButtonLabel();
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
            loadOrUnloadButton.interactable = true;

            if (modelDropdown != null)
            {
                modelDropdown.interactable = !modelRequested;
            }
        }

        private void HandleReadyStateChanged(bool ready)
        {
            modelReady = ready && modelRequested;

            if (!modelRequested)
            {
                return;
            }

            statusText.text = ready ? "Listening for speech…" : "Model not ready.";

            if (ready)
            {
                DemoUIShared.ShowLoadingComplete(progressTracker, statusText, statusText.text);
            }
            else
            {
                DemoUIShared.ShowLoading(progressTracker, statusText, "Loading model…");
            }

            UpdateButtonLabel();
        }

        private void HandleSpeakingStateChanged(bool speaking)
        {
            if (!modelRequested)
            {
                return;
            }

            // 实时显示说话状态 / Live speaking state feedback
            statusText.text = speaking ? "Speech detected" : "Silence";
        }

        private void HandleSegmentReady(float[] samples, int sampleRate)
        {
            if (samples == null || samples.Length == 0)
            {
                return;
            }

            segmentText.text = $"Captured {samples.Length / (float)sampleRate:F1}s of speech";

        }

        private void BeginLoading(string message) => DemoUIShared.ShowLoading(progressTracker, statusText, message);

        public void OpenGithubRepo()
        {
            Application.OpenURL("https://github.com/EitanWong/com.eitan.sherpa-onnx-unity");
        }
    }
}
