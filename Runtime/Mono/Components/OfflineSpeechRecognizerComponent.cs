// Runtime: Packages/com.eitan.sherpa-onnx-unity/Runtime/Mono/Components/OfflineSpeechRecognizerComponent.cs

namespace Eitan.Sherpa.Onnx.Unity.Mono.Components
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Eitan.SherpaONNXUnity.Runtime;
    using Eitan.SherpaONNXUnity.Runtime.Modules;
    using Eitan.SherpaONNXUnity.Runtime.Utilities;

    using UnityEngine;
    using UnityEngine.Events;
    using UnityEngine.Serialization;

    /// <summary>
    /// Offline ASR wrapper that expects pre-segmented speech (e.g., from <see cref="VoiceActivityDetectionComponent"/>).
    /// Use this component with offline speech recognition models.
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

        [Header("Lifecycle")]
        [SerializeField]
        [Tooltip("Start module initialization immediately when constructed. Disable to configure first, then call StartModuleInitialization manually.")]
        private bool startModuleImmediately = true;

        [Header("Recognition Options")]
        [SerializeField]
        [HideInInspector]
        [FormerlySerializedAs("language")]
        private string recognitionLanguage = string.Empty;

        [Header("Queue")]
        [SerializeField]
        [Tooltip("Maximum pending segments to buffer. Oldest is dropped when the limit is exceeded (0 = unbounded).")]
        private int maxPendingSegments = 32;

        [Header("Events")]
        [SerializeField]
        private UnityEvent<SpeechRecognition.TranscriptionResult> onTranscriptReady = new UnityEvent<SpeechRecognition.TranscriptionResult>();

        [SerializeField]
        private UnityEvent<string> onTranscriptionFailed = new UnityEvent<string>();

        /// <summary>
        /// Public hook for scripts that want to display offline transcripts without using the inspector.
        /// </summary>
        public UnityEvent<SpeechRecognition.TranscriptionResult> TranscriptReadyEvent => onTranscriptReady;

        /// <summary>
        /// Public hook for scripts to surface error messages.
        /// </summary>
        public UnityEvent<string> TranscriptionFailedEvent => onTranscriptionFailed;

        /// <summary>
        /// Optional language code used by models that support language selection.
        /// </summary>
        public string RecognitionLanguage
        {
            get => recognitionLanguage;
            set => recognitionLanguage = value ?? string.Empty;
        }

        private readonly Queue<AudioChunk> pendingSegments = new Queue<AudioChunk>();
        private readonly object queueLock = new object();

        private CancellationTokenSource processingCts;
        private VoiceActivityDetectionComponent boundSource;
        private bool drainingQueue;
        private int droppedSegments;
        private float lastDropLog;

        protected override SpeechRecognition CreateModule(string resolvedModelId, int resolvedSampleRate, SherpaONNXFeedbackReporter resolvedReporter)
        {
            if (SherpaUtils.Model.IsOnlineModel(resolvedModelId))
            {
                throw new ArgumentException(
                    $"{nameof(OfflineSpeechRecognizerComponent)} requires an offline speech recognition model. Model '{resolvedModelId}' is online/realtime.",
                    nameof(resolvedModelId));
            }

            var options = new SpeechRecognition.Options
            {
                Language = recognitionLanguage
            };

            return new SpeechRecognition(resolvedModelId, resolvedSampleRate, resolvedReporter, startImmediately: startModuleImmediately, options: options);
        }

        private void OnEnable()
        {
            processingCts = new CancellationTokenSource();
            droppedSegments = 0;
            lastDropLog = 0f;
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

        /// <summary>
        /// Starts module initialization when startModuleImmediately is disabled.
        /// </summary>
        public Task StartModuleInitializationAsync(CancellationToken cancellationToken = default)
        {
            if (Module == null && !TryLoadModule())
            {
                RaiseError("Failed to load offline speech recognizer module.");
                return Task.CompletedTask;
            }

            return Module?.StartInitialization(cancellationToken) ?? Task.CompletedTask;
        }

        public async Task<SpeechRecognition.TranscriptionResult> TranscribeClipAsync(AudioClip clip, CancellationToken cancellationToken = default)
        {
            if (clip == null)
            {
                throw new ArgumentNullException(nameof(clip));
            }

            if (!EnsureModuleReady(out var module))
            {
                return new SpeechRecognition.TranscriptionResult(SpeechRecognition.TranscriptionStatus.NotReady);
            }

            var data = new float[clip.samples * clip.channels];
            clip.GetData(data, 0);
            var mono = DownmixToMono(data, clip.channels);
            return await module.TranscribeAsync(mono, clip.frequency, cancellationToken).ConfigureAwait(false);
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
            EnqueueWithBackPressure(chunk);
        }

        private async Task DrainQueueAsync()
        {
            while (true)
            {
                if (processingCts != null && processingCts.IsCancellationRequested)
                {
                    ClearQueue();
                    return;
                }

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

                await TranscribeChunkAsync(chunk).ConfigureAwait(false);
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
                ClearQueue();
                return;
            }

            try
            {
                var token = processingCts?.Token ?? CancellationToken.None;
                if (token.IsCancellationRequested)
                {
                    return;
                }
                var result = await module.TranscribeAsync(chunk.Samples, chunk.SampleRate, token).ConfigureAwait(false);
                if (result.Status == SpeechRecognition.TranscriptionStatus.Success && !string.IsNullOrWhiteSpace(result.Text))
                {
                    DispatchToUnity(() => onTranscriptReady?.Invoke(result));
                }
                else if (result.Status == SpeechRecognition.TranscriptionStatus.Error && result.Error != null)
                {
                    SherpaLog.Error($"[{nameof(OfflineSpeechRecognizerComponent)}] Transcription failed: {result.Error.Message}");
                    var message = result.Error.Message;
                    DispatchToUnity(() => onTranscriptionFailed?.Invoke(message));
                    RaiseError(result.Error.Message);
                }
            }
            catch (OperationCanceledException)
            {
                // ignored
            }
            catch (Exception ex)
            {
                SherpaLog.Error($"[{nameof(OfflineSpeechRecognizerComponent)}] Transcription failed: {ex.Message}");
                var message = ex.Message;
                DispatchToUnity(() => onTranscriptionFailed?.Invoke(message));
                RaiseError(ex.Message);
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

        private void EnqueueWithBackPressure(AudioChunk chunk)
        {
            bool dropped = false;
            lock (queueLock)
            {
                if (maxPendingSegments > 0 && pendingSegments.Count >= maxPendingSegments)
                {
                    pendingSegments.Dequeue(); // drop oldest to bound latency
                    droppedSegments++;
                    dropped = true;
                }

                pendingSegments.Enqueue(chunk);
                if (!drainingQueue)
                {
                    drainingQueue = true;
                    _ = DrainQueueAsync();
                }
            }

            if (dropped)
            {
                MaybeLogDroppedSegments();
            }
        }

        private void MaybeLogDroppedSegments()
        {
            if (droppedSegments <= 0)
            {
                return;
            }

            var now = Time.realtimeSinceStartup;
            if (now - lastDropLog < 1f)
            {
                return;
            }

            SherpaLog.Warning($"[{nameof(OfflineSpeechRecognizerComponent)}] Dropped {droppedSegments} pending segment(s) due to back-pressure (maxPendingSegments={maxPendingSegments}).");
            lastDropLog = now;
            droppedSegments = 0;
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
