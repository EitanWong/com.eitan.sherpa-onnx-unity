
namespace Eitan.SherpaONNXUnity.Runtime.Modules
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Eitan.SherpaONNXUnity.Runtime.Utilities;
    using Eitan.SherpaONNXUnity.Runtime.Native;
    /// <summary>
    /// High-performance speech enhancement module for noise reduction and audio quality improvement.
    /// Supports both real-time streaming and batch processing with zero-GC design.
    /// </summary>
    public sealed class SpeechEnhancement : SherpaONNXModule
    {
        private OfflineSpeechDenoiser _denoiser;
        private OnlineSpeechDenoiser _onlineDenoiser;
        private readonly object _lockObject = new();
        private int _sampleRate;
        private SpeechEnhancementModelType _modelType;

        protected override SherpaONNXModuleType ModuleType => SherpaONNXModuleType.SpeechEnhancement;

        public SpeechEnhancement(string modelID, int sampleRate = 16000, SherpaONNXFeedbackReporter reporter = null)
            : base(modelID, sampleRate, reporter)
        {
        }

        protected override async Task<bool> Initialization(SherpaONNXModelMetadata metadata, int sampleRate, bool isMobilePlatform, SherpaONNXFeedbackReporter reporter, CancellationToken ct)
        {
            try
            {
                reporter?.Report(new LoadFeedback(metadata, message: $"Start Loading: {metadata.modelId}"));

                _sampleRate = sampleRate;
                _modelType = ResolveSpeechEnhancementModelType(metadata, isMobilePlatform, reporter);
                var offlineConfig = CreateOfflineSpeechDenoiserConfig(metadata, isMobilePlatform, reporter);
                var onlineConfig = CreateOnlineSpeechDenoiserConfig(metadata, isMobilePlatform, reporter);

                return await runner.RunAsync<bool>(cancellationToken =>
                {
                    try
                    {

                        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, cancellationToken);
                        linkedCts.Token.ThrowIfCancellationRequested();

                        if (IsDisposed) { return Task.FromResult(false); }

                        reporter?.Report(new LoadFeedback(metadata, message: $"Loading Speech Enhancement model: {metadata.modelId}"));
                        _denoiser = new OfflineSpeechDenoiser(offlineConfig);
                        _onlineDenoiser = new OnlineSpeechDenoiser(onlineConfig);
                        var initialized = IsSuccessInitializad(_denoiser) && IsSuccessInitializad(_onlineDenoiser);
                        if (initialized)
                        {
                            reporter?.Report(new LoadFeedback(metadata, message: $"Speech Enhancement model loaded successfully: {metadata.modelId}"));
                        }
                        return Task.FromResult(initialized);

                    }
                    catch (Exception ex)
                    {
                        reporter?.Report(new FailedFeedback(metadata, message: ex.Message, exception: ex));
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

        public int StreamingSampleRate => _onlineDenoiser?.SampleRate ?? 0;

        public int StreamingFrameShiftInSamples => _onlineDenoiser?.FrameShiftInSamples ?? 0;

        private OfflineSpeechDenoiserConfig CreateOfflineSpeechDenoiserConfig(SherpaONNXModelMetadata metadata, bool isMobilePlatform, SherpaONNXFeedbackReporter reporter)
        {
            var fallbackReporter = CreateFallbackReporter(metadata, reporter);
            var config = new OfflineSpeechDenoiserConfig
            {
                Model = new OfflineSpeechDenoiserModelConfig
                {
                    NumThreads = ThreadingUtils.GetAdaptiveThreadCount()
                }
            };

            switch (_modelType)
            {
                case SpeechEnhancementModelType.DpdfNet:
                    config.Model.Dpdfnet.Model = ResolveRequiredEnhancementModelFile(
                        metadata,
                        isMobilePlatform,
                        fallbackReporter,
                        "DPDFNet model",
                        ModelFileCriteria.FromKeywords("dpdfnet", "model"),
                        ModelFileCriteria.FromKeywords("dpdf", "model"),
                        ModelFileCriteria.FromKeywords("dpdfnet"),
                        ModelFileCriteria.FromKeywords("dpdf"));
                    config.Model.Gtcrn.Model = string.Empty;
                    break;
                case SpeechEnhancementModelType.Gtcrn:
                    config.Model.Gtcrn.Model = ResolveRequiredEnhancementModelFile(
                        metadata,
                        isMobilePlatform,
                        fallbackReporter,
                        "GTCRN model",
                        ModelFileCriteria.FromKeywords("gtcrn", "model"),
                        ModelFileCriteria.FromKeywords("gtcrn"));
                    config.Model.Dpdfnet.Model = string.Empty;
                    break;
                default:
                    throw new NotSupportedException($"Unsupported speech enhancement model type: {_modelType}");
            }

            return config;
        }

        private OnlineSpeechDenoiserConfig CreateOnlineSpeechDenoiserConfig(SherpaONNXModelMetadata metadata, bool isMobilePlatform, SherpaONNXFeedbackReporter reporter)
        {
            var offlineConfig = CreateOfflineSpeechDenoiserConfig(metadata, isMobilePlatform, reporter);
            return new OnlineSpeechDenoiserConfig
            {
                Model = offlineConfig.Model
            };
        }

        private SpeechEnhancementModelType ResolveSpeechEnhancementModelType(
            SherpaONNXModelMetadata metadata,
            bool isMobilePlatform,
            SherpaONNXFeedbackReporter reporter)
        {
            var resolvedType = SherpaUtils.Model.ResolveSpeechEnhancementModelType(metadata);
            if (resolvedType != SpeechEnhancementModelType.None)
            {
                return resolvedType;
            }

            var fallbackReporter = CreateFallbackReporter(metadata, reporter);
            if (TryResolveEnhancementModelFile(metadata, isMobilePlatform, fallbackReporter, out _, ModelFileCriteria.FromKeywords("dpdfnet", "model"), ModelFileCriteria.FromKeywords("dpdf", "model"), ModelFileCriteria.FromKeywords("dpdfnet"), ModelFileCriteria.FromKeywords("dpdf")))
            {
                return SpeechEnhancementModelType.DpdfNet;
            }

            if (TryResolveEnhancementModelFile(metadata, isMobilePlatform, fallbackReporter, out _, ModelFileCriteria.FromKeywords("gtcrn", "model"), ModelFileCriteria.FromKeywords("gtcrn")))
            {
                return SpeechEnhancementModelType.Gtcrn;
            }

            return SpeechEnhancementModelType.None;
        }

        private static string ResolveRequiredEnhancementModelFile(
            SherpaONNXModelMetadata metadata,
            bool isMobilePlatform,
            Action<string> fallbackReporter,
            string description,
            params ModelFileCriteria[] criteria)
        {
            if (TryResolveEnhancementModelFile(metadata, isMobilePlatform, fallbackReporter, out var resolvedPath, criteria))
            {
                return resolvedPath;
            }

            throw new InvalidOperationException($"Unable to locate {description} for model '{metadata?.modelId}'.");
        }

        private static bool TryResolveEnhancementModelFile(
            SherpaONNXModelMetadata metadata,
            bool isMobilePlatform,
            Action<string> fallbackReporter,
            out string resolvedPath,
            params ModelFileCriteria[] criteria)
        {
            var allCriteria = BuildEnhancementCriteria(isMobilePlatform, criteria);
            return ModelFileResolver.TryResolveFirstValidPath(
                metadata,
                out resolvedPath,
                fallbackReporter,
                recordFailures: true,
                allCriteria);
        }

        private static ModelFileCriteria[] BuildEnhancementCriteria(bool isMobilePlatform, params ModelFileCriteria[] criteria)
        {
            if (!isMobilePlatform)
            {
                var fallbackCriteria = new ModelFileCriteria[(criteria?.Length ?? 0) + 1];
                if (criteria != null && criteria.Length > 0)
                {
                    Array.Copy(criteria, fallbackCriteria, criteria.Length);
                }

                fallbackCriteria[fallbackCriteria.Length - 1] = ModelFileCriteria.FromExtensions(".onnx");
                return fallbackCriteria;
            }

            var mobileCriteria = new ModelFileCriteria[(criteria?.Length ?? 0) + 2];
            mobileCriteria[0] = ModelFileCriteria.FromKeywords("model", "int8");
            if (criteria != null && criteria.Length > 0)
            {
                Array.Copy(criteria, 0, mobileCriteria, 1, criteria.Length);
            }

            mobileCriteria[mobileCriteria.Length - 1] = ModelFileCriteria.FromExtensions(".onnx");
            return mobileCriteria;
        }

        /// <summary>
        /// Enhances audio samples asynchronously with high performance and zero-GC design.
        /// Modifies the input array in-place to avoid creating new objects.
        /// Suitable for both small buffers (160 samples) and large complete audio segments.
        /// </summary>
        /// <param name="samples">Audio samples to enhance (modified in-place)</param>
        /// <param name="sampleRate">Sample rate of the audio. If null, uses the module's sample rate.</param>
        /// <param name="ct">Cancellation token</param>
        public async Task EnhanceAsync(float[] samples, int? sampleRate = null, CancellationToken? ct = null)
        {
            if (_denoiser == null || IsDisposed)
            {
                throw new InvalidOperationException("SpeechEnhancement is not initialized or has been disposed. Please ensure it is loaded successfully before enhancing audio.");
            }

            if (samples == null || samples.Length == 0)
            {
                return;
            }

            var effectiveSampleRate = sampleRate ?? _sampleRate;

            await runner.RunAsync(cancellationToken =>
            {
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, ct ?? CancellationToken.None);
                var combinedCt = linkedCts.Token;

                if (IsDisposed || _denoiser == null)
                {
                    return;
                }

                lock (_lockObject)
                {
                    if (IsDisposed || _denoiser == null)
                    {
                        return;
                    }

                    combinedCt.ThrowIfCancellationRequested();

                    var enhancedAudio = _denoiser.Run(samples, effectiveSampleRate);
                    try
                    {
                        var enhancedSamples = enhancedAudio?.Samples;

                        if (enhancedSamples != null && enhancedSamples.Length > 0)
                        {
                            // Copy enhanced data back to input array in-place
                            var copyLength = Math.Min(samples.Length, enhancedSamples.Length);
                            Array.Copy(enhancedSamples, 0, samples, 0, copyLength);
                        }
                    }
                    finally
                    {
                        enhancedAudio?.Dispose();
                    }
                }
            });
        }


        /// <summary>
        /// Enhances audio samples synchronously for performance-critical scenarios.
        /// Modifies the input array in-place to avoid creating new objects.
        /// Use with caution as it blocks the calling thread.
        /// </summary>
        /// <param name="samples">Audio samples to enhance (modified in-place)</param>
        /// <param name="sampleRate">Sample rate of the audio. If null, uses the module's sample rate.</param>
        public void EnhanceSync(float[] samples, int? sampleRate = null)
        {
            if (_denoiser == null || IsDisposed || samples == null || samples.Length == 0)
            {
                return;
            }

            var effectiveSampleRate = sampleRate ?? _sampleRate;

            lock (_lockObject)
            {
                if (IsDisposed || _denoiser == null)
                {
                    return;
                }

                var enhancedAudio = _denoiser.Run(samples, effectiveSampleRate);
                try
                {
                    var enhancedSamples = enhancedAudio?.Samples;

                    if (enhancedSamples != null && enhancedSamples.Length > 0)
                    {
                        // Copy enhanced data back to input array in-place
                        var copyLength = Math.Min(samples.Length, enhancedSamples.Length);
                        Array.Copy(enhancedSamples, 0, samples, 0, copyLength);
                    }
                }
                finally
                {
                    enhancedAudio?.Dispose();
                }
            }
        }

        public async Task<float[]> ProcessStreamingAsync(float[] samples, int? sampleRate = null, CancellationToken? ct = null)
        {
            if (_onlineDenoiser == null || IsDisposed)
            {
                throw new InvalidOperationException("SpeechEnhancement streaming denoiser is not initialized or has been disposed.");
            }

            if (samples == null || samples.Length == 0)
            {
                return Array.Empty<float>();
            }

            var effectiveSampleRate = sampleRate ?? _sampleRate;

            return await runner.RunAsync<float[]>(cancellationToken =>
            {
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, ct ?? CancellationToken.None);
                var combinedCt = linkedCts.Token;
                combinedCt.ThrowIfCancellationRequested();

                lock (_lockObject)
                {
                    if (IsDisposed || _onlineDenoiser == null)
                    {
                        return Task.FromResult(Array.Empty<float>());
                    }

                    var enhancedAudio = _onlineDenoiser.Run(samples, effectiveSampleRate);
                    try
                    {
                        return Task.FromResult(enhancedAudio?.Samples ?? Array.Empty<float>());
                    }
                    finally
                    {
                        enhancedAudio?.Dispose();
                    }
                }
            });
        }

        public float[] ProcessStreamingSync(float[] samples, int? sampleRate = null)
        {
            if (_onlineDenoiser == null || IsDisposed || samples == null || samples.Length == 0)
            {
                return Array.Empty<float>();
            }

            var effectiveSampleRate = sampleRate ?? _sampleRate;
            lock (_lockObject)
            {
                if (IsDisposed || _onlineDenoiser == null)
                {
                    return Array.Empty<float>();
                }

                var enhancedAudio = _onlineDenoiser.Run(samples, effectiveSampleRate);
                try
                {
                    return enhancedAudio?.Samples ?? Array.Empty<float>();
                }
                finally
                {
                    enhancedAudio?.Dispose();
                }
            }
        }

        public async Task<float[]> FlushStreamingAsync(CancellationToken? ct = null)
        {
            if (_onlineDenoiser == null || IsDisposed)
            {
                throw new InvalidOperationException("SpeechEnhancement streaming denoiser is not initialized or has been disposed.");
            }

            return await runner.RunAsync<float[]>(cancellationToken =>
            {
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, ct ?? CancellationToken.None);
                linkedCts.Token.ThrowIfCancellationRequested();

                lock (_lockObject)
                {
                    if (IsDisposed || _onlineDenoiser == null)
                    {
                        return Task.FromResult(Array.Empty<float>());
                    }

                    var enhancedAudio = _onlineDenoiser.Flush();
                    try
                    {
                        return Task.FromResult(enhancedAudio?.Samples ?? Array.Empty<float>());
                    }
                    finally
                    {
                        enhancedAudio?.Dispose();
                    }
                }
            });
        }

        public float[] FlushStreamingSync()
        {
            if (_onlineDenoiser == null || IsDisposed)
            {
                return Array.Empty<float>();
            }

            lock (_lockObject)
            {
                if (IsDisposed || _onlineDenoiser == null)
                {
                    return Array.Empty<float>();
                }

                var enhancedAudio = _onlineDenoiser.Flush();
                try
                {
                    return enhancedAudio?.Samples ?? Array.Empty<float>();
                }
                finally
                {
                    enhancedAudio?.Dispose();
                }
            }
        }

        public void ResetStreaming()
        {
            if (_onlineDenoiser == null || IsDisposed)
            {
                return;
            }

            lock (_lockObject)
            {
                if (IsDisposed || _onlineDenoiser == null)
                {
                    return;
                }

                _onlineDenoiser.Reset();
            }
        }

        /// <summary>
        /// Enhances audio samples synchronously using Span input for zero-allocation processing.
        /// Modifies the input span in-place to avoid creating new objects.
        /// </summary>
        /// <param name="samples">Audio samples to enhance (modified in-place)</param>
        /// <param name="sampleRate">Sample rate of the audio. If null, uses the module's sample rate.</param>
        public void EnhanceSync(Span<float> samples, int? sampleRate = null)
        {
            if (_denoiser == null || IsDisposed || samples.Length == 0)
            {
                return;
            }

            var effectiveSampleRate = sampleRate ?? _sampleRate;

            lock (_lockObject)
            {
                if (IsDisposed || _denoiser == null)
                {
                    return;
                }

                var inputArray = SharedBuffer.RentAndCopy(samples);
                try
                {
                    var enhancedAudio = _denoiser.Run(inputArray, effectiveSampleRate);
                    try
                    {
                        var enhancedSamples = enhancedAudio?.Samples;

                        if (enhancedSamples != null && enhancedSamples.Length > 0)
                        {
                            var copyLength = Math.Min(samples.Length, enhancedSamples.Length);
                            enhancedSamples.AsSpan(0, copyLength).CopyTo(samples);
                        }
                    }
                    finally
                    {
                        enhancedAudio?.Dispose();
                    }
                }
                finally
                {
                    SharedBuffer.Return(inputArray);
                }
            }
        }

        /// <summary>
        /// Enhances a portion of an audio buffer in-place for streaming scenarios.
        /// This is the most efficient method for continuous audio processing.
        /// </summary>
        /// <param name="buffer">Audio buffer containing the samples</param>
        /// <param name="offset">Starting position in the buffer</param>
        /// <param name="length">Number of samples to process</param>
        /// <param name="sampleRate">Sample rate of the audio. If null, uses the module's sample rate.</param>
        public void EnhanceSync(float[] buffer, int offset, int length, int? sampleRate = null)
        {
            if (_denoiser == null || IsDisposed || buffer == null ||
                offset < 0 || length <= 0 || offset + length > buffer.Length)
            {
                return;
            }

            var effectiveSampleRate = sampleRate ?? _sampleRate;

            lock (_lockObject)
            {
                if (IsDisposed || _denoiser == null)
                {
                    return;
                }

                var segment = buffer.AsSpan(offset, length);
                var inputArray = SharedBuffer.RentAndCopy(segment);
                try
                {
                    var enhancedAudio = _denoiser.Run(inputArray, effectiveSampleRate);
                    try
                    {
                        var enhancedSamples = enhancedAudio?.Samples;

                        if (enhancedSamples != null && enhancedSamples.Length > 0)
                        {
                            var copyLength = Math.Min(length, enhancedSamples.Length);
                            Array.Copy(enhancedSamples, 0, buffer, offset, copyLength);
                        }
                    }
                    finally
                    {
                        enhancedAudio?.Dispose();
                    }
                }
                finally
                {
                    SharedBuffer.Return(inputArray);
                }
            }
        }

        /// <summary>
        /// High-performance batch processing for multiple audio segments.
        /// Modifies each segment in-place to avoid creating new objects.
        /// Processes segments sequentially to maintain thread safety with the underlying model.
        /// </summary>
        /// <param name="audioSegments">Collection of audio segments to enhance (each modified in-place)</param>
        /// <param name="sampleRate">Sample rate of the audio. If null, uses the module's sample rate.</param>
        /// <param name="ct">Cancellation token</param>
        public async Task EnhanceBatchAsync(float[][] audioSegments, int? sampleRate = null, CancellationToken? ct = null)
        {
            if (audioSegments == null || audioSegments.Length == 0)
            {
                return;
            }

            if (_denoiser == null || IsDisposed)
            {
                throw new InvalidOperationException("SpeechEnhancement is not initialized or has been disposed.");
            }

            var effectiveSampleRate = sampleRate ?? _sampleRate;
            var cancellationToken = ct ?? CancellationToken.None;

            // Process segments sequentially to maintain thread safety with the underlying model
            for (int i = 0; i < audioSegments.Length; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (audioSegments[i] != null && audioSegments[i].Length > 0)
                {
                    await EnhanceAsync(audioSegments[i], effectiveSampleRate, cancellationToken);
                }
            }
        }

        protected override void OnDestroy()
        {
            lock (_lockObject)
            {
                _denoiser?.Dispose();
                _onlineDenoiser?.Dispose();
                _denoiser = null;
                _onlineDenoiser = null;
            }
        }
    }
}
