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

    public class PunctuationExample : MonoBehaviour, ISherpaFeedbackHandler
    {
        [Header("UI Components")]
        [SerializeField] private Dropdown _modelIDDropdown;
        [SerializeField] private Button _modelLoadOrUnloadButton;
        [SerializeField] private Text _initMessageText;
        [SerializeField] private Eitan.SherpaOnnxUnity.Samples.UI.EasyProgressBar _totalInitProgressBar;
        [SerializeField] private Text _totalInitBarText;
        [SerializeField] private Text _tipsText;

        [Header("Punctuation UI")]
        [SerializeField] private GameObject _punctuationUIPanel;
        [SerializeField] private InputField _inputTextField;
        [SerializeField] private Text _resultText;
        [SerializeField] private Button _addPunctuationButton;

        private Punctuation punctuation;

        private bool _modelLoadFlag;

        private Color _originLoadBtnColor;
        private readonly string defaultModelID = "sherpa-onnx-punct-ct-transformer-zh-en-vocab272727-2024-04-12";
        private ModelLoadProgressTracker _progressTracker;

        private void Start()
        {
            Application.runInBackground = true;
            _modelLoadOrUnloadButton.onClick.AddListener(HandleModelLoadOrUnloadButtonClick);
            _addPunctuationButton.onClick.AddListener(HandleAddPunctuationButtonClick);

            _tipsText.text = "Please load a punctuation model first.";
            _resultText.text = string.Empty;
            _inputTextField.text = "restoring punctuation is a neat trick isn't it a model predicts commas periods and more how does it work it learns from tons of text what a concept but can it handle questions or exclamations yes advanced models analyze context to figure it out pretty smart";

            _originLoadBtnColor = _modelLoadOrUnloadButton.GetComponent<Image>().color;

            _punctuationUIPanel.SetActive(false);

            _progressTracker = new ModelLoadProgressTracker(_totalInitProgressBar, _totalInitBarText, _initMessageText);

            _ = InitDropdownAsync();
        }

        private async Task InitDropdownAsync()
        {

            _modelIDDropdown.options.Clear();
            _modelIDDropdown.captionText.text = "Fetching model manifest from GitHub…";
            _modelLoadOrUnloadButton.gameObject.SetActive(false);
            var manifest = await SherpaOnnxModelRegistry.Instance.GetManifestAsync(SherpaOnnxModuleType.AddPunctuation);
            _modelLoadOrUnloadButton.gameObject.SetActive(true);

            _modelIDDropdown.options.Clear();
            if (manifest.models != null)
            {
                List<OptionData> modelOptions = manifest.models.Select(m => new OptionData(m.modelId)).ToList();
                _modelIDDropdown.AddOptions(modelOptions);

                var defaultIndex = modelOptions.FindIndex(m => m.text == defaultModelID);
                if (defaultIndex != -1)
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

        private void HandleModelLoadOrUnloadButtonClick()
        {
            if (_modelLoadFlag)
            {
                Unload();
            }
            else
            {
                Load(_modelIDDropdown.captionText.text);
            }
        }

        private async void HandleAddPunctuationButtonClick()
        {
            if (punctuation == null || !_modelLoadFlag)
            {
                Debug.LogWarning("Punctuation model not loaded.");
                _tipsText.text = "<color=red>Punctuation model not loaded.</color>";
                return;
            }

            if (string.IsNullOrWhiteSpace(_inputTextField.text))
            {
                _tipsText.text = "<color=yellow>Please enter some text to add punctuation.</color>";
                return;
            }

            _addPunctuationButton.interactable = false;
            _tipsText.text = "Adding punctuation...";

            try
            {
                var inputText = _inputTextField.text;
                var result = await punctuation.AddPunctuationAsync(inputText);
                _resultText.text = result;
                _tipsText.text = "<color=green>Punctuation added successfully.</color>";
            }
            catch (Exception e)
            {
                Debug.LogError($"Error adding punctuation: {e.Message}");
                _tipsText.text = $"<color=red>Error: {e.Message}</color>";
            }
            finally
            {
                _addPunctuationButton.interactable = true;
            }
        }

        private void Load(string modelID)
        {
            if (punctuation != null)
            {
                UnityEngine.Debug.LogError("Please Unload current model first");
                return;
            }

            var reporter = new SherpaOnnxFeedbackReporter(null, this);
            punctuation = new Punctuation(modelID, reporter: reporter);

            _modelLoadOrUnloadButton.interactable = false;
            _modelIDDropdown.interactable = false;
        }

        private void Unload()
        {
            if (punctuation != null)
            {
                punctuation.Dispose();
                punctuation = null;
            }

            _modelLoadFlag = false;
            _progressTracker.Reset();
            _progressTracker.SetVisible(false);
            UpdateUI();
        }

        private void OnDestroy()
        {
            if (_modelLoadOrUnloadButton != null)
            {
                _modelLoadOrUnloadButton.onClick.RemoveListener(HandleModelLoadOrUnloadButtonClick);
            }
            if (_addPunctuationButton != null)
            {
                _addPunctuationButton.onClick.RemoveListener(HandleAddPunctuationButtonClick);
            }
            Unload();
        }

        private void UpdateUI()
        {
            _punctuationUIPanel.SetActive(_modelLoadFlag);
            _modelLoadOrUnloadButton.interactable = true;

            if (_modelLoadFlag)
            {
                _modelLoadOrUnloadButton.GetComponentInChildren<Text>().text = "Unload Model";
                _modelLoadOrUnloadButton.GetComponent<Image>().color = Color.red;
                _modelIDDropdown.interactable = false;
            }
            else
            {
                _modelLoadOrUnloadButton.GetComponentInChildren<Text>().text = "Load Model";
                _modelLoadOrUnloadButton.GetComponent<Image>().color = _originLoadBtnColor;
                _modelIDDropdown.interactable = true;
                _resultText.text = string.Empty;
                _tipsText.text = "Please load a punctuation model first.";
            }
        }

        #region FeedbackHandler

        public void OnFeedback(PrepareFeedback feedback)
        {
            _progressTracker.Reset();
            _progressTracker.MarkStageComplete(ModelLoadProgressTracker.Stage.Prepare, feedback.Message);
            _tipsText.text = $"<b>[Loading]:</b> {feedback.Metadata.modelId} The punctuation model is loading, please wait patiently.";
        }

        public void OnFeedback(DownloadFeedback feedback)
        {
            _progressTracker.UpdateStage(ModelLoadProgressTracker.Stage.Download, feedback.Message, feedback.Progress);
        }

        public void OnFeedback(DecompressFeedback feedback)
        {
            _progressTracker.UpdateStage(ModelLoadProgressTracker.Stage.Decompress, feedback.Message, feedback.Progress);
        }

        public void OnFeedback(VerifyFeedback feedback)
        {
            _progressTracker.UpdateStage(ModelLoadProgressTracker.Stage.Verify, feedback.Message, feedback.Progress);
        }

        public void OnFeedback(LoadFeedback feedback)
        {
            _progressTracker.MarkStageComplete(ModelLoadProgressTracker.Stage.Load, feedback.Message);
            _tipsText.text = $"<b><color=cyan>[Loading]</color>:</b> The punctuation model {feedback.Metadata.modelId} is loading.";
        }

        public void OnFeedback(CancelFeedback feedback)
        {
            _progressTracker.Reset();
            _progressTracker.SetVisible(false);
            _tipsText.text = $"<b><color=yellow>Cancelled</color>:</b> {feedback.Metadata.modelId}{feedback.Message}";
            Unload();
        }

        public void OnFeedback(SuccessFeedback feedback)
        {
            _progressTracker.Complete("Success");
            _progressTracker.SetVisible(false);
            _tipsText.text = $"<b><color=green>[Loaded]:</color></b> {feedback.Metadata.modelId} Punctuation model is ready.";

            _modelLoadFlag = true;
            UpdateUI();
        }

        public void OnFeedback(FailedFeedback feedback)
        {
            _progressTracker.Reset();
            _progressTracker.SetVisible(false);
            Debug.LogError($"[Failed] :{feedback.Message}");
            _initMessageText.text = feedback.Message;
            _tipsText.text = $"<b><color=red>[Failed]</color>:</b> The punctuation model load failed.";
            Unload();
        }

        public void OnFeedback(CleanFeedback feedback)
        {
            _progressTracker.MarkStageComplete(ModelLoadProgressTracker.Stage.Clean, feedback.Message);
        }
        #endregion

        public void OpenGithubRepo()
        {
            Application.OpenURL("https://github.com/EitanWong/com.eitan.sherpa-onnx-unity");
        }
    }
}
