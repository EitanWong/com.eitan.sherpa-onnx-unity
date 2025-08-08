
namespace Eitan.SherpaOnnxUnity.Samples
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using System.Threading.Tasks;
    using Eitan.SherpaOnnxUnity.Runtime;

    using UnityEngine;
    using UnityEngine.UI;
    using static UnityEngine.UI.Dropdown;

    [RequireComponent(typeof(AudioSource))]
    public class VoiceActivityDetectionExample : MonoBehaviour, ISherpaFeedbackHandler
    {
        // [SerializeField] private string _onlineModelID = "sherpa-onnx-streaming-zipformer-bilingual-zh-en-2023-02-20";
        [Header("UI Components")]
        [SerializeField] private Dropdown _modelIDDropdown;
        [SerializeField] private Button _modelLoadOrUnloadButton;
        [SerializeField] private Text _initMessageText;
        [SerializeField] private Eitan.SherpaOnnxUnity.Samples.UI.EasyProgressBar _totalInitProgressBar;
        [SerializeField] private Text _totalInitBarText;
        [SerializeField] private Text _tipsText;
        [SerializeField] private Text _vadStatusText;


        private VoiceActivityDetection vad;

        private readonly int SampleRate = 16000;

        private Mic.Device device;

        private Color _originLoadBtnColor;
        private readonly string defaultModelID = "ten-vad";
        
        private AudioSource audioSource;
        private readonly List<float> accumulatedSpeech = new List<float>();
        private bool isPlayingBack = false;

        /// <summary>
        /// True if the VAD model is loaded.
        /// </summary>
        private bool IsModelLoaded => vad != null;

        #region Unity Lifecycle Methods
        private void Start()
        {
            audioSource = GetComponent<AudioSource>();
            Application.runInBackground = true;
            Application.targetFrameRate = 30;
            _modelLoadOrUnloadButton.onClick.AddListener(HandleModelLoadOrUnloadButtonClick);
            _totalInitProgressBar.gameObject.SetActive(false);
            _initMessageText.gameObject.SetActive(false);
            _tipsText.text = "Load a model to begin.";
            _vadStatusText.text = "VAD Status: Model not loaded\nPlease select a model to load.";
            _originLoadBtnColor = _modelLoadOrUnloadButton.GetComponent<Image>().color;
            InitDropdown();
            UpdateLoadButtonUI();
        }

        private void OnDestroy()
        {
            // Ensure all resources are properly released when the object is destroyed.
            Cleanup();

            // Clean up UI event listeners to prevent memory leaks.
            if (_modelLoadOrUnloadButton != null)
            {
                _modelLoadOrUnloadButton.onClick.RemoveListener(HandleModelLoadOrUnloadButtonClick);
            }
        }
        #endregion

        #region Model and Recording Control
        /// <summary>
        /// Loads the VAD model with the specified ID and prepares for recording.
        /// </summary>
        /// <param name="modelID">The ID of the model to load.</param>
        private void Load(string modelID)
        {
            if (IsModelLoaded)
            {
                Debug.LogError("Please unload the current model first.");
                return;
            }

            var reporter = new SherpaOnnxFeedbackReporter(null, this);
            vad = new VoiceActivityDetection(modelID, SampleRate, reporter);
            vad.OnSpeakingStateChanged += HandleSpeechStateChanged;
            vad.OnSpeechSegmentDetected += HandleSpeechSegmentCollected;
            
            UpdateLoadButtonUI();

        }

        /// <summary>
        /// Unloads the current VAD model and releases all related resources.
        /// </summary>
        private void Unload()
        {
            if (!IsModelLoaded)
            {
                Debug.LogWarning("No model is loaded, no need to unload.");
                return;
            }
         
            
            Cleanup();
            _vadStatusText.text = "VAD Status: Model not loaded\nPlease select a model to load.";
            _tipsText.text = "Load a model to begin.";
            UpdateLoadButtonUI();
        }

        /// <summary>
        /// Starts microphone recording.
        /// </summary>
        private void StartRecording()
        {
            if (isPlayingBack)
            {
                Debug.LogWarning("Cannot start recording during playback.");
                return;
            }

            Mic.Init();
            var devices = Mic.AvailableDevices;
            if (devices.Count > 0)
            {
                // Only get a new device and subscribe if we don't have one.
                // This prevents re-subscribing the event handler on resume.
                if (device == null)
                {
                    device = devices[0];
                    device.OnFrameCollected += HandleAudioFrameCollected;
                }

                device.StartRecording(SampleRate, 10);
                Debug.Log($"Using device: {device.Name}, Sampling Frequency: {device.SamplingFrequency}");
            }
        }

        /// <summary>
        /// A unified resource cleanup method to release all occupied resources.
        /// </summary>
        private void Cleanup()
        {
            // Stop microphone and unsubscribe
            if (device != null)
            {
                device.StopRecording();
                device.OnFrameCollected -= HandleAudioFrameCollected;
                device = null;
            }

            // Destroy VAD and unsubscribe
            if (vad != null)
            {
                vad.OnSpeakingStateChanged -= HandleSpeechStateChanged;
                vad.OnSpeechSegmentDetected -= HandleSpeechSegmentCollected;
                vad.Dispose();
                vad = null;
            }

            // Reset playback state
            if (audioSource != null && audioSource.isPlaying)
            {
                audioSource.Stop();
            }
            accumulatedSpeech.Clear();
            isPlayingBack = false;
        }
        #endregion

        #region UI and Initialization
        private void InitDropdown()
        {
            var manifest = SherpaOnnxModelRegistry.Instance.GetManifest();

            _modelIDDropdown.options.Clear();
            if (manifest.models != null)
            {
                System.Collections.Generic.List<OptionData> modelOptions = manifest.Filter(m => (m.moduleType == SherpaOnnxModuleType.VoiceActivityDetection)).Select(m => new OptionData(m.modelId)).ToList();
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

            if (IsModelLoaded)// Model is already loaded
            {
                _modelLoadOrUnloadButton.GetComponentInChildren<Text>().text = "Unload Model";
                _modelLoadOrUnloadButton.GetComponent<Image>().color = Color.red;
                _modelIDDropdown.interactable = false;

                _totalInitProgressBar.gameObject.SetActive(true);
                _initMessageText.gameObject.SetActive(true);
            }
            else // No model loaded, preparing to initialize a new one
            {

                _modelLoadOrUnloadButton.GetComponentInChildren<Text>().text = "Load Model";
                _modelLoadOrUnloadButton.GetComponent<Image>().color = _originLoadBtnColor;
                _modelIDDropdown.interactable = true;

                _totalInitProgressBar.gameObject.SetActive(false);
                _initMessageText.gameObject.SetActive(false);
                _tipsText.text = "Load a model to begin.";
                
            }
        }

        private void HandleModelLoadOrUnloadButtonClick()
        {
            if (IsModelLoaded)// Model is already loaded
            {
                Unload();
            }
            else // No model loaded, initialize a new one
            {
                Load(_modelIDDropdown.captionText.text);
            }

        }
        #endregion

        #region VAD Core Logic
        private void HandleSpeechStateChanged(bool isSpeaking)
        {
            if (isSpeaking)
            {
                _vadStatusText.text = "VAD Status: <color=green>Speaking</color>";
                _tipsText.text = "Recording speech segment...";
            }
            else
            {
                _vadStatusText.text = "VAD Status: <color=red>Silent</color>";
                _tipsText.text = "Speech ended. Preparing for playback...";
                // When the user stops speaking and we have recorded something, start playback.
                if (accumulatedSpeech.Count > 0 && !isPlayingBack)
                {
                    _ = PlayAndResumeAsync();
                }
            }
        }

        private void HandleSpeechSegmentCollected(float[] segment)
        {
            if (segment == null || segment.Length == 0)
            {
                Debug.LogWarning("Received an empty speech segment.");
                return;
            }
            accumulatedSpeech.AddRange(segment);
        }

        private async Task PlayAndResumeAsync()
        {
            if (accumulatedSpeech.Count == 0)
            {
                return;
            }

            // Set a flag to prevent audio processing during playback.

            isPlayingBack = true;

            var samples = accumulatedSpeech.ToArray();
            accumulatedSpeech.Clear();

            AudioClip clip = AudioClip.Create("SpeechSegment", samples.Length, 1, SampleRate, false);
            clip.SetData(samples, 0);

            audioSource.clip = clip;
            audioSource.Play();
            _vadStatusText.text = $"Playback: <color=blue>{clip.length:F2}s</color>";
            _tipsText.text = "Playing back the last detected speech segment.";
            // Debug.Log($"Playing back {clip.length:F2} seconds of audio.");

            // Wait for playback to complete.
            while (audioSource.isPlaying)
            {
                await Task.Yield();
            }

            // Destroy the temporary AudioClip to free up memory.
            Destroy(clip);

            // Reset the flag to allow audio processing to resume.
            if (IsModelLoaded)
            {
                _vadStatusText.text = "VAD Status: <color=grey>Listening...</color>";
                // Debug.Log("Playback finished. Resuming audio processing.");
                _tipsText.text = "Playback finished. Resumed recording. Speak now.";
            }
            isPlayingBack = false;
        }

        private void HandleAudioFrameCollected(int sampleRate, int channelCount, float[] pcm)
        {
            try
            {
                // While playing back the previous segment, ignore new audio frames.
                if (vad == null || isPlayingBack)
                {
                    return;
                }
                vad.StreamDetect(pcm);

            }
            catch (Exception ex)
            {
                // Log errors to avoid crashing the application
                Debug.LogError($"An error occurred in HandleAudioFrameCollected: {ex}");
            }
        }
        #endregion

        #region ISherpaFeedbackHandler Implementation

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
        }

        public void OnFeedback(DownloadFeedback feedback)
        {
            UpdateOverallProgress(Mathf.Clamp(0.5f * feedback.Progress, 0, 0.5f), feedback.Message);
        }

        public void OnFeedback(UncompressFeedback feedback)
        {
            UpdateOverallProgress(0.5f + (0.49f * feedback.Progress), feedback.Message);
        }

        public void OnFeedback(VerifyFeedback feedback)
        {
            UpdateOverallProgress(0.99f, feedback.Message);
        }

        public void OnFeedback(LoadFeedback feedback)
        {
            UpdateOverallProgress(0.99f, feedback.Message);
            _tipsText.text = $"<b><color=cyan>[Loading]</color>:</b> \nThe model {feedback.Metadata.modelId} is loading.";
        }

        public void OnFeedback(CancelFeedback feedback)
        {
            SetProgressActive(false);
            _tipsText.text = $"<b><color=yellow>Cancelled</color>:</b> {feedback.Metadata.modelId}\n{feedback.Message}";
            Unload();
        }

        public void OnFeedback(SuccessFeedback feedback)
        {
            SetProgressActive(false);
            UpdateOverallProgress(1f, "Success");
            _initMessageText.text = string.Empty;
            _vadStatusText.text = $"SpeechSyntesis Model Loaded\nNow you can type some text to generate.";
            _tipsText.text = $"Model {feedback.Metadata.modelId} loaded.";

            StartRecording();

        }

        public void OnFeedback(FailedFeedback feedback)
        {
            SetProgressActive(false);
            UnityEngine.Debug.LogError($"[Failed] :{feedback.Message}");
            _initMessageText.text = feedback.Message;
            _tipsText.text = $"<b><color=red>[Failed]</color>:</b> \nThe model failed to load.";
            Unload();

        }

        public void OnFeedback(CleanFeedback feedback)
        {
            SetProgressActive(false);
            _initMessageText.text = feedback.Message;
        }
        #endregion

        public void OpenGithubRepo()
        {
            Application.OpenURL("https://github.com/EitanWong/com.eitan.sherpa-onnx-unity");
        }
    }

}