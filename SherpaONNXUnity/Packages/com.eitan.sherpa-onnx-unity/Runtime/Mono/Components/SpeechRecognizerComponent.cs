// Runtime: Packages/com.eitan.sherpa-onnx-unity/Runtime/Mono/Components/SpeechRecognizerComponent.cs

namespace Eitan.Sherpa.Onnx.Unity.Mono.Components
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Eitan.Sherpa.Onnx.Unity.Mono.Inputs;
    using Eitan.SherpaONNXUnity.Runtime;
    using Eitan.SherpaONNXUnity.Runtime.Modules;

    using UnityEngine;
    using UnityEngine.Events;

    /// <summary>
    /// High-level wrapper around <see cref="SpeechRecognition"/> that consumes PCM chunks
    /// from any <see cref="SherpaAudioInputSource"/> (e.g., <see cref="SherpaMicrophoneInput"/>).
    /// Streams audio into the recognizer and exposes transcripts through UnityEvents.
    /// </summary>
    [AddComponentMenu("SherpaONNX/Speech Recognition/Speech Recognizer")]
    [DisallowMultipleComponent]
    public sealed class SpeechRecognizerComponent : SherpaAudioStreamingComponent<SpeechRecognition>
    {
        [SerializeField]
        [Tooltip("Avoid emitting duplicate transcripts when the recognizer returns the same value repeatedly.")]
        private bool deduplicateStreamingResults = true;

        [Header("Lifecycle")]
        [SerializeField]
        [Tooltip("Start module initialization immediately when constructed. Disable to configure first, then call StartRecognizerAsync/StartRecognizer manually.")]
        private bool startModuleImmediately = true;

        [Header("Transcription Events")]
        [SerializeField]
        private UnityEvent<string> onTranscriptionReady = new UnityEvent<string>();

        [Header("Streaming")]
        [SerializeField]
        [Tooltip("Maximum pending audio chunks to keep before dropping the oldest. Prevents unbounded latency.")]
        private int maxPendingChunks = 8;

        [SerializeField]
        [Tooltip("Maximum concurrent transcription tasks allowed before dropping new streaming requests.")]
        private int maxInFlightTranscriptions = 2;

        [SerializeField]
        [Tooltip("Drop incoming audio when the recognizer is busy to keep latency low.")]
        private bool dropIfRecognizerBusy = true;

        [Header("Endpointing (online models only)")]
        [SerializeField]
        [Tooltip("Override default endpointing rules for online models.")]
        private bool overrideEndpointRules = false;

        [SerializeField]
        [Min(0f)]
        private float rule1MinTrailingSilence = 2.4f;

        [SerializeField]
        [Min(0f)]
        private float rule2MinTrailingSilence = 1.2f;

        [SerializeField]
        [Min(0f)]
        private float rule3MinUtteranceLength = 30f;

        /// <summary>
        /// Allows scripts to subscribe to transcription updates without relying on the inspector.
        /// </summary>
        public UnityEvent<string> TranscriptionReadyEvent => onTranscriptionReady;

        private readonly Queue<AudioChunk> pendingChunks = new Queue<AudioChunk>();
        private readonly object queueLock = new object();

        private CancellationTokenSource streamingCancellation;
        private bool drainingQueue;
        private string lastTranscript = string.Empty;
        private int droppedChunks;
        private float lastDropLog;

        protected override void OnEnable()
        {
            base.OnEnable();
            streamingCancellation = new CancellationTokenSource();
            droppedChunks = 0;
            lastDropLog = 0f;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            streamingCancellation?.Cancel();
            streamingCancellation?.Dispose();
            streamingCancellation = null;
            ClearQueue();
        }

        protected override SpeechRecognition CreateModule(string resolvedModelId, int resolvedSampleRate, SherpaONNXFeedbackReporter resolvedReporter)
        {
            SpeechRecognition.Options options = null;
            if (overrideEndpointRules)
            {
                options = new SpeechRecognition.Options
                {
                    Rule1MinTrailingSilence = rule1MinTrailingSilence,
                    Rule2MinTrailingSilence = rule2MinTrailingSilence,
                    Rule3MinUtteranceLength = rule3MinUtteranceLength
                };
            }

            var maxInFlight = Mathf.Max(1, maxInFlightTranscriptions);
            return new SpeechRecognition(resolvedModelId, resolvedSampleRate, resolvedReporter, startImmediately: startModuleImmediately, options: options, maxPendingTranscriptions: maxInFlight, dropIfBusy: dropIfRecognizerBusy);
        }

        /// <summary>
        /// Enqueues audio samples for transcription. Samples are copied internally.
        /// </summary>
        public void FeedSamples(float[] samples, int sampleRate)
        {
            if (samples == null || samples.Length == 0 || sampleRate <= 0)
            {
                return;
            }

            if (!CanProcessChunk(samples, sampleRate))
            {
                return;
            }

            var buffer = new float[samples.Length];
            Array.Copy(samples, buffer, samples.Length);
            EnqueueChunk(new AudioChunk(buffer, sampleRate));
        }

        /// <summary>
        /// Transcribes a complete AudioClip asynchronously.
        /// </summary>
        public async Task<string> TranscribeClipAsync(AudioClip clip, CancellationToken cancellationToken = default)
        {
            if (clip == null)
            {
                throw new ArgumentNullException(nameof(clip));
            }

            if (!EnsureModuleReady(out var module))
            {
                return string.Empty;
            }

            var data = new float[clip.samples * clip.channels];
            clip.GetData(data, 0);
            var mono = DownmixToMono(data, clip.channels);
            var result = await module.TranscribeAsync(mono, clip.frequency, cancellationToken).ConfigureAwait(false);
            return result.Status == SpeechRecognition.TranscriptionStatus.Success ? result.Text ?? string.Empty : string.Empty;
        }

        /// <summary>
        /// Starts module initialization when startModuleImmediately is disabled.
        /// </summary>
        public Task StartRecognizerAsync(CancellationToken cancellationToken = default)
        {
            if (Module == null && !TryLoadModule())
            {
                RaiseError("Failed to load speech recognition module.");
                return Task.CompletedTask;
            }

            return Module?.StartInitialization(cancellationToken) ?? Task.CompletedTask;
        }

        /// <summary>
        /// Binds the recognizer to a new audio input source at runtime.
        /// </summary>
        private void EnqueueChunk(AudioChunk chunk)
        {
            if (Module == null)
            {
                return;
            }

            if (!Module.InitializationStarted && !startModuleImmediately)
            {
                // Kick off initialization lazily for delayed-start scenarios.
                _ = Module.StartInitialization();
            }

            if (!Module.Initialized)
            {
                return;
            }

            bool dropped = false;
            lock (queueLock)
            {
                if (maxPendingChunks > 0 && pendingChunks.Count >= maxPendingChunks)
                {
                    pendingChunks.Dequeue(); // Drop oldest to keep latency bounded.
                    droppedChunks++;
                    dropped = true;
                }
                pendingChunks.Enqueue(chunk);
                if (drainingQueue)
                {
                    return;
                }
                drainingQueue = true;
            }

            if (dropped)
            {
                MaybeLogDroppedChunks();
            }

            _ = DrainQueueAsync();
        }

        private async Task DrainQueueAsync()
        {
            while (true)
            {
                if (streamingCancellation != null && streamingCancellation.IsCancellationRequested)
                {
                    ClearQueue();
                    return;
                }

                if (Module == null)
                {
                    ClearQueue();
                    return;
                }

                AudioChunk chunk;
                lock (queueLock)
                {
                    if (pendingChunks.Count == 0)
                    {
                        drainingQueue = false;
                        return;
                    }

                    chunk = pendingChunks.Dequeue();
                }

                if (chunk.Samples == null || chunk.Samples.Length == 0)
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
                    var token = streamingCancellation?.Token ?? default;
                    var result = await module.TranscribeAsync(chunk.Samples, chunk.SampleRate, token).ConfigureAwait(false);

                    if (result.Status == SpeechRecognition.TranscriptionStatus.Success && !string.IsNullOrWhiteSpace(result.Text))
                    {
                        var text = result.Text.Trim();
                        DispatchToUnity(() => PublishTranscript(text));
                    }
                    else if (result.Status == SpeechRecognition.TranscriptionStatus.Error && result.Error != null)
                    {
                        SherpaLog.Error($"[SpeechRecognizerComponent] Transcription failed: {result.Error.Message}");
                        RaiseError(result.Error.Message);
                    }
                    else if (result.Status == SpeechRecognition.TranscriptionStatus.Disposed)
                    {
                        return;
                    }
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception ex)
                {
                    SherpaLog.Error($"[SpeechRecognizerComponent] Transcription failed: {ex.Message}");
                    RaiseError(ex.Message);
                }
            }
        }

        private void PublishTranscript(string text)
        {
            if (deduplicateStreamingResults && string.Equals(text, lastTranscript, StringComparison.Ordinal))
            {
                return;
            }

            lastTranscript = text;
            onTranscriptionReady?.Invoke(text);
        }

        protected override void OnAudioChunkReceived(float[] samples, int sampleRate)
        {
            EnqueueChunk(new AudioChunk(samples, sampleRate));
        }

        private void ClearQueue()
        {
            lock (queueLock)
            {
                pendingChunks.Clear();
                drainingQueue = false;
            }

            MaybeLogDroppedChunks();

            lastTranscript = string.Empty;
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

            SherpaLog.Warning($"[SpeechRecognizerComponent] Dropped {droppedChunks} audio chunk(s) due to back-pressure (maxPendingChunks={maxPendingChunks}).");
            droppedChunks = 0;
            lastDropLog = now;
        }

        private static float[] DownmixToMono(float[] data, int channels)
        {
            if (data == null)
            {
                return Array.Empty<float>();
            }

            if (channels <= 1)
            {
                var clone = new float[data.Length];
                Array.Copy(data, clone, data.Length);
                return clone;
            }

            int frameCount = data.Length / channels;
            var mono = new float[frameCount];

            for (int frame = 0; frame < frameCount; frame++)
            {
                int offset = frame * channels;
                float sum = 0f;
                for (int ch = 0; ch < channels; ch++)
                {
                    sum += data[offset + ch];
                }

                mono[frame] = sum / channels;
            }

            return mono;
        }

        private readonly struct AudioChunk
        {
            public AudioChunk(float[] samples, int sampleRate)
            {
                Samples = samples;
                SampleRate = sampleRate;
            }

            public float[] Samples { get; }
            public int SampleRate { get; }
        }
    }
}
