namespace Eitan.SherpaONNXUnity.Samples
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Eitan.Sherpa.Onnx.Unity.Mono.Components;
    using Eitan.SherpaONNXUnity.Runtime;
    using UnityEngine;
    using UnityEngine.UI;

    /// <summary>
    /// Tiny UI controller that showcases <see cref="PunctuationComponent"/>.
    /// </summary>
    public sealed class PunctuationExample : MonoBehaviour
    {
        [Header("Sherpa Component")]
        [SerializeField] private PunctuationComponent punctuation;

        [Header("UI")]
        [SerializeField] private Dropdown modelDropdown;
        [SerializeField] private Button loadOrUnloadButton;
        [SerializeField] private InputField inputField;
        [SerializeField] private Button punctuateButton;
        [SerializeField] private Text resultText;
        [SerializeField] private Text statusText;

        private bool modelRequested;

        private void Awake()
        {
            if (loadOrUnloadButton != null)
            {
                loadOrUnloadButton.onClick.AddListener(ToggleModel);
            }

            if (punctuateButton != null)
            {
                punctuateButton.onClick.AddListener(ApplyPunctuationAsync);
            }

            if (punctuation != null)
            {
                punctuation.PunctuationReadyEvent.AddListener(HandlePunctuationReady);
                punctuation.PunctuationFailedEvent.AddListener(HandlePunctuationFailed);
            }
        }

        private void OnEnable()
        {
            _ = PopulateDropdownAsync();
            UpdateButtonLabel();
            resultText.text = string.Empty;
            statusText.text = "Load a punctuation model to enable the button.";
        }

        private void OnDestroy()
        {
            if (loadOrUnloadButton != null)
            {
                loadOrUnloadButton.onClick.RemoveListener(ToggleModel);
            }

            if (punctuateButton != null)
            {
                punctuateButton.onClick.RemoveListener(ApplyPunctuationAsync);
            }

            if (punctuation != null)
            {
                punctuation.PunctuationReadyEvent.RemoveListener(HandlePunctuationReady);
                punctuation.PunctuationFailedEvent.RemoveListener(HandlePunctuationFailed);
            }
        }

        private async Task PopulateDropdownAsync()
        {
            if (modelDropdown == null)
            {
                return;
            }

            modelDropdown.options.Clear();
            modelDropdown.captionText.text = "Loading punctuation models…";
            modelDropdown.interactable = false;

            var manifest = await SherpaONNXModelRegistry.Instance.GetManifestAsync(SherpaONNXModuleType.AddPunctuation).ConfigureAwait(true);

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
            if (punctuation == null)
            {
                statusText.text = "Assign the PunctuationComponent.";
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

                punctuation.ModelId = modelId.Trim();
                if (punctuation.TryLoadModule())
                {
                    modelRequested = true;
                    statusText.text = $"Loaded {punctuation.ModelId}.";
                }
            }
            else
            {
                punctuation.DisposeModule();
                modelRequested = false;
                statusText.text = "Model disposed.";
                resultText.text = string.Empty;
            }

            UpdateButtonLabel();
            punctuateButton.interactable = modelRequested;
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

        private async void ApplyPunctuationAsync()
        {
            if (!modelRequested || punctuation == null)
            {
                statusText.text = "Load a model first.";
                return;
            }

            if (string.IsNullOrWhiteSpace(inputField?.text))
            {
                statusText.text = "Enter some text to punctuate.";
                return;
            }

            statusText.text = "Processing…";
            resultText.text = string.Empty;
            var output = await punctuation.AddPunctuationAsync(inputField.text).ConfigureAwait(true);
            if (string.IsNullOrWhiteSpace(output))
            {
                statusText.text = "No text returned.";
            }
            else
            {
                statusText.text = "Done.";
            }
        }

        private void HandlePunctuationReady(string text)
        {
            if (!string.IsNullOrWhiteSpace(text))
            {
                resultText.text = text;
            }
        }

        private void HandlePunctuationFailed(string message)
        {
            statusText.text = message;
        }

        public void OpenGithubRepo()
        {
            Application.OpenURL("https://github.com/EitanWong/com.eitan.sherpa-onnx-unity");
        }
    }
}
