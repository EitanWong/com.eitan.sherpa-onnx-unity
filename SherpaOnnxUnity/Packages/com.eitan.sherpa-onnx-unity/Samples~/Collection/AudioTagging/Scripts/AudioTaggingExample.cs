
namespace Eitan.SherpaOnnxUnity.Samples
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Eitan.SherpaOnnxUnity.Runtime;

    using UnityEngine;
    using UnityEngine.UI;
    using static UnityEngine.UI.Dropdown;
    using Stage = Eitan.SherpaOnnxUnity.Samples.ModelLoadProgressTracker.Stage;
    public class AudioTaggingExample : MonoBehaviour, ISherpaFeedbackHandler
    {

        // [SerializeField] private string _onlineModelID = "sherpa-onnx-streaming-zipformer-bilingual-zh-en-2023-02-20";
        [Header("UI Components")]
        [SerializeField] private Dropdown _modelIDDropdown;
        [SerializeField] private Button _modelLoadOrUnloadButton;
        [SerializeField] private Text _initMessageText;
        [SerializeField] private Eitan.SherpaOnnxUnity.Samples.UI.EasyProgressBar _totalInitProgressBar;
        [SerializeField] private Text _totalInitBarText;
        [SerializeField] private Text _tipsText;
        [SerializeField] private Text _tagResultText;

        private AudioTagging audioTagging;

        private readonly int SampleRate = 16000;

        private Mic.Device device;

        private bool _modelLoadFlag;

        private Color _originLoadBtnColor;
        private readonly string defaultModelID = "sherpa-onnx-ced-base-audio-tagging-2024-04-19";
        private ModelLoadProgressTracker _progressTracker;
        // Start is called once before the first execution of Update after the MonoBehaviour is created
        private void Start()
        {
            Application.runInBackground = true;
            Application.targetFrameRate = 30;
            _modelLoadOrUnloadButton.onClick.AddListener(HandleModelLoadOrUnloadButtonClick);
            _totalInitProgressBar.gameObject.SetActive(false);
            _initMessageText.gameObject.SetActive(false);
            _tagResultText.text = "Please click the button to load the model";
            _tipsText.text = string.Empty;
            _originLoadBtnColor = _modelLoadOrUnloadButton.GetComponent<Image>().color;
            _progressTracker = new ModelLoadProgressTracker(_totalInitProgressBar, _totalInitBarText, _initMessageText);
            _ = InitDropdownAsync();
            UpdateLoadButtonUI();
        }

        private void Load(string modelID)
        {
            if (audioTagging == null)
            {
                var reporter = new SherpaOnnxFeedbackReporter(null, this);
                audioTagging = new AudioTagging(modelID, SampleRate, reporter);

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
            if (audioTagging == null)
            {
                UnityEngine.Debug.LogWarning("No model loaded, no need to unload");
            }
            else
            {
                audioTagging.Dispose();
                audioTagging = null;
                _modelLoadFlag = false;

            }
            if (device != null)
            {
                device.StopRecording();
                device.OnFrameCollected -= HandleAudioFrameCollected;
                device = null;
            }

            _progressTracker?.Reset();
            _progressTracker?.SetVisible(false);

            UpdateLoadButtonUI();

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
        }

        private async Task InitDropdownAsync()
        {
            _modelIDDropdown.options.Clear();
            _modelIDDropdown.captionText.text = "Fetching model manifest from GitHub…";
            _modelLoadOrUnloadButton.gameObject.SetActive(false);
            var manifest = await SherpaOnnxModelRegistry.Instance.GetManifestAsync(SherpaOnnxModuleType.AudioTagging);
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
                _tagResultText.text = string.Empty;
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
                // Don't process if the audio tagger isn't ready or is disposed
                if (audioTagging == null)
                {
                    return;
                }

                var result = await audioTagging.TagStreamAsync(pcm);
                if (result != null && result.Length > 0)
                {
                    _tagResultText.text = AudioTagExtensions.ToString(result, "\n");
                }
            }
            catch (Exception ex)
            {
                // Log errors to avoid crashing the application
                Debug.LogError($"An error occurred in HandleAudioFrameCollected: {ex}");
            }
        }

        #region FeedbackHandler

        public void OnFeedback(PrepareFeedback feedback)
        {
            _progressTracker.Reset();
            _progressTracker.MarkStageComplete(Stage.Prepare, feedback.Message);
            _tipsText.text = $"<b>[Loading]:</b> {feedback.Metadata.modelId}\nThe model is loading, please wait patiently.";
            _tagResultText.text = "Preparing audio-tagging model...";
        }

        public void OnFeedback(DownloadFeedback feedback)
        {
            _progressTracker.UpdateStage(Stage.Download, feedback.Message, feedback.Progress);
            _tagResultText.text = "Downloading audio-tagging model...";
        }

        public void OnFeedback(CleanFeedback feedback)
        {
            _progressTracker.MarkStageComplete(Stage.Clean, feedback.Message);
            _tagResultText.text = "Cleaning previous model files...";
        }

        public void OnFeedback(VerifyFeedback feedback)
        {
            _progressTracker.UpdateStage(Stage.Verify, feedback.Message, feedback.Progress);
            _tagResultText.text = "Verifying audio-tagging assets...";
        }

        public void OnFeedback(DecompressFeedback feedback)
        {
            _progressTracker.UpdateStage(Stage.Decompress, feedback.Message, feedback.Progress);
            _tagResultText.text = "Decompressing audio-tagging package...";
        }

        public void OnFeedback(LoadFeedback feedback)
        {
            _progressTracker.MarkStageComplete(Stage.Load, feedback.Message);
            _tipsText.text = $"<b><color=cyan>[Loading]</color>:</b> \nThe model {feedback.Metadata.modelId} is loading.";
            _tagResultText.text = "Finishing model load...";
        }

        public void OnFeedback(SuccessFeedback feedback)
        {
            _progressTracker.Complete("Success");
            _progressTracker.SetVisible(false);
            _initMessageText.text = string.Empty;
            _tagResultText.text = "<b><i>Now try making some noise</i></b>";
            _tipsText.text = $"<b><color=green>[Loaded]:</color></b> {feedback.Metadata.modelId}\nYou can now test audio-tagging by making some noise.";

            StartRecording();
        }

        public void OnFeedback(FailedFeedback feedback)
        {
            UnityEngine.Debug.LogError($"[Failed] :{feedback.Message}");
            Unload();
            _initMessageText.text = feedback.Message;
            _tipsText.text = $"<b><color=red>[Failed]</color>:</b> \nThe model load failed.";
            _tagResultText.text = "<color=red><b>Model load failed</b></color>";
        }

        public void OnFeedback(CancelFeedback feedback)
        {
            Unload();
            _tipsText.text = $"<b><color=yellow>Cancelled</color>:</b> {feedback.Metadata.modelId}\n{feedback.Message}";
            _tagResultText.text = "Model loading cancelled.";
        }
        #endregion

        public void OpenGithubRepo()
        {
            Application.OpenURL("https://github.com/EitanWong/com.eitan.sherpa-onnx-unity");
        }
    }

}
