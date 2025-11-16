// Runtime: Packages/com.eitan.sherpa-onnx-unity/Runtime/Mono/Components/SpeechEnhancerComponent.cs

namespace Eitan.Sherpa.Onnx.Unity.Mono.Components
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Eitan.SherpaONNXUnity.Runtime;
    using Eitan.SherpaONNXUnity.Runtime.Core;

    using UnityEngine;
    using UnityEngine.Events;

    /// <summary>
    /// User-friendly wrapper for <see cref="SpeechEnhancement"/> that denoises AudioClips in-place or as duplicates.
    /// </summary>
    [AddComponentMenu("SherpaONNX/Speech Enhancement/Speech Enhancer")]
    [DisallowMultipleComponent]
    public sealed class SpeechEnhancerComponent : SherpaModuleComponent<SpeechEnhancement>
    {
        [Header("Targets")]
        [SerializeField]
        [Tooltip("AudioSource whose clip should be enhanced automatically.")]
        private AudioSource targetAudioSource;

        [SerializeField]
        [Tooltip("Optional explicit clip reference. Falls back to Target Audio Source clip when null.")]
        private AudioClip clipReference;

        [SerializeField]
        [Tooltip("Process the referenced clip automatically when the component becomes enabled.")]
        private bool enhanceOnEnable;

        [SerializeField]
        [Tooltip("Create a new AudioClip instance instead of overwriting the source clip data.")]
        private bool duplicateClip = true;

        [Header("Events")]
        [SerializeField]
        private UnityEvent<AudioClip> onClipEnhanced = new UnityEvent<AudioClip>();

        [SerializeField]
        private UnityEvent<string> onEnhancementFailed = new UnityEvent<string>();

        /// <summary>
        /// Event invoked when an AudioClip has been denoised successfully.
        /// </summary>
        public UnityEvent<AudioClip> ClipEnhancedEvent => onClipEnhanced;

        /// <summary>
        /// Event invoked when enhancement fails so UI can surface the message.
        /// </summary>
        public UnityEvent<string> EnhancementFailedEvent => onEnhancementFailed;

        private CancellationTokenSource enhancementCancellation;

        private void OnEnable()
        {
            enhancementCancellation = new CancellationTokenSource();
            if (enhanceOnEnable)
            {
                _ = EnhanceAssignedClipAsync();
            }
        }

        private void OnDisable()
        {
            enhancementCancellation?.Cancel();
            enhancementCancellation?.Dispose();
            enhancementCancellation = null;
        }

        protected override SpeechEnhancement CreateModule(string resolvedModelId, int resolvedSampleRate, SherpaONNXFeedbackReporter resolvedReporter)
        {
            return new SpeechEnhancement(resolvedModelId, resolvedSampleRate, resolvedReporter);
        }

        /// <summary>
        /// Enhances the configured clip reference or the clip found on the target AudioSource.
        /// </summary>
        public void EnhanceAssignedClip()
        {
            _ = EnhanceAssignedClipAsync();
        }

        /// <summary>
        /// Enhances the supplied clip and returns the resulting AudioClip instance.
        /// </summary>
        public Task<AudioClip> EnhanceClipAsync(AudioClip clip, bool applyToAudioSource = true, CancellationToken cancellationToken = default)
        {
            return EnhanceClipInternalAsync(clip, applyToAudioSource, cancellationToken);
        }

        private async Task<AudioClip> EnhanceAssignedClipAsync()
        {
            var clip = clipReference != null ? clipReference : targetAudioSource?.clip;
            if (clip == null)
            {
                Debug.LogWarning("[SpeechEnhancerComponent] No clip assigned for enhancement.");
                return null;
            }

            return await EnhanceClipInternalAsync(clip, true, enhancementCancellation?.Token ?? default).ConfigureAwait(true);
        }

        private async Task<AudioClip> EnhanceClipInternalAsync(AudioClip clip, bool applyToAudioSource, CancellationToken cancellationToken)
        {
            if (clip == null)
            {
                return null;
            }

            if (!EnsureModuleReady(out var module))
            {
                return null;
            }

            try
            {
                var samplesPerChannel = clip.samples;
                var channels = Mathf.Max(1, clip.channels);
                var interleaved = new float[samplesPerChannel * channels];
                clip.GetData(interleaved, 0);

                var channelBuffer = new float[samplesPerChannel];
                for (int channel = 0; channel < channels; channel++)
                {
                    ExtractChannel(interleaved, channels, channel, channelBuffer);
                    await module.EnhanceAsync(channelBuffer, clip.frequency, cancellationToken).ConfigureAwait(true);
                    InjectChannel(interleaved, channels, channel, channelBuffer);
                }

                var outputClip = duplicateClip
                    ? AudioClip.Create($"{clip.name}_enhanced", samplesPerChannel, channels, clip.frequency, false)
                    : clip;

                outputClip.SetData(interleaved, 0);

                if (applyToAudioSource && targetAudioSource != null)
                {
                    targetAudioSource.clip = outputClip;
                    if (!targetAudioSource.isPlaying)
                    {
                        targetAudioSource.Play();
                    }
                }

                onClipEnhanced?.Invoke(outputClip);
                return outputClip;
            }
            catch (OperationCanceledException)
            {
                return null;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SpeechEnhancerComponent] Enhancement failed: {ex.Message}");
                onEnhancementFailed?.Invoke(ex.Message);
                return null;
            }
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
