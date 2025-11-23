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

        [Header("Transcription Events")]
        [SerializeField]
        private UnityEvent<string> onTranscriptionReady = new UnityEvent<string>();

        /// <summary>
        /// Allows scripts to subscribe to transcription updates without relying on the inspector.
        /// </summary>
        public UnityEvent<string> TranscriptionReadyEvent => onTranscriptionReady;

        private readonly Queue<AudioChunk> pendingChunks = new Queue<AudioChunk>();
        private readonly object queueLock = new object();

        private CancellationTokenSource streamingCancellation;
        private bool drainingQueue;
        private string lastTranscript = string.Empty;

        protected override void OnEnable()
        {
            base.OnEnable();
            streamingCancellation = new CancellationTokenSource();
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
            return new SpeechRecognition(resolvedModelId, resolvedSampleRate, resolvedReporter);
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
            var text = await module.SpeechTranscriptionAsync(mono, clip.frequency, cancellationToken).ConfigureAwait(false);
            return text ?? string.Empty;
        }

        /// <summary>
        /// Binds the recognizer to a new audio input source at runtime.
        /// </summary>
        private void EnqueueChunk(AudioChunk chunk)
        {
            if (Module == null || !Module.Initialized)
            {
                return;
            }

            lock (queueLock)
            {
                pendingChunks.Enqueue(chunk);
                if (drainingQueue)
                {
                    return;
                }
                drainingQueue = true;
            }

            _ = DrainQueueAsync();
        }

        private async Task DrainQueueAsync()
        {
            while (true)
            {
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

                try
                {
                    var token = streamingCancellation?.Token ?? default;
                    var text = await Module.SpeechTranscriptionAsync(chunk.Samples, chunk.SampleRate, token).ConfigureAwait(true);
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        PublishTranscript(text.Trim());
                    }
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[SpeechRecognizerComponent] Transcription failed: {ex.Message}");
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

            lastTranscript = string.Empty;
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
