
namespace Eitan.SherpaONNXUnity.Runtime.Modules
{
    using System;
    using System.Buffers; // For ArrayPool
    using System.Threading;
    using System.Threading.Tasks;
    using Eitan.SherpaONNXUnity.Runtime.Utilities;
    using Eitan.SherpaONNXUnity.Runtime.Native;

    /// <summary>
    /// Detects speech segments from a real-time audio stream using high-performance, zero-GC techniques,
    /// correctly interfacing with an array-only API.
    /// </summary>
    public sealed class VoiceActivityDetection : SherpaONNXModule
    {
        public event Action<float[]> OnSpeechSegmentDetected;
        public event Action<bool> OnSpeakingStateChanged;

        #region VAD Parameters
        public float Threshold { get; set; } = 0.5F;
        public float MinSilenceDuration { get; set; } = 0.3F;
        public float MinSpeechDuration { get; set; } = 0.1F;
        public float MaxSpeechDuration { get; set; } = 30.0F;
        public float LeadingPaddingDuration { get; set; } = 0.2F;
        #endregion

        private VoiceActivityDetector _detector;
        private int _windowSize;
        private readonly SendOrPostCallback _speechSegmentDispatch;

        private readonly SendOrPostCallback _speakingStateDispatch;

        // --- Core Data Flow & State ---
        private readonly int _maxBufferedSeconds;
        private readonly bool _dropIfLagging;
        private BoundedSampleQueue _sampleQueue;
        private long _lastDropLogTicks;
        private int _droppedSinceLastLog;

        // Leading padding ring buffer (SPSC)
        private CircularBuffer<float> _paddingBuffer;
        // Reusable workspace buffers to avoid GC. Initialized once.
        private float[] _acceptWaveformWorkspace;
        private float[] _segmentWorkspace;

        private bool _isSpeaking;
        private int _silentFrames;
        private int _silenceThresholdFrames;

        public VoiceActivityDetection(string modelID, int sampleRate = 16000, SherpaONNXFeedbackReporter reporter = null, int maxBufferedSeconds = 5, bool dropIfLagging = true)
            : base(modelID, sampleRate, reporter)
        {
            _maxBufferedSeconds = Math.Max(1, maxBufferedSeconds);
            _dropIfLagging = dropIfLagging;
            _speechSegmentDispatch = CreateCallback<float[]>(segment =>
            {
                OnSpeechSegmentDetected?.Invoke(segment);
            });

            _speakingStateDispatch = CreateCallback<bool>(isSpeaking =>
            {
                OnSpeakingStateChanged?.Invoke(isSpeaking);
            });

            // Constructor is kept lean. All buffer initializations are deferred to Initialization,
            // as they depend on runtime parameters like sampleRate and windowSize.
        }

        protected override SherpaONNXModuleType ModuleType => SherpaONNXModuleType.VoiceActivityDetection;

        protected override async Task<bool> Initialization(SherpaONNXModelMetadata metadata, int sampleRate, bool isMobilePlatform, SherpaONNXFeedbackReporter reporter, CancellationToken ct)
        {
            try
            {
                var modelType = SherpaUtils.Model.GetVoiceActivityDetectionModelType(metadata.modelId);
                var vadConfig = CreateVadConfig(modelType, metadata, sampleRate, isMobilePlatform, reporter);

                _windowSize = GetWindowSize(modelType, vadConfig);
                _silenceThresholdFrames = (int)(MinSilenceDuration * sampleRate / _windowSize);

                // --- Initialize all buffers here, now that we have all parameters ---
                int paddingCapacity = MathUtils.NextPowerOfTwo(Math.Max(16, (int)(LeadingPaddingDuration * sampleRate)));
                _paddingBuffer = new CircularBuffer<float>(paddingCapacity, alignment: 1);

                _acceptWaveformWorkspace = new float[_windowSize];

                _segmentWorkspace = new float[sampleRate * 15]; // 15 seconds initial capacity
                int maxBuffered = Math.Max(sampleRate * _maxBufferedSeconds, _windowSize * 4);
                _sampleQueue = new BoundedSampleQueue(maxBuffered);

                var initialized = await runner.RunAsync<bool>(cancellationToken =>
                 {

                     using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, cancellationToken);
                     linkedCts.Token.ThrowIfCancellationRequested();

                     if (IsDisposed) { return Task.FromResult(false); }

                     _detector = new VoiceActivityDetector(vadConfig, 60);
                     var initialized = IsSuccessInitializad(_detector);
                     if (initialized)
                     {
                         reporter?.Report(new LoadFeedback(metadata, message: $"VAD model loaded successfully: {metadata.modelId}"));
                     }
                     return Task.FromResult(initialized);
                 });
                if (initialized)
                {
                    _ = runner.LoopAsync(ProcessAudioLoopIteration, TimeSpan.FromMilliseconds(10), null, ct);
                }
                return initialized;
            }
            catch (Exception ex)
            {
                reporter?.Report(new FailedFeedback(metadata, ex.Message, exception: ex));
                throw;
            }
        }

