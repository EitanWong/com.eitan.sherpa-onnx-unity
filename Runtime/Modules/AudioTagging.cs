
namespace Eitan.SherpaONNXUnity.Runtime.Modules
{

    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Runtime.CompilerServices;
    using Eitan.SherpaONNXUnity.Runtime.Native;
    using Eitan.SherpaONNXUnity.Runtime.Utilities;
    /// <summary>
    /// High-level Unity wrapper for sherpa-onnx audio tagging.
    /// Provides both non-streaming (one-shot) and streaming (buffered) APIs.
    /// </summary>
    public class AudioTagging : SherpaONNXModule
    {
        private Eitan.SherpaONNXUnity.Runtime.Native.AudioTagging _tagger;
        private readonly object _lockObject = new object();

        private const float kWindowSeconds = 1.0f;
        private const float kHopSeconds = 0.5f;

        // --------------------------- Streaming state ---------------------------
        private CircularBuffer<float> _streamBuffer;
        private int _streamSampleRate;
        private int _windowSamples;
        private int _hopSamples;
        private float[] _windowWorkspace;
        private bool _streamingReady;
        private long _samplesSinceLastEmit;
        private bool _hasEmittedWindow;

        /// <summary>
        /// 轻量结果包装（比直接暴露 Native.AudioEvent 更友好）。
        /// </summary>
        public readonly struct AudioTag
        {
            public readonly string Label;
            public readonly int Index;
            public readonly float Probability;
            public AudioTag(string label, int index, float probability)
            { Label = label ?? string.Empty; Index = index; Probability = probability; }
            public override string ToString() => string.IsNullOrEmpty(Label) ? $"#{Index} ({Probability:P1})" : $"{Label} ({Probability:P1})";
            public void Deconstruct(out string label, out float probability) { label = Label; probability = Probability; }
        }

        private static AudioTag[] Wrap(AudioEvent[] events)
        {
            if (events == null || events.Length == 0)
            {
                return Array.Empty<AudioTag>();
            }


            var result = new AudioTag[events.Length];
            for (int i = 0; i < events.Length; i++)
            {
                var e = events[i];
                result[i] = new AudioTag(e?.Name, e?.Index ?? -1, e?.Prob ?? 0f);
            }
            return result;
        }

        /// <summary>默认返回 TopK。</summary>
        public int DefaultTopK = 5;
        protected override SherpaONNXModuleType ModuleType => SherpaONNXModuleType.AudioTagging;

