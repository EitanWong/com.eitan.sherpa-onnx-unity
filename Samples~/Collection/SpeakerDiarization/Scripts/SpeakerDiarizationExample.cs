namespace Eitan.SherpaONNXUnity.Samples
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Eitan.Sherpa.Onnx.Unity.Mono.Components;
    using Eitan.Sherpa.Onnx.Unity.Mono.Inputs;
    using Eitan.SherpaONNXUnity.Runtime;
    using Eitan.SherpaONNXUnity.Runtime.Modules;
    using UnityEngine;
    using UnityEngine.UI;

    /// <summary>
    /// Speaker diarization demo that asks the user to choose both the segmentation and embedding models,
    /// then records a short clip and shows who spoke when.
    /// </summary>
    public sealed class SpeakerDiarizationExample : MonoBehaviour
    {
        [Header("Sherpa Components")]
        [SerializeField] private SpeakerDiarizationComponent diarizationComponent;
        [SerializeField] private SherpaMicrophoneInput microphoneInput;

        [Header("UI")]
        [SerializeField] private Dropdown segmentationModelDropdown;
        [SerializeField] private Dropdown embeddingModelDropdown;
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
        [SerializeField] private string defaultSegmentationModelID = "sherpa-onnx-pyannote-segmentation-3-0";
        [SerializeField] private string defaultEmbeddingModelID = "3dspeaker_speech_campplus_sv_zh-cn_16k-common";

        private readonly List<float> recordedSamples = new List<float>(16000 * 8);
        private readonly List<Button> speakerPlaybackButtons = new List<Button>();
        private readonly List<AudioClip> generatedSpeakerClips = new List<AudioClip>();
        private ModelLoadProgressTracker progressTracker;
        private bool modelRequested;
        private bool modelReady;
        private bool isRecording;
        private Text segmentationLabelText;
        private Text embeddingLabelText;
        private RectTransform speakerPlaybackPanel;
        private RectTransform speakerPlaybackContent;
        private Text speakerPlaybackHeaderText;
        private int recordedSampleRate;
        private const float SpeakerPlaybackGapSeconds = 0.08f;
        private const float SpeakerPlaybackPanelMinWidth = 260f;
        private const float SpeakerPlaybackPanelMaxWidth = 520f;
        private const float SpeakerPlaybackPanelMinHeight = 150f;
        private const float SpeakerPlaybackPanelMaxHeight = 300f;
        private const float SpeakerPlaybackPanelWidthRatio = 0.46f;
        private const float SpeakerPlaybackPanelHeightRatio = 0.3f;
        private const float SpeakerPlaybackPanelGapRatio = 0.02f;
        private const float SpeakerPlaybackPanelMarginRatio = 0.02f;
        private const string SpeakerPlaybackPlaceholder = "Speaker reels will appear here after analysis.";

        private sealed class SpeakerClipSummary
        {
            public int Speaker;
            public int SegmentCount;
            public float DurationSeconds;
            public AudioClip Clip;
        }

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
            ClearSpeakerPlayback(false);
            _ = PopulateDropdownsAsync();

            if (statusText != null)
            {
                statusText.text = "Choose a segmentation model and an embedding model, then tap Load Models.";
            }

            if (recordingStatusText != null)
            {
                recordingStatusText.text = "This demo records a short conversation and labels each speaker turn.";
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
            ClearSpeakerPlayback(true);
            StopMicrophoneCapture();
        }

        private void EnsureComponents()
        {
            if (diarizationComponent == null)
            {
                diarizationComponent = FindSceneObject<SpeakerDiarizationComponent>();
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

        private static T FindSceneObject<T>() where T : UnityEngine.Object
        {
#if UNITY_2023_1_OR_NEWER
            return FindFirstObjectByType<T>();
#else
            return FindObjectOfType<T>();
#endif
        }

        private void EnsureSupplementalUi()
        {
            if (segmentationModelDropdown == null)
            {
                return;
            }

            if (embeddingModelDropdown == null)
            {
                var clone = Instantiate(segmentationModelDropdown.gameObject, segmentationModelDropdown.transform.parent);
                clone.name = "Dropdown (Embedding Model List)";

                embeddingModelDropdown = clone.GetComponent<Dropdown>();
                var sourceRect = segmentationModelDropdown.GetComponent<RectTransform>();
                var cloneRect = embeddingModelDropdown.GetComponent<RectTransform>();
                cloneRect.anchorMin = sourceRect.anchorMin;
                cloneRect.anchorMax = sourceRect.anchorMax;
                cloneRect.pivot = sourceRect.pivot;
                cloneRect.sizeDelta = sourceRect.sizeDelta;
                cloneRect.anchoredPosition = sourceRect.anchoredPosition + new Vector2(0f, -78f);
            }

            CreateOrUpdateDropdownLabel(segmentationModelDropdown, ref segmentationLabelText, "Segmentation Model");
            CreateOrUpdateDropdownLabel(embeddingModelDropdown, ref embeddingLabelText, "Embedding Model");
            EnsureSpeakerPlaybackUi();
        }

        private void BindComponentEvents()
        {
            if (diarizationComponent == null)
            {
                return;
            }

            diarizationComponent.DiarizationLogReadyEvent.AddListener(HandleDiarizationLogReady);
            diarizationComponent.DiarizationFailedEvent.AddListener(HandleDiarizationFailed);
            diarizationComponent.InitializationStateChangedEvent.AddListener(HandleInitializationChanged);
            diarizationComponent.FeedbackMessages.AddListener(HandleFeedbackMessage);
            diarizationComponent.FeedbackReceived += HandleFeedback;
        }

        private void UnbindComponentEvents()
        {
            if (diarizationComponent == null)
            {
                return;
            }

            diarizationComponent.DiarizationLogReadyEvent.RemoveListener(HandleDiarizationLogReady);
            diarizationComponent.DiarizationFailedEvent.RemoveListener(HandleDiarizationFailed);
            diarizationComponent.InitializationStateChangedEvent.RemoveListener(HandleInitializationChanged);
            diarizationComponent.FeedbackMessages.RemoveListener(HandleFeedbackMessage);
            diarizationComponent.FeedbackReceived -= HandleFeedback;
        }

        private async Task PopulateDropdownsAsync()
        {
            await PopulateDropdownAsync(
                segmentationModelDropdown,
                SherpaONNXModuleType.SpeakerDiarization,
                "Loading diarization models…",
                defaultSegmentationModelID).ConfigureAwait(true);

            await PopulateDropdownAsync(
                embeddingModelDropdown,
                SherpaONNXModuleType.Embedding,
                "Loading embedding models…",
                defaultEmbeddingModelID).ConfigureAwait(true);

            UpdateButtons();
        }

        private static async Task PopulateDropdownAsync(
            Dropdown dropdown,
            SherpaONNXModuleType moduleType,
            string loadingLabel,
            string defaultModelId)
        {
            if (dropdown == null)
            {
                return;
            }

            dropdown.options.Clear();
            dropdown.interactable = false;
            if (dropdown.captionText != null)
            {
                dropdown.captionText.text = loadingLabel;
            }

            var manifest = await SherpaONNXModelRegistry.Instance.GetManifestAsync(moduleType).ConfigureAwait(true);

            dropdown.options.Clear();
            if (manifest?.models == null || manifest.models.Count == 0)
            {
                dropdown.options.Add(new Dropdown.OptionData("<no models>"));
                dropdown.value = 0;
                dropdown.RefreshShownValue();
                return;
            }

            var options = manifest.models
                .Where(m => !string.IsNullOrWhiteSpace(m.modelId))
                .Select(m => new Dropdown.OptionData(m.modelId))
                .ToList();

            if (options.Count == 0)
            {
                dropdown.options.Add(new Dropdown.OptionData("<no models>"));
                dropdown.value = 0;
                dropdown.RefreshShownValue();
                return;
            }

            dropdown.AddOptions(options);
            var defaultIndex = options.FindIndex(m => m.text == defaultModelId);
            dropdown.value = defaultIndex >= 0 ? defaultIndex : 0;
            dropdown.interactable = true;
            dropdown.RefreshShownValue();
        }

        private string SelectedSegmentationModelId => GetSelectedModelId(segmentationModelDropdown);

        private string SelectedEmbeddingModelId => GetSelectedModelId(embeddingModelDropdown);

        private bool IsModelLoaded => diarizationComponent != null && diarizationComponent.IsInitialized;

        private void ToggleModel()
        {
            if (diarizationComponent == null)
            {
                SetStatus("Assign the SpeakerDiarizationComponent.");
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
            var segmentationModelId = SelectedSegmentationModelId;
            var embeddingModelId = SelectedEmbeddingModelId;

            if (string.IsNullOrWhiteSpace(segmentationModelId) || segmentationModelId.StartsWith("<"))
            {
                SetStatus("Pick a segmentation model first.");
                return;
            }

            if (string.IsNullOrWhiteSpace(embeddingModelId) || embeddingModelId.StartsWith("<"))
            {
                SetStatus("Pick an embedding model first.");
                return;
            }

            diarizationComponent.ModelId = segmentationModelId.Trim();
            diarizationComponent.EmbeddingModelId = embeddingModelId.Trim();

            if (diarizationComponent.TryLoadModule())
            {
                modelRequested = true;
                modelReady = false;
                DemoUIShared.ShowLoading(progressTracker, statusText, $"Loading {segmentationModelId} + {embeddingModelId}…");
                if (recordingStatusText != null)
                {
                    recordingStatusText.text = "Preparing diarization models…";
                }
            }
            else
            {
                SetStatus("Failed to start model loading.");
            }

            UpdateButtons();
        }

        private void UnloadModel()
        {
            modelRequested = false;
            modelReady = false;
            isRecording = false;

            StopMicrophoneCapture();
            diarizationComponent?.DisposeModule();

            if (playbackAudioSource != null)
            {
                playbackAudioSource.Stop();
                playbackAudioSource.clip = null;
            }

            recordedSamples.Clear();
            ClearSpeakerPlayback(true);
            progressTracker?.Reset();
            progressTracker?.SetVisible(false);

            if (statusText != null)
            {
                statusText.text = "Models unloaded. Choose both models and tap Load Models again.";
            }

            if (recordingStatusText != null)
            {
                recordingStatusText.text = "Ready for another setup.";
            }

            UpdateButtons();
        }

        private async void ToggleRecording()
        {
            if (!modelRequested || !modelReady)
            {
                SetStatus("Wait until both models are loaded before recording.");
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
                await StopRecordingAndAnalyzeAsync().ConfigureAwait(true);
            }
        }

        private void StartRecording()
        {
            recordedSamples.Clear();
            recordedSampleRate = microphoneInput.OutputSampleRate > 0 ? microphoneInput.OutputSampleRate : 16000;
            ClearSpeakerPlayback(true);

            StopMicrophoneCapture();
            microphoneInput.ChunkReady += HandleMicrophoneChunk;

            if (!microphoneInput.TryStartCapture())
            {
                microphoneInput.ChunkReady -= HandleMicrophoneChunk;
                SetStatus("Unable to start microphone capture.");
                return;
            }

            isRecording = true;
            SetStatus("Recording conversation… tap again to stop and diarize.");
            if (recordingStatusText != null)
            {
                recordingStatusText.text = "Try speaking with two voices or two people to see turn segmentation.";
            }

            UpdateButtons();
        }

        private async Task StopRecordingAndAnalyzeAsync()
        {
            StopMicrophoneCapture();
            isRecording = false;
            UpdateButtons();

            if (recordedSamples.Count == 0)
            {
                SetStatus("No audio was captured.");
                if (recordingStatusText != null)
                {
                    recordingStatusText.text = "Tap Record and speak for a few seconds.";
                }
                return;
            }

            var samples = recordedSamples.ToArray();
            if (playbackAudioSource != null)
            {
                playbackAudioSource.Stop();
                playbackAudioSource.clip = null;
            }

            SetStatus("Analyzing speakers…");
            if (recordingStatusText != null)
            {
                recordingStatusText.text = $"Captured {samples.Length / (float)recordedSampleRate:F1}s of audio. Running diarization…";
            }

            var segments = await diarizationComponent.DiarizeSamplesAsync(samples, recordedSampleRate).ConfigureAwait(true);
            if (segments == null || segments.Length == 0)
            {
                ClearSpeakerPlayback(true);
                UpdateSpeakerPlaybackHeader("No speaker turns were detected in this clip.");

                if (recordingStatusText != null)
                {
                    recordingStatusText.text = "Try a longer recording with clearer turn-taking between speakers.";
                }

                return;
            }

            var summaries = RenderSpeakerPlayback(samples, recordedSampleRate, segments);
            UpdateSpeakerPlaybackHeader(
                $"Detected {summaries.Count} speaker reel(s) across {segments.Length} segment(s). Tap a reel to play it.");

            var speakerCount = segments.Select(s => s.Speaker).Distinct().Count();
            if (recordingStatusText != null)
            {
                recordingStatusText.text = $"Detected {speakerCount} speaker(s) across {segments.Length} segment(s). Built {summaries.Count} speaker reel(s) for playback.";
            }

            SetStatus("Diarization complete. Review the timeline below and use the speaker playback buttons.");
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

        private void HandleDiarizationLogReady(string log)
        {
            if (speakerPlaybackButtons.Count == 0 && !string.IsNullOrWhiteSpace(log))
            {
                UpdateSpeakerPlaybackHeader("Analysis finished. Tap a speaker reel below when available.");
            }
        }

        private void HandleDiarizationFailed(string message)
        {
            ClearSpeakerPlayback(true);
            SetStatus(message);
            if (recordingStatusText != null)
            {
                recordingStatusText.text = "Check that both models are compatible and try again.";
            }
        }

        private void HandleInitializationChanged(bool ready)
        {
            modelReady = ready && modelRequested;

            if (modelRequested)
            {
                if (modelReady)
                {
                    DemoUIShared.ShowLoadingComplete(progressTracker, statusText, "Models loaded. Tap Record to capture a conversation.");
                    if (recordingStatusText != null)
                    {
                        recordingStatusText.text = "Press Record, talk for a few seconds, then tap Stop & Analyze.";
                    }
                }
                else
                {
                    DemoUIShared.ShowLoading(progressTracker, statusText, "Initializing models…");
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

        private void UpdateButtons()
        {
            if (loadOrUnloadButton != null)
            {
                var label = loadOrUnloadButton.GetComponentInChildren<Text>();
                if (label != null)
                {
                    label.text = (IsModelLoaded || modelRequested) ? "Unload Models" : "Load Models";
                }

                DemoUIShared.SetButtonColor(loadOrUnloadButton, (IsModelLoaded || modelRequested) ? DemoUIShared.UnloadColor : DemoUIShared.LoadColor);
                loadOrUnloadButton.interactable = !isRecording;
            }

            if (recordButton != null)
            {
                recordButton.gameObject.SetActive(modelReady);
                recordButton.interactable = modelReady;

                var label = recordButton.GetComponentInChildren<Text>();
                if (label != null)
                {
                    label.text = isRecording ? "Stop & Analyze" : "Record";
                }

                var color = !recordButton.interactable
                    ? DemoUIShared.DisabledColor
                    : (isRecording ? DemoUIShared.RecordStopColor : DemoUIShared.RecordIdleColor);
                DemoUIShared.SetButtonColor(recordButton, color);
            }

            if (segmentationModelDropdown != null)
            {
                segmentationModelDropdown.interactable = !modelRequested;
            }

            if (embeddingModelDropdown != null)
            {
                embeddingModelDropdown.interactable = !modelRequested;
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

        private void EnsureSpeakerPlaybackUi()
        {
            var parent = GetSpeakerPlaybackParent();
            var anchorRect = GetSpeakerPlaybackAnchorRect();
            if (parent == null || anchorRect == null)
            {
                return;
            }

            if (speakerPlaybackPanel == null)
            {
                var panelObject = new GameObject(
                    "Speaker Playback Panel",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image),
                    typeof(ScrollRect));
                panelObject.transform.SetParent(parent, false);
                panelObject.transform.SetSiblingIndex(anchorRect.transform.GetSiblingIndex() + 1);

                speakerPlaybackPanel = panelObject.GetComponent<RectTransform>();
                speakerPlaybackPanel.anchorMin = Vector2.zero;
                speakerPlaybackPanel.anchorMax = Vector2.zero;
                speakerPlaybackPanel.pivot = Vector2.zero;
                speakerPlaybackPanel.sizeDelta = new Vector2(SpeakerPlaybackPanelMinWidth, SpeakerPlaybackPanelMinHeight);

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
                speakerPlaybackContent = contentObject.GetComponent<RectTransform>();
                speakerPlaybackContent.anchorMin = new Vector2(0f, 1f);
                speakerPlaybackContent.anchorMax = new Vector2(1f, 1f);
                speakerPlaybackContent.pivot = new Vector2(0.5f, 1f);
                speakerPlaybackContent.anchoredPosition = Vector2.zero;
                speakerPlaybackContent.sizeDelta = new Vector2(0f, 0f);

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
                scrollRect.content = speakerPlaybackContent;
            }

            UpdateSpeakerPlaybackLayout();

            if (speakerPlaybackHeaderText == null && speakerPlaybackContent != null)
            {
                speakerPlaybackHeaderText = CreatePanelText("Speaker Playback Header", 18, FontStyle.Bold);
                if (speakerPlaybackHeaderText != null)
                {
                    speakerPlaybackHeaderText.text = SpeakerPlaybackPlaceholder;
                }
            }
        }

        private void ClearSpeakerPlayback(bool destroyGeneratedClips)
        {
            foreach (var button in speakerPlaybackButtons)
            {
                if (button != null)
                {
                    Destroy(button.gameObject);
                }
            }

            speakerPlaybackButtons.Clear();

            if (destroyGeneratedClips)
            {
                if (playbackAudioSource != null && generatedSpeakerClips.Contains(playbackAudioSource.clip))
                {
                    playbackAudioSource.Stop();
                    playbackAudioSource.clip = null;
                }

                foreach (var clip in generatedSpeakerClips)
                {
                    if (clip != null)
                    {
                        Destroy(clip);
                    }
                }

                generatedSpeakerClips.Clear();
            }

            if (speakerPlaybackHeaderText != null)
            {
                speakerPlaybackHeaderText.text = SpeakerPlaybackPlaceholder;
            }

            var scrollRect = speakerPlaybackPanel != null ? speakerPlaybackPanel.GetComponent<ScrollRect>() : null;
            if (scrollRect != null)
            {
                scrollRect.verticalNormalizedPosition = 1f;
            }
        }

        private void UpdateSpeakerPlaybackLayout()
        {
            var parent = GetSpeakerPlaybackParent();
            if (speakerPlaybackPanel == null || parent == null)
            {
                return;
            }

            var parentRect = parent.rect;
            var parentWidth = Mathf.Max(1f, parentRect.width);
            var parentHeight = Mathf.Max(1f, parentRect.height);
            var screenHeight = Mathf.Max(1f, (float)Screen.height);
            var unitsPerScreenPixel = parentHeight / screenHeight;
            var margin = Mathf.Clamp(Screen.height * SpeakerPlaybackPanelMarginRatio * unitsPerScreenPixel, 12f, 24f);
            var widthFromParent = parentWidth * SpeakerPlaybackPanelWidthRatio;
            var maxAllowedWidth = Mathf.Max(220f, parentWidth - (margin * 2f));
            var width = Mathf.Clamp(
                widthFromParent,
                SpeakerPlaybackPanelMinWidth,
                Mathf.Min(SpeakerPlaybackPanelMaxWidth, maxAllowedWidth));
            var targetHeight = Mathf.Clamp(
                Screen.height * SpeakerPlaybackPanelHeightRatio * unitsPerScreenPixel,
                SpeakerPlaybackPanelMinHeight,
                SpeakerPlaybackPanelMaxHeight);
            var height = Mathf.Clamp(targetHeight, SpeakerPlaybackPanelMinHeight, Mathf.Max(SpeakerPlaybackPanelMinHeight, parentHeight - (margin * 2f)));

            speakerPlaybackPanel.anchorMin = Vector2.zero;
            speakerPlaybackPanel.anchorMax = Vector2.zero;
            speakerPlaybackPanel.pivot = Vector2.zero;
            speakerPlaybackPanel.sizeDelta = new Vector2(width, height);
            speakerPlaybackPanel.anchoredPosition = new Vector2(margin, margin);
        }

        private List<SpeakerClipSummary> RenderSpeakerPlayback(
            float[] sourceSamples,
            int sampleRate,
            SpeakerDiarization.DiarizationSegment[] segments)
        {
            EnsureSpeakerPlaybackUi();
            ClearSpeakerPlayback(true);

            var summaries = BuildSpeakerClipSummaries(sourceSamples, sampleRate, segments);
            if (speakerPlaybackHeaderText != null)
            {
                speakerPlaybackHeaderText.text = summaries.Count == 0
                    ? SpeakerPlaybackPlaceholder
                    : $"Speaker Reels ({summaries.Count})";
            }

            foreach (var summary in summaries)
            {
                CreateSpeakerPlaybackButton(summary);
            }

            return summaries;
        }

        private List<SpeakerClipSummary> BuildSpeakerClipSummaries(
            float[] sourceSamples,
            int sampleRate,
            SpeakerDiarization.DiarizationSegment[] segments)
        {
            var summaries = new List<SpeakerClipSummary>();
            if (sourceSamples == null || sourceSamples.Length == 0 || sampleRate <= 0 || segments == null || segments.Length == 0)
            {
                return summaries;
            }

            foreach (var group in segments.GroupBy(segment => segment.Speaker).OrderBy(group => group.Key))
            {
                var orderedSegments = group.OrderBy(segment => segment.Start).ToArray();
                var speakerSamples = BuildSpeakerClipSamples(sourceSamples, sampleRate, orderedSegments);
                if (speakerSamples.Length == 0)
                {
                    continue;
                }

                var clip = CreateClip($"Speaker_{group.Key}_Reel", speakerSamples, sampleRate);
                generatedSpeakerClips.Add(clip);
                summaries.Add(new SpeakerClipSummary
                {
                    Speaker = group.Key,
                    SegmentCount = orderedSegments.Length,
                    DurationSeconds = speakerSamples.Length / (float)sampleRate,
                    Clip = clip,
                });
            }

            return summaries;
        }

        private static float[] BuildSpeakerClipSamples(
            float[] sourceSamples,
            int sampleRate,
            SpeakerDiarization.DiarizationSegment[] orderedSegments)
        {
            if (sourceSamples == null || sourceSamples.Length == 0 || sampleRate <= 0 || orderedSegments == null || orderedSegments.Length == 0)
            {
                return Array.Empty<float>();
            }

            var gapLength = Mathf.Max(0, Mathf.RoundToInt(sampleRate * SpeakerPlaybackGapSeconds));
            var gapSamples = gapLength > 0 ? new float[gapLength] : null;
            var combined = new List<float>();

            for (var i = 0; i < orderedSegments.Length; i++)
            {
                var segment = orderedSegments[i];
                var startIndex = Mathf.Clamp(Mathf.FloorToInt(segment.Start * sampleRate), 0, sourceSamples.Length);
                var endIndex = Mathf.Clamp(Mathf.CeilToInt(segment.End * sampleRate), startIndex, sourceSamples.Length);
                var sampleCount = endIndex - startIndex;
                if (sampleCount <= 0)
                {
                    continue;
                }

                var chunk = new float[sampleCount];
                Array.Copy(sourceSamples, startIndex, chunk, 0, sampleCount);
                combined.AddRange(chunk);

                if (gapSamples != null && i < orderedSegments.Length - 1)
                {
                    combined.AddRange(gapSamples);
                }
            }

            return combined.ToArray();
        }

        private void CreateSpeakerPlaybackButton(SpeakerClipSummary summary)
        {
            if (summary == null || summary.Clip == null || speakerPlaybackContent == null)
            {
                return;
            }

            var buttonObject = new GameObject(
                $"Button (Speaker {summary.Speaker})",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button),
                typeof(LayoutElement));
            buttonObject.transform.SetParent(speakerPlaybackContent, false);

            var button = buttonObject.GetComponent<Button>();
            var image = buttonObject.GetComponent<Image>();
            image.color = Color.Lerp(DemoUIShared.RecordIdleColor, Color.white, 0.15f);
            button.targetGraphic = image;
            button.onClick.AddListener(() => PlaySpeakerSummary(summary));

            var layout = buttonObject.GetComponent<LayoutElement>();
            layout.preferredHeight = 76f;
            layout.minHeight = 76f;

            var label = CreatePanelText(
                $"Label (Speaker {summary.Speaker})",
                16,
                FontStyle.Normal,
                buttonObject.transform);
            if (label != null)
            {
                var labelRect = label.rectTransform;
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.offsetMin = Vector2.zero;
                labelRect.offsetMax = Vector2.zero;
                label.alignment = TextAnchor.MiddleLeft;
                label.text =
                    $"Speaker {summary.Speaker}\n" +
                    $"{summary.DurationSeconds:F1}s combined • {summary.SegmentCount} turns\n" +
                    "Tap to play this clustered reel";
            }

            speakerPlaybackButtons.Add(button);
        }

        private Text CreatePanelText(string objectName, int fontSize, FontStyle fontStyle, Transform parentOverride = null)
        {
            var parent = parentOverride != null ? parentOverride : speakerPlaybackContent;
            if (parent == null)
            {
                return null;
            }

            var textObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(LayoutElement));
            textObject.transform.SetParent(parent, false);

            var text = textObject.GetComponent<Text>();
            var fontSource = statusText != null ? statusText : recordingStatusText;
            text.font = fontSource != null ? fontSource.font : Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontStyle = fontStyle;
            text.fontSize = fontSize;
            text.color = Color.white;
            text.alignment = TextAnchor.UpperLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;

            var layout = textObject.GetComponent<LayoutElement>();
            layout.minHeight = fontSize + 8f;
            layout.preferredHeight = fontSize + 10f;

            return text;
        }

        private RectTransform GetSpeakerPlaybackAnchorRect()
        {
            if (recordingStatusText != null)
            {
                return recordingStatusText.rectTransform;
            }

            if (statusText != null)
            {
                return statusText.rectTransform;
            }

            if (embeddingModelDropdown != null)
            {
                return embeddingModelDropdown.GetComponent<RectTransform>();
            }

            if (segmentationModelDropdown != null)
            {
                return segmentationModelDropdown.GetComponent<RectTransform>();
            }

            return null;
        }

        private RectTransform GetSpeakerPlaybackParent()
        {
            var anchorRect = GetSpeakerPlaybackAnchorRect();
            return anchorRect != null ? anchorRect.parent as RectTransform : null;
        }

        private static void AddProtectedRect(List<Rect> protectedRects, RectTransform parent, RectTransform target)
        {
            if (protectedRects == null || parent == null || target == null || !target.gameObject.activeInHierarchy)
            {
                return;
            }

            var corners = new Vector3[4];
            target.GetWorldCorners(corners);
            var min = (Vector2)parent.InverseTransformPoint(corners[0]);
            var max = (Vector2)parent.InverseTransformPoint(corners[2]);
            protectedRects.Add(Rect.MinMaxRect(min.x, min.y, max.x, max.y));
        }

        private static float ScorePanelRect(
            Rect panelRect,
            List<Rect> protectedRects,
            Vector2 anchorBottomLeft,
            Vector2 anchorTopRight,
            float gap)
        {
            var score = 0f;
            if (protectedRects != null)
            {
                foreach (var protectedRect in protectedRects)
                {
                    if (!panelRect.Overlaps(protectedRect))
                    {
                        continue;
                    }

                    var overlapWidth = Mathf.Min(panelRect.xMax, protectedRect.xMax) - Mathf.Max(panelRect.xMin, protectedRect.xMin);
                    var overlapHeight = Mathf.Min(panelRect.yMax, protectedRect.yMax) - Mathf.Max(panelRect.yMin, protectedRect.yMin);
                    score += Mathf.Max(0f, overlapWidth) * Mathf.Max(0f, overlapHeight) * 1000f;
                }
            }

            var anchorArea = Rect.MinMaxRect(anchorBottomLeft.x, anchorBottomLeft.y, anchorTopRight.x, anchorTopRight.y);
            if (panelRect.Overlaps(anchorArea))
            {
                score += 100000f;
            }

            var centerDistance = Vector2.Distance(panelRect.center, anchorArea.center);
            score += centerDistance * 0.1f;

            var verticalGap = panelRect.yMin > anchorArea.yMax
                ? panelRect.yMin - anchorArea.yMax
                : (anchorArea.yMin > panelRect.yMax ? anchorArea.yMin - panelRect.yMax : 0f);
            if (verticalGap < gap)
            {
                score += (gap - verticalGap) * 100f;
            }

            return score;
        }

        private void UpdateSpeakerPlaybackHeader(string message)
        {
            EnsureSpeakerPlaybackUi();
            if (speakerPlaybackHeaderText != null)
            {
                speakerPlaybackHeaderText.text = string.IsNullOrWhiteSpace(message)
                    ? SpeakerPlaybackPlaceholder
                    : message;
            }
        }

        private void PlaySpeakerSummary(SpeakerClipSummary summary)
        {
            if (summary == null || summary.Clip == null || playbackAudioSource == null)
            {
                return;
            }

            playbackAudioSource.Stop();
            playbackAudioSource.clip = summary.Clip;
            playbackAudioSource.Play();

            SetStatus($"Playing Speaker {summary.Speaker} reel.");
            if (recordingStatusText != null)
            {
                recordingStatusText.text = $"Speaker {summary.Speaker}: {summary.DurationSeconds:F1}s combined from {summary.SegmentCount} turns.";
            }
        }

        private static AudioClip CreateClip(float[] samples, int sampleRate)
        {
            return CreateClip("SpeakerDiarizationRecording", samples, sampleRate);
        }

        private static AudioClip CreateClip(string clipName, float[] samples, int sampleRate)
        {
            var clip = AudioClip.Create(clipName, samples.Length, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static Text FindTextByName(string objectName)
        {
            var go = GameObject.Find(objectName);
            return go != null ? go.GetComponent<Text>() : null;
        }

        public void OpenGithubRepo()
        {
            Application.OpenURL("https://github.com/EitanWong/com.eitan.sherpa-onnx-unity");
        }
    }
}
