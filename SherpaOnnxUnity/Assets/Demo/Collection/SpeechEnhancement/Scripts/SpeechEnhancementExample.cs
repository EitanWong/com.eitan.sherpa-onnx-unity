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
    /// Lightweight UI for <see cref="SpeechEnhancerComponent"/>.
    /// Assign a target AudioSource/clip on the component and press Enhance to denoise it.
    /// </summary>
    public sealed class SpeechEnhancementExample : MonoBehaviour
    {
        [Header("Sherpa Component")]
        [SerializeField] private SpeechEnhancerComponent enhancer;

        [Header("UI")]
        [SerializeField] private Dropdown modelDropdown;
        [SerializeField] private Button loadOrUnloadButton;
        [SerializeField] private Button enhanceButton;
        [SerializeField] private Text statusText;

        private bool modelRequested;

        private void Awake()
        {
            if (loadOrUnloadButton != null)
            {
                loadOrUnloadButton.onClick.AddListener(ToggleModel);
            }

            if (enhanceButton != null)
            {
                enhanceButton.onClick.AddListener(StartEnhancement);
            }

            if (enhancer != null)
            {
                enhancer.ClipEnhancedEvent.AddListener(HandleClipEnhanced);
                enhancer.EnhancementFailedEvent.AddListener(HandleEnhancementFailed);
            }
        }

        private void OnEnable()
        {
            _ = PopulateDropdownAsync();
            UpdateButtonLabel();
            enhanceButton.interactable = false;
            statusText.text = "Load a model to enable enhancement.";
        }

        private void OnDestroy()
        {
            if (loadOrUnloadButton != null)
            {
                loadOrUnloadButton.onClick.RemoveListener(ToggleModel);
            }

            if (enhanceButton != null)
            {
                enhanceButton.onClick.RemoveListener(StartEnhancement);
            }

            if (enhancer != null)
            {
                enhancer.ClipEnhancedEvent.RemoveListener(HandleClipEnhanced);
                enhancer.EnhancementFailedEvent.RemoveListener(HandleEnhancementFailed);
            }
        }

        private async Task PopulateDropdownAsync()
        {
            if (modelDropdown == null)
            {
                return;
            }

            modelDropdown.options.Clear();
            modelDropdown.captionText.text = "Loading enhancer models…";
            modelDropdown.interactable = false;

            var manifest = await SherpaONNXModelRegistry.Instance.GetManifestAsync(SherpaONNXModuleType.SpeechEnhancement).ConfigureAwait(true);

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
            if (enhancer == null)
            {
                statusText.text = "Assign the SpeechEnhancerComponent.";
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

                enhancer.ModelId = modelId.Trim();
                if (enhancer.TryLoadModule())
                {
                    modelRequested = true;
                    statusText.text = $"Loaded {enhancer.ModelId}.";
                }
            }
            else
            {
                enhancer.DisposeModule();
                modelRequested = false;
                statusText.text = "Model disposed.";
            }

            UpdateButtonLabel();
            enhanceButton.interactable = modelRequested;
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

        private void StartEnhancement()
        {
            if (!modelRequested)
            {
                statusText.text = "Load a model first.";
                return;
            }

            statusText.text = "Enhancing clip…";
            enhancer.EnhanceAssignedClip();
        }

        private void HandleClipEnhanced(AudioClip clip)
        {
            if (clip == null)
            {
                statusText.text = "Enhancement finished but no clip returned.";
                return;
            }

            statusText.text = $"Enhanced clip ready ({clip.length:F1}s).";
        }

        private void HandleEnhancementFailed(string message)
        {
            statusText.text = message;
        }

        public void OpenGithubRepo()
        {
            Application.OpenURL("https://github.com/EitanWong/com.eitan.sherpa-onnx-unity");
        }
    }
}
