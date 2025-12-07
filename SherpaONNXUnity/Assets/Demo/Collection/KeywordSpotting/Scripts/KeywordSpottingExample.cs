namespace Eitan.SherpaONNXUnity.Samples
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Threading;

    using System.Threading.Tasks;
    using Eitan.Sherpa.Onnx.Unity.Mono.Components;
    using Eitan.Sherpa.Onnx.Unity.Mono.Inputs;
    using Eitan.SherpaONNXUnity.Runtime;
    using Eitan.SherpaONNXUnity.Runtime.Modules;
    using UnityEngine;
    using UnityEngine.UI;
    using Stage = Eitan.SherpaONNXUnity.Samples.ModelLoadProgressTracker.Stage;

    /// <summary>
    /// Keyword spotting demo with progress visuals and color-coded UI.
    /// 关键词唤醒示例，带有加载进度和颜色提示。
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

        [Header("Custom Keyword UI")]
        [SerializeField] private InputField customKeywordInput;
        [SerializeField] private Button registerKeywordButton;
        [SerializeField] private Button clearKeywordsButton;
        [SerializeField] private Transform keywordListContent;
        [SerializeField] private GameObject keywordItemTemplate;

        [Header("Loading UI / Progress")]
        [SerializeField] private UI.EasyProgressBar progressBar;
        [SerializeField] private Text progressValueText;
        [SerializeField] private Text progressMessageText;

        [Header("Sound")]
        [SerializeField] private AudioClip keywordTriggerSound;

        [Header("Defaults")]
        [SerializeField] private string defaultModelID = "sherpa-onnx-kws-zipformer-wenetspeech-3.3M-2024-01-01";

        private const float DefaultCustomKeywordScore = 2f;
        private const float DefaultCustomKeywordThreshold = 0.25f;

        private readonly List<string> customKeywords = new List<string>();
        private readonly Dictionary<string, GameObject> keywordItemInstances = new Dictionary<string, GameObject>(StringComparer.OrdinalIgnoreCase);

        private bool moduleRequested;
        private bool modelReady;
        private Coroutine clearRoutine;
        private ModelLoadProgressTracker progressTracker;
        private FieldInfo customKeywordsField;
        private CancellationTokenSource manifestCts;

        private void Awake()
        {
            if (loadOrUnloadButton != null)
            {
                loadOrUnloadButton.onClick.AddListener(ToggleModel);
            }

            progressTracker = new ModelLoadProgressTracker(
                progressBar,
                progressValueText,
                progressMessageText != null ? progressMessageText : statusText);
            progressTracker.SetVisible(false);

            if (keywordSpotter != null)
            {
                keywordSpotter.KeywordDetectedEvent.AddListener(HandleKeywordDetected);
                keywordSpotter.InitializationStateChangedEvent.AddListener(HandleReadyStateChanged);
                keywordSpotter.FeedbackMessages.AddListener(HandleFeedbackMessage);
                keywordSpotter.FeedbackReceived += HandleFeedback;
                if (microphone != null)
                {
                    keywordSpotter.BindInput(microphone);
                }
            }

            if (registerKeywordButton != null)
            {
                registerKeywordButton.onClick.AddListener(RegisterCustomKeywordFromInput);
            }

            if (clearKeywordsButton != null)
            {
                clearKeywordsButton.onClick.AddListener(ClearCustomKeywords);
            }

            if (customKeywordInput != null)
            {
                customKeywordInput.onValueChanged.AddListener(_ => UpdateCustomKeywordUIState());
            }

            customKeywordsField = typeof(KeywordSpottingComponent)
                .GetField("customKeywords", BindingFlags.Instance | BindingFlags.NonPublic);

            SyncCustomKeywordsFromComponent();
            RefreshKeywordListUI();
        }

        private void OnEnable()
        {
            manifestCts = new CancellationTokenSource();
            _ = PopulateDropdownAsync(manifestCts.Token);
            keywordDisplay.text = "Load a keyword model and speak the wake word. Add custom keywords below before loading.";
            statusText.text = "Select a model to start. Custom keywords are optional.";
            modelReady = false;
            UpdateButtonLabel();
            UpdateCustomKeywordUIState();
            RefreshKeywordListUI();
        }

        private void OnDestroy()
        {
            if (loadOrUnloadButton != null)
            {
                loadOrUnloadButton.onClick.RemoveListener(ToggleModel);
            }

            if (registerKeywordButton != null)
            {
                registerKeywordButton.onClick.RemoveListener(RegisterCustomKeywordFromInput);
            }

            if (clearKeywordsButton != null)
            {
                clearKeywordsButton.onClick.RemoveListener(ClearCustomKeywords);
            }

            if (customKeywordInput != null)
            {
                customKeywordInput.onValueChanged.RemoveAllListeners();
            }

            if (keywordSpotter != null)
            {
                keywordSpotter.KeywordDetectedEvent.RemoveListener(HandleKeywordDetected);
                keywordSpotter.InitializationStateChangedEvent.RemoveListener(HandleReadyStateChanged);
                keywordSpotter.FeedbackMessages.RemoveListener(HandleFeedbackMessage);
                keywordSpotter.FeedbackReceived -= HandleFeedback;
            }

            manifestCts?.Cancel();
            manifestCts?.Dispose();
        }

        private async Task PopulateDropdownAsync(CancellationToken cancellationToken)
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
            try
            {
                var manifest = await SherpaONNXModelRegistry.Instance
                    .GetManifestAsync(SherpaONNXModuleType.KeywordSpotting, cancellationToken)
                    .ConfigureAwait(true);

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

                var options = manifest.models
                    .Where(m => !string.IsNullOrWhiteSpace(m.modelId))
                    .Select(m => new Dropdown.OptionData(m.modelId))
                    .ToList();

                modelDropdown.AddOptions(options);
                var defaultIndex = options.FindIndex(m => m.text == defaultModelID);
                modelDropdown.value = defaultIndex >= 0 ? defaultIndex : Mathf.Clamp(modelDropdown.value, 0, Mathf.Max(0, options.Count - 1));
                modelDropdown.interactable = options.Count > 0;
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                modelDropdown.options.Clear();
                modelDropdown.options.Add(new Dropdown.OptionData("<manifest unavailable>"));
                modelDropdown.interactable = false;
                statusText.text = $"Manifest fetch failed: {ex.Message}";
                if (loadOrUnloadButton != null)
                {
                    loadOrUnloadButton.interactable = false;
                }
            }
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
                ApplyCustomKeywordsToComponent();
                if (keywordSpotter.TryLoadModule())
                {
                    moduleRequested = true;
                    modelReady = false;
                    BeginLoading($"Loading {keywordSpotter.ModelId}…");
                }
            }
            else
            {
                keywordSpotter.DisposeModule();
                moduleRequested = false;
                modelReady = false;
                statusText.text = "Model unloaded.";
                keywordDisplay.text = string.Empty;
                progressTracker?.Reset();
                progressTracker?.SetVisible(false);
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

            DemoUIShared.SetButtonColor(loadOrUnloadButton, moduleRequested ? DemoUIShared.UnloadColor : DemoUIShared.LoadColor);

            if (modelDropdown != null)
            {
                modelDropdown.interactable = !moduleRequested;
            }

            UpdateCustomKeywordUIState();
        }

        private void HandleReadyStateChanged(bool ready)
        {
            modelReady = ready && moduleRequested;

            if (!moduleRequested)
            {
                return;
            }

            statusText.text = ready
                ? "Listening for registered wake words…"
                : "Model not ready.";

            if (ready)
            {
                DemoUIShared.ShowLoadingComplete(progressTracker, statusText, statusText.text);
            }
            else
            {
                DemoUIShared.ShowLoading(progressTracker, statusText, "Loading model…");
            }
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
            if (keywordTriggerSound)
            {
                AudioSource.PlayClipAtPoint(keywordTriggerSound, Camera.main.transform.position);
            }
        }

        private System.Collections.IEnumerator ClearKeywordAfterDelay()
        {
            yield return new WaitForSeconds(keywordHoldSeconds);
            keywordDisplay.text = modelReady ? "Awaiting keyword…" : "Model not ready.";
            statusText.text = modelReady ? "Listening…" : "Load a model.";
            clearRoutine = null;
        }

        private void RegisterCustomKeywordFromInput()
        {
            if (customKeywordInput == null)
            {
                return;
            }

            var keyword = customKeywordInput.text;
            if (TryAddCustomKeyword(keyword))
            {
                customKeywordInput.text = string.Empty;
            }
        }

        private bool TryAddCustomKeyword(string keyword)
        {
            var trimmed = keyword?.Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                if (statusText != null)
                {
                    statusText.text = "Enter a keyword to register.";
                }
                return false;
            }

            if (customKeywords.Any(k => string.Equals(k, trimmed, StringComparison.OrdinalIgnoreCase)))
            {
                if (statusText != null)
                {
                    statusText.text = $"Keyword '{trimmed}' is already registered.";
                }
                return false;
            }

            customKeywords.Add(trimmed);
            ApplyCustomKeywordsToComponent();
            RefreshKeywordListUI();

            if (statusText != null)
            {
                statusText.text = moduleRequested
                    ? $"Added '{trimmed}'. Unload/Reload to apply."
                    : $"Registered '{trimmed}'. Load model to apply.";
            }

            return true;
        }

        private void RemoveCustomKeyword(string keyword)
        {
            if (string.IsNullOrEmpty(keyword))
            {
                return;
            }

            var removed = customKeywords.RemoveAll(k => string.Equals(k, keyword, StringComparison.OrdinalIgnoreCase)) > 0;
            if (!removed)
            {
                return;
            }

            ApplyCustomKeywordsToComponent();
            RefreshKeywordListUI();

            if (statusText != null)
            {
                statusText.text = $"Removed '{keyword}'.";
            }
        }

        private void ClearCustomKeywords()
        {
            if (customKeywords.Count == 0)
            {
                return;
            }

            customKeywords.Clear();
            ApplyCustomKeywordsToComponent();
            RefreshKeywordListUI();

            if (statusText != null)
            {
                statusText.text = "Cleared all custom keywords.";
            }
        }

        private void ApplyCustomKeywordsToComponent()
        {
            if (keywordSpotter == null || customKeywordsField == null)
            {
                return;
            }

            if (customKeywordsField.GetValue(keywordSpotter) is List<KeywordSpotting.KeywordRegistration> backingList)
            {
                backingList.Clear();
                for (int i = 0; i < customKeywords.Count; i++)
                {
                    var word = customKeywords[i];
                    backingList.Add(new KeywordSpotting.KeywordRegistration(word, DefaultCustomKeywordScore, DefaultCustomKeywordThreshold));
                }
            }
        }

        private void RefreshKeywordListUI()
        {
            if (keywordListContent == null || keywordItemTemplate == null)
            {
                return;
            }

            foreach (var instance in keywordItemInstances.Values)
            {
                if (instance != null)
                {
                    Destroy(instance);
                }
            }

            keywordItemInstances.Clear();

            for (int i = 0; i < customKeywords.Count; i++)
            {
                var word = customKeywords[i];
                var item = Instantiate(keywordItemTemplate, keywordListContent);
                item.SetActive(true);

                var label = item.transform.GetChild(0).GetComponent<Text>();
                if (label != null)
                {
                    label.text = word;
                }

                var deleteButton = item.GetComponentInChildren<Button>();
                if (deleteButton != null)
                {
                    var captured = word;
                    deleteButton.onClick.RemoveAllListeners();
                    deleteButton.onClick.AddListener(() => RemoveCustomKeyword(captured));
                }

                keywordItemInstances[word] = item;
            }

            if (keywordItemTemplate.activeSelf)
            {
                keywordItemTemplate.SetActive(false);
            }

            UpdateCustomKeywordUIState();
        }

        private void SyncCustomKeywordsFromComponent()
        {
            if (keywordSpotter == null || customKeywordsField == null)
            {
                return;
            }

            if (customKeywordsField.GetValue(keywordSpotter) is List<KeywordSpotting.KeywordRegistration> backingList)
            {
                customKeywords.Clear();
                foreach (var entry in backingList)
                {
                    var word = entry.Keyword?.Trim();
                    if (string.IsNullOrEmpty(word))
                    {
                        continue;
                    }

                    if (!customKeywords.Any(k => string.Equals(k, word, StringComparison.OrdinalIgnoreCase)))
                    {
                        customKeywords.Add(word);
                    }
                }
            }
        }

        private void UpdateCustomKeywordUIState()
        {
            var hasInput = customKeywordInput != null && !string.IsNullOrWhiteSpace(customKeywordInput.text);
            if (registerKeywordButton != null)
            {
                registerKeywordButton.interactable = hasInput && !moduleRequested;
            }

            if (clearKeywordsButton != null)
            {
                clearKeywordsButton.interactable = customKeywords.Count > 0 && !moduleRequested;
            }

            if (customKeywordInput != null)
            {
                customKeywordInput.interactable = !moduleRequested;
            }

            foreach (var instance in keywordItemInstances.Values)
            {
                if (instance == null)
                {
                    continue;
                }

                var deleteButton = instance.GetComponentInChildren<Button>();
                if (deleteButton != null)
                {
                    deleteButton.interactable = !moduleRequested;
                }
            }
        }

        private void BeginLoading(string message) => DemoUIShared.ShowLoading(progressTracker, statusText, message);

        private void HandleFeedbackMessage(string message)
        {
            if (progressMessageText != null)
            {
                progressMessageText.text = message;
            }
        }

        private void HandleFeedback(SherpaFeedback feedback)
        {
            DemoUIShared.UpdateProgressFromFeedback(progressTracker, progressMessageText, feedback);
        }

        public void OpenGithubRepo()
        {
            Application.OpenURL("https://github.com/EitanWong/com.eitan.sherpa-onnx-unity");
        }
    }
}
