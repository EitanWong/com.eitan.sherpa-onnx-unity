namespace Eitan.SherpaONNXUnity.Samples
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Eitan.Sherpa.Onnx.Unity.Mono.Components;
    using Eitan.Sherpa.Onnx.Unity.Mono.Inputs;
    using Eitan.SherpaONNXUnity.Runtime;
    using UnityEngine;
    using UnityEngine.UI;
    using Stage = Eitan.SherpaONNXUnity.Samples.ModelLoadProgressTracker.Stage;

    /// <summary>
    /// Demo showing recording + optional enhancement with clear UI and progress.
    /// 录音并可选降噪增强的示例，包含进度和颜色提示。
    /// </summary>
    public sealed class SpeechEnhancementExample : MonoBehaviour
    {
        [Header("Sherpa Components")]
        [SerializeField] private SpeechEnhancementComponent enhancementComponent;
        [SerializeField] private SherpaMicrophoneInput microphoneInput;

        [Header("UI")]
        [SerializeField] private Dropdown modelDropdown;
        [SerializeField] private Button loadOrUnloadButton;
        [SerializeField] private Button recordButton;
        [SerializeField] private Toggle enhanceToggle;
        [SerializeField] private Text statusText;
        [SerializeField] private AudioSource playbackAudioSource;

        [Header("Loading UI / Progress")]
        [SerializeField] private UI.EasyProgressBar progressBar;
        [SerializeField] private Text progressValueText;
        [SerializeField] private Text progressMessageText;

        [Header("Defaults")]
        [SerializeField] private string defaultModelID = "gtcrn-simple";

        private bool modelRequested;
        private bool isRecording;
        private bool isProcessingClip;
        private AudioClip rawClip;
        private AudioClip enhancedClip;
        private readonly List<float> recordedSamples = new List<float>();
        private int recordedSampleRate;
        private ModelLoadProgressTracker progressTracker;

        private bool UseEnhancement => enhanceToggle != null && enhanceToggle.isOn;

        private void Awake()
        {
            if (loadOrUnloadButton != null)
            {
                loadOrUnloadButton.onClick.AddListener(ToggleModel);
            }

            if (recordButton != null)
            {
                recordButton.onClick.AddListener(ToggleRecording);
            }

            if (enhanceToggle != null)
            {
                enhanceToggle.onValueChanged.AddListener(HandleEnhanceToggleChanged);
            }

            progressTracker = new ModelLoadProgressTracker(
                progressBar,
                progressValueText,
                progressMessageText != null ? progressMessageText : statusText);
            progressTracker.SetVisible(false);

            if (enhancementComponent != null)
            {
                enhancementComponent.ClipReadyEvent.AddListener(HandleClipReady);
                enhancementComponent.ErrorEvent.AddListener(HandleError);
                enhancementComponent.InitializationStateChangedEvent.AddListener(HandleInitializationChanged);
            }


            if (playbackAudioSource == null)
            {
                playbackAudioSource = GetComponent<AudioSource>();
                if (!playbackAudioSource)
                {
                    playbackAudioSource = gameObject.AddComponent<AudioSource>();
                }
            }

            playbackAudioSource.playOnAwake = false;
            playbackAudioSource.loop = false;
        }

        private void OnEnable()
        {
            _ = PopulateDropdownAsync();
            statusText.text = "Load a model to begin.";
            UpdateButtonLabels();
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

            if (enhanceToggle != null)
            {
                enhanceToggle.onValueChanged.RemoveListener(HandleEnhanceToggleChanged);
            }

            if (enhancementComponent != null)
            {
                enhancementComponent.ClipReadyEvent.RemoveListener(HandleClipReady);
                enhancementComponent.ErrorEvent.RemoveListener(HandleError);
                enhancementComponent.InitializationStateChangedEvent.RemoveListener(HandleInitializationChanged);
            }

            StopListeningToMicrophone();
            DisposeCachedClips();
        }

        private async Task PopulateDropdownAsync()
        {
            if (modelDropdown == null)
            {
                return;
            }

            modelDropdown.options.Clear();
            modelDropdown.captionText.text = "Loading enhancer models…";
            modelDropdown.interactable = false;

            var manifest = await SherpaONNXModelRegistry.Instance
                .GetManifestAsync(SherpaONNXModuleType.SpeechEnhancement)
                .ConfigureAwait(true);
            if (manifest.models == null || manifest.models.Count == 0)
            {
                modelDropdown.options.Add(new Dropdown.OptionData("<no models>"));
                return;
            }

            var options = manifest.models.ConvertAll(m => new Dropdown.OptionData(m.modelId));
            modelDropdown.AddOptions(options);
            var defaultIndex = options.FindIndex(m => m.text == defaultModelID);
            modelDropdown.value = defaultIndex >= 0 ? defaultIndex : 0;
            modelDropdown.interactable = options.Count > 0;
        }

        private string SelectedModelId =>
            modelDropdown != null && modelDropdown.options.Count > 0
                ? modelDropdown.options[modelDropdown.value].text
                : string.Empty;

        private bool IsModelLoaded => enhancementComponent != null && enhancementComponent.IsInitialized;

        private void ToggleModel()
        {
            if (enhancementComponent == null)
            {
                statusText.text = "Assign the SpeechEnhancementComponent.";
                return;
            }

            if (!modelRequested)
            {
                var modelId = SelectedModelId;
                if (string.IsNullOrWhiteSpace(modelId))
                {
                    statusText.text = "Select a model first.";
                    return;
                }

                enhancementComponent.ModelId = modelId.Trim();
                if (enhancementComponent.TryLoadModule())
                {
                    modelRequested = true;
                    statusText.text = $"Loading model {modelId}…";
                    BeginLoading(statusText.text);
                }
            }
            else
            {
                enhancementComponent.DisposeModule();
                modelRequested = false;
                statusText.text = "Model disposed.";
                progressTracker?.Reset();
                progressTracker?.SetVisible(false);
            }

            UpdateButtonLabels();
        }

        private void UpdateButtonLabels()
        {
            if (loadOrUnloadButton != null)
            {
                var label = loadOrUnloadButton.GetComponentInChildren<Text>();
                if (label != null)
                {
                    label.text = modelRequested ? "Unload Model" : "Load Model";
                }

                DemoUIShared.SetButtonColor(loadOrUnloadButton, modelRequested ? DemoUIShared.UnloadColor : DemoUIShared.LoadColor);
            }

            if (recordButton != null)
            {
                var label = recordButton.GetComponentInChildren<Text>();
                if (label != null)
                {
                    label.text = isRecording ? "Stop & Play" : "Record";
                }

                recordButton.interactable = IsModelLoaded;
                var color = !recordButton.interactable ? DemoUIShared.DisabledColor : (isRecording ? DemoUIShared.RecordStopColor : DemoUIShared.RecordIdleColor);
                DemoUIShared.SetButtonColor(recordButton, color);
                recordButton.gameObject.SetActive(IsModelLoaded);
            }

            if (modelDropdown != null)
            {
                modelDropdown.interactable = !modelRequested;
            }
        }

        private async void ToggleRecording()
        {
            if (enhancementComponent == null)
            {
                statusText.text = "Assign the SpeechEnhancementComponent.";
                return;
            }

            if (microphoneInput == null)
            {
                statusText.text = "Assign a SherpaMicrophoneInput.";
                return;
            }

            if (!IsModelLoaded)
            {
                statusText.text = "Load a model first.";
                return;
            }

            if (!isRecording)
            {
                StartMicrophoneRecording();
            }
            else
            {
                await FinalizeMicrophoneRecordingAsync();
            }
        }

        private void StartMicrophoneRecording()
        {
            DisposeCachedClips();
            recordedSamples.Clear();
            recordedSampleRate = microphoneInput != null && microphoneInput.OutputSampleRate > 0
                ? microphoneInput.OutputSampleRate
                : 16000;

            StopListeningToMicrophone();
            microphoneInput.ChunkReady += HandleMicrophoneChunk;

            if (playbackAudioSource != null)
            {
                playbackAudioSource.Stop();
            }

            if (!microphoneInput.TryStartCapture())
            {
                microphoneInput.ChunkReady -= HandleMicrophoneChunk;
                statusText.text = "Unable to start microphone capture.";
                return;
            }

            isRecording = true;
            statusText.text = "Recording microphone… click again to stop.";
            UpdateButtonLabels();
        }

        private async Task FinalizeMicrophoneRecordingAsync()
        {
            statusText.text = "Finalizing recording…";

            if (recordButton != null)
            {
                recordButton.interactable = false;
            }

            StopListeningToMicrophone();
            isRecording = false;
            UpdateButtonLabels();

            if (recordedSamples.Count == 0)
            {
                statusText.text = "No audio was captured.";
                if (recordButton != null)
                {
                    recordButton.interactable = IsModelLoaded;
                }
                return;
            }

            var sampleArray = recordedSamples.ToArray();
            rawClip = CreateClip(sampleArray, recordedSampleRate);
            enhancedClip = null;

            if (UseEnhancement)
            {
                isProcessingClip = true;
                statusText.text = "Enhancing microphone recording…";
                var buffer = new float[sampleArray.Length];
                Array.Copy(sampleArray, buffer, sampleArray.Length);
                enhancedClip = await enhancementComponent.EnhanceSamplesAsync(buffer, recordedSampleRate, applyToPlayback: false);
                isProcessingClip = false;
            }

            PlayCurrentClip();

            if (recordButton != null)
            {
                recordButton.interactable = IsModelLoaded;
            }
        }

        private void HandleMicrophoneChunk(float[] samples, int sampleRate)
        {
            if (!isRecording || samples == null || samples.Length == 0)
            {
                return;
            }

            recordedSampleRate = sampleRate;
            recordedSamples.AddRange(samples);
        }

        private void StopListeningToMicrophone()
        {
            if (microphoneInput == null)
            {
                return;
            }

            microphoneInput.ChunkReady -= HandleMicrophoneChunk;
            microphoneInput.StopCapture();
        }

        private static AudioClip CreateClip(float[] samples, int sampleRate)
        {
            var clip = AudioClip.Create("mic_recording", samples.Length, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private async void HandleEnhanceToggleChanged(bool enabled)
        {
            if (enhancementComponent == null)
            {
                return;
            }

            if (isRecording || rawClip == null || isProcessingClip)
            {
                return;
            }

            if (enabled && enhancedClip == null)
            {
                if (!IsModelLoaded)
                {
                    statusText.text = "Load a model to enhance the last recording.";
                    if (enhanceToggle != null)
                    {
                        enhanceToggle.isOn = false;
                    }
                    return;
                }

                isProcessingClip = true;
                statusText.text = "Enhancing microphone recording…";
                enhancedClip = await enhancementComponent.EnhanceClipAsync(rawClip, applyToPlayback: false);
                isProcessingClip = false;
            }

            PlayCurrentClip();
        }

        private void PlayCurrentClip()
        {
            var useEnhanced = UseEnhancement && enhancedClip != null;
            var clipToPlay = useEnhanced ? enhancedClip : rawClip;

            if (clipToPlay == null)
            {
                statusText.text = "No clip ready for playback.";
                return;
            }

            statusText.text = useEnhanced ? "Playing enhanced audio." : "Playing raw audio.";

            if (playbackAudioSource != null)
            {
                playbackAudioSource.Stop();
                playbackAudioSource.clip = clipToPlay;
                playbackAudioSource.Play();
            }
            else
            {
                AudioSource.PlayClipAtPoint(clipToPlay, Vector3.zero);
            }
        }

        private void DisposeCachedClips()
        {
            if (rawClip != null)
            {
                Destroy(rawClip);
                rawClip = null;
            }

            if (enhancedClip != null)
            {
                Destroy(enhancedClip);
                enhancedClip = null;
            }
        }

        private void HandleClipReady(AudioClip clip)
        {
            if (clip == null)
            {
                statusText.text = "Enhancement finished but no clip returned.";
                return;
            }

            if (isProcessingClip)
            {
                return;
            }

            statusText.text = $"Recording captured ({clip.length:F1}s).";
        }

        private void HandleError(string message)
        {
            statusText.text = message;
        }

        private void HandleInitializationChanged(bool ready)
        {
            if (ready)
            {
                CompleteLoading("Model ready. Press Record to capture and enhance.");
            }
            else if (modelRequested)
            {
                BeginLoading("Loading model…");
            }

            UpdateButtonLabels();
        }

        private void BeginLoading(string message)
        {
            DemoUIShared.ShowLoading(progressTracker, statusText, message);
        }

        private void CompleteLoading(string message)
        {
            DemoUIShared.ShowLoadingComplete(progressTracker, statusText, message);
        }

        public void OpenGithubRepo()
        {
            Application.OpenURL("https://github.com/EitanWong/com.eitan.sherpa-onnx-unity");
        }
    }
}
