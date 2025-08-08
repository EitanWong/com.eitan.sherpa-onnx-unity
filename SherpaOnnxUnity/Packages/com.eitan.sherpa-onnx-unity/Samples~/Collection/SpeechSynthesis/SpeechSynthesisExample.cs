
namespace Eitan.SherpaOnnxUnity.Samples
{
    using System.Collections.Generic;
    using System.Linq;

    using System.Threading.Tasks;
    using Eitan.SherpaOnnxUnity.Runtime;

    using UnityEngine;
    using UnityEngine.UI;
    using static UnityEngine.UI.Dropdown;

    [RequireComponent(typeof(AudioSource))]
    public class SpeechSynthesisExample : MonoBehaviour, ISherpaFeedbackHandler
    {
        // [SerializeField] private string _onlineModelID = "sherpa-onnx-streaming-zipformer-bilingual-zh-en-2023-02-20";
        [Header("UI Components")]
        [SerializeField] private Dropdown _modelIDDropdown;
        [SerializeField] private Button _modelLoadOrUnloadButton;
        [SerializeField] private Text _initMessageText;
        [SerializeField] private Eitan.SherpaOnnxUnity.Samples.UI.EasyProgressBar _totalInitProgressBar;
        [SerializeField] private Text _totalInitBarText;
        [SerializeField] private Text _tipsText;
        [SerializeField] private Text _speechSynthesisStatusText;

        // here is the components for TTS demo needed
        [Header("TTS Components")]
        [SerializeField] private RectTransform _containerRectTransform;
        [SerializeField] private InputField _voiceInputField;
        [SerializeField] private Slider _speedSlider;
        [SerializeField] private Text _speedValueText;
        [SerializeField] private InputField _contentInputField;
        [SerializeField] private Button _generateButton;
        private SpeechSynthesis TTS;

        private Color _originLoadBtnColor;
        private readonly string defaultModelID = "vits-melo-tts-zh_en";
        
        private AudioSource audioSource;
        private bool _isGenerating = false;
        private AudioClip _lastGeneratedClip;

        /// <summary>
        /// True if the TTS model is loaded.
        /// </summary>
        private bool IsModelLoaded => TTS != null;

        #region Unity Lifecycle Methods
        private void Start()
        {
            audioSource = GetComponent<AudioSource>();
            Application.runInBackground = true;
            Application.targetFrameRate = 30;
            _modelLoadOrUnloadButton.onClick.AddListener(HandleModelLoadOrUnloadButtonClick);
            _generateButton.onClick.AddListener(HandleGenerateButtonClick);
            _speedSlider.onValueChanged.AddListener(OnSpeedSliderChanged);
            _totalInitProgressBar.gameObject.SetActive(false);
            _initMessageText.gameObject.SetActive(false);
            _tipsText.text = "Load a model to begin.";
            _speechSynthesisStatusText.text = "TTS Status: Model not loaded\nPlease select a model to load.";
            _originLoadBtnColor = _modelLoadOrUnloadButton.GetComponent<Image>().color;
            InitDropdown();
            UpdateLoadButtonUI();
            SetOperationContainerDisplay(false);
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
            if (_generateButton != null)
            {
                _generateButton.onClick.RemoveListener(HandleGenerateButtonClick);
            }
            if( _speedSlider != null)
            {
                _speedSlider.onValueChanged.RemoveListener(OnSpeedSliderChanged);
            }
        }
        #endregion

        #region Model and Recording Control
        /// <summary>
        /// Loads the TTS model with the specified ID and prepares for recording.
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
            TTS = new SpeechSynthesis(modelID,  reporter: reporter);
            
            UpdateLoadButtonUI();

        }

        /// <summary>
        /// Unloads the current TTS model and releases all related resources.
        /// </summary>
        private void Unload()
        {
            if (!IsModelLoaded)
            {
                Debug.LogWarning("No model is loaded, no need to unload.");
                return;
            }

            Cleanup();
            _speechSynthesisStatusText.text = "TTS Status: Model not loaded\nPlease select a model to load.";
            _tipsText.text = "Load a model to begin.";
            UpdateLoadButtonUI();
            SetOperationContainerDisplay(false); 
        }

        /// <summary>
        /// A unified resource cleanup method to release all occupied resources.
        /// </summary>
        private void Cleanup()
        {
            // Reset playback state
            if (audioSource != null && audioSource.isPlaying)
            {
                audioSource.Stop();
            }
            // accumulatedSpeech.Clear();
            if (TTS != null)
            {
                TTS?.Dispose();
                TTS=null;
            }
        }
        #endregion

        #region UI and Initialization

