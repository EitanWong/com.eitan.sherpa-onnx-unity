namespace Eitan.SherpaOnnxUnity.Samples
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Eitan.SherpaOnnxUnity.Runtime;
    using UnityEngine;
    using UnityEngine.UI;
    using static UnityEngine.UI.Dropdown;
    using Stage = Eitan.SherpaOnnxUnity.Samples.ModelLoadProgressTracker.Stage;

    public class KeywordSpottingExample : MonoBehaviour, ISherpaFeedbackHandler
    {

        [Header("UI Components")]
        [SerializeField] private Dropdown _modelIDDropdown;
        [SerializeField] private Button _modelLoadOrUnloadButton;
        [SerializeField] private Text _initMessageText;
        [SerializeField] private Eitan.SherpaOnnxUnity.Samples.UI.EasyProgressBar _totalInitProgressBar;
        [SerializeField] private Text _totalInitBarText;
        [SerializeField] private Text _tipsText;
        [SerializeField] private Text _keywordText;

        [SerializeField] private RectTransform _keywordsPanel;
        [SerializeField] private InputField _keywordInput;
        [SerializeField] private ScrollRect _keywordsListScrollView;
        [SerializeField] private Button _clearKeywordsBtn;
        [SerializeField] private GameObject _keywordTemplate;

        [Header("Kws Setup")]
        [SerializeField] private KeywordSpotting.KeywordRegistration[] kwsKeywords;

        [Header("Audio")]
        [SerializeField] private AudioClip wakeupSoundClip;

        private KeywordSpotting keywordSpotting;

        private readonly int SampleRate = 16000;

        private Mic.Device device;

        private bool _modelLoadFlag;

        private Color _originLoadBtnColor;
        private readonly string defaultModelID = "sherpa-onnx-kws-zipformer-wenetspeech-3.3M-2024-01-01";
        private readonly List<KeywordSpotting.KeywordRegistration> _runtimeKeywords = new();
        private Button _registerKeywordButton;
        private const string KeywordTemplateLabelPath = "Text (Legacy)";
        private const string KeywordTemplateDeleteButtonPath = "Button (Register)";

        // For combo effect
        private int _comboCount;
        private string _lastKeyword;
        private float _lastDetectionTime;
        private Coroutine _resetCoroutine;
        private const float DisplayDuration = 3f; // seconds
        private int _originalFontSize;
        private string _loadedMessage;
        private static readonly string[] InterestingFeedback = {
            "Double Kill!",
            "Triple Kill!",
            "Rampage!",
            "Godlike!",
            "Beyond Godlike!"
        };
        private ModelLoadProgressTracker _progressTracker;

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        private void Start()
        {
            Application.runInBackground = true;
            Application.targetFrameRate = 30;
            _modelLoadOrUnloadButton.onClick.AddListener(HandleModelLoadOrUnloadButtonClick);
            _totalInitProgressBar.gameObject.SetActive(false);
            _initMessageText.gameObject.SetActive(false);
            _keywordText.text = "Please click the button to load the keyword spotting model";
            _tipsText.text = string.Empty;
            _originLoadBtnColor = _modelLoadOrUnloadButton.GetComponent<Image>().color;
            if (_keywordText != null)
            {
                _originalFontSize = _keywordText.fontSize;
            }
            _progressTracker = new ModelLoadProgressTracker(_totalInitProgressBar, _totalInitBarText, _initMessageText);
            _ = InitDropdownAsync();
            InitKeywordsPanelUI();
            UpdateUI();

        }

        private void Load(string modelID)
        {
            if (keywordSpotting == null)
            {
                var reporter = new SherpaOnnxFeedbackReporter(null, this);
                keywordSpotting = new KeywordSpotting(modelID, SampleRate, 2.0f, 0.25f, kwsKeywords, reporter);
                keywordSpotting.OnKeywordDetected += HandleKeywordDetected;

                _modelLoadFlag = true;
            }
            else
            {
                UnityEngine.Debug.LogError("Please Unload current model first");
            }
            UpdateUI();

        }
        private void Unload()
        {
            if (keywordSpotting == null)
            {
                UnityEngine.Debug.LogWarning("No model loaded, no need to unload");
            }
            else
            {
                keywordSpotting.OnKeywordDetected -= HandleKeywordDetected;
                keywordSpotting.Dispose();
                keywordSpotting = null;
                _modelLoadFlag = false;

            }
            if (device != null)
            {
                device.StopRecording();
                device.OnFrameCollected -= HandleAudioFrameCollected;
                device = null;
            }

            if (_resetCoroutine != null)
            {
                StopCoroutine(_resetCoroutine);
                _resetCoroutine = null;
            }

            _progressTracker?.Reset();
            _progressTracker?.SetVisible(false);

            UpdateUI();

        }

        private void StartRecording()
        {
            if (!Mic.Initialized)
            {
                Mic.Init();
            }
            var devices = Mic.AvailableDevices;
            if (devices.Count > 0)
            {
                // use default device
                device = devices[0];
                device.OnFrameCollected += HandleAudioFrameCollected;
                device.StartRecording(SampleRate, 10); // 16kHz sample rate
                Debug.Log($"Using device: {device.Name}, Sampling Frequency: {device.SamplingFrequency}");
            }
        }


        private void OnDestroy()
        {
            if (device != null)
            {
                device.OnFrameCollected -= HandleAudioFrameCollected;
            }

            if (_modelLoadOrUnloadButton != null)
            {
                _modelLoadOrUnloadButton.onClick.AddListener(HandleModelLoadOrUnloadButtonClick);
            }

            if (_registerKeywordButton != null)
            {
                _registerKeywordButton.onClick.RemoveListener(HandleAddKeywordButtonClick);
            }
            if (_clearKeywordsBtn != null)
            {
                _clearKeywordsBtn.onClick.RemoveListener(HandleClearKeywordsButtonClick);
            }

            if (_resetCoroutine != null)
            {
                StopCoroutine(_resetCoroutine);
                _resetCoroutine = null;
            }
        }

        private async Task InitDropdownAsync()
        {
            _modelIDDropdown.options.Clear();

            _modelIDDropdown.captionText.text = "Fetching model manifest from GitHub…";
            _modelLoadOrUnloadButton.gameObject.SetActive(false);
            var manifest = await SherpaOnnxModelRegistry.Instance.GetManifestAsync(SherpaOnnxModuleType.KeywordSpotting);
            _modelLoadOrUnloadButton.gameObject.SetActive(true);

            _modelIDDropdown.options.Clear();
            if (manifest.models != null)
            {
                System.Collections.Generic.List<OptionData> modelOptions = manifest.models.Select(m => new OptionData(m.modelId)).ToList();
                _modelIDDropdown.AddOptions(modelOptions);

                var defaultIndex = modelOptions.FindIndex(m => m.text == defaultModelID);
                _modelIDDropdown.value = defaultIndex;
                _modelIDDropdown.interactable = true;
            }
            else
            {
                _modelIDDropdown.interactable = false;
            }
        }

        private void InitKeywordsPanelUI()
        {
            if (_keywordsPanel == null || _keywordInput == null || _keywordsListScrollView == null || _keywordTemplate == null)
            {
                Debug.LogWarning("[KeywordSpottingExample] Missing keyword UI references.");
                return;
            }

            if (_registerKeywordButton == null)
            {
                _registerKeywordButton = _keywordInput.GetComponentInChildren<Button>(true);
                if (_registerKeywordButton != null)
                {
                    _registerKeywordButton.onClick.AddListener(HandleAddKeywordButtonClick);
                }
                else
                {
                    Debug.LogWarning("[KeywordSpottingExample] Register keyword button not found under input field.");
                }
            }

            if (_keywordTemplate.activeSelf)
            {
                _keywordTemplate.SetActive(false);
            }

            _runtimeKeywords.Clear();
            if (kwsKeywords != null)
            {
                foreach (var registration in kwsKeywords)
                {
                    if (string.IsNullOrWhiteSpace(registration.Keyword))
                    {
                        continue;
                    }

                    _runtimeKeywords.Add(new KeywordSpotting.KeywordRegistration(registration.Keyword.Trim(), registration.BoostingScore, registration.TriggerThreshold));
                }
            }

            if (_clearKeywordsBtn != null)
            {
                _clearKeywordsBtn.onClick.RemoveAllListeners();
                _clearKeywordsBtn.onClick.AddListener(HandleClearKeywordsButtonClick);
            }

            SyncSerializedKeywords();
            RefreshKeywordsUI();
        }
        private void HandleClearKeywordsButtonClick()
        {
            if (_modelLoadFlag)
            {
                Debug.LogWarning("[KeywordSpottingExample] Cannot clear keywords while model is loaded.");
                return;
            }

            _runtimeKeywords.Clear();
            SyncSerializedKeywords();
            RefreshKeywordsUI();
            if (_tipsText != null)
            {
                _tipsText.text = "<color=yellow><b>All keywords cleared.</b></color>";
            }
        }

        private void HandleAddKeywordButtonClick()
        {
            if (_keywordInput == null)
            {
                return;
            }

            TryAddCustomKeyword(_keywordInput.text);
        }

        private void TryAddCustomKeyword(string candidate)
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                return;
            }

            var keyword = candidate.Trim();
            if (string.IsNullOrEmpty(keyword))
            {
                return;
            }

            if (_runtimeKeywords.Any(k => string.Equals(k.Keyword, keyword, StringComparison.OrdinalIgnoreCase)))
            {
                Debug.LogWarning($"[KeywordSpottingExample] Keyword '{keyword}' already exists.");
                return;
            }

            var registration = new KeywordSpotting.KeywordRegistration(keyword);
            _runtimeKeywords.Add(registration);
            SyncSerializedKeywords();
            CreateKeywordItem(registration);
            UpdateKeywordListLayout(forceScrollToLatest: true);

            if (_keywordInput != null)
            {
                _keywordInput.text = string.Empty;
            }

            if (keywordSpotting != null && _tipsText != null)
            {
                _tipsText.text = "<color=yellow><b>Keywords updated.</b></color> Reload model to apply changes.";
            }
        }

        private void RefreshKeywordsUI()
        {
            if (_keywordsListScrollView == null || _keywordsListScrollView.content == null)
            {
                return;
            }

            var content = _keywordsListScrollView.content;
            var itemsToDestroy = new List<GameObject>();
            foreach (Transform child in content)
            {
                if (child == null || child.gameObject == _keywordTemplate)
                {
                    continue;
                }

                itemsToDestroy.Add(child.gameObject);
            }

            foreach (var item in itemsToDestroy)
            {
                Destroy(item);
            }

            foreach (var registration in _runtimeKeywords)
            {
                CreateKeywordItem(registration);
            }

            UpdateKeywordListLayout(forceScrollToLatest: false);
        }

        private void CreateKeywordItem(KeywordSpotting.KeywordRegistration registration)
        {
            if (_keywordsListScrollView == null || _keywordsListScrollView.content == null || _keywordTemplate == null)
            {
                return;
            }

            var item = Instantiate(_keywordTemplate, _keywordsListScrollView.content);
            item.name = $"Keyword_{registration.Keyword}";
            item.SetActive(true);

            var keywordLabel = item.transform.Find(KeywordTemplateLabelPath)?.GetComponent<Text>();
            if (keywordLabel != null)
            {
                keywordLabel.text = registration.Keyword;
            }

            var deleteButtonTransform = item.transform.Find(KeywordTemplateDeleteButtonPath);
            if (deleteButtonTransform != null && deleteButtonTransform.TryGetComponent<Button>(out var deleteButton))
            {
                var keyword = registration.Keyword;
                deleteButton.onClick.RemoveAllListeners();
                deleteButton.onClick.AddListener(() => RemoveCustomKeyword(keyword, item));
            }
            else
            {
                Debug.LogWarning("[KeywordSpottingExample] Keyword template missing delete button.");
            }
        }

        private void RemoveCustomKeyword(string keyword, GameObject item)
        {
            var index = _runtimeKeywords.FindIndex(k => string.Equals(k.Keyword, keyword, StringComparison.OrdinalIgnoreCase));
            if (index < 0)
            {
                return;
            }

            _runtimeKeywords.RemoveAt(index);
            SyncSerializedKeywords();

            if (item != null)
            {
                Destroy(item);
            }

            UpdateKeywordListLayout(forceScrollToLatest: false);

            if (keywordSpotting != null && _tipsText != null)
            {
                _tipsText.text = "<color=yellow><b>Keywords updated.</b></color> Reload model to apply changes.";
            }
        }

        private void SyncSerializedKeywords()
        {
            kwsKeywords = _runtimeKeywords.ToArray();
        }

        private void UpdateKeywordListLayout(bool forceScrollToLatest)
        {
            if (_keywordsListScrollView == null || _keywordsListScrollView.content == null)
            {
                return;
            }

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(_keywordsListScrollView.content);

            if (forceScrollToLatest)
            {
                _keywordsListScrollView.verticalNormalizedPosition = 0f;
            }
        }

        private void UpdateUI()
        {

            if (_modelLoadFlag)// already has model loaded
            {
                _modelLoadOrUnloadButton.GetComponentInChildren<Text>().text = "Unload Model";
                _modelLoadOrUnloadButton.GetComponent<Image>().color = Color.red;
                _modelIDDropdown.interactable = false;

                _totalInitProgressBar.gameObject.SetActive(true);
                _initMessageText.gameObject.SetActive(true);
                _keywordInput.interactable = false;
                _registerKeywordButton.interactable = false;
                _clearKeywordsBtn.interactable = false;
                UpdateKeywordsListDeleteBtnInteractable(false);
            }
            else // no model loaded, init new model
            {

                _modelLoadOrUnloadButton.GetComponentInChildren<Text>().text = "Load Model";
                _modelLoadOrUnloadButton.GetComponent<Image>().color = _originLoadBtnColor;
                _modelIDDropdown.interactable = true;

                _totalInitProgressBar.gameObject.SetActive(false);
                _initMessageText.gameObject.SetActive(false);
                _keywordText.text = string.Empty;
                _tipsText.text = string.Empty;

                _keywordInput.interactable = true;
                _registerKeywordButton.interactable = true;
                _clearKeywordsBtn.interactable = true;
                UpdateKeywordsListDeleteBtnInteractable(true);
            }

        }
        private void UpdateKeywordsListDeleteBtnInteractable(bool interactable)
        {

            if (_keywordsListScrollView && _keywordsListScrollView.content)
            {
                foreach (RectTransform child in _keywordsListScrollView.content)
                {
                    var childBtn = child.GetComponentInChildren<Button>();
                    if (childBtn)
                    {
                        childBtn.interactable = interactable;
                    }
                }
            }
        }


        private void HandleModelLoadOrUnloadButtonClick()
        {
            if (_modelLoadFlag)// already has model loaded
            {
                Unload();
            }
            else // no model loaded, init new model
            {
                Load(_modelIDDropdown.captionText.text);
            }

        }

        private void HandleAudioFrameCollected(int sampleRate, int channelCount, float[] pcm)
        {
            try
            {
                if (keywordSpotting == null)
                {
                    return;
                }

                keywordSpotting.StreamDetect(pcm);
            }
            catch (Exception ex)
            {
                Debug.LogError($"An error occurred in HandleAudioFrameCollected: {ex}");
            }
        }

        private void HandleKeywordDetected(string keyword)
        {
            if (string.IsNullOrEmpty(keyword))
            {
                return;
            }

            if (_resetCoroutine != null)
            {
                StopCoroutine(_resetCoroutine);
                _resetCoroutine = null;
            }

            // Reset combo if different keyword or too much time has passed
            if (!string.IsNullOrEmpty(_lastKeyword) && _lastKeyword == keyword && (Time.time - _lastDetectionTime) < DisplayDuration)
            {
                _comboCount++;
            }
            else
            {
                _comboCount = 1;
            }

            _lastKeyword = keyword;
            _lastDetectionTime = Time.time;

            var comboDisplay = _comboCount > 1 ? $" x{_comboCount}" : "";
            _keywordText.text = $"<color=cyan><b>{keyword}</b></color>{comboDisplay}";
            _keywordText.fontSize = _originalFontSize + (_comboCount - 1) * 4; // Increase font size

            if (_comboCount > 1)
            {
                var feedbackIndex = Mathf.Clamp(_comboCount - 2, 0, InterestingFeedback.Length - 1);
                _tipsText.text = $"<b><color=yellow>[COMBO]</color></b> {InterestingFeedback[feedbackIndex]}";
            }
            else
            {
                _tipsText.text = $"<b><color=green>[Detected]</color></b> Say the keyword again to start a combo!";
            }

            Debug.Log($"Wake-up word detected: {keyword}, combo: {_comboCount}");

            _resetCoroutine = StartCoroutine(ResetKeywordDisplayAfterDelay());


            if (wakeupSoundClip)
            {
                AudioSource.PlayClipAtPoint(wakeupSoundClip, Camera.main.transform.position);
            }
        }

        private IEnumerator ResetKeywordDisplayAfterDelay()
        {
            yield return new WaitForSeconds(DisplayDuration);

            _keywordText.text = "<b><i>Listening for wake-up words...</i></b>";
            _tipsText.text = _loadedMessage;
            if (_keywordText != null)
            {
                _keywordText.fontSize = _originalFontSize;
            }
            _comboCount = 0;
            _lastKeyword = string.Empty;
            _resetCoroutine = null;
        }


        #region FeedbackHandler

        public void OnFeedback(PrepareFeedback feedback)
        {
            _progressTracker.Reset();
            _progressTracker.MarkStageComplete(Stage.Prepare, feedback.Message);
            _tipsText.text = $"<b>[Loading]:</b> {feedback.Metadata.modelId}The keyword spotting model is loading, please wait patiently.";
            _keywordText.text = "Preparing keyword spotting model...";
        }

        public void OnFeedback(DownloadFeedback feedback)
        {
            _progressTracker.UpdateStage(Stage.Download, feedback.Message, feedback.Progress);
            _keywordText.text = "Downloading keyword spotting model...";
        }

        public void OnFeedback(CleanFeedback feedback)
        {
            _progressTracker.MarkStageComplete(Stage.Clean, feedback.Message);
            _keywordText.text = "Cleaning old keyword spotting files...";
        }

        public void OnFeedback(VerifyFeedback feedback)
        {
            _progressTracker.UpdateStage(Stage.Verify, feedback.Message, feedback.Progress);
            _keywordText.text = "Verifying keyword spotting assets...";
        }

        public void OnFeedback(DecompressFeedback feedback)
        {
            _progressTracker.UpdateStage(Stage.Decompress, feedback.Message, feedback.Progress);
            _keywordText.text = "Decompressing keyword spotting package...";
        }

        public void OnFeedback(LoadFeedback feedback)
        {
            _progressTracker.MarkStageComplete(Stage.Load, feedback.Message);
            _tipsText.text = $"<b><color=cyan>[Loading]</color>:</b> The keyword spotting model {feedback.Metadata.modelId} is loading.";
            _keywordText.text = "Loading keyword spotting runtime...";
        }

        public void OnFeedback(CancelFeedback feedback)
        {
            Unload();
            _tipsText.text = $"<b><color=yellow>Cancelled</color>:</b> {feedback.Metadata.modelId}{feedback.Message}";
            _keywordText.text = "Keyword spotting model loading cancelled.";
        }

        public void OnFeedback(SuccessFeedback feedback)
        {
            _progressTracker.Complete("Success");
            _progressTracker.SetVisible(false);
            _initMessageText.text = string.Empty;
            _keywordText.text = "<b><i>Listening for wake-up words...</i></b>";
            _loadedMessage = $"<b><color=green>[Loaded]:</color></b> {feedback.Metadata.modelId}Keyword spotting is active. Say a wake-up word to test.";
            _tipsText.text = _loadedMessage;

            StartRecording();
        }

        public void OnFeedback(FailedFeedback feedback)
        {
            Debug.LogError($"[Failed] :{feedback.Message}");
            Unload();
            _initMessageText.text = feedback.Message;
            _tipsText.text = $"<b><color=red>[Failed]</color>:</b> The keyword spotting model load failed.";
            _keywordText.text = "<color=red><b>Keyword spotting model load failed</b></color>";
        }
        #endregion

        public void OpenGithubRepo()
        {
            Application.OpenURL("https://github.com/EitanWong/com.eitan.sherpa-onnx-unity");
        }
    }

}
