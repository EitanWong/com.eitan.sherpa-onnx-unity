// Runtime: Packages/com.eitan.sherpa-onnx-unity/Runtime/Mono/Components/ZeroShotSpeechSynthesisComponent.cs

namespace Eitan.Sherpa.Onnx.Unity.Mono.Components
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Eitan.SherpaONNXUnity.Runtime;
    using Eitan.SherpaONNXUnity.Runtime.Modules;
    using UnityEngine;
    using UnityEngine.Events;

    /// <summary>
    /// High-level MonoBehaviour wrapper for zero-shot speech synthesis.
    /// </summary>
    [AddComponentMenu("SherpaONNX/Speech Synthesis/Zero-Shot Speech Synthesizer")]
    [DisallowMultipleComponent]
    public sealed class ZeroShotSpeechSynthesisComponent : SherpaModuleComponent<SpeechSynthesis>
    {
        [Header("Output")]
        [SerializeField]
        [Tooltip("Optional AudioSource used to play synthesized audio automatically.")]
        private AudioSource outputAudioSource;

        [SerializeField]
        [Tooltip("Start playback automatically after synthesis completes.")]
        private bool autoplay = true;

        [Header("Zero-Shot Defaults")]
        [SerializeField]
        [Tooltip("Fallback prompt text used when GenerateWithDefaults is called.")]
        private string defaultPromptText = string.Empty;

        [SerializeField]
        [Tooltip("Fallback prompt audio clip for zero-shot voice cloning.")]
        private AudioClip defaultPromptClip;

        [SerializeField]
        [Tooltip("Speech speed multiplier applied to zero-shot generation.")]
        [Range(0.5f, 2.5f)]
        private float speechRate = 1f;

        [SerializeField]
        [Tooltip("Diffusion/iterative refinement steps used by the generator.")]
        [Min(1)]
        private int generationSteps = 4;

        [Header("Concurrency")]
        [SerializeField]
        [Tooltip("Reject new requests while a generation is in progress instead of silently ignoring them.")]
        private bool rejectConcurrentRequests = true;

        [Header("Events")]
        [SerializeField]
        private UnityEvent onGenerationStarted = new UnityEvent();

        [SerializeField]
        private UnityEvent<AudioClip> onClipReady = new UnityEvent<AudioClip>();

        [SerializeField]
        private UnityEvent<string> onGenerationFailed = new UnityEvent<string>();

        private CancellationTokenSource sharedCts;
        private CancellationTokenSource activeGenerationCts;
        private bool isGenerating;

        /// <summary>Invoked when a synthesis request starts.</summary>
        public UnityEvent GenerationStartedEvent => onGenerationStarted;

        /// <summary>Invoked when a synthesized AudioClip is ready.</summary>
        public UnityEvent<AudioClip> ClipReadyEvent => onClipReady;

        /// <summary>Invoked when synthesis fails.</summary>
        public UnityEvent<string> GenerationFailedEvent => onGenerationFailed;

        /// <summary>AudioSource used for playback. Can be set at runtime.</summary>
        public AudioSource OutputAudioSource
        {
            get => outputAudioSource;
            set => outputAudioSource = value;
        }

        /// <summary>Whether generated clips should play automatically.</summary>
        public bool Autoplay
        {
            get => autoplay;
            set => autoplay = value;
        }

        /// <summary>Default speech rate applied when no override is provided.</summary>
        public float SpeechRate
        {
            get => speechRate;
            set => speechRate = Mathf.Clamp(value, 0.5f, 2.5f);
        }

        /// <summary>Default generation steps applied when no override is provided.</summary>
        public int GenerationSteps
        {
            get => generationSteps;
            set => generationSteps = Mathf.Max(1, value);
        }

        protected override SpeechSynthesis CreateModule(string resolvedModelId, int resolvedSampleRate, SherpaONNXFeedbackReporter resolvedReporter)
        {
            return new SpeechSynthesis(resolvedModelId, resolvedSampleRate, resolvedReporter);
        }

        protected override void Awake()
        {
            base.Awake();
            sharedCts = new CancellationTokenSource();
        }

        private void OnEnable()
        {
            if (!Application.isPlaying && SampleRate != -1)
            {
                SetSampleRateForInspector(-1);
            }

            generationSteps = Mathf.Max(1, generationSteps);
            speechRate = Mathf.Clamp(speechRate, 0.5f, 2.5f);
        }

        protected override void OnDestroy()
        {
            CancelActiveGeneration();
            sharedCts?.Cancel();
            sharedCts?.Dispose();
            base.OnDestroy();
        }

        private void Reset()
        {
            SetSampleRateForInspector(-1);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!Application.isPlaying && SampleRate != -1)
            {
                SetSampleRateForInspector(-1);
            }

            generationSteps = Mathf.Max(1, generationSteps);
            speechRate = Mathf.Clamp(speechRate, 0.5f, 2.5f);
        }