        private void InitDropdown()
        {
            var manifest = SherpaOnnxModelRegistry.Instance.GetManifest();

            _modelIDDropdown.options.Clear();
            if (manifest.models != null)
            {
                System.Collections.Generic.List<OptionData> modelOptions = manifest.Filter(m => (m.moduleType == SherpaOnnxModuleType.SpeechSynthesis)).Select(m => new OptionData(m.modelId)).ToList();
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

        private void SetOperationContainerDisplay(bool isVisible)
        {
            _containerRectTransform.gameObject.SetActive(isVisible);
            if (isVisible)
            {
                _tipsText.text = "Model loaded. Enter text and click Generate.";
            }
            else
            {
                _tipsText.text = "Load a model to begin.";
            }
        }

        private async void HandleGenerateButtonClick()
        {
            await GenerateSpeechAsync(_contentInputField.text, int.Parse(_voiceInputField.text), _speedSlider.value);
        }


        #endregion

        #region TTS Core Logic
        private async Task PlayAndResumeAsync(AudioClip clip)
        {

            audioSource.clip = clip;
            audioSource.Play();
            _speechSynthesisStatusText.text = $"Playing: <color=blue>{clip.length:F2}s</color>";
            _tipsText.text = "Playing generated speech.";
            // Debug.Log($"Playing back {clip.length:F2} seconds of audio.");

            // Wait for playback to complete.
            while (audioSource.isPlaying)
            {
                await Task.Yield();
            }


            // Reset the flag to allow audio processing to resume.
            if (IsModelLoaded)
            {
                _speechSynthesisStatusText.text = "TTS Status: <color=green>Ready</color>";
                _tipsText.text = "Playback finished. Enter text and click Generate.";
            }
        }

        private async Task GenerateSpeechAsync(string text, int voiceID, float speed)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                _tipsText.text = "Please enter text to generate speech.";
                return;
            }

            if (voiceID < 0)
            {
                _tipsText.text = "Please enter a valid number of voiceID.";
                return;
            }

            if (!IsModelLoaded)
            {
                _tipsText.text = "No model loaded. Please load a model first.";
                return;
            }

            _speechSynthesisStatusText.text = "TTS Status: <color=yellow>Generating...</color>";
            _tipsText.text = "Generating speech, please wait...";

            var generatedAudio = await TTS.GenerateAsync(text, voiceID, speed);
            // UnityEngine.Debug.Log(generatedAudio);
            if (generatedAudio == null)
            {
                _tipsText.text = "Failed to generate speech. Please check the input text and model.";
                _speechSynthesisStatusText.text = "TTS Status: <color=red>Generated Failed</color>";
                return;
            }
            await PlayAndResumeAsync(generatedAudio);
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
            _speechSynthesisStatusText.text = "TTS Status: <color=green>Ready</color>";
            _tipsText.text = $"Model {feedback.Metadata.modelId} loaded. Enter text and click Generate.";
            SetOperationContainerDisplay(true);
            InitializeDefaultValues();

        }

        public void OnFeedback(FailedFeedback feedback)
        {
            SetProgressActive(false);
            Debug.LogError($"[Failed] :{feedback.Message}");
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
        
        #region Enhanced UX Methods
        
        private void InitializeDefaultValues()
        {
            // Set helpful defaults
            if (_voiceInputField != null)
            {
                _voiceInputField.text = "0";
                _voiceInputField.placeholder.GetComponent<Text>().text = "Voice ID (0, 1, 2...)";
            }
            
            if (_speedSlider != null)
            {
                _speedSlider.value = 1f;
                _speedSlider.minValue = 0.5f;
                _speedSlider.maxValue = 2.0f;
            }
            
            if (_contentInputField != null)
            {
                _contentInputField.placeholder.GetComponent<Text>().text = "Enter text to synthesize speech...";
            }
        }
        
        
        
        private void OnSpeedSliderChanged(float value)
        {
            // Show speed value in tips when changing
            if (!_isGenerating && IsModelLoaded)
            {
                // _tipsText.text = $"<b>🎛 Speed Adjusted:</b> {value:F1}x\n• 0.5x = Slower speech\n• 1.0x = Normal speed\n• 2.0x = Faster speech";
                _speedValueText.text = $"Speed {value:F2}x：";
            }
        }
        
        private bool ValidateInputs()
        {
            if (string.IsNullOrWhiteSpace(_contentInputField.text))
            {
                _tipsText.text = "<b><color=orange>⚠ Missing Text</color></b>\nPlease enter text to generate speech.";
                _speechSynthesisStatusText.text = "<color=orange>⚠ Input Required</color>";
                return false;
            }
            
            if (!int.TryParse(_voiceInputField.text, out int voiceID) || voiceID < 0)
            {
                _tipsText.text = "<b><color=orange>⚠ Invalid Voice ID</color></b>\nPlease enter a valid voice ID number.\n\n<b>Common Values:</b> 0, 1, 2, 3...";
                _speechSynthesisStatusText.text = "<color=orange>⚠ Invalid Voice ID</color>";
                return false;
            }
            
            if (!IsModelLoaded)
            {
                _tipsText.text = "<b><color=red>❌ No Model Loaded</color></b>\nPlease load a TTS model first.\n\n<b>Steps:</b>\n1. Select model from dropdown\n2. Click 'Load Model'\n3. Wait for loading to complete";
                _speechSynthesisStatusText.text = "<color=red>❌ Model Required</color>";
                return false;
            }
            
            if (_isGenerating)
            {
                _tipsText.text = "<b><color=yellow>⏳ Generation in Progress</color></b>\nPlease wait for current generation to complete.";
                return false;
            }
            
            return true;
        }
        
        private void UpdateGenerateButtonState()
        {
            if (_generateButton != null)
            {
                bool canGenerate = IsModelLoaded && !_isGenerating;
                _generateButton.interactable = canGenerate;
                
                var buttonText = _generateButton.GetComponentInChildren<Text>();
                if (buttonText != null)
                {
                    if (_isGenerating)
                    {
                        buttonText.text = "Generating...";
                    }
                    else if (!IsModelLoaded)
                    {
                        buttonText.text = "Load Model First";
                    }
                    else
                    {
                        buttonText.text = "Generate Speech";
                    }
                }
            }
        }
        #endregion

        public void OpenGithubRepo()
        {
            Application.OpenURL("https://github.com/EitanWong/com.eitan.sherpa-onnx-unity");
            _tipsText.text = "<b>🔗 Opening GitHub Repository</b>\nOpening project page in your default browser...";
        }
    }

}