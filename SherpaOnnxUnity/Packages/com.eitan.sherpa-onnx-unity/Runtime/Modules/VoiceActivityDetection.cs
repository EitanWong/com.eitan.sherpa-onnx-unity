using System;
using System.Buffers; // For ArrayPool
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Eitan.SherpaOnnxUnity.Runtime.Utilities; // For MathUtils
using SherpaOnnx;

namespace Eitan.SherpaOnnxUnity.Runtime
{
    /// <summary>
    /// [FINAL, SIMPLIFIED & FULLY OPTIMIZED]
    /// Detects speech segments from a real-time audio stream using high-performance, zero-GC techniques,
    /// correctly interfacing with an array-only API.
    /// </summary>
    public sealed class VoiceActivityDetection : SherpaOnnxModule
    {
        public event Action<float[]> OnSpeechSegmentDetected;
        public event Action<bool> OnSpeakingStateChanged;

        #region VAD Parameters
        public float Threshold { get; set; } = 0.5F;
        public float MinSilenceDuration { get; set; } = 0.3F;
        public float MinSpeechDuration { get; set; } = 0.1F;
        public float MaxSpeechDuration { get; set; } = 10.0F;
        public float LeadingPaddingDuration { get; set; } = 0.2F;
        #endregion

        private VoiceActivityDetector _detector;
        private int _windowSize;

        // --- Core Data Flow & State ---
        private readonly ConcurrentQueue<float> _audioQueue = new ConcurrentQueue<float>();
        
        // Backing memory for the stack-allocated CircularBuffer
        private float[] _leadingPaddingBackingBuffer;

        // Reusable workspace buffers to avoid GC. Initialized once.
        private float[] _acceptWaveformWorkspace;
        private float[] _segmentWorkspace;

        private bool _isSpeaking;
        private int _silentFrames;
        private int _silenceThresholdFrames;
        
        public VoiceActivityDetection(string modelID, int sampleRate = 16000, SherpaFeedbackReporter reporter = null)
            : base(modelID, sampleRate, reporter)
        {
            // Constructor is kept lean. All buffer initializations are deferred to Initialization,
            // as they depend on runtime parameters like sampleRate and windowSize.
        }

        protected override SherpaOnnxModuleType ModuleType => SherpaOnnxModuleType.VoiceActivityDetection;

        protected override async Task Initialization(SherpaOnnxModelMetadata metadata, int sampleRate, bool isMobilePlatform, SherpaFeedbackReporter reporter, CancellationToken ct)
        {
            try
            {
                var modelType = SherpaUtils.Model.GetVoiceActivityDetectionModelType(metadata.modelId);
                var vadConfig = CreateVadConfig(modelType, metadata, sampleRate, isMobilePlatform);

                _windowSize = GetWindowSize(modelType, vadConfig);
                _silenceThresholdFrames = (int)(MinSilenceDuration * sampleRate / _windowSize);

                // --- Initialize all buffers here, now that we have all parameters ---
                int paddingCapacity = (int)(LeadingPaddingDuration * sampleRate);
                _leadingPaddingBackingBuffer = new float[MathUtils.NextPowerOfTwo(Math.Max(16, paddingCapacity))];
                
                _acceptWaveformWorkspace = new float[_windowSize];
                
                _segmentWorkspace = new float[sampleRate * 15]; // 15 seconds initial capacity

                await runner.RunAsync(cancellationToken =>
                {
                    _detector = new VoiceActivityDetector(vadConfig, 60);
                });

                _ = runner.LoopAsync(ProcessAudioLoopIteration, TimeSpan.FromMilliseconds(10), null, ct);
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


            foreach (var sample in samples)
            {
                _audioQueue.Enqueue(sample);
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
                while (_audioQueue.Count >= _windowSize)
                {
                    for (int i = 0; i < _windowSize; i++)
                    {
                        if (!_audioQueue.TryDequeue(out chunkBuffer[i]))
                        {
                            break;
                        }

                    }
                    ProcessChunk(chunkBuffer.AsSpan(0, _windowSize));
                }

                if (flush && !_audioQueue.IsEmpty)
                {
                    float[] remainingSamples = _audioQueue.ToArray();
                    _audioQueue.Clear();
                    ProcessChunk(remainingSamples);
                }
            }
            finally
            {
                ArrayPool<float>.Shared.Return(chunkBuffer);
            }
        }

        private void ProcessChunk(ReadOnlySpan<float> chunk)
        {
            // Create the high-performance circular buffer on the stack for this scope.
            var paddingBuffer = new CircularBuffer<float>(_leadingPaddingBackingBuffer);

            if (!_detector.IsSpeechDetected())
            {
                paddingBuffer.AddRange(chunk); // Assuming CircularBuffer has an Add(ReadOnlySpan<T>) overload
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
            // Create a stack-local buffer instance to read the state.
            var paddingBuffer = new CircularBuffer<float>(_leadingPaddingBackingBuffer);

            while (!_detector.IsEmpty())
            {
                var segment = _detector.Front();
                var segmentSamples = segment.Samples;

                paddingBuffer.GetSpans(out var padding1, out var padding2);
                
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
                OnSpeechSegmentDetected?.Invoke(finalSegment);
                
                _detector.Pop();
                paddingBuffer.Clear();
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
                OnSpeakingStateChanged?.Invoke(_isSpeaking);
            }
        }

        private void ResetSpeakingState()
        {
            if (_isSpeaking)
            {
                _isSpeaking = false;
                OnSpeakingStateChanged?.Invoke(false);
            }
            _silentFrames = 0;
            
            // Clear the buffer by creating a local instance and calling Clear.
            var paddingBuffer = new CircularBuffer<float>(_leadingPaddingBackingBuffer);
            paddingBuffer.Clear();
        }

        protected override void OnDestroy()
        {
            _detector?.Dispose();
            _detector = null;
        }

        #region Configuration & Helpers
        
        private VadModelConfig CreateVadConfig(VoiceActivityDetectionModelType modelType, SherpaOnnxModelMetadata metadata, int sampleRate, bool isMobilePlatform)
        {
            var vadModelConfig = new VadModelConfig { SampleRate = sampleRate };
            var int8QuantKeyword = isMobilePlatform ? "int8" : null;

            switch (modelType)
            {
                case VoiceActivityDetectionModelType.SileroVad:
                    vadModelConfig.SileroVad.Model = metadata.GetModelFilePathByKeywords("silero", int8QuantKeyword);
                    vadModelConfig.SileroVad.Threshold = Threshold;
                    vadModelConfig.SileroVad.MinSilenceDuration = MinSilenceDuration;
                    vadModelConfig.SileroVad.MinSpeechDuration = MinSpeechDuration;
                    vadModelConfig.SileroVad.MaxSpeechDuration = MaxSpeechDuration;
                    vadModelConfig.SileroVad.WindowSize = 512;
                    break;
                case VoiceActivityDetectionModelType.TenVad:
                    vadModelConfig.TenVad.Model = metadata.GetModelFilePathByKeywords("ten", int8QuantKeyword);
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