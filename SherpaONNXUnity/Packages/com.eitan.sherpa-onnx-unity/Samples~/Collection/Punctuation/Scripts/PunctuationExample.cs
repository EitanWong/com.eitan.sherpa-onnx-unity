namespace Eitan.SherpaONNXUnity.Samples
{
    using System.Linq;
    using System.Threading.Tasks;
    using Eitan.Sherpa.Onnx.Unity.Mono.Components;
    using Eitan.SherpaONNXUnity.Runtime;
    using UnityEngine;
    using UnityEngine.UI;
    using Stage = Eitan.SherpaONNXUnity.Samples.ModelLoadProgressTracker.Stage;

    /// <summary>
    /// Lightweight punctuation demo with gated UI and progress visuals.
    /// 断句标点示例，带有进度提示与交互管控。
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

        [Header("Loading UI / Progress")]
        [SerializeField] private UI.EasyProgressBar progressBar;
        [SerializeField] private Text progressValueText;
        [SerializeField] private Text progressMessageText;

        [Header("Defaults")]
        [SerializeField] private string defaultModelID = "sherpa-onnx-punct-ct-transformer-zh-en-vocab272727-2024-04-12-int8";

        private bool modelRequested;
        private bool modelReady;
        private ModelLoadProgressTracker progressTracker;

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

            progressTracker = new ModelLoadProgressTracker(
                progressBar,
                progressValueText,
                progressMessageText != null ? progressMessageText : statusText);
            progressTracker.SetVisible(false);

            if (punctuation != null)
            {
                punctuation.PunctuationReadyEvent.AddListener(HandlePunctuationReady);
                punctuation.PunctuationFailedEvent.AddListener(HandlePunctuationFailed);
                punctuation.InitializationStateChangedEvent.AddListener(HandleReadyState);
            }
        }

        private void OnEnable()
        {
            _ = PopulateDropdownAsync();
            UpdateButtonLabel();
            resultText.text = "Select the model and click the Load button to use punctuation.";
            statusText.text = "Load a punctuation model to enable the button.";
            modelReady = false;
            punctuateButton.interactable = false;
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
                punctuation.InitializationStateChangedEvent.RemoveListener(HandleReadyState);
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

            var manifest = await SherpaONNXModelRegistry.Instance
                .GetManifestAsync(SherpaONNXModuleType.AddPunctuation)
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
                    modelReady = false;
                    BeginLoading($"Loading {punctuation.ModelId}…");
                }
            }
            else
            {
                punctuation.DisposeModule();
                modelRequested = false;
                modelReady = false;
                statusText.text = "Model disposed.";
                resultText.text = string.Empty;
                progressTracker?.Reset();
                progressTracker?.SetVisible(false);
            }

            UpdateButtonLabel();
            punctuateButton.interactable = modelReady;
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
            resultText.text = "The model has been unloaded.";

            if (modelDropdown != null)
            {
                modelDropdown.interactable = !modelRequested;
            }
        }

        private async void ApplyPunctuationAsync()
        {
            if (!modelRequested || punctuation == null || !modelReady)
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
                resultText.text = output;
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

        private void HandleReadyState(bool ready)
        {
            modelReady = ready && modelRequested;
            if (modelRequested)
            {
                if (ready)
                {
                    CompleteLoading($"Loaded {punctuation.ModelId}. Enter text to punctuate.");
                }
                else
                {
                    BeginLoading("Loading model…");
                }
            }

            punctuateButton.interactable = modelReady;
            UpdateButtonLabel();
        }

        private void BeginLoading(string message)
        {
            DemoUIShared.ShowLoading(progressTracker, statusText, message);
        }

        private void CompleteLoading(string message)
        {
            DemoUIShared.ShowLoadingComplete(progressTracker, statusText, message);
        }

        public void OpenGithubRepo()
        {
            Application.OpenURL("https://github.com/EitanWong/com.eitan.sherpa-onnx-unity");
        }
    }
}
