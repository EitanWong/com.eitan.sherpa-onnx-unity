
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
    /// Minimal controller that wires <see cref="SpeechRecognizerComponent"/> to the UI and microphone.
    /// Demonstrates the component driven workflow instead of the manual module management that was used previously.
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

        [SerializeField]
        [Tooltip("Optional message shown while fetching the manifest.")]
        private string loadingMessage = "Fetching speech recognition manifest…";

        private bool moduleRequested;

        private void Awake()
        {
            if (loadOrUnloadButton != null)
            {
                loadOrUnloadButton.onClick.AddListener(ToggleModel);
            }

            if (recognizer != null)
            {
                recognizer.TranscriptionReadyEvent.AddListener(HandleTranscriptReady);
                recognizer.InitializationStateChangedEvent.AddListener(HandleRecognizerReadyState);
                recognizer.FeedbackMessages.AddListener(message => statusText.text = message);
                if (microphone != null)
                {
                    recognizer.BindInput(microphone);
                }
            }
        }

        private void OnEnable()
        {
            _ = PopulateModelDropdownAsync();
            RefreshButtonLabel();
            transcriptText.text = "Tap Load Model to start streaming transcription.";
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
            }
        }

        private async Task PopulateModelDropdownAsync()
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

            var manifest = await SherpaONNXModelRegistry.Instance.GetManifestAsync(SherpaONNXModuleType.SpeechRecognition).ConfigureAwait(true);

            if (loadOrUnloadButton != null)
            {
                loadOrUnloadButton.interactable = true;
            }

            modelDropdown.options.Clear();

            if (manifest.models == null || manifest.models.Count == 0)
            {
                modelDropdown.options.Add(new Dropdown.OptionData("<no speech models>"));
                modelDropdown.interactable = false;
                return;
            }

            List<Dropdown.OptionData> options = manifest.models
                .Where(m => !string.IsNullOrWhiteSpace(m.modelId))
                .Select(m => new Dropdown.OptionData(m.modelId))
                .ToList();

            modelDropdown.AddOptions(options);
            modelDropdown.interactable = true;
            modelDropdown.value = Mathf.Clamp(modelDropdown.value, 0, options.Count - 1);
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
                statusText.text = "SpeechRecognizerComponent reference missing.";
                return;
            }

            if (!moduleRequested)
            {
                var modelId = SelectedModelId;
                if (string.IsNullOrWhiteSpace(modelId))
                {
                    statusText.text = "Select a model first.";
                    return;
                }

                recognizer.ModelId = modelId.Trim();
                if (recognizer.TryLoadModule())
                {
                    moduleRequested = true;
                    statusText.text = $"Loading {recognizer.ModelId}…";
                }
                else
                {
                    statusText.text = "Model already loading or missing configuration.";
                }
            }
            else
            {
                recognizer.DisposeModule();
                moduleRequested = false;
                transcriptText.text = string.Empty;
                statusText.text = "Model disposed.";
            }

            RefreshButtonLabel();
        }

        private void RefreshButtonLabel()
        {
            if (loadOrUnloadButton == null)
            {
                return;
            }

            var label = loadOrUnloadButton.GetComponentInChildren<Text>();
            if (label == null)
            {
                return;
            }

            label.text = moduleRequested ? "Unload Model" : "Load Model";
        }

        private void HandleRecognizerReadyState(bool ready)
        {
            if (!moduleRequested)
            {
                return;
            }

            if (ready)
            {
                statusText.text = "Recognizer ready. Speak into the microphone.";
                transcriptText.text = "Awaiting speech…";
            }
            else
            {
                statusText.text = "Recognizer not ready.";
            }
        }

        private void HandleTranscriptReady(string transcript)
        {
            if (string.IsNullOrWhiteSpace(transcript))
            {
                return;
            }

            transcriptText.text = transcript;
        }

        public void OpenGithubRepo()
        {
            Application.OpenURL("https://github.com/EitanWong/com.eitan.sherpa-onnx-unity");
        }
    }
}
