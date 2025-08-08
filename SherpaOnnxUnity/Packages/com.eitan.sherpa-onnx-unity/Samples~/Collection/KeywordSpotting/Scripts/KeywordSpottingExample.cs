namespace Eitan.SherpaOnnxUnity.Samples
{
    using System;
    using System.Collections;
    using System.Linq;
    using System.Threading.Tasks;
    using Eitan.SherpaOnnxUnity.Runtime;
    using UnityEngine;
    using UnityEngine.UI;
    using static UnityEngine.UI.Dropdown;


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

        private KeywordSpotting keywordSpotting;

        private readonly int SampleRate = 16000;

        private Mic.Device device;

        private bool _modelLoadFlag;

        private Color _originLoadBtnColor;
        private readonly string defaultModelID = "sherpa-onnx-kws-zipformer-wenetspeech-3.3M-2024-01-01";
        
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
            InitDropdown();
            UpdateLoadButtonUI();
        }

        private void Load(string modelID)
        {
            if (keywordSpotting == null)
            {
                var reporter = new SherpaOnnxFeedbackReporter(null, this);
                keywordSpotting = new KeywordSpotting(modelID, SampleRate, 2.0f, 0.25f, null,reporter);
                keywordSpotting.OnKeywordDetected += HandleKeywordDetected;

                _modelLoadFlag = true;
            }
            else
            {
                UnityEngine.Debug.LogError("Please Unload current model first");
            }
            UpdateLoadButtonUI();

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

            UpdateLoadButtonUI();

        }

        private void StartRecording()
        {

            Mic.Init();
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
            
            if (_resetCoroutine != null)
            {
                StopCoroutine(_resetCoroutine);
                _resetCoroutine = null;
            }
        }

        private void InitDropdown()
        {
            var manifest = SherpaOnnxModelRegistry.Instance.GetManifest();

            _modelIDDropdown.options.Clear();
            if (manifest.models != null)
            {
                System.Collections.Generic.List<OptionData> modelOptions = manifest.Filter(m => m.moduleType == SherpaOnnxModuleType.KeywordSpotting).Select(m => new OptionData(m.modelId)).ToList();
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

        private void UpdateLoadButtonUI()
        {

            if (_modelLoadFlag)// already has model loaded
            {
                _modelLoadOrUnloadButton.GetComponentInChildren<Text>().text = "Unload Model";
                _modelLoadOrUnloadButton.GetComponent<Image>().color = Color.red;
                _modelIDDropdown.interactable = false;

                _totalInitProgressBar.gameObject.SetActive(true);
                _initMessageText.gameObject.SetActive(true);
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

        private void SetProgressActive(bool isActive)
        {
            _totalInitProgressBar.gameObject.SetActive(isActive);
        }

        private void UpdateOverallProgress(float progress, string message)
        {
            _initMessageText.text = message;
            _totalInitProgressBar.FillAmount = progress;
            _totalInitBarText.text = $"{progress * 100:F0}%";
        }

        public void OnFeedback(PrepareFeedback feedback)
        {
            SetProgressActive(true);
            UpdateOverallProgress(0f, feedback.Message);
            _tipsText.text = $"<b>[Loading]:</b> {feedback.Metadata.modelId}The keyword spotting model is loading, please wait patiently.";
            _keywordText.text = "Please wait for the keyword spotting model to load";
        }

        public void OnFeedback(DownloadFeedback feedback)
        {
            UpdateOverallProgress(Mathf.Clamp(0.5f * feedback.Progress, 0, 0.5f), feedback.Message);
            _keywordText.text = "Please wait for the keyword spotting model to download.";
        }

        public void OnFeedback(UncompressFeedback feedback)
        {
            UpdateOverallProgress(0.5f + (0.49f * feedback.Progress), feedback.Message);
            _keywordText.text = "Wait keyword spotting model zip file uncompress";
        }

        public void OnFeedback(VerifyFeedback feedback)
        {
            UpdateOverallProgress(0.99f, feedback.Message);
            _keywordText.text = "Verifying keyword spotting model...";
        }

        public void OnFeedback(LoadFeedback feedback)
        {
            UpdateOverallProgress(0.99f, feedback.Message);
            _tipsText.text = $"<b><color=cyan>[Loading]</color>:</b> The keyword spotting model {feedback.Metadata.modelId} is loading.";
            _keywordText.text = "Loading keyword spotting model...";
        }

        public void OnFeedback(CancelFeedback feedback)
        {
            SetProgressActive(false);
            _tipsText.text = $"<b><color=yellow>Cancelled</color>:</b> {feedback.Metadata.modelId}{feedback.Message}";
            _keywordText.text = "Keyword spotting model loading cancelled.";
            Unload();
        }

        public void OnFeedback(SuccessFeedback feedback)
        {
            SetProgressActive(false);
            UpdateOverallProgress(1f, "Success");
            _initMessageText.text = string.Empty;
            _keywordText.text = "<b><i>Listening for wake-up words...</i></b>";
            _loadedMessage = $"<b><color=green>[Loaded]:</color></b> {feedback.Metadata.modelId}Keyword spotting is active. Say a wake-up word to test.";
            _tipsText.text = _loadedMessage;

            StartRecording();

        }

        public void OnFeedback(FailedFeedback feedback)
        {
            SetProgressActive(false);
            Debug.LogError($"[Failed] :{feedback.Message}");
            _initMessageText.text = feedback.Message;
            _tipsText.text = $"<b><color=red>[Failed]</color>:</b> The keyword spotting model load failed.";
            _keywordText.text = "<color=red><b>Keyword spotting model load failed</b></color>";
            Unload();

        }

        public void OnFeedback(CleanFeedback feedback)
        {
            SetProgressActive(false);
            _initMessageText.text = feedback.Message;
            _keywordText.text = "<color=yellow><b>Init canceled</b></color>";
        }
        #endregion

        public void OpenGithubRepo()
        {
            Application.OpenURL("https://github.com/EitanWong/com.eitan.sherpa-onnx-unity");
        }
    }

}