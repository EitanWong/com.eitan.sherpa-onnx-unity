// Runtime: Packages/com.eitan.sherpa-onnx-unity/Runtime/Mono/Components/AudioTaggingComponent.cs

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
    /// MonoBehaviour wrapper for <see cref="AudioTagging"/> that supports both streaming microphone
    /// input (via <see cref="SherpaAudioInputSource"/>) and one-shot AudioClip tagging.
    /// </summary>
    [AddComponentMenu("SherpaONNX/Audio/Audio Tagging")]
    [DisallowMultipleComponent]
    public sealed class AudioTaggingComponent : SherpaAudioStreamingComponent<AudioTagging>
    {
        [Header("Offline Clip")]
        [SerializeField]
        [Tooltip("Optional AudioClip to tag once the component starts.")]
        private AudioClip clipToTag;

        [SerializeField]
        [Tooltip("Automatically tags the assigned clip on Start when enabled.")]
        private bool tagClipOnStart;

        [Header("Streaming")]
        [SerializeField]
        [Tooltip("Number of tags to return for each evaluation.")]
        [Min(1)]
        private int topK = 5;

        [SerializeField]
        [Tooltip("Log a warning when the incoming audio sample rate differs from the configured module rate.")]
        private bool warnOnSampleRateMismatch = true;

        [Header("Events")]
        [SerializeField]
        private UnityEvent<AudioTagging.AudioTag[]> onTagsReady = new UnityEvent<AudioTagging.AudioTag[]>();

        [SerializeField]
        private UnityEvent<string> onTaggingFailed = new UnityEvent<string>();

        private CancellationTokenSource streamingCts;
        private bool loggedSampleRateMismatch;

        /// <summary>Raised whenever tagging completes (streaming or offline).</summary>
        public UnityEvent<AudioTagging.AudioTag[]> TagsReadyEvent => onTagsReady;

        /// <summary>Raised when tagging encounters an error condition.</summary>
        public UnityEvent<string> TaggingFailedEvent => onTaggingFailed;

        /// <summary>Gets or sets the AudioClip used for one-shot tagging.</summary>
        public AudioClip ClipToTag
        {
            get => clipToTag;
            set => clipToTag = value;
        }

        /// <summary>Gets or sets the Top-K value used for tagging.</summary>
        public int TopK
        {
            get => topK;
            set => topK = Mathf.Max(1, value);
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            streamingCts = new CancellationTokenSource();
            loggedSampleRateMismatch = false;
        }

        protected override void OnDisable()
        {
            streamingCts?.Cancel();
            streamingCts?.Dispose();
            streamingCts = null;
            base.OnDisable();
        }

        private void Start()
        {
            if (tagClipOnStart && clipToTag != null)
            {
                _ = TagClipAsync(clipToTag);
            }
        }

        protected override void OnDestroy()
        {
            streamingCts?.Cancel();
            streamingCts?.Dispose();
            base.OnDestroy();
        }

        protected override AudioTagging CreateModule(string resolvedModelId, int resolvedSampleRate, SherpaONNXFeedbackReporter resolvedReporter)
        {
            return new AudioTagging(resolvedModelId, resolvedSampleRate, resolvedReporter)
            {
                DefaultTopK = Mathf.Max(1, topK)
            };
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            topK = Mathf.Max(1, topK);
            if (Module != null)
            {
                Module.DefaultTopK = topK;
            }
        }
#endif

        /// <summary>
        /// Manually feeds PCM samples into the streaming tagger.
        /// Useful when audio comes from a custom recorder instead of <see cref="SherpaAudioInputSource"/>.
        /// </summary>
        public void FeedSamples(float[] samples, int sampleRate)
        {
            if (!CanProcessChunk(samples, sampleRate))
            {
                return;
            }

            OnAudioChunkReceived(samples, sampleRate);
        }

        /// <summary>
        /// Tags the assigned AudioClip once and returns the detected tags.
        /// </summary>
        public Task<AudioTagging.AudioTag[]> TagAssignedClipAsync(CancellationToken cancellationToken = default)
        {
            return TagClipAsync(clipToTag, cancellationToken);
        }

        /// <summary>
        /// Tags the provided AudioClip once and raises <see cref="TagsReadyEvent"/>.
        /// </summary>
        public async Task<AudioTagging.AudioTag[]> TagClipAsync(AudioClip clip, CancellationToken cancellationToken = default)
        {
            if (clip == null)
            {
                onTaggingFailed?.Invoke("Missing AudioClip reference.");
                return Array.Empty<AudioTagging.AudioTag>();
            }

            if (!EnsureModuleReady(out var module))
            {
                return Array.Empty<AudioTagging.AudioTag>();
            }

            try
            {
                var mono = ExtractMono(clip);
                var tags = await module.TagAsync(mono, clip.frequency, topK, cancellationToken).ConfigureAwait(true);
                onTagsReady?.Invoke(tags ?? Array.Empty<AudioTagging.AudioTag>());
                return tags ?? Array.Empty<AudioTagging.AudioTag>();
            }
            catch (OperationCanceledException)
            {
                return Array.Empty<AudioTagging.AudioTag>();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AudioTaggingComponent] Tagging failed: {ex.Message}");
                onTaggingFailed?.Invoke(ex.Message);
                return Array.Empty<AudioTagging.AudioTag>();
            }
        }

        /// <summary>
        /// Clears the internal streaming buffer, typically when switching sources.
        /// </summary>
        public void ResetStreamingBuffer()
        {
            if (Module != null)
            {
                Module.ClearStreamingBuffer();
            }
        }

        protected override void OnAudioChunkReceived(float[] samples, int sampleRate)
        {
            if (warnOnSampleRateMismatch && sampleRate != SampleRate && !loggedSampleRateMismatch)
            {
                loggedSampleRateMismatch = true;
                Debug.LogWarning($"[AudioTaggingComponent] Sample rate mismatch. Input={sampleRate}Hz Component={SampleRate}Hz. Results may drift.");
            }

            _ = ProcessStreamingAsync(samples);
        }

        private async Task ProcessStreamingAsync(float[] samples)
        {
            if (!EnsureModuleReady(out var module))
            {
                return;
            }

            var buffer = new float[samples.Length];
            Array.Copy(samples, buffer, samples.Length);

            try
            {
                var tags = await module.TagStreamAsync(buffer, topK, streamingCts?.Token ?? default).ConfigureAwait(true);
                if (tags != null && tags.Length > 0)
                {
                    onTagsReady?.Invoke(tags);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected during teardown.
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AudioTaggingComponent] Streaming tagging failed: {ex.Message}");
                onTaggingFailed?.Invoke(ex.Message);
            }
        }

        private static float[] ExtractMono(AudioClip clip)
        {
            var frames = clip.samples;
            var channels = Mathf.Max(1, clip.channels);
            var interleaved = new float[frames * channels];
            clip.GetData(interleaved, 0);

            if (channels == 1)
            {
                return interleaved;
            }

            var mono = new float[frames];
            for (int frame = 0; frame < frames; frame++)
            {
                float sum = 0f;
                for (int channel = 0; channel < channels; channel++)
                {
                    sum += interleaved[frame * channels + channel];
                }
                mono[frame] = sum / channels;
            }

            return mono;
        }
    }
}
