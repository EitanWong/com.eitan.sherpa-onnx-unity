namespace Eitan.SherpaOnnxUnity.Samples
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Eitan.SherpaOnnxUnity.Runtime;
    using UnityEngine;
    using UnityEngine.UI;
    using static UnityEngine.UI.Dropdown;

    /// <summary>
    /// 离线语音识别示例 / Offline Speech Recognition Example
    /// 提供语音识别模型的加载/卸载和录音转录功能
    /// Provides speech recognition model loading/unloading and recording transcription functionality
    /// </summary>
    public class OfflineSpeechRecognitionExample : MonoBehaviour, ISherpaFeedbackHandler
    {
        #region Constants and Configuration
        // 常量配置 / Constants Configuration
        private const int SAMPLE_RATE = 16000;
        private const int MAX_RECORDING_DURATION = 60;
        private const string DEFAULT_MODEL_ID = "sherpa-onnx-zipformer-ctc-zh-int8-2025-07-03";
        private const int TARGET_FRAME_RATE = 30;
        private const int MIC_FRAME_LENGTH = 10; // 毫秒 / milliseconds
        #endregion

        #region UI Components
        // UI组件引用 / UI Component References
        [Header("UI Components")]
        [SerializeField] private Dropdown _modelIDDropdown;
        [SerializeField] private Button _modelLoadOrUnloadButton;
        [SerializeField] private Text _initMessageText;
        [SerializeField] private Eitan.SherpaOnnxUnity.Samples.UI.EasyProgressBar _totalInitProgressBar;
        [SerializeField] private Text _totalInitBarText;
        [SerializeField] private Text _tipsText;
        [SerializeField] private Text _transcriptionText;
        [SerializeField] private Button _recordingBtn;
        #endregion

        #region Private Fields
        // 核心组件 / Core Components
        private SpeechRecognition speechRecognition;
        private Mic.Device device;
        private RingBuffer<float> _ringBuffer;

        // 状态管理 / State Management
        private bool _modelLoadFlag;
        private string lastCachedText;

        // UI状态缓存 / UI State Cache
        private Color _originLoadBtnColor;
        private Color _originRecordingBtnColor;
        #endregion

        #region Properties
        // 属性 / Properties
        /// <summary>
        /// 当前是否正在录音 / Whether currently recording
        /// </summary>
        private bool IsRecording => device != null && device.IsRecording;

        /// <summary>
        /// 模型是否已加载 / Whether model is loaded
        /// </summary>
        private bool IsModelLoaded => speechRecognition != null && _modelLoadFlag;
        #endregion

        #region Unity Lifecycle
        /// <summary>
        /// 初始化组件 / Initialize components
        /// </summary>
        private void Start()
        {
            InitializeApplication();
            InitializeUI();
            InitializeEventListeners();
            _ = InitializeDropdownAsync();
        }

        /// <summary>
        /// 清理资源 / Clean up resources
        /// </summary>
        private void OnDestroy()
        {
            CleanupResources();
        }
        #endregion

        #region Initialization Methods
        /// <summary>
        /// 初始化应用程序设置 / Initialize application settings
        /// </summary>
        private void InitializeApplication()
        {
            Application.runInBackground = true;
            Application.targetFrameRate = TARGET_FRAME_RATE;
        }

        /// <summary>
        /// 初始化UI状态 / Initialize UI state
        /// </summary>
        private void InitializeUI()
        {
            // 隐藏初始化相关UI / Hide initialization related UI
            _totalInitProgressBar.gameObject.SetActive(false);
            _initMessageText.gameObject.SetActive(false);
            _recordingBtn.gameObject.SetActive(false);

            // 设置初始文本 / Set initial text
            _transcriptionText.text = "Please click the button to load the model";
            _tipsText.text = string.Empty;

            // 缓存原始颜色 / Cache original colors
            _originLoadBtnColor = _modelLoadOrUnloadButton.GetComponent<Image>().color;
            _originRecordingBtnColor = _recordingBtn.GetComponent<Image>().color;

            // 更新UI状态 / Update UI state
            UpdateLoadButtonUI();
            UpdateRecordingButtonUI();
        }

        /// <summary>
        /// 初始化事件监听器 / Initialize event listeners
        /// </summary>
        private void InitializeEventListeners()
        {
            _modelLoadOrUnloadButton.onClick.AddListener(HandleModelLoadOrUnloadButtonClick);
            _recordingBtn.onClick.AddListener(HandleRecordingButtonClick);
        }

        /// <summary>
        /// 异步初始化下拉菜单 / Asynchronously initialize dropdown
        /// </summary>
        private async Task InitializeDropdownAsync()
        {
            try
            {
                var manifest = await SherpaOnnxModelRegistry.Instance.GetManifestAsync();
                PopulateModelDropdown(manifest);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to initialize dropdown: {ex.Message}");
                _modelIDDropdown.interactable = false;
            }
        }

        /// <summary>
        /// 填充模型下拉菜单 / Populate model dropdown
        /// </summary>
        private void PopulateModelDropdown(SherpaOnnxModelManifest manifest)
        {
            _modelIDDropdown.options.Clear();

            if (manifest.models != null)
            {
                var modelOptions = manifest
                    .Filter(m => m.moduleType == SherpaOnnxModuleType.SpeechRecognition && !m.IsOnlineModel())
                    .Select(m => new OptionData(m.modelId))
                    .ToList();

                _modelIDDropdown.AddOptions(modelOptions);

                // 设置默认选中项 / Set default selected item
                var defaultIndex = modelOptions.FindIndex(m => m.text == DEFAULT_MODEL_ID);
                if (defaultIndex >= 0)
                {
                    _modelIDDropdown.value = defaultIndex;
                }

                _modelIDDropdown.interactable = true;
            }
            else
            {
                _modelIDDropdown.interactable = false;
            }
        }
        #endregion

        #region Model Management
        /// <summary>
        /// 加载语音识别模型 / Load speech recognition model
        /// </summary>
        /// <param name="modelID">模型ID / Model ID</param>
        private void LoadModel(string modelID)
        {
            if (IsModelLoaded)
            {
                Debug.LogError("Please unload current model first");
                return;
            }

            try
            {
                var reporter = new SherpaFeedbackReporter(null, this);
                speechRecognition = new SpeechRecognition(modelID, SAMPLE_RATE, reporter);
                _modelLoadFlag = true;
                UpdateLoadButtonUI();
                
                Debug.Log($"Started loading model: {modelID}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to load model: {ex.Message}");
                UnloadModel();
            }
        }

        /// <summary>
        /// 卸载语音识别模型 / Unload speech recognition model
        /// </summary>
        private void UnloadModel()
        {
            if (!IsModelLoaded)
            {
                Debug.LogWarning("No model loaded, no need to unload");
                return;
            }

            try
            {
                speechRecognition?.Dispose();
                speechRecognition = null;
                _modelLoadFlag = false;
                UpdateLoadButtonUI();
                
                Debug.Log("Model unloaded successfully");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to unload model: {ex.Message}");
            }
        }
        #endregion

        #region Recording Management
        /// <summary>
        /// 开始录音 / Start recording
        /// </summary>
        private void StartRecording()
        {
            if (!IsModelLoaded)
            {
                Debug.LogError("Please load model first");
                return;
            }

            try
            {
                Mic.Init();
                _ringBuffer = new RingBuffer<float>(SAMPLE_RATE * MAX_RECORDING_DURATION);
                
                var devices = Mic.AvailableDevices;
                if (devices.Count > 0)
                {
                    // 使用默认设备或重用现有设备 / Use default device or reuse existing device
                    if (device == null || device != devices[0])
                    {
                        device = devices[0];
                        device.OnFrameCollected += HandleAudioFrameCollected;
                    }
                    
                    device.StartRecording(SAMPLE_RATE, MIC_FRAME_LENGTH);
                    Debug.Log($"Recording started with device: {device.Name}, Sample Rate: {device.SamplingFrequency}Hz");
                }
                else
                {
                    Debug.LogError("No microphone device available");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to start recording: {ex.Message}");
            }
        }

        /// <summary>
        /// 停止录音 / Stop recording
        /// </summary>
        private void StopRecording()
        {
            if (device != null)
            {
                try
                {
                    device.OnFrameCollected -= HandleAudioFrameCollected;
                    device.StopRecording();
                    device = null;
                    
                    Debug.Log("Recording stopped");
                    _ = ProcessSpeechTranscriptionAsync();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Failed to stop recording: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 处理音频帧数据 / Handle audio frame data
        /// </summary>
        private void HandleAudioFrameCollected(int sampleRate, int channelCount, float[] pcm)
        {
            try
            {
                if (_ringBuffer != null)
                {
                    // 将音频数据添加到环形缓冲区 / Add audio data to ring buffer
                    for (int i = 0; i < pcm.Length; i++)
                    {
                        _ringBuffer.Enqueue(pcm[i]);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error in HandleAudioFrameCollected: {ex.Message}");
            }
        }
        #endregion

        #region Speech Transcription
        /// <summary>
        /// 异步处理语音转录 / Asynchronously process speech transcription
        /// </summary>
        private async Task ProcessSpeechTranscriptionAsync()
        {
            if (_ringBuffer == null || _ringBuffer.Count == 0)
            {
                Debug.LogWarning("No audio data to transcribe");
                return;
            }

            try
            {
                // 从环形缓冲区提取音频数据 / Extract audio data from ring buffer
                float[] samples = new float[_ringBuffer.Count];
                for (int i = 0; i < samples.Length; i++)
                {
                    samples[i] = _ringBuffer.Dequeue();
                }

                // 可选：保存音频文件用于调试 / Optional: Save audio file for debugging
                // SaveAudioClipForDebug(samples);

                // 执行语音转录 / Perform speech transcription
                var result = await speechRecognition.SpeechTranscriptionAsync(samples, SAMPLE_RATE);
                
                // 更新UI显示结果 / Update UI with result
                UpdateTranscriptionResult(result);
                
                Debug.Log($"Transcription completed: {result}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to process transcription: {ex.Message}");
                _transcriptionText.text = "<color=red><b>Transcription failed</b></color>";
            }
        }

        /// <summary>
        /// 保存音频剪辑用于调试 / Save audio clip for debugging
        /// </summary>
        private void SaveAudioClipForDebug(float[] samples)
        {
            try
            {
                AudioClip clip = AudioClip.Create("RecordedAudio", samples.Length, 1, SAMPLE_RATE, false);
                clip.SetData(samples, 0);
                clip.Save("RecordedAudio");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Failed to save audio clip: {ex.Message}");
            }
        }

        /// <summary>
        /// 更新转录结果显示 / Update transcription result display
        /// </summary>
        private void UpdateTranscriptionResult(string result)
        {
            if (!string.IsNullOrWhiteSpace(result))
            {
                _transcriptionText.text = result;
                lastCachedText = result;
            }
            else
            {
                _transcriptionText.text = "<i>No speech detected</i>";
            }
        }
        #endregion

        #region UI Updates
        /// <summary>
        /// 更新加载按钮UI状态 / Update load button UI state
        /// </summary>
        private void UpdateLoadButtonUI()
        {
            var buttonText = _modelLoadOrUnloadButton.GetComponentInChildren<Text>();
            var buttonImage = _modelLoadOrUnloadButton.GetComponent<Image>();

            if (_modelLoadFlag) // 模型已加载 / Model loaded
            {
                buttonText.text = "Unload Model";
                buttonImage.color = Color.red;
                _modelIDDropdown.interactable = false;
                
                // 显示加载进度UI / Show loading progress UI
                _totalInitProgressBar.gameObject.SetActive(true);
                _initMessageText.gameObject.SetActive(true);
                
                // 停止录音并隐藏录音按钮 / Stop recording and hide recording button
                StopRecording();
                _recordingBtn.gameObject.SetActive(false);
            }
            else // 模型未加载 / Model not loaded
            {
                buttonText.text = "Load Model";
                buttonImage.color = _originLoadBtnColor;
                _modelIDDropdown.interactable = true;
                
                // 隐藏加载进度UI / Hide loading progress UI
                _totalInitProgressBar.gameObject.SetActive(false);
                _initMessageText.gameObject.SetActive(false);
                
                // 清空显示内容 / Clear display content
                _transcriptionText.text = string.Empty;
                _tipsText.text = string.Empty;
                _recordingBtn.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// 更新录音按钮UI状态 / Update recording button UI state
        /// </summary>
        private void UpdateRecordingButtonUI()
        {
            var buttonText = _recordingBtn.GetComponentInChildren<Text>();
            var buttonImage = _recordingBtn.GetComponent<Image>();

            if (IsRecording) // 正在录音 / Currently recording
            {
                buttonText.text = "Stop Recording";
                buttonImage.color = Color.red;
                
                // 禁用其他控件 / Disable other controls
                _modelLoadOrUnloadButton.interactable = false;
                _modelIDDropdown.interactable = false;
                
                // 隐藏进度条 / Hide progress bar
                _totalInitProgressBar.gameObject.SetActive(false);
                _initMessageText.gameObject.SetActive(false);
                
                // 显示录音提示 / Show recording hint
                _transcriptionText.text = "<b><i>When you are done speaking, \nclick the Stop Recording button.</i></b>";
            }
            else // 未在录音 / Not recording
            {
                buttonText.text = "Start Recording";
                buttonImage.color = _originRecordingBtnColor;
                
                // 启用其他控件 / Enable other controls
                _modelLoadOrUnloadButton.interactable = true;
                _modelIDDropdown.interactable = !_modelLoadFlag;
                
                // 隐藏进度条 / Hide progress bar
                _totalInitProgressBar.gameObject.SetActive(false);
                _initMessageText.gameObject.SetActive(false);
            }
        }
        #endregion

        #region Event Handlers
        /// <summary>
        /// 处理模型加载/卸载按钮点击 / Handle model load/unload button click
        /// </summary>
        private void HandleModelLoadOrUnloadButtonClick()
        {
            if (_modelLoadFlag) // 已加载模型，执行卸载 / Model loaded, perform unload
            {
                UnloadModel();
            }
            else // 未加载模型，执行加载 / Model not loaded, perform load
            {
                var selectedModelID = _modelIDDropdown.captionText.text;
                LoadModel(selectedModelID);
            }
        }

        /// <summary>
        /// 处理录音按钮点击 / Handle recording button click
        /// </summary>
        private void HandleRecordingButtonClick()
        {
            if (IsRecording) // 正在录音，停止录音 / Currently recording, stop recording
            {
                StopRecording();
            }
            else // 未在录音，开始录音 / Not recording, start recording
            {
                StartRecording();
            }
            
            UpdateRecordingButtonUI();
        }
        #endregion

        #region Resource Management
        /// <summary>
        /// 清理资源 / Clean up resources
        /// </summary>
        private void CleanupResources()
        {
            try
            {
                // 清理音频设备 / Clean up audio device
                if (device != null)
                {
                    device.OnFrameCollected -= HandleAudioFrameCollected;
                    device.StopRecording();
                    device = null;
                }

                // 清理语音识别 / Clean up speech recognition
                speechRecognition?.Dispose();
                speechRecognition = null;

                // 清理事件监听器 / Clean up event listeners
                if (_modelLoadOrUnloadButton != null)
                {
                    _modelLoadOrUnloadButton.onClick.RemoveListener(HandleModelLoadOrUnloadButtonClick);
                }
                
                if (_recordingBtn != null)
                {
                    _recordingBtn.onClick.RemoveListener(HandleRecordingButtonClick);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error during cleanup: {ex.Message}");
            }
        }
        #endregion

        #region Feedback Handler Implementation
        /// <summary>
        /// 设置进度条可见性 / Set progress bar visibility
        /// </summary>
        private void SetProgressActive(bool isActive)
        {
            _totalInitProgressBar.gameObject.SetActive(isActive);
        }

        /// <summary>
        /// 更新总体进度 / Update overall progress
        /// </summary>
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
            UnloadModel();
        }

        public void OnFeedback(SuccessFeedback feedback)
        {
            SetProgressActive(false);
            UpdateOverallProgress(1f, "Success");
            _initMessageText.text = string.Empty;
            _transcriptionText.text = "<b><i>Please click the record button and start speaking.</i></b>";
            _tipsText.text = $"<b><color=green>[Loaded]:</color></b> {feedback.Metadata.modelId}\nYou can now test speech-to-text by speaking directly.";
            _recordingBtn.gameObject.SetActive(true);
        }

        public void OnFeedback(FailedFeedback feedback)
        {
            SetProgressActive(false);
            Debug.LogError($"[Failed] :{feedback.Message}");
            _initMessageText.text = feedback.Message;
            _tipsText.text = $"<b><color=red>[Failed]</color>:</b> \nThe model loading failed.";
            _transcriptionText.text = "<color=red><b>Model load failed</b></color>";
            UnloadModel();
        }

        public void OnFeedback(CleanFeedback feedback)
        {
            SetProgressActive(false);
            _initMessageText.text = feedback.Message;
            _transcriptionText.text = "<color=yellow><b>Init canceled</b></color>";
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// 打开GitHub仓库链接 / Open GitHub repository link
        /// </summary>
        public void OpenGithubRepo()
        {
            Application.OpenURL("https://github.com/EitanWong/com.eitan.sherpa-onnx-unity");
        }
        #endregion
    }
}