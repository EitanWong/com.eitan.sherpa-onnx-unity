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
    public class SpeechEnhancementExample : MonoBehaviour, ISherpaFeedbackHandler
    {
        [Header("UI Components")]
        [SerializeField] private Dropdown _modelIDDropdown;
        [SerializeField] private Button _modelLoadOrUnloadButton;
        [SerializeField] private Button _recordStopButton;
        [SerializeField] private Toggle _enhancementEnabledToggle;
        [SerializeField] private Text _initMessageText;
        [SerializeField] private UI.EasyProgressBar _totalInitProgressBar;
        [SerializeField] private Text _totalInitBarText;
        [SerializeField] private Text _tipsText;
        [SerializeField] private Text _recordingStatusText;
        [SerializeField] private Text _enhancementStatusText;

        [Header("Recording Settings")]
        [SerializeField] private float _recordingBufferTime = 0.1f; // Process audio in 100ms chunks

        private SpeechEnhancement _speechEnhancement;
        private readonly int SampleRate = 16000;
        private Mic.Device _device;
        private AudioSource _audioSource;

        // Recording state
        private bool _isRecording = false;
        private bool _isPlayingBack = false;
        private bool _isEnhancementEnabled = true; // Default to enabled
        private readonly List<float> _recordedAudio = new();
        private readonly List<float> _processedAudio = new(); // Enhanced or original based on enhancement setting

        // UI colors
        private Color _originLoadBtnColor;
        private Color _originRecordBtnColor;
        private readonly string _defaultModelID = "gtcrn-simple";

        /// <summary>
        /// True if the Speech Enhancement model is loaded.
        /// </summary>
        private bool IsModelLoaded => _speechEnhancement != null;

        #region Unity Lifecycle Methods
        private void Start()
        {
            _audioSource = GetComponent<AudioSource>();
            Application.runInBackground = true;
            Application.targetFrameRate = 30;

            // Initialize UI
            _modelLoadOrUnloadButton.onClick.AddListener(HandleModelLoadOrUnloadButtonClick);
            _recordStopButton.onClick.AddListener(HandleRecordStopButtonClick);
            _enhancementEnabledToggle.onValueChanged.AddListener(HandleEnhancementToggleChanged);
            
            // Set toggle UI to match default enhancement state
            _enhancementEnabledToggle.isOn = _isEnhancementEnabled;
            
            _totalInitProgressBar.gameObject.SetActive(false);
            _initMessageText.gameObject.SetActive(false);
            _tipsText.text = "Please load a Speech Enhancement model first to begin recording.";
            
            // Hide recording-related UI when model is not loaded
            _recordingStatusText.gameObject.SetActive(false);
            _enhancementStatusText.gameObject.SetActive(false);
            _recordStopButton.gameObject.SetActive(false);
            _enhancementEnabledToggle.gameObject.SetActive(false);
            
            _originLoadBtnColor = _modelLoadOrUnloadButton.GetComponent<Image>().color;
            _originRecordBtnColor = _recordStopButton.GetComponent<Image>().color;
            
            _ = InitDropdown();
            UpdateUI();
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
            if (_recordStopButton != null)
            {
                _recordStopButton.onClick.RemoveListener(HandleRecordStopButtonClick);
            }
        }
        #endregion

        #region Model and Recording Control
        /// <summary>
        /// Loads the Speech Enhancement model with the specified ID.
        /// </summary>
        /// <param name="modelID">The ID of the model to load.</param>
        private void Load(string modelID)
        {
            if (IsModelLoaded)
            {
                Debug.LogError("Please unload the current model first.");
                return;
            }

            var reporter = new SherpaFeedbackReporter(null, this);
            _speechEnhancement = new SpeechEnhancement(modelID, SampleRate, reporter);
            UpdateUI();
        }

        /// <summary>
        /// Unloads the current Speech Enhancement model and releases all related resources.
        /// </summary>
        private void Unload()
        {
            if (!IsModelLoaded)
            {
                Debug.LogWarning("No model is loaded, no need to unload.");
                return;
            }

            // Prevent unloading during recording
            if (_isRecording)
            {
                Debug.LogWarning("Cannot unload model while recording is in progress.");
                _tipsText.text = "Cannot unload model during recording. Please stop recording first.";
                return;
            }

            StopRecording();
            Cleanup();
            _tipsText.text = "Please load a Speech Enhancement model first to begin recording.";
            UpdateUI();
        }

        /// <summary>
        /// Starts microphone recording and real-time speech enhancement.
        /// </summary>
        private void StartRecording()
        {
            if (!IsModelLoaded)
            {
                Debug.LogError("Cannot start recording without a loaded model.");
                return;
            }

            if (_isRecording || _isPlayingBack)
            {
                Debug.LogWarning("Cannot start recording during recording or playback.");
                return;
            }

            // Clear previous recordings
            _recordedAudio.Clear();
            _processedAudio.Clear();

            // Initialize microphone
            Mic.Init();
            var devices = Mic.AvailableDevices;
            if (devices.Count > 0)
            {
                _device = devices[0];
                _device.OnFrameCollected += HandleAudioFrameCollected;
                _device.StartRecording(SampleRate, Mathf.RoundToInt(_recordingBufferTime * 1000));
                
                _isRecording = true;
                _recordingStatusText.text = "Recording Status: <color=red>● Recording</color>";
                _enhancementStatusText.text = "Enhancement Status: <color=green>Processing</color>";
                _tipsText.text = "Recording in progress. Speak into your microphone.\nClick 'Stop Recording' when finished.";
                
                Debug.Log($"Recording started with device: {_device.Name}");
            }
            else
            {
                Debug.LogError("No microphone devices available.");
            }

            UpdateUI();
        }

        /// <summary>
        /// Stops microphone recording and processes the final enhanced audio.
        /// </summary>
        private void StopRecording()
        {
            if (!_isRecording)
            {
                return;
            }

            _isRecording = false;

            // Stop microphone
            if (_device != null)
            {
                _device.StopRecording();
                _device.OnFrameCollected -= HandleAudioFrameCollected;
                _device = null;
            }

            _recordingStatusText.text = "Recording Status: <color=blue>Processing final audio...</color>";
            _enhancementStatusText.text = "Enhancement Status: <color=orange>Finalizing</color>";
            _tipsText.text = "Recording stopped. Processing and preparing playback...";

            // Play the processed audio
            _ = PlayProcessedAudioAsync();

            UpdateUI();
        }

        /// <summary>
        /// A unified resource cleanup method to release all occupied resources.
        /// </summary>
        private void Cleanup()
        {
            // Stop recording if active
            if (_isRecording)
            {
                StopRecording();
            }

            // Stop microphone and unsubscribe
            if (_device != null)
            {
                _device.StopRecording();
                _device.OnFrameCollected -= HandleAudioFrameCollected;
                _device = null;
            }

            // Destroy Speech Enhancement and dispose
            _speechEnhancement?.Dispose();
            _speechEnhancement = null;

            // Reset playback state
            if (_audioSource != null && _audioSource.isPlaying)
            {
                _audioSource.Stop();
            }
            
            _recordedAudio.Clear();
            _processedAudio.Clear();
            _isRecording = false;
            _isPlayingBack = false;
        }
        #endregion

        #region Audio Processing
        /// <summary>
        /// Handles incoming audio frames from the microphone for real-time enhancement.
        /// </summary>
        private void HandleAudioFrameCollected(int sampleRate, int channelCount, float[] pcm)
        {
            try
            {
                if (!IsModelLoaded || !_isRecording || _isPlayingBack || pcm == null || pcm.Length == 0)
                {
                    return;
                }

                // Add to recorded audio for comparison
                _recordedAudio.AddRange(pcm);

                // Process audio based on internal enhancement flag
                float[] processedChunk;
                if (_isEnhancementEnabled)
                {
                    // Create a copy for enhancement (in-place processing modifies the original)
                    processedChunk = new float[pcm.Length];
                    Array.Copy(pcm, processedChunk, pcm.Length);
                    
                    // Enhance the audio in-place
                    _speechEnhancement.EnhanceSync(processedChunk, sampleRate);
                }
                else
                {
                    // Use original audio without enhancement
                    processedChunk = pcm;
                }

                // Add processed audio to collection
                _processedAudio.AddRange(processedChunk);

                // Update UI with processing info
                var totalSeconds = _processedAudio.Count / (float)SampleRate;
                var enhancementStatus = _isEnhancementEnabled ? "<color=green>Enhancing</color>" : "<color=orange>Bypassed</color>";
                _enhancementStatusText.text = $"Enhancement Status: {enhancementStatus}\nProcessed: {totalSeconds:F1}s";
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error in HandleAudioFrameCollected: {ex}");
                _enhancementStatusText.text = "Enhancement Status: <color=red>Error occurred</color>";
            }
        }

        /// <summary>
        /// Plays the processed audio (enhanced or original based on toggle setting).
        /// </summary>
        private async Task PlayProcessedAudioAsync()
        {
            if (_processedAudio.Count == 0)
            {
                _tipsText.text = "No enhanced audio to play. Try recording again.";
                return;
            }

            _isPlayingBack = true;
            _recordingStatusText.text = "Recording Status: <color=blue>Playback</color>";

            try
            {
                // Create processed audio clip
                var processedSamples = _processedAudio.ToArray();
                var processedClip = AudioClip.Create("ProcessedAudio", processedSamples.Length, 1, SampleRate, false);
                processedClip.SetData(processedSamples, 0);

                // Play processed audio
                _audioSource.clip = processedClip;
                _audioSource.Play();

                var duration = processedClip.length;
                var audioType = _isEnhancementEnabled ? "Enhanced" : "Original";
                var playbackColor = _isEnhancementEnabled ? "blue" : "orange";
                
                _enhancementStatusText.text = $"Enhancement Status: <color={playbackColor}>Playing {audioType} Audio</color>\nDuration: {duration:F1}s";
                
                if (_isEnhancementEnabled)
                {
                    _tipsText.text = $"Playing enhanced audio ({duration:F1}s). Noise reduction and quality improvement applied.";
                }
                else
                {
                    _tipsText.text = $"Playing original audio ({duration:F1}s). No enhancement applied for comparison.";
                }

                Debug.Log($"Playing {audioType.ToLower()} audio: {duration:F2} seconds");

                // Wait for playback to complete
                while (_audioSource.isPlaying)
                {
                    await Task.Yield();
                }

                // Clean up
                Destroy(processedClip);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error playing enhanced audio: {ex}");
                _enhancementStatusText.text = "Enhancement Status: <color=red>Playback Error</color>";
            }
            finally
            {
                // Reset state
                _isPlayingBack = false;
                if (IsModelLoaded)
                {
                    _recordingStatusText.text = "Recording Status: Ready";
                    _enhancementStatusText.text = "Enhancement Status: <color=green>Ready</color>";
                    _tipsText.text = "Playback complete. You can record again or compare with the original audio.";
                }
                else
                {
                    _tipsText.text = "Please load a Speech Enhancement model first to begin recording.";
                }
                
                UpdateUI();
            }
        }
        #endregion

        #region UI and Initialization
        private async Task InitDropdown()
        {
            var manifest = await SherpaOnnxModelRegistry.Instance.GetManifestAsync();

            _modelIDDropdown.options.Clear();
            if (manifest.models != null)
            {
                var modelOptions = manifest.Filter(m => m.moduleType == SherpaOnnxModuleType.SpeechEnhancement)
                    .Select(m => new OptionData(m.modelId)).ToList();

                if (modelOptions.Count > 0)
                {
                    _modelIDDropdown.AddOptions(modelOptions);
                    var defaultIndex = modelOptions.FindIndex(m => m.text == _defaultModelID);
                    if (defaultIndex >= 0)
                    {
                        _modelIDDropdown.value = defaultIndex;
                    }
                    _modelIDDropdown.interactable = true;
                }
                else
                {
                    _modelIDDropdown.AddOptions(new List<OptionData> { new("No Speech Enhancement models available") });
                    _modelIDDropdown.interactable = false;
                }
            }
            else
            {
                _modelIDDropdown.interactable = false;
            }
        }

        private void UpdateUI()
        {
            // Update Load/Unload button
            if (IsModelLoaded)
            {
                _modelLoadOrUnloadButton.GetComponentInChildren<Text>().text = "Unload Model";
                _modelLoadOrUnloadButton.GetComponent<Image>().color = Color.red;
                _modelIDDropdown.interactable = false;
                
                // Hide progress bar and init message when model is loaded
                _totalInitProgressBar.gameObject.SetActive(false);
                _initMessageText.gameObject.SetActive(false);
                
                // Show recording UI when model is loaded
                _recordingStatusText.gameObject.SetActive(true);
                _enhancementStatusText.gameObject.SetActive(true);
                _recordStopButton.gameObject.SetActive(true);
                _enhancementEnabledToggle.gameObject.SetActive(true);
                
                // Prevent unloading during recording
                _modelLoadOrUnloadButton.interactable = !_isRecording;
                if (_isRecording)
                {
                    _modelLoadOrUnloadButton.GetComponent<Image>().color = Color.gray;
                }
            }
            else
            {
                _modelLoadOrUnloadButton.GetComponentInChildren<Text>().text = "Load Model";
                _modelLoadOrUnloadButton.GetComponent<Image>().color = _originLoadBtnColor;
                _modelLoadOrUnloadButton.interactable = true;
                _modelIDDropdown.interactable = true;
                _totalInitProgressBar.gameObject.SetActive(false);
                _initMessageText.gameObject.SetActive(false);
                
                // Hide recording UI when model is not loaded
                _recordingStatusText.gameObject.SetActive(false);
                _enhancementStatusText.gameObject.SetActive(false);
                _recordStopButton.gameObject.SetActive(false);
                _enhancementEnabledToggle.gameObject.SetActive(false);
            }

            // Update Record/Stop button (only when visible)
            if (IsModelLoaded)
            {
                _recordStopButton.interactable = !_isPlayingBack;

                if (_isRecording)
                {
                    _recordStopButton.GetComponentInChildren<Text>().text = "Stop Recording";
                    _recordStopButton.GetComponent<Image>().color = Color.red;
                }
                else
                {
                    _recordStopButton.GetComponentInChildren<Text>().text = "Start Recording";
                    _recordStopButton.GetComponent<Image>().color = _originRecordBtnColor;
                }

                // Disable record button during playback
                if (_isPlayingBack)
                {
                    _recordStopButton.GetComponent<Image>().color = Color.gray;
                }
            }
        }

        private void HandleModelLoadOrUnloadButtonClick()
        {
            if (IsModelLoaded)
            {
                // Show warning if trying to unload during recording
                if (_isRecording)
                {
                    _tipsText.text = "<color=orange>Warning:</color> Cannot unload model during recording.\nPlease stop recording first.";
                    return;
                }
                Unload();
            }
            else
            {
                Load(_modelIDDropdown.captionText.text);
            }
        }

        private void HandleRecordStopButtonClick()
        {
            if (_isRecording)
            {
                StopRecording();
            }
            else
            {
                StartRecording();
            }
        }

        /// <summary>
        /// Handles the enhancement toggle value change.
        /// Updates the internal bool variable when toggle changes.
        /// </summary>
        private void HandleEnhancementToggleChanged(bool isEnabled)
        {
            // Update internal enhancement state
            _isEnhancementEnabled = isEnabled;
            
            UpdateEnhancementStatusText();
            
            if (IsModelLoaded && !_isRecording && !_isPlayingBack)
            {
                if (isEnabled)
                {
                    _tipsText.text = "Enhancement enabled. Recording will apply noise reduction and quality improvement.";
                }
                else
                {
                    _tipsText.text = "Enhancement disabled. Recording will capture original audio for comparison.";
                }
            }
        }

        /// <summary>
        /// Updates the enhancement status text based on internal enhancement state.
        /// </summary>
        private void UpdateEnhancementStatusText()
        {
            if (_isEnhancementEnabled)
            {
                _enhancementStatusText.text = "Enhancement Status: <color=green>Enabled - Model loaded</color>";
            }
            else
            {
                _enhancementStatusText.text = "Enhancement Status: <color=orange>Disabled - Bypass mode</color>";
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
            _tipsText.text = $"<b>[Loading]:</b> {feedback.Metadata.modelId}\nThe Speech Enhancement model is loading, please wait patiently.";
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
            _tipsText.text = $"<b><color=cyan>[Loading]</color>:</b> \nThe Speech Enhancement model {feedback.Metadata.modelId} is loading.";
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
            _recordingStatusText.text = "Recording Status: Ready";
            _enhancementStatusText.text = "Enhancement Status: <color=green>Model loaded successfully</color>";
            _tipsText.text = $"✓ Speech Enhancement model '{feedback.Metadata.modelId}' loaded successfully!\nClick 'Start Recording' to begin noise reduction and audio enhancement.";
            UpdateUI();
        }

        public void OnFeedback(FailedFeedback feedback)
        {
            SetProgressActive(false);
            Debug.LogError($"[Failed]: {feedback.Message}");
            _initMessageText.text = feedback.Message;
            _tipsText.text = $"<b><color=red>[Failed]</color>:</b> \nThe Speech Enhancement model failed to load.\nError: {feedback.Message}";
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

        #region Debug Methods (Optional)
        /// <summary>
        /// For debugging: Play original recorded audio for comparison.
        /// </summary>
        public async void PlayOriginalAudio()
        {
            if (_recordedAudio.Count == 0 || _isRecording || _isPlayingBack)
            {
                return;
            }

            _isPlayingBack = true;
            var originalSamples = _recordedAudio.ToArray();
            var originalClip = AudioClip.Create("OriginalAudio", originalSamples.Length, 1, SampleRate, false);
            originalClip.SetData(originalSamples, 0);

            _audioSource.clip = originalClip;
            _audioSource.Play();
            _tipsText.text = $"Playing original audio ({originalClip.length:F1}s) for comparison.";

            while (_audioSource.isPlaying)
            {
                await Task.Yield();
            }

            Destroy(originalClip);
            _isPlayingBack = false;
            _tipsText.text = "Original audio playback complete.";
            UpdateUI();
        }
        #endregion
    }
}