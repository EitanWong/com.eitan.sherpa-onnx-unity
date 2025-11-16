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
    /// Minimal example using <see cref="SpokenLanguageIdentificationComponent"/> with a short microphone recording.
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

        [Header("Recording")]
        [SerializeField] private float captureDurationSeconds = 4f;

        private readonly List<float> captureBuffer = new List<float>(8192);
        private bool modelRequested;
        private bool modelReady;
        private bool recording;
        private Coroutine captureRoutine;

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
            UpdateButtons();
            statusText.text = "Load a language id model.";
            resultText.text = string.Empty;
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

            if (captureRoutine != null)
            {
                StopCoroutine(captureRoutine);
                captureRoutine = null;
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

            var manifest = await SherpaONNXModelRegistry.Instance.GetManifestAsync(SherpaONNXModuleType.SpokenLanguageIdentification).ConfigureAwait(true);

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
            modelDropdown.interactable = true;
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
                    statusText.text = $"Loading {identifier.ModelId}…";
                }
            }
            else
            {
                identifier.DisposeModule();
                modelRequested = false;
                modelReady = false;
                statusText.text = "Model disposed.";
                resultText.text = string.Empty;
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
            }

            if (recordButton != null)
            {
                recordButton.interactable = modelRequested && modelReady && !recording;
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
            statusText.text = "Recording sample…";
            captureRoutine = StartCoroutine(CaptureRoutine());
        }

        private System.Collections.IEnumerator CaptureRoutine()
        {
            yield return new WaitForSeconds(captureDurationSeconds);
            StopRecordingAndIdentify();
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
            if (captureRoutine != null)
            {
                StopCoroutine(captureRoutine);
                captureRoutine = null;
            }
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
            }
        }

        private void HandleIdentifierReadyState(bool ready)
        {
            modelReady = ready && modelRequested;
            if (!ready && recording)
            {
                StopRecordingAndIdentify();
            }

            UpdateButtons();

            if (modelRequested)
            {
                statusText.text = ready
                    ? $"Loaded {identifier.ModelId}. Tap record and speak."
                    : "Loading model…";
            }
        }

        public void OpenGithubRepo()
        {
            Application.OpenURL("https://github.com/EitanWong/com.eitan.sherpa-onnx-unity");
        }
    }
}
