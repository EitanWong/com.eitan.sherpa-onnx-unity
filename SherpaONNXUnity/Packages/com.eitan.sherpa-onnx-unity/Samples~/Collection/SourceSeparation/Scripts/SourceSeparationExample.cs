namespace Eitan.SherpaONNXUnity.Samples
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Eitan.Sherpa.Onnx.Unity.Mono.Components;
    using Eitan.Sherpa.Onnx.Unity.Mono.Inputs;
    using Eitan.SherpaONNXUnity.Runtime;
    using UnityEngine;
    using UnityEngine.UI;

    /// <summary>
    /// Source separation demo that records from the microphone, separates the captured clip,
    /// and exposes each returned stem as an individual playback button.
    /// </summary>
    public sealed class SourceSeparationExample : MonoBehaviour
    {
        [Header("Sherpa Components")]
        [SerializeField] private SourceSeparationComponent sourceSeparationComponent;
        [SerializeField] private SherpaMicrophoneInput microphoneInput;

        [Header("UI")]
        [SerializeField] private Dropdown modelDropdown;
        [SerializeField] private Button loadOrUnloadButton;
        [SerializeField] private Button recordButton;
        [SerializeField] private Text statusText;
        [SerializeField] private Text recordingStatusText;
        [SerializeField] private AudioSource playbackAudioSource;

        [Header("Loading UI / Progress")]
        [SerializeField] private UI.EasyProgressBar progressBar;
        [SerializeField] private Text progressValueText;
        [SerializeField] private Text progressMessageText;

        [Header("Defaults")]
        [SerializeField] private string defaultModelID = "sherpa-onnx-spleeter-2stems-int8";

        private readonly List<float> recordedSamples = new List<float>(16000 * 10);
        private readonly List<Button> stemPlaybackButtons = new List<Button>();
        private readonly List<AudioClip> generatedStemClips = new List<AudioClip>();

        private ModelLoadProgressTracker progressTracker;
        private bool modelRequested;
        private bool modelReady;
        private bool isRecording;
        private bool isSeparating;
        private int recordedSampleRate;
        private Text dropdownLabelText;
        private RectTransform stemPlaybackPanel;
        private RectTransform stemPlaybackContent;
        private Text stemPlaybackHeaderText;

        private const float StemPlaybackPanelMinWidth = 260f;
        private const float StemPlaybackPanelMaxWidth = 520f;
        private const float StemPlaybackPanelMinHeight = 150f;
        private const float StemPlaybackPanelMaxHeight = 300f;
        private const float StemPlaybackPanelWidthRatio = 0.46f;
        private const float StemPlaybackPanelHeightRatio = 0.3f;
        private const float StemPlaybackPanelMarginRatio = 0.02f;
        private const float StemPlaybackPanelBottomLiftRatio = 0.08f;
        private const string StemPlaybackPlaceholder = "Separated stems will appear here after analysis.";

        private void Awake()
        {
            EnsureComponents();
            EnsureSupplementalUi();

            if (loadOrUnloadButton != null)
            {
                loadOrUnloadButton.onClick.AddListener(ToggleModel);
            }

            if (recordButton != null)
            {
                recordButton.onClick.AddListener(ToggleRecording);
            }

            progressTracker = new ModelLoadProgressTracker(
                progressBar,
                progressValueText,
                progressMessageText != null ? progressMessageText : statusText);
            progressTracker.SetVisible(false);

            BindComponentEvents();
        }

        private void OnEnable()
        {
            EnsureSupplementalUi();
            ClearStemPlayback(true);
            _ = PopulateDropdownAsync();

            SetStatus("Choose a source separation model, then tap Load Model.");

            if (recordingStatusText != null)
            {
                recordingStatusText.text = "Record a mixed signal, then split it into separate stems.";
            }

            UpdateButtons();
        }

        private void OnDestroy()
        {
            if (loadOrUnloadButton != null)
            {
                loadOrUnloadButton.onClick.RemoveListener(ToggleModel);
            }

            if (recordButton != null)
            {
                recordButton.onClick.RemoveListener(ToggleRecording);
            }

            UnbindComponentEvents();
            StopMicrophoneCapture();
            ClearStemPlayback(true);
        }

        private void EnsureComponents()
        {
            if (sourceSeparationComponent == null)
            {
                sourceSeparationComponent = FindSceneObject<SourceSeparationComponent>();
            }

            if (microphoneInput == null)
            {
                microphoneInput = FindSceneObject<SherpaMicrophoneInput>();
            }

            if (playbackAudioSource == null)
            {
                playbackAudioSource = GetComponent<AudioSource>();
            }

            if (recordingStatusText == null)
            {
                recordingStatusText = FindTextByName("Text (RecordingStatus)");
            }

            if (statusText == null)
            {
                statusText = FindTextByName("Text (Status)");
            }

            if (playbackAudioSource != null)
            {
                playbackAudioSource.playOnAwake = false;
                playbackAudioSource.loop = false;
            }
        }

        private void EnsureSupplementalUi()
        {
            CreateOrUpdateDropdownLabel(modelDropdown, ref dropdownLabelText, "Source Separation Model");
            EnsureStemPlaybackUi();
        }

        private void BindComponentEvents()
        {
            if (sourceSeparationComponent == null)
            {
                return;
            }

            sourceSeparationComponent.InitializationStateChangedEvent.AddListener(HandleInitializationChanged);
            sourceSeparationComponent.FeedbackMessages.AddListener(HandleFeedbackMessage);
            sourceSeparationComponent.FeedbackReceived += HandleFeedback;
            sourceSeparationComponent.SeparationReadyEvent.AddListener(HandleSeparationReady);
            sourceSeparationComponent.ErrorEvent.AddListener(HandleError);
        }

        private void UnbindComponentEvents()
        {
            if (sourceSeparationComponent == null)
            {
                return;
            }

            sourceSeparationComponent.InitializationStateChangedEvent.RemoveListener(HandleInitializationChanged);
            sourceSeparationComponent.FeedbackMessages.RemoveListener(HandleFeedbackMessage);
            sourceSeparationComponent.FeedbackReceived -= HandleFeedback;
            sourceSeparationComponent.SeparationReadyEvent.RemoveListener(HandleSeparationReady);
            sourceSeparationComponent.ErrorEvent.RemoveListener(HandleError);
        }

        private async Task PopulateDropdownAsync()
        {
            if (modelDropdown == null)
            {
                return;
            }

            modelDropdown.options.Clear();
            modelDropdown.interactable = false;
            if (modelDropdown.captionText != null)
            {
                modelDropdown.captionText.text = "Loading source separation models...";
            }

            var manifest = await SherpaONNXModelRegistry.Instance
                .GetManifestAsync(SherpaONNXModuleType.SourceSeparation)
                .ConfigureAwait(true);

            modelDropdown.options.Clear();
            if (manifest?.models == null || manifest.models.Count == 0)
            {
                modelDropdown.options.Add(new Dropdown.OptionData("<no models>"));
                modelDropdown.value = 0;
                modelDropdown.RefreshShownValue();
                return;
            }

            var options = manifest.models
                .Where(model => !string.IsNullOrWhiteSpace(model.modelId))
                .Select(model => new Dropdown.OptionData(model.modelId))
                .ToList();

            if (options.Count == 0)
            {
                modelDropdown.options.Add(new Dropdown.OptionData("<no models>"));
                modelDropdown.value = 0;
                modelDropdown.RefreshShownValue();
                return;
            }

            modelDropdown.AddOptions(options);
            var defaultIndex = options.FindIndex(option => option.text == defaultModelID);
            modelDropdown.value = defaultIndex >= 0 ? defaultIndex : 0;
            modelDropdown.interactable = true;
            modelDropdown.RefreshShownValue();
        }

        private string SelectedModelId => GetSelectedModelId(modelDropdown);

        private bool IsModelLoaded => sourceSeparationComponent != null && sourceSeparationComponent.IsInitialized;

        private void ToggleModel()
        {
            if (sourceSeparationComponent == null)
            {
                SetStatus("Assign the SourceSeparationComponent.");
                return;
            }

            if (IsModelLoaded || modelRequested)
            {
                UnloadModel();
            }
            else
            {
                LoadModel();
            }
        }

        private void LoadModel()
        {
            var modelId = SelectedModelId;
            if (string.IsNullOrWhiteSpace(modelId) || modelId.StartsWith("<", StringComparison.Ordinal))
            {
                SetStatus("Pick a source separation model first.");
                return;
            }

            sourceSeparationComponent.ModelId = modelId.Trim();
            if (!sourceSeparationComponent.TryLoadModule())
            {
                SetStatus("Failed to start model loading.");
                return;
            }

            modelRequested = true;
            modelReady = false;
            ClearStemPlayback(true);
            DemoUIShared.ShowLoading(progressTracker, statusText, $"Loading {modelId}...");

            if (recordingStatusText != null)
            {
                recordingStatusText.text = "Preparing source separation model...";
            }

            UpdateButtons();
        }

        private void UnloadModel()
        {
            modelRequested = false;
            modelReady = false;
            isRecording = false;

            StopMicrophoneCapture();
            sourceSeparationComponent?.DisposeModule();
            progressTracker?.Reset();
            progressTracker?.SetVisible(false);

            if (playbackAudioSource != null)
            {
                playbackAudioSource.Stop();
                playbackAudioSource.clip = null;
            }

            recordedSamples.Clear();
            ClearStemPlayback(true);

            SetStatus("Model unloaded. Choose another model and tap Load Model.");
            if (recordingStatusText != null)
            {
                recordingStatusText.text = "Ready for another recording.";
            }

            UpdateButtons();
        }

        private async void ToggleRecording()
        {
            if (!modelRequested || !modelReady)
            {
                SetStatus("Wait until the model is loaded before recording.");
                return;
            }

            if (microphoneInput == null)
            {
                SetStatus("Assign a SherpaMicrophoneInput.");
                return;
            }

            if (!isRecording)
            {
                StartRecording();
            }
            else
            {
                await StopRecordingAndSeparateAsync().ConfigureAwait(true);
            }
        }

        private void StartRecording()
        {
            recordedSamples.Clear();
            recordedSampleRate = microphoneInput.OutputSampleRate > 0 ? microphoneInput.OutputSampleRate : 16000;
            ClearStemPlayback(true);

            if (playbackAudioSource != null)
            {
                playbackAudioSource.Stop();
                playbackAudioSource.clip = null;
            }

            StopMicrophoneCapture();
            microphoneInput.ChunkReady += HandleMicrophoneChunk;

            if (!microphoneInput.TryStartCapture())
            {
                microphoneInput.ChunkReady -= HandleMicrophoneChunk;
                SetStatus("Unable to start microphone capture.");
                return;
            }

            isRecording = true;
            SetStatus("Recording mixed audio... tap again to stop and separate.");
            if (recordingStatusText != null)
            {
                recordingStatusText.text = "Try voice plus music, or two overlapping sources, for a clearer demo.";
            }

            UpdateButtons();
        }

        private async Task StopRecordingAndSeparateAsync()
        {
            StopMicrophoneCapture();
            isRecording = false;
            isSeparating = true;
            UpdateButtons();

            try
            {
                if (recordedSamples.Count == 0)
                {
                    SetStatus("No audio was captured.");
                    if (recordingStatusText != null)
                    {
                        recordingStatusText.text = "Tap Record and speak or play audio for a few seconds.";
                    }
                    return;
                }

                var samples = recordedSamples.ToArray();
                SherpaLog.Info(
                    $"[SourceSeparationExample] Submitting recorded buffer: samples={samples.Length}, channels=1, sampleRate={recordedSampleRate}Hz.",
                    category: "SourceSeparation");
                SetStatus("Separating recorded audio...");
                if (recordingStatusText != null)
                {
                    recordingStatusText.text = $"Captured {samples.Length / (float)Mathf.Max(1, recordedSampleRate):F1}s of audio. Running source separation...";
                }

                var clipSet = await sourceSeparationComponent
                    .SeparateSamplesAsync(samples, 1, recordedSampleRate, applyToPlayback: false)
                    .ConfigureAwait(true);

                if (clipSet == null || clipSet.stems == null || clipSet.stems.Length == 0)
                {
                    ClearStemPlayback(true);
                    UpdateStemPlaybackHeader("No stems were produced for this recording.");
                    if (recordingStatusText != null)
                    {
                        recordingStatusText.text = "Try a longer clip or a different source separation model.";
                    }
                    return;
                }
            }
            finally
            {
                isSeparating = false;
                UpdateButtons();
            }
        }

        private void HandleMicrophoneChunk(float[] samples, int sampleRate)
        {
            if (!isRecording || samples == null || samples.Length == 0)
            {
                return;
            }

            if (sampleRate > 0)
            {
                recordedSampleRate = sampleRate;
            }

            recordedSamples.AddRange(samples);
        }

        private void HandleInitializationChanged(bool ready)
        {
            modelReady = ready && modelRequested;

            if (modelRequested)
            {
                if (modelReady)
                {
                    DemoUIShared.ShowLoadingComplete(progressTracker, statusText, "Model ready. Tap Record to capture mixed audio.");
                    if (recordingStatusText != null)
                    {
                        recordingStatusText.text = "Press Record, capture a mixture, then tap Stop & Separate.";
                    }
                }
                else
                {
                    DemoUIShared.ShowLoading(progressTracker, statusText, "Initializing model...");
                }
            }

            UpdateButtons();
        }

        private void HandleFeedbackMessage(string message)
        {
            if (progressMessageText != null)
            {
                progressMessageText.text = message;
            }
        }

        private void HandleFeedback(SherpaFeedback feedback)
        {
            DemoUIShared.UpdateProgressFromFeedback(progressTracker, progressMessageText != null ? progressMessageText : statusText, feedback);
        }

        private void HandleSeparationReady(SourceSeparationComponent.SeparatedClipSet clipSet)
        {
            if (clipSet == null || clipSet.stems == null || clipSet.stems.Length == 0)
            {
                return;
            }

            RenderStemPlayback(clipSet);
        }

        private void HandleError(string message)
        {
            SetStatus(message);
            if (recordingStatusText != null && !string.IsNullOrWhiteSpace(message))
            {
                recordingStatusText.text = "Check the selected model and try another recording.";
            }
        }

        private void RenderStemPlayback(SourceSeparationComponent.SeparatedClipSet clipSet)
        {
            EnsureStemPlaybackUi();
            ClearStemPlayback(true);

            if (clipSet?.stems == null || clipSet.stems.Length == 0)
            {
                UpdateStemPlaybackHeader("No stems were returned.");
                return;
            }

            foreach (var stem in clipSet.stems.Where(stem => stem != null && stem.clip != null))
            {
                generatedStemClips.Add(stem.clip);
            }

            var validStems = clipSet.stems.Where(stem => stem != null && stem.clip != null).ToArray();
            if (validStems.Length == 0)
            {
                UpdateStemPlaybackHeader("No playable stems were returned.");
                return;
            }

            UpdateStemPlaybackHeader($"Separated {validStems.Length} stem(s). Tap one to preview.");
            for (int i = 0; i < validStems.Length; i++)
            {
                CreateStemPlaybackButton(validStems[i], i);
            }

            if (recordingStatusText != null)
            {
                recordingStatusText.text = $"Model type: {clipSet.modelType}. Output sample rate: {clipSet.sampleRate} Hz.";
            }

            SetStatus("Source separation complete. Preview each stem below.");
        }

        private void UpdateButtons()
        {
            if (loadOrUnloadButton != null)
            {
                var label = loadOrUnloadButton.GetComponentInChildren<Text>();
                if (label != null)
                {
                    label.text = (IsModelLoaded || modelRequested) ? "Unload Model" : "Load Model";
                }

                DemoUIShared.SetButtonColor(loadOrUnloadButton, (IsModelLoaded || modelRequested) ? DemoUIShared.UnloadColor : DemoUIShared.LoadColor);
                loadOrUnloadButton.interactable = !isRecording && !isSeparating;
            }

            if (recordButton != null)
            {
                bool showRecordButton = modelReady && !isSeparating;
                recordButton.gameObject.SetActive(showRecordButton);
                recordButton.interactable = showRecordButton;

                var label = recordButton.GetComponentInChildren<Text>();
                if (label != null)
                {
                    label.text = isRecording ? "Stop & Separate" : "Record";
                }

                var color = !recordButton.interactable
                    ? DemoUIShared.DisabledColor
                    : (isRecording ? DemoUIShared.RecordStopColor : DemoUIShared.RecordIdleColor);
                DemoUIShared.SetButtonColor(recordButton, color);
            }

            if (modelDropdown != null)
            {
                modelDropdown.interactable = !modelRequested;
            }
        }

        private void StopMicrophoneCapture()
        {
            if (microphoneInput == null)
            {
                return;
            }

            microphoneInput.StopCapture();
            microphoneInput.ChunkReady -= HandleMicrophoneChunk;
        }

        private void CreateOrUpdateDropdownLabel(Dropdown dropdown, ref Text labelText, string label)
        {
            if (dropdown == null)
            {
                return;
            }

            if (labelText == null)
            {
                var labelObject = new GameObject($"{dropdown.gameObject.name} Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                labelObject.transform.SetParent(dropdown.transform.parent, false);
                labelText = labelObject.GetComponent<Text>();

                var fontSource = statusText != null ? statusText : dropdown.captionText;
                labelText.font = fontSource != null ? fontSource.font : Resources.GetBuiltinResource<Font>("Arial.ttf");
                labelText.fontStyle = FontStyle.Bold;
                labelText.fontSize = 18;
                labelText.color = Color.white;
                labelText.alignment = TextAnchor.MiddleLeft;
                labelText.raycastTarget = false;
            }

            labelText.text = label;

            var dropdownRect = dropdown.GetComponent<RectTransform>();
            var labelRect = labelText.rectTransform;
            labelRect.anchorMin = dropdownRect.anchorMin;
            labelRect.anchorMax = dropdownRect.anchorMax;
            labelRect.pivot = dropdownRect.pivot;
            labelRect.sizeDelta = new Vector2(dropdownRect.sizeDelta.x, 24f);
            labelRect.anchoredPosition = dropdownRect.anchoredPosition + new Vector2(0f, 42f);
        }

        private void EnsureStemPlaybackUi()
        {
            var parent = GetStemPlaybackParent();
            var anchorRect = GetStemPlaybackAnchorRect();
            if (parent == null || anchorRect == null)
            {
                return;
            }

            if (stemPlaybackPanel == null)
            {
                var panelObject = new GameObject(
                    "Stem Playback Panel",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image),
                    typeof(ScrollRect));
                panelObject.transform.SetParent(parent, false);
                panelObject.transform.SetSiblingIndex(anchorRect.transform.GetSiblingIndex() + 1);

                stemPlaybackPanel = panelObject.GetComponent<RectTransform>();
                stemPlaybackPanel.anchorMin = Vector2.zero;
                stemPlaybackPanel.anchorMax = Vector2.zero;
                stemPlaybackPanel.pivot = Vector2.zero;
                stemPlaybackPanel.sizeDelta = new Vector2(StemPlaybackPanelMinWidth, StemPlaybackPanelMinHeight);

                var panelImage = panelObject.GetComponent<Image>();
                panelImage.color = new Color(0.05f, 0.08f, 0.12f, 0.45f);

                var scrollRect = panelObject.GetComponent<ScrollRect>();
                scrollRect.horizontal = false;
                scrollRect.vertical = true;
                scrollRect.movementType = ScrollRect.MovementType.Clamped;
                scrollRect.scrollSensitivity = 24f;

                var viewportObject = new GameObject(
                    "Viewport",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image),
                    typeof(Mask));
                viewportObject.transform.SetParent(panelObject.transform, false);
                var viewportRect = viewportObject.GetComponent<RectTransform>();
                viewportRect.anchorMin = Vector2.zero;
                viewportRect.anchorMax = Vector2.one;
                viewportRect.offsetMin = new Vector2(12f, 12f);
                viewportRect.offsetMax = new Vector2(-12f, -12f);
                var viewportImage = viewportObject.GetComponent<Image>();
                viewportImage.color = new Color(1f, 1f, 1f, 0.02f);
                viewportObject.GetComponent<Mask>().showMaskGraphic = false;

                var contentObject = new GameObject(
                    "Content",
                    typeof(RectTransform),
                    typeof(VerticalLayoutGroup),
                    typeof(ContentSizeFitter));
                contentObject.transform.SetParent(viewportObject.transform, false);
                stemPlaybackContent = contentObject.GetComponent<RectTransform>();
                stemPlaybackContent.anchorMin = new Vector2(0f, 1f);
                stemPlaybackContent.anchorMax = new Vector2(1f, 1f);
                stemPlaybackContent.pivot = new Vector2(0.5f, 1f);
                stemPlaybackContent.anchoredPosition = Vector2.zero;
                stemPlaybackContent.sizeDelta = Vector2.zero;

                var layout = contentObject.GetComponent<VerticalLayoutGroup>();
                layout.childAlignment = TextAnchor.UpperLeft;
                layout.childControlWidth = true;
                layout.childControlHeight = true;
                layout.childForceExpandWidth = true;
                layout.childForceExpandHeight = false;
                layout.spacing = 10f;
                layout.padding = new RectOffset(4, 4, 4, 4);

                var fitter = contentObject.GetComponent<ContentSizeFitter>();
                fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                scrollRect.viewport = viewportRect;
                scrollRect.content = stemPlaybackContent;
            }

            UpdateStemPlaybackLayout();

            if (stemPlaybackHeaderText == null && stemPlaybackContent != null)
            {
                stemPlaybackHeaderText = CreatePanelText("Stem Playback Header", 18, FontStyle.Bold);
                if (stemPlaybackHeaderText != null)
                {
                    stemPlaybackHeaderText.text = StemPlaybackPlaceholder;
                }
            }
        }

        private void ClearStemPlayback(bool destroyGeneratedClips)
        {
            foreach (var button in stemPlaybackButtons)
            {
                if (button != null)
                {
                    Destroy(button.gameObject);
                }
            }

            stemPlaybackButtons.Clear();

            if (destroyGeneratedClips)
            {
                if (playbackAudioSource != null && generatedStemClips.Contains(playbackAudioSource.clip))
                {
                    playbackAudioSource.Stop();
                    playbackAudioSource.clip = null;
                }

                foreach (var clip in generatedStemClips)
                {
                    if (clip != null)
                    {
                        Destroy(clip);
                    }
                }

                generatedStemClips.Clear();
            }

            UpdateStemPlaybackHeader(StemPlaybackPlaceholder);

            var scrollRect = stemPlaybackPanel != null ? stemPlaybackPanel.GetComponent<ScrollRect>() : null;
            if (scrollRect != null)
            {
                scrollRect.verticalNormalizedPosition = 1f;
            }
        }

        private void UpdateStemPlaybackLayout()
        {
            var parent = GetStemPlaybackParent();
            if (stemPlaybackPanel == null || parent == null)
            {
                return;
            }

            var parentRect = parent.rect;
            var parentWidth = Mathf.Max(1f, parentRect.width);
            var parentHeight = Mathf.Max(1f, parentRect.height);
            var screenHeight = Mathf.Max(1f, (float)Screen.height);
            var unitsPerScreenPixel = parentHeight / screenHeight;
            var margin = Mathf.Clamp(Screen.height * StemPlaybackPanelMarginRatio * unitsPerScreenPixel, 12f, 24f);
            var widthFromParent = parentWidth * StemPlaybackPanelWidthRatio;
            var maxAllowedWidth = Mathf.Max(220f, parentWidth - (margin * 2f));
            var width = Mathf.Clamp(widthFromParent, StemPlaybackPanelMinWidth, Mathf.Min(StemPlaybackPanelMaxWidth, maxAllowedWidth));
            var targetHeight = Mathf.Clamp(
                Screen.height * StemPlaybackPanelHeightRatio * unitsPerScreenPixel,
                StemPlaybackPanelMinHeight,
                StemPlaybackPanelMaxHeight);
            var height = Mathf.Clamp(targetHeight, StemPlaybackPanelMinHeight, Mathf.Max(StemPlaybackPanelMinHeight, parentHeight - (margin * 2f)));
            var bottomLift = Mathf.Clamp(Screen.height * StemPlaybackPanelBottomLiftRatio * unitsPerScreenPixel, 36f, 96f);

            stemPlaybackPanel.anchorMin = new Vector2(1f, 0f);
            stemPlaybackPanel.anchorMax = new Vector2(1f, 0f);
            stemPlaybackPanel.pivot = new Vector2(1f, 0f);
            stemPlaybackPanel.sizeDelta = new Vector2(width, height);
            stemPlaybackPanel.anchoredPosition = new Vector2(-margin, margin + bottomLift);
        }

        private void CreateStemPlaybackButton(SourceSeparationComponent.SeparatedStemClip stem, int index)
        {
            if (stemPlaybackContent == null || stem == null || stem.clip == null)
            {
                return;
            }

            var buttonObject = new GameObject(
                $"Stem Playback Button {index}",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button),
                typeof(LayoutElement));
            buttonObject.transform.SetParent(stemPlaybackContent, false);

            var image = buttonObject.GetComponent<Image>();
            image.color = Color.Lerp(DemoUIShared.RecordIdleColor, Color.white, 0.15f);

            var layout = buttonObject.GetComponent<LayoutElement>();
            layout.minHeight = 56f;
            layout.preferredHeight = 64f;

            var button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;

            var labelObject = new GameObject(
                "Label",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Text));
            labelObject.transform.SetParent(buttonObject.transform, false);

            var label = labelObject.GetComponent<Text>();
            var fontSource = statusText != null ? statusText : recordingStatusText;
            label.font = fontSource != null ? fontSource.font : Resources.GetBuiltinResource<Font>("Arial.ttf");
            label.fontStyle = FontStyle.Bold;
            label.fontSize = 18;
            label.color = Color.white;
            label.alignment = TextAnchor.MiddleLeft;
            label.raycastTarget = false;

            var labelRect = label.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(16f, 8f);
            labelRect.offsetMax = new Vector2(-16f, -8f);

            var stemName = string.IsNullOrWhiteSpace(stem.stemName) ? $"Stem {index + 1}" : stem.stemName;
            label.text = $"{stemName}  |  {stem.clip.length:F1}s  |  {stem.channels}ch @ {stem.sampleRate}Hz";

            button.onClick.AddListener(() => PlayStem(stem, stemName));
            stemPlaybackButtons.Add(button);
        }

        private void PlayStem(SourceSeparationComponent.SeparatedStemClip stem, string stemName)
        {
            if (stem == null || stem.clip == null)
            {
                SetStatus("Selected stem is not available.");
                return;
            }

            if (playbackAudioSource == null)
            {
                SetStatus("Assign an AudioSource for playback.");
                return;
            }

            playbackAudioSource.Stop();
            playbackAudioSource.clip = stem.clip;
            playbackAudioSource.Play();

            SetStatus($"Playing stem: {stemName}.");
        }

        private Text CreatePanelText(string objectName, int fontSize, FontStyle fontStyle)
        {
            if (stemPlaybackContent == null)
            {
                return null;
            }

            var textObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(LayoutElement));
            textObject.transform.SetParent(stemPlaybackContent, false);

            var layout = textObject.GetComponent<LayoutElement>();
            layout.minHeight = 30f;
            layout.preferredHeight = 36f;

            var text = textObject.GetComponent<Text>();
            var fontSource = statusText != null ? statusText : recordingStatusText;
            text.font = fontSource != null ? fontSource.font : Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontStyle = fontStyle;
            text.fontSize = fontSize;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleLeft;
            text.raycastTarget = false;

            return text;
        }

        private void UpdateStemPlaybackHeader(string message)
        {
            if (stemPlaybackHeaderText != null)
            {
                stemPlaybackHeaderText.text = message;
            }
        }

        private RectTransform GetStemPlaybackParent()
        {
            var anchor = GetStemPlaybackAnchorRect();
            if (anchor != null && anchor.parent is RectTransform rectParent)
            {
                return rectParent;
            }

            return modelDropdown != null ? modelDropdown.transform.parent as RectTransform : null;
        }

        private RectTransform GetStemPlaybackAnchorRect()
        {
            if (recordButton != null)
            {
                return recordButton.GetComponent<RectTransform>();
            }

            if (loadOrUnloadButton != null)
            {
                return loadOrUnloadButton.GetComponent<RectTransform>();
            }

            if (modelDropdown != null)
            {
                return modelDropdown.GetComponent<RectTransform>();
            }

            return null;
        }

        private void SetStatus(string message)
        {
            if (statusText != null)
            {
                statusText.text = message;
            }
        }

        private static string GetSelectedModelId(Dropdown dropdown)
        {
            if (dropdown == null || dropdown.options == null || dropdown.options.Count == 0)
            {
                return string.Empty;
            }

            var index = Mathf.Clamp(dropdown.value, 0, dropdown.options.Count - 1);
            return dropdown.options[index]?.text ?? string.Empty;
        }

        private static T FindSceneObject<T>() where T : UnityEngine.Object
        {
#if UNITY_2023_1_OR_NEWER
            return FindAnyObjectByType<T>();
#else
            return FindObjectOfType<T>();
#endif
        }

        private static Text FindTextByName(string objectName)
        {
#if UNITY_2023_1_OR_NEWER
            var texts = FindObjectsByType<Text>(FindObjectsSortMode.None);
#else
            var texts = FindObjectsOfType<Text>();
#endif
            foreach (var text in texts)
            {
                if (text != null && text.name == objectName)
                {
                    return text;
                }
            }

            return null;
        }

        public void OpenGithubRepo()
        {
            Application.OpenURL("https://github.com/EitanWong/com.eitan.sherpa-onnx-unity");
        }
    }
}
