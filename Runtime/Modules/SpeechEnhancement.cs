
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
        private readonly object _lockObject = new();
        private int _sampleRate;

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
                var config = CreateSpeechDenoiserConfig(metadata, isMobilePlatform, reporter);

                return await runner.RunAsync<bool>(cancellationToken =>
                {
                    try
                    {

                        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, cancellationToken);
                        linkedCts.Token.ThrowIfCancellationRequested();

                        if (IsDisposed) { return Task.FromResult(false); }

                        reporter?.Report(new LoadFeedback(metadata, message: $"Loading Speech Enhancement model: {metadata.modelId}"));
                        _denoiser = new OfflineSpeechDenoiser(config);
                        var initialized = IsSuccessInitializad(_denoiser);
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

        private OfflineSpeechDenoiserConfig CreateSpeechDenoiserConfig(SherpaONNXModelMetadata metadata, bool isMobilePlatform, SherpaONNXFeedbackReporter reporter)
        {
            var fallbackReporter = CreateFallbackReporter(metadata, reporter);
            var config = new OfflineSpeechDenoiserConfig
            {
                Model = new OfflineSpeechDenoiserModelConfig
                {
                    NumThreads = ThreadingUtils.GetAdaptiveThreadCount()
                }
            };

            var int8QuantKeyword = isMobilePlatform ? "int8" : null;

            config.Model.Gtcrn.Model = ModelFileResolver.ResolveRequiredFile(
                metadata,
                "GTCRN model",
                fallbackReporter,
                ModelFileCriteria.FromKeywords("gtcrn", "model", int8QuantKeyword),
                ModelFileCriteria.FromKeywords("gtcrn", "model"),
                ModelFileCriteria.FromExtensions(".onnx"));

            return config;
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
                    var enhancedSamples = enhancedAudio?.Samples;

                    if (enhancedSamples != null && enhancedSamples.Length > 0)
                    {
                        // Copy enhanced data back to input array in-place
                        var copyLength = Math.Min(samples.Length, enhancedSamples.Length);
                        Array.Copy(enhancedSamples, 0, samples, 0, copyLength);
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
                var enhancedSamples = enhancedAudio?.Samples;

                if (enhancedSamples != null && enhancedSamples.Length > 0)
                {
                    // Copy enhanced data back to input array in-place
                    var copyLength = Math.Min(samples.Length, enhancedSamples.Length);
                    Array.Copy(enhancedSamples, 0, samples, 0, copyLength);
                }
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
                    var enhancedSamples = enhancedAudio?.Samples;

                    if (enhancedSamples != null && enhancedSamples.Length > 0)
                    {
                        var copyLength = Math.Min(samples.Length, enhancedSamples.Length);
                        enhancedSamples.AsSpan(0, copyLength).CopyTo(samples);
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
                    var enhancedSamples = enhancedAudio?.Samples;

                    if (enhancedSamples != null && enhancedSamples.Length > 0)
                    {
                        var copyLength = Math.Min(length, enhancedSamples.Length);
                        Array.Copy(enhancedSamples, 0, buffer, offset, copyLength);
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
                _denoiser = null;
            }
        }
    }
}