        public void StreamDetect(float[] samples)
        {
            if (IsDisposed || _detector == null || samples.Length == 0)
            {
                return;
            }

            var dropped = _sampleQueue?.Enqueue(samples) ?? 0;
            if (_dropIfLagging && dropped > 0)
            {
                var totalDropped = Interlocked.Add(ref _droppedSinceLastLog, dropped);
                var nowTicks = DateTime.UtcNow.Ticks;
                var last = Volatile.Read(ref _lastDropLogTicks);
                if (TimeSpan.FromTicks(nowTicks - last) >= TimeSpan.FromSeconds(1) &&
                    Interlocked.CompareExchange(ref _lastDropLogTicks, nowTicks, last) == last)
                {
                    var flushed = Interlocked.Exchange(ref _droppedSinceLastLog, 0);
                    SherpaLog.Warning($"[VAD] Dropped {flushed} stale samples to maintain bounded buffer ({_sampleQueue?.QueuedSamples ?? 0}/{_sampleQueue?.MaxSamples}).");
                }
            }
        }

        public async Task FlushAsync()
        {
            if (IsDisposed || _detector == null)
            {
                return;
            }

            await runner.RunAsync(ct =>
            {
                ProcessAudioQueue(flush: true);
                _detector.Flush();
                ProcessDetectedSegments();
                ResetSpeakingState();
            });
        }

        private Task ProcessAudioLoopIteration(CancellationToken token)
        {
            if (token.IsCancellationRequested || IsDisposed || _detector == null)
            {
                return Task.CompletedTask;
            }

            ProcessAudioQueue(flush: false);
            return Task.CompletedTask;
        }

        private void ProcessAudioQueue(bool flush)
        {
            float[] chunkBuffer = ArrayPool<float>.Shared.Rent(_windowSize);
            try
            {
                while (_sampleQueue != null && _sampleQueue.QueuedSamples >= _windowSize)
                {
                    int read = _sampleQueue.DequeueInto(chunkBuffer.AsSpan(0, _windowSize));
                    if (read < _windowSize)
                    {
                        break;
                    }
                    ProcessChunk(chunkBuffer.AsSpan(0, _windowSize));
                }

                if (flush && _sampleQueue != null && _sampleQueue.QueuedSamples > 0)
                {
                    int remaining = Math.Min(_windowSize, _sampleQueue.QueuedSamples);
                    int read = _sampleQueue.DequeueInto(chunkBuffer.AsSpan(0, remaining));
                    if (read > 0)
                    {
                        ProcessChunk(chunkBuffer.AsSpan(0, read));
                    }
                }
            }
            finally
            {
                ArrayPool<float>.Shared.Return(chunkBuffer);
            }
        }
        private void ProcessChunk(ReadOnlySpan<float> chunk)
        {

            if (!_detector.IsSpeechDetected())
            {
                _paddingBuffer.Write(chunk);
            }

            // --- CORRECTED API CALL: Use the pre-allocated workspace to avoid GC ---
            if (chunk.Length == _windowSize)
            {
                chunk.CopyTo(_acceptWaveformWorkspace);

                _detector.AcceptWaveform(_acceptWaveformWorkspace);
            }
            else
            {
                // This path is for the smaller, final chunk during a flush.
                // ToArray() is acceptable here as it's an infrequent operation.

                _detector.AcceptWaveform(chunk.ToArray());
            }

            ProcessDetectedSegments();
            UpdateSpeakingState();
        }

        private void ProcessDetectedSegments()
        {

            while (!_detector.IsEmpty())
            {
                var segment = _detector.Front();
                var segmentSamples = segment.Samples;

                _paddingBuffer.GetSpans(out var padding1, out var padding2);
                int totalSamples = padding1.Length + padding2.Length + segmentSamples.Length;

                if (_segmentWorkspace.Length < totalSamples)
                {
                    _segmentWorkspace = new float[MathUtils.NextPowerOfTwo(totalSamples)];
                }

                // --- Zero-copy segment assembly using Spans ---
                var workspaceSpan = _segmentWorkspace.AsSpan();
                padding1.CopyTo(workspaceSpan);
                padding2.CopyTo(workspaceSpan.Slice(padding1.Length));
                segmentSamples.CopyTo(workspaceSpan.Slice(padding1.Length + padding2.Length));

                // Create final, perfectly-sized array for the event. This is the only required allocation.
                var finalSegment = workspaceSpan.Slice(0, totalSamples).ToArray();
                DispatchSpeechSegment(finalSegment);

                _detector.Pop();
                _paddingBuffer.Clear();
            }
        }

