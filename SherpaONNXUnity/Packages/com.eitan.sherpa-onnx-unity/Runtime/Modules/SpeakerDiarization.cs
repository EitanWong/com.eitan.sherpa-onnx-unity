namespace Eitan.SherpaONNXUnity.Runtime.Modules
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using UnityEngine;
    using Eitan.SherpaONNXUnity.Runtime;
    using Eitan.SherpaONNXUnity.Runtime.Native;
    using Eitan.SherpaONNXUnity.Runtime.Utilities;

    public sealed class SpeakerDiarization : SherpaONNXModule
    {
        public sealed class Options
        {
            public float MinDurationOn { get; set; } = 0.3f;
            public float MinDurationOff { get; set; } = 0.5f;
            public int NumClusters { get; set; } = -1;
            public float ClusteringThreshold { get; set; } = 0.5f;
        }

        public readonly struct DiarizationSegment
        {
            public DiarizationSegment(float start, float end, int speaker)
            {
                Start = start;
                End = end;
                Speaker = speaker;
            }

            public float Start { get; }
            public float End { get; }
            public int Speaker { get; }
            public float Duration => End - Start;

            public override string ToString() => $"Speaker {Speaker}: {Start:F2}s - {End:F2}s";
        }

        private OfflineSpeakerDiarization _diarization;
        private readonly object _lockObject = new object();
        private readonly string _embeddingModelId;
        private Options _options;
        private OfflineSpeakerDiarizationConfig _config;
        private bool _hasConfig;
        private int _sampleRate;
        private SpeakerDiarizationModelType _modelType;

        public SpeakerDiarization(
            string segmentationModelId,
            string embeddingModelId,
            SherpaONNXFeedbackReporter reporter = null,
            bool startImmediately = true,
            int maxConcurrentTasks = 0,
            Options options = null)
            : base(segmentationModelId, -1, reporter, startImmediately, maxConcurrentTasks)
        {
            if (string.IsNullOrWhiteSpace(embeddingModelId))
            {
                throw new ArgumentNullException(nameof(embeddingModelId));
            }

            _embeddingModelId = embeddingModelId;
            _options = SanitizeOptions(options);
        }

        protected override SherpaONNXModuleType ModuleType => SherpaONNXModuleType.SpeakerDiarization;

        public int SampleRate => _diarization?.SampleRate ?? _sampleRate;

        public string EmbeddingModelId => _embeddingModelId;

        public SpeakerDiarizationModelType ModelType => _modelType;

        public Options CurrentOptions
        {
            get
            {
                lock (_lockObject)
                {
                    return CloneOptions(_options);
                }
            }
        }

        protected override async Task<bool> Initialization(
            SherpaONNXModelMetadata metadata,
            int sampleRate,
            bool isMobilePlatform,
            SherpaONNXFeedbackReporter reporter,
            CancellationToken ct)
        {
            try
            {
                var segmentationModelMetadata = metadata;
                var embeddingModelMetadata = await SherpaONNXModelRegistry.Instance.GetMetadataAsync(_embeddingModelId, ct).ConfigureAwait(false);

                reporter?.Report(new LoadFeedback(segmentationModelMetadata, message: $"Start Loading: {segmentationModelMetadata.modelId}"));
                reporter?.Report(new LoadFeedback(embeddingModelMetadata, message: $"Start Loading: {embeddingModelMetadata.modelId}"));
                TryReportAndroid32BitRuntimeRisk(segmentationModelMetadata, reporter, "SpeakerDiarization");
                TryReportAndroid32BitRuntimeRisk(embeddingModelMetadata, reporter, "SpeakerEmbedding");

                var embeddingPrepareResult = await SherpaUtils.Prepare.PrepareAndLoadModelWithResultAsync(embeddingModelMetadata, reporter, ct).ConfigureAwait(false);
                if (!embeddingPrepareResult.Success)
                {
                    if (embeddingPrepareResult.ErrorCode == PrepareErrorCode.Cancelled)
                    {
                        throw new OperationCanceledException("Embedding model preparation canceled.", ct);
                    }

                    throw new InvalidOperationException(
                        $"Embedding model {embeddingModelMetadata.modelId} initialization failed ({embeddingPrepareResult.ErrorCode})\nplease download from url:{embeddingModelMetadata.downloadUrl}\nthen uncompress it to {GetManualInstallTarget(embeddingModelMetadata.modelId)} manually.");
                }

                _modelType = SherpaUtils.Model.ResolveSpeakerDiarizationModelType(segmentationModelMetadata);
                var config = CreateSpeakerDiarizationConfig(segmentationModelMetadata, embeddingModelMetadata, isMobilePlatform, reporter);

                return await runner.RunAsync<bool>(cancellationToken =>
                {
                    try
                    {
                        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, cancellationToken);
                        linkedCts.Token.ThrowIfCancellationRequested();

                        if (IsDisposed)
                        {
                            return Task.FromResult(false);
                        }

                        reporter?.Report(new LoadFeedback(segmentationModelMetadata, message: $"Loading Speaker Diarization model: {segmentationModelMetadata.modelId}"));

                        lock (_lockObject)
                        {
                            if (IsDisposed)
                            {
                                return Task.FromResult(false);
                            }

                            _diarization = new OfflineSpeakerDiarization(config);
                            _config = config;
                            _hasConfig = true;
                            _sampleRate = _diarization?.SampleRate ?? segmentationModelMetadata.sampleRate;
                        }

                        var initialized = IsSuccessInitializad(_diarization);
                        if (initialized)
                        {
                            reporter?.Report(new LoadFeedback(segmentationModelMetadata, message: $"Speaker Diarization model loaded successfully: {segmentationModelMetadata.modelId}"));
                        }

                        return Task.FromResult(initialized);
                    }
                    catch (Exception ex)
                    {
                        reporter?.Report(new FailedFeedback(segmentationModelMetadata, message: ex.Message, exception: ex));
                        throw;
                    }
                });
            }
            catch (Exception ex)
            {
                reporter?.Report(new FailedFeedback(metadata, ex.Message, exception: ex));
                throw;
            }
        }

        public void UpdateOptions(Options options)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            lock (_lockObject)
            {
                _options = SanitizeOptions(options);
                if (!_hasConfig || _diarization == null || IsDisposed)
                {
                    return;
                }

                ApplyRuntimeOptions(ref _config, _options);
                _diarization.SetConfig(_config);
            }
        }

        public async Task<DiarizationSegment[]> DiarizeAsync(float[] samples, CancellationToken cancellationToken = default)
        {
            ValidateSamples(samples, nameof(samples));
            ThrowIfNotReady();
            return await ProcessAsync(samples, cancellationToken).ConfigureAwait(false);
        }

        public async Task<DiarizationSegment[]> DiarizeAsync(float[] samples, int sampleRate, CancellationToken cancellationToken = default)
        {
            ValidateSamples(samples, nameof(samples));
            ValidateSampleRate(sampleRate);
            ThrowIfNotReady();
            return await ProcessAsync(samples, cancellationToken).ConfigureAwait(false);
        }

        public async Task<DiarizationSegment[]> DiarizeAsync(AudioClip clip, CancellationToken cancellationToken = default)
        {
            if (clip == null)
            {
                throw new ArgumentNullException(nameof(clip));
            }

            var expectedSampleRate = SampleRate;
            if (expectedSampleRate > 0 && clip.frequency > 0 && clip.frequency != expectedSampleRate)
            {
                throw new ArgumentException(
                    $"AudioClip sample rate mismatch. Expected {expectedSampleRate} Hz but received {clip.frequency} Hz.",
                    nameof(clip));
            }

            var interleaved = new float[clip.samples * clip.channels];
            clip.GetData(interleaved, 0);

            var mono = clip.channels > 1 ? DownmixToMono(interleaved, clip.channels) : interleaved;
            return await DiarizeAsync(mono, cancellationToken).ConfigureAwait(false);
        }

        private Task<DiarizationSegment[]> ProcessAsync(float[] samples, CancellationToken cancellationToken)
        {
            return runner.RunAsync<DiarizationSegment[]>(ct =>
            {
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, cancellationToken);
                linked.Token.ThrowIfCancellationRequested();

                if (IsDisposed || _diarization == null)
                {
                    return Task.FromResult(Array.Empty<DiarizationSegment>());
                }

                lock (_lockObject)
                {
                    linked.Token.ThrowIfCancellationRequested();

                    if (IsDisposed || _diarization == null)
                    {
                        return Task.FromResult(Array.Empty<DiarizationSegment>());
                    }

                    var result = _diarization.Process(samples);
                    return Task.FromResult(Wrap(result));
                }
            }, cancellationToken: cancellationToken, policy: ExecutionPolicy.Auto);
        }

        private OfflineSpeakerDiarizationConfig CreateSpeakerDiarizationConfig(
            SherpaONNXModelMetadata segmentationModelMetadata,
            SherpaONNXModelMetadata embeddingModelMetadata,
            bool isMobilePlatform,
            SherpaONNXFeedbackReporter reporter)
        {
            var segmentationFallbackReporter = CreateFallbackReporter(segmentationModelMetadata, reporter);
            var embeddingFallbackReporter = CreateFallbackReporter(embeddingModelMetadata, reporter);
            var threadCount = ThreadingUtils.GetAdaptiveThreadCount();
            var int8QuantKeyword = isMobilePlatform ? "int8" : null;
            var config = new OfflineSpeakerDiarizationConfig();
            var effectiveModelType = _modelType != SpeakerDiarizationModelType.None
                ? _modelType
                : SherpaUtils.Model.ResolveSpeakerDiarizationModelType(segmentationModelMetadata);

            if (effectiveModelType != _modelType)
            {
                _modelType = effectiveModelType;
            }

            switch (effectiveModelType)
            {
                case SpeakerDiarizationModelType.Pyannote:
                    config.Segmentation.Pyannote.Model = ResolvePyannoteSegmentationModelPath(
                        segmentationModelMetadata,
                        segmentationFallbackReporter,
                        int8QuantKeyword);
                    break;
                default:
                    throw new NotSupportedException($"Unsupported speaker diarization model type: {effectiveModelType}");
            }

            config.Segmentation.NumThreads = threadCount;
            config.Embedding.Model = ModelFileResolver.ResolveRequiredFileWithBindings(
                embeddingModelMetadata,
                "Embedding model",
                embeddingFallbackReporter,
                new[] { SherpaONNXModelFileKey.Embedding, SherpaONNXModelFileKey.Model },
                ModelFileCriteria.FromKeywords("model", int8QuantKeyword),
                ModelFileCriteria.FromKeywords("embedding", int8QuantKeyword),
                ModelFileCriteria.FromKeywords("model"),
                ModelFileCriteria.FromKeywords("embedding"),
                ModelFileCriteria.FromExtensions(".onnx"));
            config.Embedding.NumThreads = threadCount;

            ApplyRuntimeOptions(ref config, _options);
            return config;
        }

        private static string ResolvePyannoteSegmentationModelPath(
            SherpaONNXModelMetadata metadata,
            Action<string> fallbackReporter,
            string int8QuantKeyword)
        {
            try
            {
                return ModelFileResolver.ResolveRequiredFileWithBindings(
                    metadata,
                    "Pyannote model",
                    fallbackReporter,
                    new[] { SherpaONNXModelFileKey.Model },
                    ModelFileCriteria.FromKeywords("model", int8QuantKeyword),
                    ModelFileCriteria.FromKeywords("segmentation", int8QuantKeyword),
                    ModelFileCriteria.FromKeywords("model"),
                    ModelFileCriteria.FromKeywords("segmentation"),
                    ModelFileCriteria.FromExtensions(".onnx"));
            }
            catch (InvalidOperationException)
            {
                var fallback = TryResolvePyannoteOnnxByDirectory(metadata, int8QuantKeyword);
                if (!string.IsNullOrWhiteSpace(fallback))
                {
                    fallbackReporter?.Invoke($"Resolved Pyannote model via direct directory scan: {fallback}");
                    return fallback;
                }

                throw;
            }
        }

        private static string TryResolvePyannoteOnnxByDirectory(SherpaONNXModelMetadata metadata, string int8QuantKeyword)
        {
            if (metadata == null || string.IsNullOrWhiteSpace(metadata.modelId))
            {
                return string.Empty;
            }

            string modelRootPath;
            try
            {
                modelRootPath = SherpaPathResolver.GetModelRootPath(metadata.modelId);
            }
            catch
            {
                return string.Empty;
            }

            if (string.IsNullOrWhiteSpace(modelRootPath) || !Directory.Exists(modelRootPath))
            {
                return string.Empty;
            }

            var files = Directory.EnumerateFiles(modelRootPath, "*.onnx", SearchOption.TopDirectoryOnly)
                .Where(File.Exists)
                .OrderByDescending(path => !string.IsNullOrWhiteSpace(int8QuantKeyword) &&
                                           Path.GetFileName(path).IndexOf(int8QuantKeyword, StringComparison.OrdinalIgnoreCase) >= 0)
                .ThenByDescending(path => string.Equals(Path.GetFileName(path), "model.onnx", StringComparison.OrdinalIgnoreCase))
                .ThenByDescending(path => string.Equals(Path.GetFileName(path), "model.int8.onnx", StringComparison.OrdinalIgnoreCase))
                .ThenBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (files.Length == 0)
            {
                return string.Empty;
            }

            return files[0];
        }

        private static DiarizationSegment[] Wrap(OfflineSpeakerDiarizationSegment[] segments)
        {
            if (segments == null || segments.Length == 0)
            {
                return Array.Empty<DiarizationSegment>();
            }

            var result = new DiarizationSegment[segments.Length];
            for (int i = 0; i < segments.Length; i++)
            {
                var segment = segments[i];
                if (segment == null)
                {
                    result[i] = default;
                    continue;
                }

                result[i] = new DiarizationSegment(segment.Start, segment.End, segment.Speaker);
            }

            return result;
        }

        private void ValidateSampleRate(int sampleRate)
        {
            var expectedSampleRate = SampleRate;
            if (expectedSampleRate <= 0 || sampleRate <= 0)
            {
                return;
            }

            if (sampleRate != expectedSampleRate)
            {
                throw new ArgumentException(
                    $"Audio sample rate mismatch. Expected {expectedSampleRate} Hz but received {sampleRate} Hz.",
                    nameof(sampleRate));
            }
        }

        private static void ValidateSamples(float[] samples, string paramName)
        {
            if (samples == null)
            {
                throw new ArgumentNullException(paramName);
            }

            if (samples.Length == 0)
            {
                throw new ArgumentException("Audio sample buffer cannot be empty.", paramName);
            }
        }

        private void ThrowIfNotReady()
        {
            if (_diarization == null || IsDisposed)
            {
                throw new InvalidOperationException("SpeakerDiarization is not initialized or has been disposed. Please ensure it is loaded successfully before diarizing audio.");
            }
        }

        private static float[] DownmixToMono(float[] interleavedSamples, int channels)
        {
            if (interleavedSamples == null)
            {
                return Array.Empty<float>();
            }

            if (channels <= 1)
            {
                return interleavedSamples;
            }

            var frameCount = interleavedSamples.Length / channels;
            var mono = new float[frameCount];

            // Average all channels into a single mono track for diarization.
            for (int frame = 0; frame < frameCount; frame++)
            {
                float sum = 0f;
                var baseIndex = frame * channels;
                for (int channel = 0; channel < channels; channel++)
                {
                    sum += interleavedSamples[baseIndex + channel];
                }

                mono[frame] = sum / channels;
            }

            return mono;
        }

        private static Options CloneOptions(Options options)
        {
            var source = options ?? new Options();
            return new Options
            {
                MinDurationOn = source.MinDurationOn,
                MinDurationOff = source.MinDurationOff,
                NumClusters = source.NumClusters,
                ClusteringThreshold = source.ClusteringThreshold
            };
        }

        private static Options SanitizeOptions(Options options)
        {
            var sanitized = CloneOptions(options);

            sanitized.MinDurationOn = SanitizePositiveFloat(sanitized.MinDurationOn, 0.3f, nameof(Options.MinDurationOn));
            sanitized.MinDurationOff = SanitizePositiveFloat(sanitized.MinDurationOff, 0.5f, nameof(Options.MinDurationOff));
            sanitized.ClusteringThreshold = SanitizeThreshold(sanitized.ClusteringThreshold, 0.5f, nameof(Options.ClusteringThreshold));
            sanitized.NumClusters = sanitized.NumClusters <= 0 ? -1 : sanitized.NumClusters;

            return sanitized;
        }

        private static float SanitizePositiveFloat(float value, float fallback, string name)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0f)
            {
                SherpaLog.Warning($"Invalid {name} value '{value}'. Falling back to {fallback}.");
                return fallback;
            }

            return value;
        }

        private static float SanitizeThreshold(float value, float fallback, string name)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0f || value > 1f)
            {
                SherpaLog.Warning($"Invalid {name} value '{value}'. Falling back to {fallback}.");
                return fallback;
            }

            return value;
        }

        private static void ApplyRuntimeOptions(ref OfflineSpeakerDiarizationConfig config, Options options)
        {
            var effectiveOptions = SanitizeOptions(options);
            config.MinDurationOn = effectiveOptions.MinDurationOn;
            config.MinDurationOff = effectiveOptions.MinDurationOff;
            config.Clustering.NumClusters = effectiveOptions.NumClusters;
            config.Clustering.Threshold = effectiveOptions.ClusteringThreshold;
        }

        protected override void OnDestroy()
        {
            lock (_lockObject)
            {
                _diarization?.Dispose();
                _diarization = null;
                _config = default;
                _hasConfig = false;
            }
        }
    }
}