        public AudioTagging(string modelID, int sampleRate = 16000, SherpaONNXFeedbackReporter reporter = null)
            : base(modelID, sampleRate, reporter)
        {
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void EnsureStreamingInitialized()
        {
            if (_streamingReady)
            {
                return;
            }
            if (_streamSampleRate <= 0)
            {
                throw new Exception("Stream sampling rate must be greater than 0");
            }

            _windowSamples = Math.Max(1, (int)MathF.Ceiling(_streamSampleRate * kWindowSeconds));
            _hopSamples = Math.Max(1, (int)MathF.Ceiling(_streamSampleRate * kHopSeconds));
            int capacity = _windowSamples + _hopSamples;
            _streamBuffer = new CircularBuffer<float>(capacity, alignment: 1);
            _windowWorkspace = new float[_windowSamples];
            _samplesSinceLastEmit = 0;
            _hasEmittedWindow = false;
            _streamingReady = true;
        }

        // ------------------------------- Streaming API -------------------------------
        /// <summary>
        /// Unified streaming API: feed samples and, if enough audio has been accumulated,
        /// perform one tagging compute and return results. Otherwise returns an empty array.
        /// </summary>
        public async Task<AudioTag[]> TagStreamAsync(float[] samples,
                                                     int topK = -1,
                                                     CancellationToken cancellationToken = default)
        {
            if (IsDisposed || runner.IsDisposed || _tagger == null)
            {
                return Array.Empty<AudioTag>();
            }

            EnsureStreamingInitialized();

            int offset = 0;
            AudioTag[] emitted = null;

            while (true)
            {
                bool wrote = false;

                if (samples != null && offset < samples.Length)
                {
                    int written = AppendWithCapacity(samples.AsSpan(offset));
                    if (written > 0)
                    {
                        offset += written;
                        wrote = true;
                    }
                }

                if (!wrote && emitted == null && CanEmitWindow())
                {
                    emitted = await EmitWindowAsync(topK, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                if (!wrote)
                {
                    break;
                }
            }

            return emitted ?? Array.Empty<AudioTag>();
        }

        private int AppendWithCapacity(ReadOnlySpan<float> samples)
        {
            if (samples.IsEmpty)
            {
                return 0;
            }

            int readable = _streamBuffer.ReadableCount;
            int writable = _streamBuffer.Capacity - readable;
            if (writable <= 0)
            {
                return 0;
            }

            int toWrite = Math.Min(writable, samples.Length);
            int written = _streamBuffer.Write(samples.Slice(0, toWrite));
            _samplesSinceLastEmit += written;
            return written;
        }

        private bool CanEmitWindow()
        {
            if (_streamBuffer.ReadableCount < _windowSamples)
            {
                return false;
            }

            long required = _hasEmittedWindow ? _hopSamples : _windowSamples;
            return _samplesSinceLastEmit >= required;
        }

        private async Task<AudioTag[]> EmitWindowAsync(int topK, CancellationToken cancellationToken)
        {
            int copied = _streamBuffer.Peek(_windowWorkspace);
            if (copied < _windowSamples)
            {
                return Array.Empty<AudioTag>();
            }

            var tags = await ComputeTagsAsync(_windowWorkspace, _streamSampleRate, topK, cancellationToken).ConfigureAwait(false);

            int skip = Math.Min(_hopSamples, _streamBuffer.ReadableCount);
            if (skip > 0)
            {
                _streamBuffer.Skip(skip);
            }

            long consumed = _hasEmittedWindow ? _hopSamples : _windowSamples;
            if (_samplesSinceLastEmit >= consumed)
            {
                _samplesSinceLastEmit -= consumed;
            }
            else
            {
                _samplesSinceLastEmit = 0;
            }

            _hasEmittedWindow = true;
            return tags;
        }

        /// <summary>Clear streaming buffer (useful when switching sources).</summary>
        public void ClearStreamingBuffer()
        {
            if (!_streamingReady)
            {
                return;
            }

            _streamBuffer.Clear();
            _samplesSinceLastEmit = 0;
            _hasEmittedWindow = false;
        }

        // ------------------------------- Non-streaming API -------------------------------
        /// <summary>一次性对整段 PCM 做打标（离线）。</summary>
        public async Task<AudioTag[]> TagAsync(float[] audioSamples,
                                               int sampleRate,
                                               int topK = -1,
                                               CancellationToken cancellationToken = default)
        {
            if (IsDisposed || runner.IsDisposed || _tagger == null || audioSamples == null || audioSamples.Length == 0)
            {

                return Array.Empty<AudioTag>();
            }


            return await ComputeTagsAsync(audioSamples, sampleRate, topK, cancellationToken);
        }

        // ------------------------------- Core compute path -------------------------------
        private Task<AudioTag[]> ComputeTagsAsync(float[] audioSamples,
                                                  int sampleRate,
                                                  int topK,
                                                  CancellationToken cancellationToken)
        {
            return runner.RunAsync<AudioTag[]>(ct =>
            {
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, cancellationToken);
                if (IsDisposed || _tagger == null)
                {
                    return Task.FromResult(Array.Empty<AudioTag>());
                }


                lock (_lockObject)
                {
                    if (IsDisposed || _tagger == null)
                    {
                        return Task.FromResult(Array.Empty<AudioTag>());
                    }


                    using var stream = _tagger.CreateStream();
                    stream.AcceptWaveform(sampleRate, audioSamples);
                    var k = topK <= 0 ? DefaultTopK : topK;
                    var events = _tagger.Compute(stream, k);
                    return Task.FromResult(Wrap(events));
                }
            });
        }

        // ------------------------------- Module lifecycle -------------------------------
        protected override async Task<bool> Initialization(
           SherpaONNXModelMetadata metadata,
           int sampleRate,
           bool isMobilePlatform,
           SherpaONNXFeedbackReporter reporter,
           CancellationToken ct)
        {
            try
            {
                reporter?.Report(new LoadFeedback(metadata, message: $"Start Loading (AudioTagging): {metadata.modelId}"));
                _streamSampleRate = sampleRate;
                var config = CreateAudioTaggingConfig(metadata, isMobilePlatform, reporter);
                return await runner.RunAsync<bool>(cancellationToken =>
                {
                    using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, cancellationToken);
                    linked.Token.ThrowIfCancellationRequested();
                    if (IsDisposed)
                    {
                        return Task.FromResult(false);
                    }
                    _tagger = new Eitan.SherpaONNXUnity.Runtime.Native.AudioTagging(config);
                    var initialized = IsSuccessInitializad(_tagger);
                    if (initialized)
                    {
                        reporter?.Report(new LoadFeedback(metadata, message: $"Loaded audio tagging model: {metadata.modelId}"));
                    }


                    return Task.FromResult(initialized);
                });
            }
            catch (Exception ex)
            {
                reporter?.Report(new FailedFeedback(metadata, ex.Message, exception: ex));
                throw;
            }
        }

        private AudioTaggingConfig CreateAudioTaggingConfig(
            SherpaONNXModelMetadata metadata,
            bool isMobilePlatform,
            SherpaONNXFeedbackReporter reporter)
        {
            var _modelType = SherpaUtils.Model.GetAudioTaggingModelType(metadata.modelId);
            var fallbackReporter = CreateFallbackReporter(metadata, reporter);
            var threadCount = ThreadingUtils.GetAdaptiveThreadCount();
            var preferInt8 = isMobilePlatform ? "int8" : null;

            var labelsPath = ModelFileResolver.ResolveRequiredByKeywords(
                metadata,
                description: "labels file",
                fallbackReporter,
                new[] { "class_labels_indices.csv", "labels" }
            );

            var cfg = new AudioTaggingConfig
            {
                Model = new AudioTaggingModelConfig
                {
                    NumThreads = threadCount,
                    Debug = 0,
                    Provider = "cpu",
                },
                Labels = labelsPath,
                TopK = DefaultTopK
            };

            switch (_modelType)
            {
                case AudioTaggingModelType.Ced:
                    cfg.Model.CED = ModelFileResolver.ResolveOptionalByKeywords(
                        metadata,
                        fallbackReporter,
                        new[] { "ced", "model", preferInt8, ".onnx" }
                    );
                    break;
                case AudioTaggingModelType.Zipformer:
                    cfg.Model.Zipformer.Model = ModelFileResolver.ResolveOptionalByKeywords(
                            metadata,
                            fallbackReporter,
                            new[] { "zipformer", "model", preferInt8, ".onnx" });
                    break;
                default:
                    throw new NotSupportedException($"Unsupported audio-tagging model type: {_modelType}");
            }

            return cfg;
        }

        protected override void OnDestroy()
        {
            lock (_lockObject)
            {
                _tagger?.Dispose();
                _tagger = null;
            }
        }
    }
}
