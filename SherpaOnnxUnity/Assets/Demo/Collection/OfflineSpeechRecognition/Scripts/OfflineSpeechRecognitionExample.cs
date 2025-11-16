namespace Eitan.SherpaONNXUnity.Samples
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Eitan.Sherpa.Onnx.Unity.Mono.Components;
    using Eitan.Sherpa.Onnx.Unity.Mono.Inputs;
    using Eitan.SherpaONNXUnity.Runtime;
    using UnityEngine;
    using UnityEngine.UI;

    /// <summary>
    /// Demonstrates an offline transcription pipeline built purely from sherpa-onnx MonoBehaviours.
    /// VoiceActivityDetectionComponent segments speech while OfflineSpeechRecognizerComponent transcribes it.
    /// </summary>
    public sealed class OfflineSpeechRecognitionExample : MonoBehaviour
    {
        [Header("Sherpa Components")]
        [SerializeField] private OfflineSpeechRecognizerComponent offlineRecognizer;
        [SerializeField] private VoiceActivityDetectionComponent voiceActivity;
        [SerializeField] private SherpaMicrophoneInput microphone;

        [Header("UI")]
        [SerializeField] private Dropdown speechModelDropdown;
        [SerializeField] private Button loadOrUnloadButton;
        [SerializeField] private Text statusText;
        [SerializeField] private Text transcriptText;

        [SerializeField]
        [Tooltip("Model id used for the VAD component.")]
        private string vadModelId = "silero-vad-latest";

        private readonly StringBuilder rollingTranscript = new StringBuilder();
        private bool modulesRequested;

        private void Awake()
        {
            if (loadOrUnloadButton != null)
            {
                loadOrUnloadButton.onClick.AddListener(ToggleModules);
            }

            if (offlineRecognizer != null)
            {
                offlineRecognizer.TranscriptReadyEvent.AddListener(HandleTranscriptReady);
                offlineRecognizer.TranscriptionFailedEvent.AddListener(message => statusText.text = message);
            }

            if (voiceActivity != null)
            {
                voiceActivity.SpeakingStateChanged += HandleSpeakingStateChanged;
                voiceActivity.InitializationStateChangedEvent.AddListener(HandleVadReadyState);
                if (microphone != null)
                {
                    voiceActivity.BindInput(microphone);
                }
            }

            if (offlineRecognizer != null && voiceActivity != null)
            {
                offlineRecognizer.BindVoiceActivitySource(voiceActivity);
            }
        }

        private void OnEnable()
        {
            _ = PopulateSpeechModelsAsync();
            transcriptText.text = "Load a model to start segment based transcription.";
            UpdateButtonLabel();
        }

        private void OnDestroy()
        {
            if (loadOrUnloadButton != null)
            {
                loadOrUnloadButton.onClick.RemoveListener(ToggleModules);
            }

            if (offlineRecognizer != null)
            {
                offlineRecognizer.TranscriptReadyEvent.RemoveListener(HandleTranscriptReady);
            }

            if (voiceActivity != null)
            {
                voiceActivity.SpeakingStateChanged -= HandleSpeakingStateChanged;
                voiceActivity.InitializationStateChangedEvent.RemoveListener(HandleVadReadyState);
            }
        }

        private async Task PopulateSpeechModelsAsync()
        {
            if (speechModelDropdown == null)
            {
                return;
            }

            speechModelDropdown.options.Clear();
            speechModelDropdown.captionText.text = "Loading speech models…";
            speechModelDropdown.interactable = false;
            if (loadOrUnloadButton != null)
            {
                loadOrUnloadButton.interactable = false;
            }

            var manifest = await SherpaONNXModelRegistry.Instance.GetManifestAsync(SherpaONNXModuleType.SpeechRecognition).ConfigureAwait(true);

            if (loadOrUnloadButton != null)
            {
                loadOrUnloadButton.interactable = true;
            }

            speechModelDropdown.options.Clear();
            if (manifest.models == null || manifest.models.Count == 0)
            {
                speechModelDropdown.options.Add(new Dropdown.OptionData("<no offline models>"));
                return;
            }

            List<Dropdown.OptionData> options = manifest.models
                .Where(m => !SherpaONNXUnityAPI.IsOnlineModel(m.modelId))
                .Select(m => new Dropdown.OptionData(m.modelId))
                .ToList();

            speechModelDropdown.AddOptions(options);
            speechModelDropdown.interactable = true;
        }

        private string SelectedSpeechModelId =>
            speechModelDropdown != null && speechModelDropdown.options.Count > 0
                ? speechModelDropdown.options[speechModelDropdown.value].text
                : string.Empty;

        private void ToggleModules()
        {
            if (offlineRecognizer == null || voiceActivity == null)
            {
                statusText.text = "Assign the sherpa components in the inspector.";
                return;
            }

            if (!modulesRequested)
            {
                var asrModelId = SelectedSpeechModelId;
                if (string.IsNullOrWhiteSpace(asrModelId))
                {
                    statusText.text = "Select an offline ASR model first.";
                    return;
                }

                voiceActivity.ModelId = vadModelId;
                offlineRecognizer.ModelId = asrModelId.Trim();

                var vadLoaded = voiceActivity.TryLoadModule();
                var asrLoaded = offlineRecognizer.TryLoadModule();
                modulesRequested = vadLoaded && asrLoaded;
                statusText.text = modulesRequested
                    ? $"Loading {offlineRecognizer.ModelId} with VAD {vadModelId}"
                    : "Unable to start modules.";
            }
            else
            {
                offlineRecognizer.DisposeModule();
                voiceActivity.DisposeModule();
                modulesRequested = false;
                statusText.text = "Modules disposed.";
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
                label.text = modulesRequested ? "Unload Modules" : "Load Modules";
            }
        }

        private void HandleVadReadyState(bool ready)
        {
            if (!modulesRequested)
            {
                return;
            }

            statusText.text = ready
                ? "Listening for speech segments…"
                : "Voice activity detector not ready.";
        }

        private void HandleSpeakingStateChanged(bool speaking)
        {
            if (!modulesRequested)
            {
                return;
            }

            statusText.text = speaking ? "Speech detected…" : "Waiting for speech";
        }

        private void HandleTranscriptReady(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            if (rollingTranscript.Length > 0)
            {
                rollingTranscript.AppendLine();
            }

            rollingTranscript.Append(text.Trim());
            transcriptText.text = rollingTranscript.ToString();
        }

        public void OpenGithubRepo()
        {
            Application.OpenURL("https://github.com/EitanWong/com.eitan.sherpa-onnx-unity");
        }
    }
}
