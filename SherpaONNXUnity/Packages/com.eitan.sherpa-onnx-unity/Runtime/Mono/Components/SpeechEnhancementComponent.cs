// Runtime: Packages/com.eitan.sherpa-onnx-unity/Runtime/Mono/Components/SpeechEnhancementComponent.cs

namespace Eitan.Sherpa.Onnx.Unity.Mono.Components
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Eitan.SherpaONNXUnity.Runtime;
    using Eitan.SherpaONNXUnity.Runtime.Modules;
    using UnityEngine;
    using UnityEngine.Events;
    using UnityEngine.Serialization;

    /// <summary>
    /// Reusable MonoBehaviour that enhances provided audio clips or raw sample buffers.
    /// Pair with <see cref="Eitan.Sherpa.Onnx.Unity.Mono.Inputs.SherpaMicrophoneInput"/> when you need microphone capture.
    /// </summary>
    [AddComponentMenu("SherpaONNX/Speech Enhancement/Speech Enhancement Component")]
    [DisallowMultipleComponent]
    public sealed class SpeechEnhancementComponent : SherpaModuleComponent<SpeechEnhancement>
    {
        [Header("Playback")]
        [SerializeField]
        [Tooltip("Optional AudioSource for playback. If not assigned, the component will not autoplay.")]
        private AudioSource playbackAudioSource;

        [SerializeField]
        [Tooltip("Automatically play the processed clip on the playback AudioSource.")]
        private bool autoplay = true;

        [Header("Clip Enhancement")]
        [SerializeField]
        [Tooltip("Optional AudioClip to enhance directly. Falls back to the Playback Audio Source clip when null.")]
        private AudioClip clipReference;

        [SerializeField]
        [Tooltip("Process the referenced clip automatically when the component becomes enabled in play mode.")]
        private bool enhanceOnEnable;

        [SerializeField]
        [Tooltip("Create a new AudioClip when enhancing existing clips instead of overwriting their data.")]
        private bool duplicateClip = true;

        [Header("Events")]
        [SerializeField]
        private UnityEvent<AudioClip> onClipReady = new UnityEvent<AudioClip>();

        [SerializeField]
        private UnityEvent<string> onError = new UnityEvent<string>();

        private CancellationTokenSource enhancementCts;

        /// <summary>Raised when a processed clip is ready.</summary>
        public UnityEvent<AudioClip> ClipReadyEvent => onClipReady;

        /// <summary>Raised when the component encounters an error.</summary>
        public new UnityEvent<string> ErrorEvent => onError;

        protected override SpeechEnhancement CreateModule(string resolvedModelId, int resolvedSampleRate, SherpaONNXFeedbackReporter resolvedReporter)
        {
            return new SpeechEnhancement(resolvedModelId, resolvedSampleRate, resolvedReporter);
        }

        protected override void Awake()
        {
            base.Awake();
        }

        private void OnEnable()
        {
            if (enhancementCts == null || enhancementCts.IsCancellationRequested)
            {
                enhancementCts?.Dispose();
                enhancementCts = new CancellationTokenSource();
            }

            if (Application.isPlaying && enhanceOnEnable && IsInitialized)
            {
                _ = EnhanceAssignedClipAsync();
            }
        }

        private void OnDisable()
        {
            CancelEnhancementOperations(false);
        }

        protected override void OnDestroy()
        {
            CancelEnhancementOperations(false);
            enhancementCts?.Dispose();
            base.OnDestroy();
        }

        protected override void OnModuleInitializationStateChanged(bool ready)
        {
            base.OnModuleInitializationStateChanged(ready);

            if (!Application.isPlaying)
            {
                return;
            }

            if (ready && enhanceOnEnable)
            {
                _ = EnhanceAssignedClipAsync();
            }
        }

        /// <summary>
        /// Enhances an existing AudioClip and returns the processed version.
        /// </summary>
        public void EnhanceAssignedClip()
        {
            _ = EnhanceAssignedClipAsync();
        }

        /// <summary>
        /// Enhances a provided clip or the referenced clip and returns the processed version.
        /// </summary>
        public async Task<AudioClip> EnhanceClipAsync(AudioClip clip, bool applyToPlayback = true, AudioSource playbackOverride = null, CancellationToken cancellationToken = default)
        {
            if (clip == null)
            {
                ReportError("No AudioClip provided for enhancement.");
                return null;
            }

            if (!EnsureModuleReady(out var module))
            {
                return null;
            }

            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                enhancementCts?.Token ?? CancellationToken.None,
                cancellationToken);

            try
            {
                var samplesPerChannel = clip.samples;
                var channels = Mathf.Max(1, clip.channels);
                var interleaved = new float[samplesPerChannel * channels];
                clip.GetData(interleaved, 0);

                var workingBuffer = duplicateClip ? new float[interleaved.Length] : interleaved;
                var channelBuffer = new float[samplesPerChannel];

                for (int channel = 0; channel < channels; channel++)
                {
                    ExtractChannel(interleaved, channels, channel, channelBuffer);
                    await module.EnhanceAsync(channelBuffer, clip.frequency, linkedCts.Token).ConfigureAwait(false);
                    InjectChannel(workingBuffer, channels, channel, channelBuffer);
                }

                AudioClip processedClip = null;
                await RunOnUnityThreadAsync(() =>
                {
                    processedClip = duplicateClip
                        ? AudioClip.Create($"{clip.name}_enhanced", samplesPerChannel, channels, clip.frequency, false)
                        : clip;

                    processedClip.SetData(workingBuffer, 0);

                    if (applyToPlayback)
                    {
                        var output = playbackOverride != null ? playbackOverride : playbackAudioSource;
                        HandleClipReady(processedClip, output);
                    }
                }).ConfigureAwait(false);

                return processedClip;
            }
            catch (OperationCanceledException)
            {
                return null;
            }
        }

        /// <summary>
        /// Enhances a raw buffer of PCM samples and optionally plays it back.
        /// </summary>
        public async Task<AudioClip> EnhanceSamplesAsync(
            float[] samples,
            int sampleRate,
            bool applyToPlayback = true,
            AudioSource playbackOverride = null,
            CancellationToken cancellationToken = default)
        {
            if (samples == null || samples.Length == 0)
            {
                ReportError("No samples provided for enhancement.");
                return null;
            }

            if (!EnsureModuleReady(out var module))
            {
                return null;
            }

            var buffer = new float[samples.Length];
            Array.Copy(samples, buffer, samples.Length);

            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                enhancementCts?.Token ?? CancellationToken.None,
                cancellationToken);

            try
            {
                await module.EnhanceAsync(buffer, sampleRate, linkedCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return null;
            }

            AudioClip clip = null;
            await RunOnUnityThreadAsync(() =>
            {
                clip = CreateClipFromSamples(buffer, sampleRate);
                if (applyToPlayback)
                {
                    var output = playbackOverride != null ? playbackOverride : playbackAudioSource;
                    HandleClipReady(clip, output);
                }
            }).ConfigureAwait(false);

            return clip;
        }

        private async Task<AudioClip> EnhanceAssignedClipAsync()
        {
            var clip = clipReference != null ? clipReference : playbackAudioSource?.clip;
            if (clip == null)
            {
                ReportError("No clip assigned for enhancement. Set Clip Reference or assign a clip to the Playback Audio Source.");
                return null;
            }

            var output = playbackAudioSource != null ? playbackAudioSource : null;
            return await EnhanceClipAsync(
                clip,
                applyToPlayback: output != null,
                playbackOverride: output,
                cancellationToken: enhancementCts?.Token ?? default).ConfigureAwait(true);
        }

        private void HandleClipReady(AudioClip clip, AudioSource playbackOverride = null)
        {
            onClipReady?.Invoke(clip);

            var output = playbackOverride != null ? playbackOverride : playbackAudioSource;
            if (autoplay && output != null && clip != null)
            {
                output.Stop();
                output.clip = clip;
                output.Play();
            }
        }

        private void CancelEnhancementOperations(bool recreateToken = true)
        {
            if (enhancementCts != null)
            {
                enhancementCts.Cancel();
                enhancementCts.Dispose();
                enhancementCts = recreateToken ? new CancellationTokenSource() : null;
            }
        }

        private void ReportError(string message)
        {
            SherpaLog.Error($"[SpeechEnhancementComponent] {message}");
            DispatchToUnity(() => onError?.Invoke(message));
            RaiseError(message);
        }

        private Task RunOnUnityThreadAsync(Action action)
        {
            if (action == null)
            {
                return Task.CompletedTask;
            }

            var tcs = new TaskCompletionSource<bool>();
            DispatchToUnity(() =>
            {
                try
                {
                    action();
                    tcs.TrySetResult(true);
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            });

            return tcs.Task;
        }

        private static AudioClip CreateClipFromSamples(float[] monoSamples, int sampleRate)
        {
            var clip = AudioClip.Create("speech_enhancement_output", monoSamples.Length, 1, sampleRate, false);
            clip.SetData(monoSamples, 0);
            return clip;
        }

        private static void ExtractChannel(float[] interleaved, int channelCount, int channelIndex, float[] destination)
        {
            int frameCount = destination.Length;
            for (int frame = 0; frame < frameCount; frame++)
            {
                destination[frame] = interleaved[frame * channelCount + channelIndex];
            }
        }

        private static void InjectChannel(float[] interleaved, int channelCount, int channelIndex, float[] source)
        {
            int frameCount = source.Length;
            for (int frame = 0; frame < frameCount; frame++)
            {
                interleaved[frame * channelCount + channelIndex] = source[frame];
            }
        }
    }
}
