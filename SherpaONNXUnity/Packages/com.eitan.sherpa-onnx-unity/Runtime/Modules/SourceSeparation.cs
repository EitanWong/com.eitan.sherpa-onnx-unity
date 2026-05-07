
namespace Eitan.SherpaONNXUnity.Runtime.Modules
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Eitan.SherpaONNXUnity.Runtime.Native;
    using Eitan.SherpaONNXUnity.Runtime.Utilities;
    using UnityEngine;

    /// <summary>
    /// High-level Unity wrapper for sherpa-onnx offline source separation.
    /// Supports Spleeter two-stem models and UVR/MDX-Net vocal separation models.
    /// </summary>
    public sealed class SourceSeparation : SherpaONNXModule
    {
        #region Result Types
        public readonly struct Stem
        {
            public Stem(string name, float[][] channels)
            {
                Name = name ?? string.Empty;
                Channels = channels ?? Array.Empty<float[]>();
            }

            public string Name { get; }
            public float[][] Channels { get; }
            public int NumChannels => Channels?.Length ?? 0;
            public int NumSamplesPerChannel => NumChannels == 0 || Channels[0] == null ? 0 : Channels[0].Length;

            public float[] ToInterleaved()
            {
                if (NumChannels == 0)
                {
                    return Array.Empty<float>();
                }

                var frames = NumSamplesPerChannel;
                var interleaved = new float[frames * NumChannels];
                for (int frame = 0; frame < frames; frame++)
                {
                    var baseIndex = frame * NumChannels;
                    for (int channel = 0; channel < NumChannels; channel++)
                    {
                        var channelData = Channels[channel];
                        interleaved[baseIndex + channel] = channelData != null && frame < channelData.Length
                            ? channelData[frame]
                            : 0f;
                    }
                }

                return interleaved;
            }
        }

        public sealed class Result
        {
            public Result(int sampleRate, Stem[] stems, SourceSeparationModelType modelType)
            {
                SampleRate = sampleRate;
                Stems = stems ?? Array.Empty<Stem>();
                ModelType = modelType;
            }

            public int SampleRate { get; }
            public Stem[] Stems { get; }
            public SourceSeparationModelType ModelType { get; }
            public int NumStems => Stems.Length;

            public bool TryGetStem(string name, out Stem stem)
            {
                if (!string.IsNullOrWhiteSpace(name))
                {
                    for (int i = 0; i < Stems.Length; i++)
                    {
                        if (string.Equals(Stems[i].Name, name, StringComparison.OrdinalIgnoreCase))
                        {
                            stem = Stems[i];
                            return true;
                        }
                    }
                }

                stem = default;
                return false;
            }
        }
        #endregion

        #region Fields
        private OfflineSourceSeparation _separator;
        private readonly object _lockObject = new object();
        private int _sampleRate;
        private SourceSeparationModelType _modelType;
        #endregion

        #region Properties
        protected override SherpaONNXModuleType ModuleType => SherpaONNXModuleType.SourceSeparation;

        public int OutputSampleRate => _separator?.OutputSampleRate ?? 0;

        public int NumberOfStems => _separator?.NumberOfStems ?? 0;
        #endregion

        #region Lifecycle
        public SourceSeparation(
            string modelID,
            int sampleRate = 44100,
            SherpaONNXFeedbackReporter reporter = null)
            : base(modelID, sampleRate, reporter)
        {
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
                reporter?.Report(new LoadFeedback(metadata, message: $"Start Loading (SourceSeparation): {metadata.modelId}"));
                TryReportAndroid32BitRuntimeRisk(metadata, reporter, "SourceSeparation");

                _sampleRate = sampleRate;
                _modelType = ResolveSourceSeparationModelType(metadata, isMobilePlatform, reporter);
                var config = CreateSourceSeparationConfig(metadata, isMobilePlatform, reporter);

                return await runner.RunAsync<bool>(cancellationToken =>
                {
                    using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, cancellationToken);
                    linked.Token.ThrowIfCancellationRequested();

                    if (IsDisposed)
                    {
                        return Task.FromResult(false);
                    }

                    _separator = new OfflineSourceSeparation(config);
                    var initialized = IsSuccessInitializad(_separator);
                    if (initialized)
                    {
                        reporter?.Report(new LoadFeedback(metadata, message: $"Loaded source separation model: {metadata.modelId}"));
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

        protected override void OnDestroy()
        {
            SafeExecute(() =>
            {
                _separator?.Dispose();
                _separator = null;
            });
        }
        #endregion

        #region Public API
        public async Task<Result> SeparateAsync(
            float[][] channels,
            int? sampleRate = null,
            AudioProcessingOptions? outputProcessingOptions = null,
            CancellationToken cancellationToken = default)
        {
            if (_separator == null || IsDisposed)
            {
                throw new InvalidOperationException("SourceSeparation is not initialized or has been disposed. Please ensure it is loaded successfully before separating audio.");
            }

            if (channels == null || channels.Length == 0)
            {
                return new Result(sampleRate ?? _sampleRate, Array.Empty<Stem>(), _modelType);
            }

            var effectiveSampleRate = sampleRate ?? _sampleRate;
            var normalizedChannels = NormalizeInputChannels(channels, effectiveSampleRate);

            return await runner.RunAsync<Result>(ct =>
            {
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, cancellationToken);
                linked.Token.ThrowIfCancellationRequested();

                lock (_lockObject)
                {
                    if (IsDisposed || _separator == null)
                    {
                        return Task.FromResult(new Result(effectiveSampleRate, Array.Empty<Stem>(), _modelType));
                    }

                    using var output = _separator.Process(normalizedChannels, effectiveSampleRate);
                    return Task.FromResult(CreateManagedResult(output, outputProcessingOptions ?? AudioProcessingOptions.SourceSeparationSafeDefault));
                }
            });
        }

        public Result Separate(float[][] channels, int? sampleRate = null, AudioProcessingOptions? outputProcessingOptions = null)
        {
            if (_separator == null || IsDisposed)
            {
                throw new InvalidOperationException("SourceSeparation is not initialized or has been disposed. Please ensure it is loaded successfully before separating audio.");
            }

            if (channels == null || channels.Length == 0)
            {
                return new Result(sampleRate ?? _sampleRate, Array.Empty<Stem>(), _modelType);
            }

            var effectiveSampleRate = sampleRate ?? _sampleRate;
            var normalizedChannels = NormalizeInputChannels(channels, effectiveSampleRate);

            lock (_lockObject)
            {
                if (IsDisposed || _separator == null)
                {
                    return new Result(effectiveSampleRate, Array.Empty<Stem>(), _modelType);
                }

                using var output = _separator.Process(normalizedChannels, effectiveSampleRate);
                return CreateManagedResult(output, outputProcessingOptions ?? AudioProcessingOptions.SourceSeparationSafeDefault);
            }
        }

        public Task<Result> SeparateAsync(
            float[] interleavedSamples,
            int numChannels,
            int? sampleRate = null,
            AudioProcessingOptions? outputProcessingOptions = null,
            CancellationToken cancellationToken = default)
        {
            return SeparateAsync(SplitInterleavedChannels(interleavedSamples, numChannels), sampleRate, outputProcessingOptions, cancellationToken);
        }

        public Result Separate(float[] interleavedSamples, int numChannels, int? sampleRate = null, AudioProcessingOptions? outputProcessingOptions = null)
        {
            return Separate(SplitInterleavedChannels(interleavedSamples, numChannels), sampleRate, outputProcessingOptions);
        }

        public async Task<Result> SeparateAsync(
            AudioClip clip,
            AudioProcessingOptions? outputProcessingOptions = null,
            CancellationToken cancellationToken = default)
        {
            if (clip == null)
            {
                throw new ArgumentNullException(nameof(clip));
            }

            var interleaved = new float[clip.samples * clip.channels];
            clip.GetData(interleaved, 0);
            return await SeparateAsync(interleaved, clip.channels, clip.frequency, outputProcessingOptions, cancellationToken).ConfigureAwait(false);
        }

        public Result Separate(AudioClip clip, AudioProcessingOptions? outputProcessingOptions = null)
        {
            if (clip == null)
            {
                throw new ArgumentNullException(nameof(clip));
            }

            var interleaved = new float[clip.samples * clip.channels];
            clip.GetData(interleaved, 0);
            return Separate(interleaved, clip.channels, clip.frequency, outputProcessingOptions);
        }
        #endregion

        #region Configuration
        private OfflineSourceSeparationConfig CreateSourceSeparationConfig(
            SherpaONNXModelMetadata metadata,
            bool isMobilePlatform,
            SherpaONNXFeedbackReporter reporter)
        {
            var fallbackReporter = CreateFallbackReporter(metadata, reporter);
            var config = new OfflineSourceSeparationConfig
            {
                Model = new OfflineSourceSeparationModelConfig
                {
                    NumThreads = ThreadingUtils.GetAdaptiveThreadCount(),
                    Debug = 0,
                    Provider = "cpu",
                }
            };

            switch (_modelType)
            {
                case SourceSeparationModelType.Spleeter:
                    config.Model.Spleeter.Vocals = ResolveRequiredSourceSeparationModelFile(
                        metadata,
                        isMobilePlatform,
                        fallbackReporter,
                        "Spleeter vocals model",
                        CreateSpleeterStemCriteria(isMobilePlatform, "vocals"));
                    config.Model.Spleeter.Accompaniment = ResolveRequiredSourceSeparationModelFile(
                        metadata,
                        isMobilePlatform,
                        fallbackReporter,
                        "Spleeter accompaniment model",
                        CreateSpleeterStemCriteria(isMobilePlatform, "accompaniment"));
                    config.Model.Uvr.Model = string.Empty;
                    break;
                case SourceSeparationModelType.Uvr:
                    config.Model.Uvr.Model = ResolveRequiredSourceSeparationModelFile(
                        metadata,
                        isMobilePlatform,
                        fallbackReporter,
                        "UVR model",
                        CreateUvrCriteria(isMobilePlatform));
                    config.Model.Spleeter.Vocals = string.Empty;
                    config.Model.Spleeter.Accompaniment = string.Empty;
                    break;
                default:
                    throw new NotSupportedException($"Unsupported source separation model type: {_modelType}");
            }

            return config;
        }

        private SourceSeparationModelType ResolveSourceSeparationModelType(
            SherpaONNXModelMetadata metadata,
            bool isMobilePlatform,
            SherpaONNXFeedbackReporter reporter)
        {
            var resolvedType = SherpaUtils.Model.ResolveSourceSeparationModelType(metadata);
            if (resolvedType != SourceSeparationModelType.None)
            {
                return resolvedType;
            }

            var fallbackReporter = CreateFallbackReporter(metadata, reporter);
            if (TryResolveSourceSeparationModelFile(metadata, fallbackReporter, out _, CreateSpleeterStemCriteria(isMobilePlatform, "vocals"))
                && TryResolveSourceSeparationModelFile(metadata, fallbackReporter, out _, CreateSpleeterStemCriteria(isMobilePlatform, "accompaniment")))
            {
                return SourceSeparationModelType.Spleeter;
            }

            if (TryResolveSourceSeparationModelFile(metadata, fallbackReporter, out _, CreateUvrCriteria(isMobilePlatform)))
            {
                return SourceSeparationModelType.Uvr;
            }

            return SourceSeparationModelType.None;
        }
        #endregion

        #region File Resolution
        private static string ResolveRequiredSourceSeparationModelFile(
            SherpaONNXModelMetadata metadata,
            bool isMobilePlatform,
            Action<string> fallbackReporter,
            string description,
            params ModelFileCriteria[] criteria)
        {
            if (TryResolveSourceSeparationModelFile(metadata, fallbackReporter, out var resolvedPath, criteria))
            {
                return resolvedPath;
            }

            throw new InvalidOperationException($"Unable to locate {description} for model '{metadata?.modelId}'.");
        }

        private static bool TryResolveSourceSeparationModelFile(
            SherpaONNXModelMetadata metadata,
            Action<string> fallbackReporter,
            out string resolvedPath,
            params ModelFileCriteria[] criteria)
        {
            return ModelFileResolver.TryResolveFirstValidPath(
                metadata,
                out resolvedPath,
                fallbackReporter,
                recordFailures: true,
                criteria);
        }

        private static ModelFileCriteria[] CreateSpleeterStemCriteria(bool isMobilePlatform, string stemName)
        {
            var stem = stemName?.Trim()?.ToLowerInvariant() ?? string.Empty;
            if (string.IsNullOrEmpty(stem))
            {
                return Array.Empty<ModelFileCriteria>();
            }

            if (isMobilePlatform)
            {
                return new[]
                {
                    ModelFileCriteria.FromKeywords(stem, "int8"),
                    ModelFileCriteria.FromKeywords(stem),
                };
            }

            return new[]
            {
                ModelFileCriteria.FromKeywords(stem, "fp16"),
                ModelFileCriteria.FromKeywords(stem),
            };
        }

        private static ModelFileCriteria[] CreateUvrCriteria(bool isMobilePlatform)
        {
            if (isMobilePlatform)
            {
                return new[]
                {
                    ModelFileCriteria.FromBindingKeys(SherpaONNXModelFileKey.Model),
                    ModelFileCriteria.FromKeywords("uvr", "int8"),
                    ModelFileCriteria.FromKeywords("mdx", "int8"),
                    ModelFileCriteria.FromKeywords("uvr"),
                    ModelFileCriteria.FromKeywords("mdx"),
                    ModelFileCriteria.FromExtensions(".onnx"),
                };
            }

            return new[]
            {
                ModelFileCriteria.FromBindingKeys(SherpaONNXModelFileKey.Model),
                ModelFileCriteria.FromKeywords("uvr", "fp16"),
                ModelFileCriteria.FromKeywords("mdx", "fp16"),
                ModelFileCriteria.FromKeywords("uvr"),
                ModelFileCriteria.FromKeywords("mdx"),
                ModelFileCriteria.FromExtensions(".onnx"),
            };
        }
        #endregion

        #region Result Conversion
        private Result CreateManagedResult(SourceSeparationOutput output, AudioProcessingOptions outputProcessingOptions)
        {
            if (output == null || output.Handle == IntPtr.Zero)
            {
                return new Result(_sampleRate, Array.Empty<Stem>(), _modelType);
            }

            var stems = new Stem[output.NumStems];
            var stemNames = GetStemNames(_modelType, output.NumStems);
            var sampleRate = output.SampleRate > 0 ? output.SampleRate : _sampleRate;
            for (int i = 0; i < output.NumStems; i++)
            {
                var stemChannels = output.GetStemSamples(i);
                AudioProcessingUtils.ProcessChannelsInPlace(stemChannels, sampleRate, outputProcessingOptions);
                stems[i] = new Stem(stemNames[i], stemChannels);
            }

            return new Result(sampleRate, stems, _modelType);
        }

        private static string[] GetStemNames(SourceSeparationModelType modelType, int numStems)
        {
            string[] defaults;
            switch (modelType)
            {
                case SourceSeparationModelType.Spleeter:
                    defaults = new[] { "vocals", "accompaniment" };
                    break;
                case SourceSeparationModelType.Uvr:
                    defaults = new[] { "non-vocals", "vocals" };
                    break;
                default:
                    defaults = Array.Empty<string>();
                    break;
            }

            var result = new string[Math.Max(0, numStems)];
            for (int i = 0; i < result.Length; i++)
            {
                result[i] = i < defaults.Length ? defaults[i] : $"stem-{i}";
            }

            return result;
        }
        #endregion

        #region Audio Helpers
        private static float[][] SplitInterleavedChannels(float[] interleavedSamples, int numChannels)
        {
            if (interleavedSamples == null || interleavedSamples.Length == 0 || numChannels <= 0)
            {
                return Array.Empty<float[]>();
            }

            if (interleavedSamples.Length % numChannels != 0)
            {
                throw new ArgumentException(
                    $"Interleaved sample length {interleavedSamples.Length} is not divisible by channel count {numChannels}.",
                    nameof(interleavedSamples));
            }

            if (numChannels == 1)
            {
                return new[] { interleavedSamples };
            }

            var frames = interleavedSamples.Length / numChannels;
            var channels = new float[numChannels][];
            for (int channel = 0; channel < numChannels; channel++)
            {
                channels[channel] = new float[frames];
            }

            for (int frame = 0; frame < frames; frame++)
            {
                var baseIndex = frame * numChannels;
                for (int channel = 0; channel < numChannels; channel++)
                {
                    channels[channel][frame] = interleavedSamples[baseIndex + channel];
                }
            }

            return channels;
        }

        private float[][] NormalizeInputChannels(float[][] channels, int sampleRate)
        {
            if (sampleRate <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sampleRate), sampleRate, "Sample rate must be greater than zero.");
            }

            if (channels == null || channels.Length == 0)
            {
                return Array.Empty<float[]>();
            }

            var normalized = new float[channels.Length][];
            int frames = -1;
            for (int i = 0; i < channels.Length; i++)
            {
                var channel = channels[i];
                if (channel == null)
                {
                    throw new ArgumentException($"Input channel {i} is null.", nameof(channels));
                }

                if (frames < 0)
                {
                    frames = channel.Length;
                }
                else if (channel.Length != frames)
                {
                    throw new ArgumentException(
                        $"All channels must have the same number of samples. Channel 0 has {frames}, channel {i} has {channel.Length}.",
                        nameof(channels));
                }

                normalized[i] = channel;
            }

            if (_modelType == SourceSeparationModelType.Spleeter || _modelType == SourceSeparationModelType.Uvr)
            {
                if (normalized.Length == 1)
                {
                    SherpaLog.Warning(
                        $"[SourceSeparation] Model '{ModelId}' expects stereo input. Duplicating mono channel before native processing.");
                    return DuplicateMonoToStereo(normalized[0]);
                }

                if (normalized.Length != 2)
                {
                    throw new ArgumentException(
                        $"Model '{ModelId}' expects mono or stereo input compatible with sherpa-onnx source separation. Received {normalized.Length} channels.",
                        nameof(channels));
                }
            }

            SherpaLog.Info(
                $"[SourceSeparation] Processing model '{ModelId}' with {normalized.Length} channel(s), {Math.Max(frames, 0)} frame(s) per channel at {sampleRate}Hz.",
                category: "SourceSeparation");
            return normalized;
        }

        private static float[][] DuplicateMonoToStereo(float[] monoSamples)
        {
            if (monoSamples == null)
            {
                return Array.Empty<float[]>();
            }

            var left = new float[monoSamples.Length];
            var right = new float[monoSamples.Length];
            Array.Copy(monoSamples, left, monoSamples.Length);
            Array.Copy(monoSamples, right, monoSamples.Length);
            return new[] { left, right };
        }
        #endregion
    }
}
