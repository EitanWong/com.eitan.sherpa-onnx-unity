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
    /// Small wrapper that shows how to drive <see cref="KeywordSpottingComponent"/> from UI controls.
    /// </summary>
    public sealed class KeywordSpottingExample : MonoBehaviour
    {
        [Header("Sherpa Components")]
        [SerializeField] private KeywordSpottingComponent keywordSpotter;
        [SerializeField] private SherpaMicrophoneInput microphone;

        [Header("UI")]
        [SerializeField] private Dropdown modelDropdown;
        [SerializeField] private Button loadOrUnloadButton;
        [SerializeField] private Text statusText;
        [SerializeField] private Text keywordDisplay;
        [SerializeField] private float keywordHoldSeconds = 2f;

        private bool moduleRequested;
        private Coroutine clearRoutine;

        private void Awake()
        {
            if (loadOrUnloadButton != null)
            {
                loadOrUnloadButton.onClick.AddListener(ToggleModel);
            }

            if (keywordSpotter != null)
            {
                keywordSpotter.KeywordDetectedEvent.AddListener(HandleKeywordDetected);
                keywordSpotter.InitializationStateChangedEvent.AddListener(HandleReadyStateChanged);
                keywordSpotter.FeedbackMessages.AddListener(message => statusText.text = message);
                if (microphone != null)
                {
                    keywordSpotter.BindInput(microphone);
                }
            }
        }

        private void OnEnable()
        {
            _ = PopulateDropdownAsync();
            UpdateButtonLabel();
            keywordDisplay.text = "Load a keyword model and speak the wake word.";
        }

        private void OnDestroy()
        {
            if (loadOrUnloadButton != null)
            {
                loadOrUnloadButton.onClick.RemoveListener(ToggleModel);
            }

            if (keywordSpotter != null)
            {
                keywordSpotter.KeywordDetectedEvent.RemoveListener(HandleKeywordDetected);
                keywordSpotter.InitializationStateChangedEvent.RemoveListener(HandleReadyStateChanged);
            }
        }

        private async Task PopulateDropdownAsync()
        {
            if (modelDropdown == null)
            {
                return;
            }

            modelDropdown.options.Clear();
            modelDropdown.captionText.text = "Loading keyword models…";
            modelDropdown.interactable = false;
            if (loadOrUnloadButton != null)
            {
                loadOrUnloadButton.interactable = false;
            }

            var manifest = await SherpaONNXModelRegistry.Instance.GetManifestAsync(SherpaONNXModuleType.KeywordSpotting).ConfigureAwait(true);

            if (loadOrUnloadButton != null)
            {
                loadOrUnloadButton.interactable = true;
            }

            modelDropdown.options.Clear();
            if (manifest.models == null || manifest.models.Count == 0)
            {
                modelDropdown.options.Add(new Dropdown.OptionData("<no keyword models>"));
                return;
            }

            List<Dropdown.OptionData> options = manifest.models
                .Where(m => !string.IsNullOrWhiteSpace(m.modelId))
                .Select(m => new Dropdown.OptionData(m.modelId))
                .ToList();

            modelDropdown.AddOptions(options);
            modelDropdown.interactable = true;
        }

        private void ToggleModel()
        {
            if (keywordSpotter == null)
            {
                statusText.text = "KeywordSpottingComponent missing.";
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

                keywordSpotter.ModelId = modelId.Trim();
                if (keywordSpotter.TryLoadModule())
                {
                    moduleRequested = true;
                    statusText.text = $"Loading {keywordSpotter.ModelId}…";
                }
            }
            else
            {
                keywordSpotter.DisposeModule();
                moduleRequested = false;
                statusText.text = "Model unloaded.";
                keywordDisplay.text = string.Empty;
            }

            UpdateButtonLabel();
        }

        private string SelectedModelId =>
            modelDropdown != null && modelDropdown.options.Count > 0
                ? modelDropdown.options[modelDropdown.value].text
                : string.Empty;

        private void UpdateButtonLabel()
        {
            if (loadOrUnloadButton == null)
            {
                return;
            }

            var label = loadOrUnloadButton.GetComponentInChildren<Text>();
            if (label != null)
            {
                label.text = moduleRequested ? "Unload Model" : "Load Model";
            }
        }

        private void HandleReadyStateChanged(bool ready)
        {
            if (!moduleRequested)
            {
                return;
            }

            statusText.text = ready
                ? "Listening for registered wake words…"
                : "Model not ready.";
        }

        private void HandleKeywordDetected(string keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword))
            {
                return;
            }

            if (clearRoutine != null)
            {
                StopCoroutine(clearRoutine);
            }

            keywordDisplay.text = $"<color=cyan><b>{keyword}</b></color>";
            statusText.text = "Keyword detected!";
            clearRoutine = StartCoroutine(ClearKeywordAfterDelay());
        }

        private System.Collections.IEnumerator ClearKeywordAfterDelay()
        {
            yield return new WaitForSeconds(keywordHoldSeconds);
            keywordDisplay.text = "Awaiting keyword…";
            statusText.text = "Listening…";
            clearRoutine = null;
        }

        public void OpenGithubRepo()
        {
            Application.OpenURL("https://github.com/EitanWong/com.eitan.sherpa-onnx-unity");
        }
    }
}
