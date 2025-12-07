// Runtime: Packages/com.eitan.sherpa-onnx-unity/Runtime/Mono/Components/AudioTaggingComponent.cs

namespace Eitan.Sherpa.Onnx.Unity.Mono.Components
{
    using System;
    using System.Collections.Generic;

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

        [Header("Back-Pressure")]
        [SerializeField]
        [Tooltip("Maximum pending chunks to buffer. Oldest chunk is dropped when the limit is exceeded (0 = unbounded).")]
        private int maxPendingChunks = 8;

        [Header("Events")]
        [SerializeField]
        private UnityEvent<AudioTagging.AudioTag[]> onTagsReady = new UnityEvent<AudioTagging.AudioTag[]>();

        [SerializeField]
        private UnityEvent<string> onTaggingFailed = new UnityEvent<string>();

        private CancellationTokenSource streamingCts;
        private bool loggedSampleRateMismatch;
        private readonly Queue<float[]> pendingChunks = new Queue<float[]>();
        private readonly object queueLock = new object();
        private bool drainingQueue;
        private int droppedChunks;
        private float lastDropLog;

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
            droppedChunks = 0;
            lastDropLog = 0f;
        }

        protected override void OnDisable()
        {
            streamingCts?.Cancel();
            streamingCts?.Dispose();
            streamingCts = null;
            ClearQueue();
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
            ClearQueue();
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
                var msg = "Missing AudioClip reference.";
                onTaggingFailed?.Invoke(msg);
                RaiseError(msg);
                return Array.Empty<AudioTagging.AudioTag>();
            }

            if (!EnsureModuleReady(out var module))
            {
                return Array.Empty<AudioTagging.AudioTag>();
            }

            try
            {
                var mono = ExtractMono(clip);
                var tags = await module.TagAsync(mono, clip.frequency, topK, cancellationToken).ConfigureAwait(false);
                var safeTags = tags ?? Array.Empty<AudioTagging.AudioTag>();
                DispatchToUnity(() => onTagsReady?.Invoke(safeTags));
                return safeTags;
            }
            catch (OperationCanceledException)
            {
                return Array.Empty<AudioTagging.AudioTag>();
            }
            catch (Exception ex)
            {
                SherpaLog.Error($"[AudioTaggingComponent] Tagging failed: {ex.Message}");
                var message = ex.Message;
                DispatchToUnity(() => onTaggingFailed?.Invoke(message));
                RaiseError(ex.Message);
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
                SherpaLog.Warning($"[AudioTaggingComponent] Sample rate mismatch. Input={sampleRate}Hz Component={SampleRate}Hz. Results may drift.");
            }

            EnqueueChunk(samples);
        }

        private void EnqueueChunk(float[] samples)
        {
            if (samples == null || samples.Length == 0)
            {
                return;
            }

            // Clone to avoid consumer-side mutation; reuse queue for back-pressure.
            var clone = new float[samples.Length];
            Array.Copy(samples, clone, samples.Length);

            lock (queueLock)
            {
                if (maxPendingChunks > 0 && pendingChunks.Count >= maxPendingChunks)
                {
                    pendingChunks.Dequeue(); // Drop oldest to bound latency.
                    droppedChunks++;
                }

                pendingChunks.Enqueue(clone);
                if (drainingQueue)
                {
                    return;
                }
                drainingQueue = true;
            }

            MaybeLogDroppedChunks();
            _ = DrainQueueAsync();
        }

        private async Task DrainQueueAsync()
        {
            while (true)
            {
                float[] buffer;
                lock (queueLock)
                {
                    if (pendingChunks.Count == 0)
                    {
                        drainingQueue = false;
                        return;
                    }

                    buffer = pendingChunks.Dequeue();
                }

                if (buffer == null || buffer.Length == 0)
                {
                    continue;
                }

                if (!EnsureModuleReady(out var module))
                {
                    ClearQueue();
                    return;
                }

                try
                {
                    var tags = await module.TagStreamAsync(buffer, topK, streamingCts?.Token ?? default).ConfigureAwait(false);
                    if (tags != null && tags.Length > 0)
                    {
                        var captured = tags;
                        DispatchToUnity(() => onTagsReady?.Invoke(captured));
                    }
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception ex)
                {
                    SherpaLog.Error($"[AudioTaggingComponent] Streaming tagging failed: {ex.Message}");
                    var message = ex.Message;
                    DispatchToUnity(() => onTaggingFailed?.Invoke(message));
                    RaiseError(ex.Message);
                }
            }
        }

        private void ClearQueue()
        {
            lock (queueLock)
            {
                pendingChunks.Clear();
                drainingQueue = false;
            }
        }

        private void MaybeLogDroppedChunks()
        {
            if (droppedChunks <= 0)
            {
                return;
            }

            var now = Time.realtimeSinceStartup;
            if (now - lastDropLog < 1f)
            {
                return;
            }

            SherpaLog.Warning($"[AudioTaggingComponent] Dropped {droppedChunks} audio chunk(s) due to back-pressure (maxPendingChunks={maxPendingChunks}).");
            droppedChunks = 0;
            lastDropLog = now;
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
