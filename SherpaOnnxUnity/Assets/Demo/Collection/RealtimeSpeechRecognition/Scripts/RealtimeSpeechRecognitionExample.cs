
namespace Eitan.SherpaOnnxUnity.Samples
{
    using System;
    using System.Linq;

    using System.Threading.Tasks;
    using Eitan.SherpaOnnxUnity.Runtime;

    using UnityEngine;
    using UnityEngine.UI;
    using static UnityEngine.UI.Dropdown;


    public class RealtimeSpeechRecognitionExample : MonoBehaviour, ISherpaFeedbackHandler
    {

        // [SerializeField] private string _onlineModelID = "sherpa-onnx-streaming-zipformer-bilingual-zh-en-2023-02-20";
        [Header("UI Components")]
        [SerializeField] private Dropdown _modelIDDropdown;
        [SerializeField] private Button _modelLoadOrUnloadButton;
        [SerializeField] private Text _initMessageText;
        [SerializeField] private Eitan.SherpaOnnxUnity.Samples.UI.EasyProgressBar _totalInitProgressBar;
        [SerializeField] private Text _totalInitBarText;
        [SerializeField] private Text _tipsText;
        [SerializeField] private Text _transcriptionText;

        private SpeechRecognition speechRecognition;

        private readonly int SampleRate = 16000;

        private Mic.Device device;
        private string lastCachedText;

        private bool _modelLoadFlag;

        private Color _originLoadBtnColor;
        private readonly string defaultModelID = "sherpa-onnx-streaming-zipformer-bilingual-zh-en-2023-02-20";
        // Start is called once before the first execution of Update after the MonoBehaviour is created
        private void Start()
        {
            Application.runInBackground = true;
            Application.targetFrameRate = 30;
            _modelLoadOrUnloadButton.onClick.AddListener(HandleModelLoadOrUnloadButtonClick);
            _totalInitProgressBar.gameObject.SetActive(false);
            _initMessageText.gameObject.SetActive(false);
            _transcriptionText.text = "Please click the button to load the model";
            _tipsText.text = string.Empty;
            _originLoadBtnColor = _modelLoadOrUnloadButton.GetComponent<Image>().color;
            _ = InitDropdown();
            UpdateLoadButtonUI();
        }

        private void Load(string modelID)
        {
            if (speechRecognition == null)
            {
                var reporter = new SherpaFeedbackReporter(null, this);
                speechRecognition = new SpeechRecognition(modelID, SampleRate, reporter);

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
            if (speechRecognition == null)
            {
                UnityEngine.Debug.LogWarning("No model loaded, no need to unload");
            }
            else
            {
                speechRecognition.Dispose();
                speechRecognition = null;
                _modelLoadFlag = false;

            }
            if (device != null)
            {
                device.StopRecording();
                device.OnFrameCollected -= HandleAudioFrameCollected;
                device = null;
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
        }

        private async Task InitDropdown()
        {
            var manifest = await SherpaOnnxModelRegistry.Instance.GetManifestAsync();

            _modelIDDropdown.options.Clear();
            if (manifest.models != null)
            {
                System.Collections.Generic.List<OptionData> modelOptions = manifest.Filter(m => (m.moduleType == SherpaOnnxModuleType.SpeechRecognition && m.IsOnlineModel())).Select(m => new OptionData(m.modelId)).ToList();
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
                _transcriptionText.text = string.Empty;
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

        private async void HandleAudioFrameCollected(int sampleRate, int channelCount, float[] pcm)
        {
            try
            {
                // Don't process if the recognizer isn't ready or is disposed
                if (speechRecognition == null)
                {
                    return;
                }

                var result = await speechRecognition.SpeechTranscriptionAsync(pcm, sampleRate);
                if (result != lastCachedText)
                {
                    lastCachedText = result;
                    if (!string.IsNullOrWhiteSpace(lastCachedText))
                    {
                        _transcriptionText.text = lastCachedText;
                    }
                }
            }
            catch (Exception ex)
            {
                // Log errors to avoid crashing the application
                Debug.LogError($"An error occurred in HandleAudioFrameCollected: {ex}");
            }
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
            _tipsText.text = $"<b>[Loading]:</b> {feedback.Metadata.modelId}\nThe model is loading, please wait patiently.";
            _transcriptionText.text = "Please wait for the model to load";
        }

        public void OnFeedback(DownloadFeedback feedback)
        {
            UpdateOverallProgress(Mathf.Clamp(0.5f * feedback.Progress, 0, 0.5f), feedback.Message);
            _transcriptionText.text = "Please wait for the model to download.";
        }

        public void OnFeedback(UncompressFeedback feedback)
        {
            UpdateOverallProgress(0.5f + (0.49f * feedback.Progress), feedback.Message);
            _transcriptionText.text = "Wait model zip file uncompress";
        }

        public void OnFeedback(VerifyFeedback feedback)
        {
            UpdateOverallProgress(0.99f, feedback.Message);
            _transcriptionText.text = "Verifying model...";
        }

        public void OnFeedback(LoadFeedback feedback)
        {
            UpdateOverallProgress(0.99f, feedback.Message);
            _tipsText.text = $"<b><color=cyan>[Loading]</color>:</b> \nThe model {feedback.Metadata.modelId} is loading.";
            _transcriptionText.text = "Loading model...";
        }

        public void OnFeedback(CancelFeedback feedback)
        {
            SetProgressActive(false);
            _tipsText.text = $"<b><color=yellow>Cancelled</color>:</b> {feedback.Metadata.modelId}\n{feedback.Message}";
            _transcriptionText.text = "Model loading cancelled.";
            Unload();
        }

        public void OnFeedback(SuccessFeedback feedback)
        {
            SetProgressActive(false);
            UpdateOverallProgress(1f, "Success");
            _initMessageText.text = string.Empty;
            _transcriptionText.text = "<b><i>Now you can speak</i></b>";
            _tipsText.text = $"<b><color=green>[Loaded]:</color></b> {feedback.Metadata.modelId}\nYou can now test speech-to-text by speaking directly.";

            StartRecording();

        }

        public void OnFeedback(FailedFeedback feedback)
        {
            SetProgressActive(false);
            UnityEngine.Debug.LogError($"[Failed] :{feedback.Message}");
            _initMessageText.text = feedback.Message;
            _tipsText.text = $"<b><color=red>[Failed]</color>:</b> \nThe model is load failed.";
            _transcriptionText.text = "<color=red><b>Model load failed</b></color>";
            Unload();

        }

        public void OnFeedback(CleanFeedback feedback)
        {
            SetProgressActive(false);
            _initMessageText.text = feedback.Message;
            _transcriptionText.text = "<color=yellow><b>Init canceled</b></color>";
        }
        #endregion

        public void OpenGithubRepo()
        {
            Application.OpenURL("https://github.com/EitanWong/com.eitan.sherpa-onnx-unity");
        }
    }

}