        private void UpdateSpeakingState()
        {
            bool detectedSpeaking = _detector.IsSpeechDetected();

            if (!detectedSpeaking && _isSpeaking)
            {
                _silentFrames++;
                if (_silentFrames < _silenceThresholdFrames)
                {
                    detectedSpeaking = true;
                }

            }
            else
            {
                _silentFrames = 0;
            }

            if (detectedSpeaking != _isSpeaking)
            {
                _isSpeaking = detectedSpeaking;

                ExecuteOnMainThread(_ =>
                {
                    OnSpeakingStateChanged?.Invoke(_isSpeaking);
                });
            }
        }

        private void DispatchSpeechSegment(float[] segment)
        {
            if (segment == null)
            {
                return;
            }

            ExecuteOnMainThread(_speechSegmentDispatch, segment);
        }

        private void ResetSpeakingState()
        {
            if (_isSpeaking)
            {
                _isSpeaking = false;
                ExecuteOnMainThread(_speakingStateDispatch, _isSpeaking);
            }
            _silentFrames = 0;

            _paddingBuffer?.Clear();
            _sampleQueue?.Clear();
        }

        protected override void OnDestroy()
        {
            _detector?.Dispose();
            _detector = null;
            _sampleQueue?.Dispose();
            _sampleQueue = null;
        }

        #region Configuration & Helpers

        private VadModelConfig CreateVadConfig(VoiceActivityDetectionModelType modelType, SherpaONNXModelMetadata metadata, int sampleRate, bool isMobilePlatform, SherpaONNXFeedbackReporter reporter)
        {
            var fallbackReporter = CreateFallbackReporter(metadata, reporter);
            var vadModelConfig = new VadModelConfig { SampleRate = sampleRate, NumThreads = ThreadingUtils.GetAdaptiveThreadCount() };
            var int8QuantKeyword = isMobilePlatform ? "int8" : null;

            switch (modelType)
            {
                case VoiceActivityDetectionModelType.SileroVad:
                    vadModelConfig.SileroVad.Model = ModelFileResolver.ResolveRequiredFile(
                        metadata,
                        "Silero VAD model",
                        fallbackReporter,
                        ModelFileCriteria.FromKeywords("silero", int8QuantKeyword),
                        ModelFileCriteria.FromKeywords("silero"));
                    vadModelConfig.SileroVad.Threshold = Threshold;
                    vadModelConfig.SileroVad.MinSilenceDuration = MinSilenceDuration;
                    vadModelConfig.SileroVad.MinSpeechDuration = MinSpeechDuration;
                    vadModelConfig.SileroVad.MaxSpeechDuration = MaxSpeechDuration;
                    vadModelConfig.SileroVad.WindowSize = 512;
                    break;
                case VoiceActivityDetectionModelType.TenVad:
                    vadModelConfig.TenVad.Model = ModelFileResolver.ResolveRequiredFile(
                        metadata,
                        "Ten VAD model",
                        fallbackReporter,
                        ModelFileCriteria.FromKeywords("ten", int8QuantKeyword),
                        ModelFileCriteria.FromKeywords("ten"));
                    vadModelConfig.TenVad.Threshold = Threshold;
                    vadModelConfig.TenVad.MinSilenceDuration = MinSilenceDuration;
                    vadModelConfig.TenVad.MinSpeechDuration = MinSpeechDuration;
                    vadModelConfig.TenVad.MaxSpeechDuration = MaxSpeechDuration;
                    vadModelConfig.TenVad.WindowSize = 256;
                    break;
                default:
                    throw new NotSupportedException($"Unsupported VAD model type: {modelType}");
            }
            return vadModelConfig;
        }

        private int GetWindowSize(VoiceActivityDetectionModelType modelType, VadModelConfig config)
        {
            switch (modelType)
            {
                case VoiceActivityDetectionModelType.SileroVad: return config.SileroVad.WindowSize;
                case VoiceActivityDetectionModelType.TenVad: return config.TenVad.WindowSize;
                default: throw new NotSupportedException($"Unsupported VAD model type: {modelType}");
            }
        }

        #endregion
    }
}