#endif

        /// <summary>
        /// Launch zero-shot synthesis with the provided prompt.
        /// </summary>
        public void Generate(string text, string promptText, AudioClip promptClip)
        {
            if (isGenerating)
            {
                if (rejectConcurrentRequests)
                {
                    NotifyBusy();
                    return;
                }
            }
            _ = GenerateAsync(text, promptText, promptClip);
        }

        /// <summary>
        /// Launch zero-shot synthesis using the configured default prompt.
        /// </summary>
        public void GenerateWithDefaults(string text)
        {
            if (isGenerating)
            {
                if (rejectConcurrentRequests)
                {
                    NotifyBusy();
                    return;
                }
            }
            _ = GenerateAsync(text, defaultPromptText, defaultPromptClip);
        }

        /// <summary>
        /// Performs zero-shot synthesis asynchronously.
        /// </summary>
        public async Task<AudioClip> GenerateAsync(
            string text,
            string promptText,
            AudioClip promptClip,
            float? speedOverride = null,
            int? stepsOverride = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                ReportFailure("Input text is empty.");
                return null;
            }

            if (string.IsNullOrWhiteSpace(promptText))
            {
                ReportFailure("Prompt text is required for zero-shot synthesis.");
                return null;
            }

            if (promptClip == null)
            {
                ReportFailure("Prompt AudioClip is missing.");
                return null;
            }

            if (!EnsureModuleReady(out var module))
            {
                return null;
            }

            CancelActiveGeneration();

            var linked = CancellationTokenSource.CreateLinkedTokenSource(sharedCts?.Token ?? CancellationToken.None, cancellationToken);
            activeGenerationCts = linked;
            isGenerating = true;

            onGenerationStarted?.Invoke();

            try
            {
                var clip = await module.GenerateZeroShotAsync(
                    text.Trim(),
                    promptText.Trim(),
                    promptClip,
                    speedOverride ?? speechRate,
                    stepsOverride ?? generationSteps,
                    linked.Token).ConfigureAwait(false);

                if (clip != null)
                {
                    DispatchToUnity(() => HandleClipReady(clip));
                }
                else
                {
                    ReportFailure("Zero-shot synthesis returned no audio.");
                }

                return clip;
            }
            catch (OperationCanceledException)
            {
                return null;
            }
            catch (Exception ex)
            {
                ReportFailure($"Generation failed: {ex.Message}");
                return null;
            }
            finally
            {
                CleanupActiveGenerationCts(linked);
                isGenerating = false;
            }
        }

        /// <summary>Stops any active synthesis request.</summary>
        public void CancelActiveGeneration()
        {
            if (activeGenerationCts == null)
            {
                return;
            }

            activeGenerationCts.Cancel();
            CleanupActiveGenerationCts(activeGenerationCts);
            isGenerating = false;
        }

        private void HandleClipReady(AudioClip clip)
        {
            onClipReady?.Invoke(clip);

            if (autoplay && outputAudioSource != null && clip != null)
            {
                outputAudioSource.Stop();
                outputAudioSource.clip = clip;
                outputAudioSource.Play();
            }
        }

        private void ReportFailure(string message)
        {
            SherpaLog.Error($"[ZeroShotSpeechSynthesisComponent] {message}");
            DispatchToUnity(() => onGenerationFailed?.Invoke(message));
            RaiseError(message);
        }

        private void CleanupActiveGenerationCts(CancellationTokenSource cts)
        {
            if (activeGenerationCts != cts)
            {
                return;
            }

            activeGenerationCts.Dispose();
            activeGenerationCts = null;
        }

        private void NotifyBusy()
        {
            const string message = "Generation request ignored because another synthesis is already in progress.";
            SherpaLog.Warning($"[ZeroShotSpeechSynthesisComponent] {message}");
            DispatchToUnity(() => onGenerationFailed?.Invoke(message));
            RaiseError(message);
        }
    }
}
