namespace Eitan.SherpaONNXUnity.Samples
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Eitan.Sherpa.Onnx.Unity.Mono.Components;
    using Eitan.SherpaONNXUnity.Runtime;
    using UnityEngine;
    using UnityEngine.UI;

    /// <summary>
    /// Simple UI bridge for <see cref="SpeechSynthesizerComponent"/>.
    /// Type a sentence, optionally tweak voice/speed, then generate speech.
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

        [SerializeField]
        [Tooltip("Placeholder text that can be inserted into the text field on Enable.")]
        private string defaultUtterance = "SherpaONNX makes neural speech easy.";

        private bool modelRequested;

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

            if (synthesizer != null)
            {
                synthesizer.SynthesisStartedEvent.AddListener(() => statusText.text = "Generating audio…");
                synthesizer.ClipReadyEvent.AddListener(clip => statusText.text = clip != null ? $"Generated {clip.length:F1}s clip." : "Empty clip returned.");
                synthesizer.SynthesisFailedEvent.AddListener(message => statusText.text = message);
            }
        }

        private void OnEnable()
        {
            if (textInput != null && string.IsNullOrWhiteSpace(textInput.text))
            {
                textInput.text = defaultUtterance;
            }

            if (speedSlider != null)
            {
                speedLabel.text = $"Speed: {speedSlider.value:F1}x";
            }

            _ = PopulateDropdownAsync();
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
            }
        }

        private async Task PopulateDropdownAsync()
        {
            if (modelDropdown == null)
            {
                return;
            }

            modelDropdown.options.Clear();
            modelDropdown.captionText.text = "Loading TTS models…";
            modelDropdown.interactable = false;

            var manifest = await SherpaONNXModelRegistry.Instance.GetManifestAsync(SherpaONNXModuleType.SpeechSynthesis).ConfigureAwait(true);

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
                    statusText.text = $"Loaded {synthesizer.ModelId}.";
                }
            }
            else
            {
                synthesizer.DisposeModule();
                modelRequested = false;
                statusText.text = "Model disposed.";
            }

            UpdateButtonLabel();
            synthesizeButton.interactable = modelRequested;
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

        private async void StartSynthesis()
        {
            if (!modelRequested || synthesizer == null)
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
        }

        public void OpenGithubRepo()
        {
            Application.OpenURL("https://github.com/EitanWong/com.eitan.sherpa-onnx-unity");
        }
    }
}
