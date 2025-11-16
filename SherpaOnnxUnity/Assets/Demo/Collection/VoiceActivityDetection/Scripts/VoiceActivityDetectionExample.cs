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
    /// Quick-start UI for <see cref="VoiceActivityDetectionComponent"/>.
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

        private bool modelRequested;

        private void Awake()
        {
            if (loadOrUnloadButton != null)
            {
                loadOrUnloadButton.onClick.AddListener(ToggleModel);
            }

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
            _ = PopulateDropdownAsync();
            statusText.text = "Load a VAD model.";
            segmentText.text = string.Empty;
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
        }

        private async Task PopulateDropdownAsync()
        {
            if (modelDropdown == null)
            {
                return;
            }

            modelDropdown.options.Clear();
            modelDropdown.captionText.text = "Loading VAD models…";
            modelDropdown.interactable = false;

            var manifest = await SherpaONNXModelRegistry.Instance.GetManifestAsync(SherpaONNXModuleType.VoiceActivityDetection).ConfigureAwait(true);

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
                    statusText.text = $"Loaded {voiceActivity.ModelId}.";
                }
            }
            else
            {
                voiceActivity.DisposeModule();
                modelRequested = false;
                statusText.text = "Model disposed.";
                segmentText.text = string.Empty;
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
        }

        private void HandleReadyStateChanged(bool ready)
        {
            if (!modelRequested)
            {
                return;
            }

            statusText.text = ready ? "Listening for speech…" : "Model not ready.";
        }

        private void HandleSpeakingStateChanged(bool speaking)
        {
            if (!modelRequested)
            {
                return;
            }

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

        public void OpenGithubRepo()
        {
            Application.OpenURL("https://github.com/EitanWong/com.eitan.sherpa-onnx-unity");
        }
    }
}
