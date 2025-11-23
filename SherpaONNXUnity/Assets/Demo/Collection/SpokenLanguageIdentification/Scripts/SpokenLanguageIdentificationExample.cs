namespace Eitan.SherpaONNXUnity.Samples
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Eitan.Sherpa.Onnx.Unity.Mono.Components;
    using Eitan.Sherpa.Onnx.Unity.Mono.Inputs;
    using Eitan.SherpaONNXUnity.Runtime;
    using UnityEngine;
    using UnityEngine.UI;

    /// <summary>
    /// Spoken language ID demo that stays UI-friendly and easy to follow.
    /// 口语语言识别示例，界面友好、逻辑简单易懂。
    /// </summary>
    public sealed class SpokenLanguageIdentificationExample : MonoBehaviour
    {
        [Header("Sherpa Components")]
        [SerializeField] private SpokenLanguageIdentificationComponent identifier;
        [SerializeField] private SherpaMicrophoneInput microphone;

        [Header("UI")]
        [SerializeField] private Dropdown modelDropdown;
        [SerializeField] private Button loadOrUnloadButton;
        [SerializeField] private Button recordButton;
        [SerializeField] private Text statusText;
        [SerializeField] private Text resultText;

        [Header("Loading UI / Progress")]
        [SerializeField] private UI.EasyProgressBar progressBar;
        [SerializeField] private Text progressValueText;
        [SerializeField] private Text progressMessageText;

        [Header("Defaults")]
        [SerializeField] private string defaultModelID = "sherpa-onnx-whisper-tiny";

        private readonly List<float> captureBuffer = new List<float>(8192);
        private ModelLoadProgressTracker progressTracker;
        private bool modelRequested;
        private bool modelReady;
        private bool recording;

        private void Awake()
        {
            if (loadOrUnloadButton != null)
            {
                loadOrUnloadButton.onClick.AddListener(ToggleModel);
            }

            if (recordButton != null)
            {
                recordButton.onClick.AddListener(ToggleRecording);
            }

            // 初始化进度条跟踪 / Initialize progress tracking UI
            progressTracker = new ModelLoadProgressTracker(
                progressBar,
                progressValueText,
                progressMessageText != null ? progressMessageText : statusText);
            progressTracker.SetVisible(false);

            if (identifier != null)
            {
                identifier.LanguageIdentifiedEvent.AddListener(text => resultText.text = text);
                identifier.IdentificationFailedEvent.AddListener(message => statusText.text = message);
                identifier.InitializationStateChangedEvent.AddListener(HandleIdentifierReadyState);
            }
        }

        private void OnEnable()
        {
            _ = PopulateDropdownAsync();
            statusText.text = "Load a language id model.";
            resultText.text = "Tap Load Model to start spoken language identification.";
            SetRecordingUiVisible(false);
            UpdateButtons();
        }

        private void OnDestroy()
        {
            if (loadOrUnloadButton != null)
            {
                loadOrUnloadButton.onClick.RemoveListener(ToggleModel);
            }

            if (recordButton != null)
            {
                recordButton.onClick.RemoveListener(ToggleRecording);
            }

            if (identifier != null)
            {
                identifier.LanguageIdentifiedEvent.RemoveAllListeners();
                identifier.IdentificationFailedEvent.RemoveAllListeners();
                identifier.InitializationStateChangedEvent.RemoveListener(HandleIdentifierReadyState);
            }

            if (microphone != null)
            {
                microphone.ChunkReady -= HandleMicrophoneChunk;
            }

        }

        private async Task PopulateDropdownAsync()
        {
            if (modelDropdown == null)
            {
                return;
            }

            modelDropdown.options.Clear();
            modelDropdown.captionText.text = "Loading language models…";
            modelDropdown.interactable = false;

            var manifest = await SherpaONNXModelRegistry.Instance
                .GetManifestAsync(SherpaONNXModuleType.SpokenLanguageIdentification)
                .ConfigureAwait(true);

            if (manifest.models == null || manifest.models.Count == 0)
            {
                modelDropdown.options.Add(new Dropdown.OptionData("<no models>"));
                return;
            }

            List<Dropdown.OptionData> options = manifest.models
                .Where(m => !string.IsNullOrWhiteSpace(m.modelId))
                .Select(m => new Dropdown.OptionData(m.modelId))
                .ToList();

            modelDropdown.AddOptions(options);
            var defaultIndex = options.FindIndex(m => m.text == defaultModelID);
            modelDropdown.value = defaultIndex >= 0 ? defaultIndex : 0;
            modelDropdown.interactable = options.Count > 0;
        }

        private string SelectedModelId =>
            modelDropdown != null && modelDropdown.options.Count > 0
                ? modelDropdown.options[modelDropdown.value].text
                : string.Empty;

        private void ToggleModel()
        {
            if (identifier == null)
            {
                statusText.text = "Assign the SpokenLanguageIdentificationComponent.";
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

                identifier.ModelId = modelId.Trim();
                if (identifier.TryLoadModule())
                {
                    modelRequested = true;
                    modelReady = false;
                    BeginLoading($"Loading {identifier.ModelId}…");
                }
            }
            else
            {
                identifier.DisposeModule();
                modelRequested = false;
                modelReady = false;
                progressTracker?.Reset();
                progressTracker?.SetVisible(false);
                statusText.text = "Model disposed.";
                captureBuffer.Clear();
                SetRecordingUiVisible(false);
            }

            UpdateButtons();
        }

        private void UpdateButtons()
        {
            if (loadOrUnloadButton != null)
            {
                var label = loadOrUnloadButton.GetComponentInChildren<Text>();
                if (label != null)
                {
                    label.text = modelRequested ? "Unload Model" : "Load Model";
                }
                DemoUIShared.SetButtonColor(loadOrUnloadButton, modelRequested ? DemoUIShared.UnloadColor : DemoUIShared.LoadColor);
                loadOrUnloadButton.interactable = !recording;
            }

            if (recordButton != null)
            {
                recordButton.interactable = modelRequested && modelReady;
                recordButton.gameObject.SetActive(modelReady);
                var label = recordButton.GetComponentInChildren<Text>();
                if (label != null)
                {
                    label.text = recording ? "Stop" : "Record";
                }

                var color = !recordButton.interactable
                    ? DemoUIShared.DisabledColor
                    : (recording ? DemoUIShared.RecordStopColor : DemoUIShared.RecordIdleColor);
                DemoUIShared.SetButtonColor(recordButton, color);
            }

            if (modelDropdown != null)
            {
                modelDropdown.interactable = !modelRequested;
            }
        }

        private void ToggleRecording()
        {
            if (!modelRequested || !modelReady || microphone == null)
            {
                statusText.text = "Wait for the model to finish loading before recording.";
                return;
            }

            if (recording)
            {
                StopRecordingAndIdentify();
                return;
            }

            captureBuffer.Clear();
            microphone.ChunkReady += HandleMicrophoneChunk;
            if (!microphone.TryStartCapture())
            {
                statusText.text = "Unable to access microphone.";
                microphone.ChunkReady -= HandleMicrophoneChunk;
                return;
            }

            recording = true;
            UpdateButtons();
            statusText.text = "Recording… tap again to stop.";
        }

        private void StopRecordingAndIdentify()
        {
            if (!recording)
            {
                return;
            }

            microphone.StopCapture();
            microphone.ChunkReady -= HandleMicrophoneChunk;
            recording = false;
            DemoUIShared.SetButtonColor(recordButton, DemoUIShared.RecordIdleColor);
            UpdateButtons();

            if (captureBuffer.Count == 0)
            {
                statusText.text = "No audio captured.";
                return;
            }

            _ = IdentifyAsync();
        }

        private void HandleMicrophoneChunk(float[] samples, int sampleRate)
        {
            if (!recording || samples == null)
            {
                return;
            }

            captureBuffer.AddRange(samples);
        }

        private async Task IdentifyAsync()
        {
            if (identifier == null)
            {
                return;
            }

            // 推理并实时反馈 / Run inference and update feedback
            statusText.text = "Inferring language…";
            var samples = captureBuffer.ToArray();
            var language = await identifier.IdentifySamplesAsync(samples, microphone.OutputSampleRate).ConfigureAwait(true);
            if (string.IsNullOrWhiteSpace(language))
            {
                statusText.text = "No language detected.";
            }
            else
            {
                statusText.text = "Language detected:";
                resultText.text = language;
            }
        }

        private void HandleIdentifierReadyState(bool ready)
        {
            modelReady = ready && modelRequested;
            if (!ready && recording)
            {
                StopRecordingAndIdentify();
            }

            if (modelRequested)
            {
                if (ready)
                {
                    DemoUIShared.ShowLoadingComplete(progressTracker, statusText, $"Loaded {identifier.ModelId}. Tap record and speak.");
                    SetRecordingUiVisible(true);
                }
                else
                {
                    DemoUIShared.ShowLoading(progressTracker, statusText, "Loading model…");
                }
            }

            UpdateButtons();
        }

        private void BeginLoading(string message)
        {
            DemoUIShared.ShowLoading(progressTracker, statusText, message);
            SetRecordingUiVisible(false);
        }

        private void SetRecordingUiVisible(bool visible)
        {
            if (recordButton != null)
            {
                recordButton.gameObject.SetActive(visible);
            }
        }

        public void OpenGithubRepo()
        {
            Application.OpenURL("https://github.com/EitanWong/com.eitan.sherpa-onnx-unity");
        }
    }
}
