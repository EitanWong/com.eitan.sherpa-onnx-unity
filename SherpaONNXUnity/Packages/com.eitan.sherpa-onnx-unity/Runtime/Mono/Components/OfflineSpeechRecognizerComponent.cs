// Runtime: Packages/com.eitan.sherpa-onnx-unity/Runtime/Mono/Components/OfflineSpeechRecognizerComponent.cs

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
    /// Offline ASR wrapper that expects pre-segmented speech (e.g., from <see cref="VoiceActivityDetectionComponent"/>).
    /// </summary>
    [AddComponentMenu("SherpaONNX/Speech Recognition/Offline Speech Recognizer")]
    [DisallowMultipleComponent]
    public sealed class OfflineSpeechRecognizerComponent : SherpaModuleComponent<SpeechRecognition>
    {
        [Header("Speech Segments")]
        [SerializeField]
        [Tooltip("VoiceActivityDetectionComponent that publishes speech segments.")]
        private VoiceActivityDetectionComponent voiceActivitySource;

        [SerializeField]
        [Tooltip("Automatically subscribe to the assigned VAD source on enable.")]
        private bool autoBindVoiceActivitySource = true;

        [Header("Events")]
        [SerializeField]
        private UnityEvent<string> onTranscriptReady = new UnityEvent<string>();

        [SerializeField]
        private UnityEvent<string> onTranscriptionFailed = new UnityEvent<string>();

        /// <summary>
        /// Public hook for scripts that want to display offline transcripts without using the inspector.
        /// </summary>
        public UnityEvent<string> TranscriptReadyEvent => onTranscriptReady;

        /// <summary>
        /// Public hook for scripts to surface error messages.
        /// </summary>
        public UnityEvent<string> TranscriptionFailedEvent => onTranscriptionFailed;

        private readonly Queue<AudioChunk> pendingSegments = new Queue<AudioChunk>();
        private readonly object queueLock = new object();

        private CancellationTokenSource processingCts;
        private VoiceActivityDetectionComponent boundSource;
        private bool drainingQueue;

        protected override SpeechRecognition CreateModule(string resolvedModelId, int resolvedSampleRate, SherpaONNXFeedbackReporter resolvedReporter)
        {
            return new SpeechRecognition(resolvedModelId, resolvedSampleRate, resolvedReporter);
        }

        private void OnEnable()
        {
            processingCts = new CancellationTokenSource();
            if (Application.isPlaying && autoBindVoiceActivitySource)
            {
                BindVoiceActivitySource(voiceActivitySource);
            }
        }

        private void OnDisable()
        {
            UnbindVoiceActivitySource(boundSource);
            processingCts?.Cancel();
            processingCts?.Dispose();
            processingCts = null;
            ClearQueue();
        }

        public void BindVoiceActivitySource(VoiceActivityDetectionComponent source)
        {
            if (boundSource == source)
            {
                return;
            }

            UnbindVoiceActivitySource(boundSource);
            if (source == null)
            {
                return;
            }

            source.SpeechSegmentReady += HandleSpeechSegment;
            boundSource = source;
        }

        public void UnbindVoiceActivitySource(VoiceActivityDetectionComponent source)
        {
            if (source == null)
            {
                return;
            }

            source.SpeechSegmentReady -= HandleSpeechSegment;
            if (boundSource == source)
            {
                boundSource = null;
            }
        }

        public void FeedSegment(float[] samples, int sampleRate)
        {
            HandleSpeechSegment(samples, sampleRate);
        }

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
            var text = await module.SpeechTranscriptionAsync(mono, clip.frequency, cancellationToken).ConfigureAwait(true);
            return text ?? string.Empty;
        }

        private void HandleSpeechSegment(float[] samples, int sampleRate)
        {
            if (samples == null || samples.Length == 0)
            {
                return;
            }

            var buffer = new float[samples.Length];
            Array.Copy(samples, buffer, samples.Length);
            EnqueueSegment(new AudioChunk(buffer, sampleRate));
        }

        private void EnqueueSegment(AudioChunk chunk)
        {
            lock (queueLock)
            {
                pendingSegments.Enqueue(chunk);
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
                    if (pendingSegments.Count == 0)
                    {
                        drainingQueue = false;
                        return;
                    }

                    chunk = pendingSegments.Dequeue();
                }

                await TranscribeChunkAsync(chunk).ConfigureAwait(true);
            }
        }

        private async Task TranscribeChunkAsync(AudioChunk chunk)
        {
            if (chunk.Samples == null || chunk.Samples.Length == 0)
            {
                return;
            }

            if (!EnsureModuleReady(out var module))
            {
                return;
            }

            try
            {
                var token = processingCts?.Token ?? CancellationToken.None;
                var text = await module.SpeechTranscriptionAsync(chunk.Samples, chunk.SampleRate, token).ConfigureAwait(true);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    onTranscriptReady?.Invoke(text.Trim());
                }
            }
            catch (OperationCanceledException)
            {
                // ignored
            }
            catch (Exception ex)
            {
                Debug.LogError($"[OfflineSpeechRecognizerComponent] Transcription failed: {ex.Message}");
                onTranscriptionFailed?.Invoke(ex.Message);
            }
        }

        private void ClearQueue()
        {
            lock (queueLock)
            {
                pendingSegments.Clear();
                drainingQueue = false;
            }
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
