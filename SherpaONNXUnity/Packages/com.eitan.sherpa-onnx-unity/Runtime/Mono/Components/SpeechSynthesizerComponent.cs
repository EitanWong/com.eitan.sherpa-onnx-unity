// Runtime: Packages/com.eitan.sherpa-onnx-unity/Runtime/Mono/Components/SpeechSynthesizerComponent.cs

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
    /// MonoBehaviour wrapper for <see cref="SpeechSynthesis"/> that exposes a simple text-to-speech workflow.
    /// </summary>
    [AddComponentMenu("SherpaONNX/Speech Synthesis/Speech Synthesizer")]
    [DisallowMultipleComponent]
    public sealed class SpeechSynthesizerComponent : SherpaModuleComponent<SpeechSynthesis>
    {
        [Header("Output")]
        [SerializeField]
        [Tooltip("Optional audio source used to play synthesized clips.")]
        private AudioSource outputAudioSource;

        [SerializeField]
        [Tooltip("Start playback automatically after a clip is generated.")]
        private bool autoplay = true;

        [Header("Synthesis Parameters")]
        [SerializeField]
        [Tooltip("Default speaker/voice identifier exposed by the model.")]
        private int voiceId;

        [SerializeField]
        [Tooltip("Speech speed multiplier. 1 = original speed.")]
        [Range(0.5f, 2.5f)]
        private float speechRate = 1f;

        [Header("Concurrency")]
        [SerializeField]
        [Tooltip("Reject new synthesis requests while another generation is in flight.")]
        private bool rejectConcurrentRequests = true;

        [Header("Events")]
        [SerializeField]
        private UnityEvent onSynthesisStarted = new UnityEvent();

        [SerializeField]
        private UnityEvent<AudioClip> onClipReady = new UnityEvent<AudioClip>();

        [SerializeField]
        private UnityEvent<string> onSynthesisFailed = new UnityEvent<string>();

        /// <summary>
        /// Invoked whenever a synthesis request begins.
        /// </summary>
        public UnityEvent SynthesisStartedEvent => onSynthesisStarted;

        /// <summary>
        /// Exposes synthesized clips to caller scripts.
        /// </summary>
        public UnityEvent<AudioClip> ClipReadyEvent => onClipReady;

        /// <summary>
        /// Exposes synthesis failure messages.
        /// </summary>
        public UnityEvent<string> SynthesisFailedEvent => onSynthesisFailed;

        private CancellationTokenSource sharedCancellation;
        private CancellationTokenSource activeGenerationCts;

        protected override void Awake()
        {
            base.Awake();
        }

        private void OnEnable()
        {
            sharedCancellation = new CancellationTokenSource();
        }

        private void OnDisable()
        {
            CancelActiveGeneration();
            sharedCancellation?.Cancel();
            sharedCancellation?.Dispose();
            sharedCancellation = null;
        }

        private void Reset()
        {
            SetSampleRateForInspector(-1);
        }

        protected override SpeechSynthesis CreateModule(string resolvedModelId, int resolvedSampleRate, SherpaONNXFeedbackReporter resolvedReporter)
        {
            return new SpeechSynthesis(resolvedModelId, resolvedSampleRate, resolvedReporter);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!Application.isPlaying && SampleRate != -1)
            {
                SetSampleRateForInspector(-1);
            }
        }
#endif

        /// <summary>
        /// Initiates synthesis for the provided text using the configured voice and speed.
        /// </summary>
        public void SynthesizeText(string text)
        {
            _ = GenerateClipAsync(text);
        }

        /// <summary>
        /// Generates speech audio asynchronously and returns the resulting clip.
        /// </summary>
        public async Task<AudioClip> GenerateClipAsync(string text, int? voiceOverride = null, float? speedOverride = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                SherpaLog.Warning("[SpeechSynthesizerComponent] Cannot synthesize empty text.");
                return null;
            }

            if (activeGenerationCts != null && !activeGenerationCts.IsCancellationRequested)
            {
                if (rejectConcurrentRequests)
                {
                    NotifyBusy();
                    return null;
                }
            }

            if (!EnsureModuleReady(out var module))
            {
                return null;
            }

            CancelActiveGeneration();

            var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                sharedCancellation?.Token ?? CancellationToken.None,
                cancellationToken);
            activeGenerationCts = linkedCts;

            onSynthesisStarted?.Invoke();

            try
            {
                var clip = await module.GenerateAsync(
                    text.Trim(),
                    voiceOverride ?? voiceId,
                    speedOverride ?? speechRate,
                    linkedCts.Token).ConfigureAwait(false);

                if (clip != null)
                {
                    DispatchToUnity(() => HandleClipReady(clip));
                }

                return clip;
            }
            catch (OperationCanceledException)
            {
                return null;
            }
            catch (Exception ex)
            {
                SherpaLog.Error($"[SpeechSynthesizerComponent] Generation failed: {ex.Message}");
                var message = ex.Message;
                DispatchToUnity(() => onSynthesisFailed?.Invoke(message));
                RaiseError(ex.Message);
                return null;
            }
            finally
            {
                CleanupActiveGenerationCts(linkedCts);
            }
        }

        /// <summary>
        /// Cancels the currently running synthesis request, if any.
        /// </summary>
        public void CancelActiveGeneration()
        {
            if (activeGenerationCts == null)
            {
                return;
            }

            activeGenerationCts.Cancel();
            CleanupActiveGenerationCts(activeGenerationCts);
        }

        private void HandleClipReady(AudioClip clip)
        {
            onClipReady?.Invoke(clip);

            if (autoplay && outputAudioSource != null)
            {
                outputAudioSource.Stop();
                outputAudioSource.clip = clip;
                outputAudioSource.Play();
            }
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
            const string message = "Synthesis request ignored because another generation is already running.";
            SherpaLog.Warning($"[SpeechSynthesizerComponent] {message}");
            DispatchToUnity(() => onSynthesisFailed?.Invoke(message));
            RaiseError(message);
        }
    }
}
