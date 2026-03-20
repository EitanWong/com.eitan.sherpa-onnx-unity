using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Eitan.SherpaONNXUnity.Runtime.Native;
using Eitan.SherpaONNXUnity.Runtime.Utilities;
using Eitan.SherpaONNXUnity.Runtime.Utilities.Pinyin;

namespace Eitan.SherpaONNXUnity.Runtime.Modules
{
    public sealed class KeywordSpotting : SherpaONNXModule
    {
        private const float DefaultBoostingScore = 2.0f;
        private const float DefaultTriggerThreshold = 0.1f;

        [Serializable]
        public struct KeywordRegistration
        {
            public KeywordRegistration(string keyword, float boostingScore = DefaultBoostingScore, float triggerThreshold = DefaultTriggerThreshold)
            {
                Keyword = keyword;
                BoostingScore = boostingScore;
                TriggerThreshold = triggerThreshold;
            }

            public string Keyword;

            [UnityEngine.MinAttribute(0.0001f)]
            public float BoostingScore;

            [UnityEngine.RangeAttribute(0f, 1f)]
            public float TriggerThreshold;
        }

        public event Action<string> OnKeywordDetected;


        private readonly SendOrPostCallback _keywordDetectedDispatch;

        private KeywordSpotter _keywordSpotter;
        private OnlineStream _stream;
        private readonly ConcurrentQueue<float> _audioQueue = new();
        private readonly object _lockObject = new();
        private int _isDetecting;
        private int _sampleRate;
        private readonly int _maxQueuedSamples;
        private readonly bool _dropIfLagging;
        private int _queuedSamples;
        private readonly float _keywordsScore;
        private readonly float _keywordsThreshold;

        private string[] _registedKeywords = Array.Empty<string>();
        private string _keywordsPayload;
        private readonly KeywordRegistration[] _keywordConfigs;

        protected override SherpaONNXModuleType ModuleType => SherpaONNXModuleType.KeywordSpotting;

        // 支持 open-vocabulary 关键词。
        // 中文关键词走拼音分词；若模型目录中存在 en.phone，则英文关键词走 phone lexicon。
        public KeywordSpotting(string modelID, int sampleRate = 16000, float keywordsScore = 2.0f, float keywordsThreshold = 0.25f, KeywordRegistration[] customKeywords = null, SherpaONNXFeedbackReporter reporter = null, int maxQueuedSamples = 16000, bool dropIfLagging = true, bool startImmediately = true)
            : base(modelID, sampleRate, reporter, startImmediately)
        {
            _keywordsScore = keywordsScore;
            _keywordsThreshold = keywordsThreshold;
            _keywordConfigs = customKeywords;
            _maxQueuedSamples = Math.Max(8000, maxQueuedSamples);
            _dropIfLagging = dropIfLagging;
            _keywordDetectedDispatch = CreateCallback<string>(keyword =>
            {
                OnKeywordDetected?.Invoke(keyword);
            });

        }

        protected override async Task<bool> Initialization(SherpaONNXModelMetadata metadata, int sampleRate, bool isMobilePlatform, SherpaONNXFeedbackReporter reporter, CancellationToken ct)
        {
            try
            {
                reporter?.Report(new LoadFeedback(metadata, message: $"Start Loading: {metadata.modelId}"));

                var config = await CreateKeywordSpotterConfig(metadata, sampleRate, isMobilePlatform, reporter, ct);

                return await runner.RunAsync<bool>(cancellationToken =>
                {
                    try
                    {

                        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, cancellationToken);
                        linkedCts.Token.ThrowIfCancellationRequested();

                        if (IsDisposed) { return Task.FromResult(false); }

                        reporter?.Report(new LoadFeedback(metadata, message: $"Loading KWS model: {metadata.modelId}"));
                        SherpaLog.Info(
                            "[KeywordSpotting] Resolved config:\n" +
                            $"  Encoder: {config.ModelConfig.Transducer.Encoder}\n" +
                            $"  Decoder: {config.ModelConfig.Transducer.Decoder}\n" +
                            $"  Joiner: {config.ModelConfig.Transducer.Joiner}\n" +
                            $"  Tokens: {config.ModelConfig.Tokens}\n" +
                            $"  KeywordsFile: {config.KeywordsFile}\n" +
                            $"  KeywordsBufSize: {config.KeywordsBufSize}\n" +
                            $"  ModelingUnit: {config.ModelConfig.ModelingUnit}\n" +
                            $"  BpeVocab: {config.ModelConfig.BpeVocab}",
                            category: "KeywordSpotting");

                        _keywordSpotter = new KeywordSpotter(config);
                        var spotterHandle = _keywordSpotter?.Handle ?? IntPtr.Zero;
                        var initialized = spotterHandle != IntPtr.Zero;
                        SherpaLog.Info(
                            $"[KeywordSpotting] Native spotter handle: 0x{spotterHandle.ToInt64():X} Valid={initialized}",
                            category: "KeywordSpotting");

                        if (!initialized)
                        {
                            throw new InvalidOperationException(
                                $"Failed to create keyword spotter. " +
                                $"Encoder='{config.ModelConfig.Transducer.Encoder}', " +
                                $"Decoder='{config.ModelConfig.Transducer.Decoder}', " +
                                $"Joiner='{config.ModelConfig.Transducer.Joiner}', " +
                                $"Tokens='{config.ModelConfig.Tokens}', " +
                                $"KeywordsFile='{config.KeywordsFile}', " +
                                $"KeywordsBufSize={config.KeywordsBufSize}");
                        }

                        if (!string.IsNullOrEmpty(_keywordsPayload))
                        {
                            _stream = _keywordSpotter.CreateStream(_keywordsPayload);
                        }
                        else
                        {
                            _stream = _keywordSpotter.CreateStream();
                        }

                        var streamHandle = _stream?.Handle ?? IntPtr.Zero;
                        var streamInitialized = streamHandle != IntPtr.Zero;
                        SherpaLog.Info(
                            $"[KeywordSpotting] Stream handle: 0x{streamHandle.ToInt64():X} Valid={streamInitialized}",
                            category: "KeywordSpotting");

                        if (!streamInitialized)
                        {
                            throw new InvalidOperationException(
                                $"Failed to create keyword stream for model '{metadata.modelId}'. " +
                                $"Keywords source={(string.IsNullOrEmpty(_keywordsPayload) ? "file" : "buffer")}.");
                        }

                        reporter?.Report(new LoadFeedback(metadata, message: $"KWS model loaded successfully: {metadata.modelId}"));
                        return Task.FromResult(true);

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

        private Task<KeywordSpotterConfig> CreateKeywordSpotterConfig(SherpaONNXModelMetadata metadata, int sampleRate, bool isMobilePlatform, SherpaONNXFeedbackReporter reporter, CancellationToken ct)
        {
            _sampleRate = sampleRate;

            var fallbackReporter = CreateFallbackReporter(metadata, reporter);

            var config = new KeywordSpotterConfig
            {
                FeatConfig = { SampleRate = sampleRate, FeatureDim = 80 },
                ModelConfig = {
                    Provider = "cpu",
                    NumThreads = ThreadingUtils.GetAdaptiveThreadCount(),
                },
                KeywordsScore = _keywordsScore,
                KeywordsThreshold = _keywordsThreshold
            };

            var int8QuantKeyword = isMobilePlatform ? "int8" : null;

            var triplet = ResolveKeywordSpotterTriplet(metadata, isMobilePlatform, fallbackReporter);
            config.ModelConfig.Transducer.Encoder = triplet.Encoder;
            config.ModelConfig.Transducer.Decoder = triplet.Decoder;
            config.ModelConfig.Transducer.Joiner = triplet.Joiner;
            var tokensPath = ModelFileResolver.ResolveRequiredFileWithBindings(
                metadata,
                "tokens.txt",
                fallbackReporter,
                new[] { SherpaONNXModelFileKey.Tokens },
                ModelFileCriteria.FromKeywords("tokens.txt"),
                ModelFileCriteria.FromKeywords("tokens"));
            config.ModelConfig.Tokens = tokensPath;

            var englishLexiconPath = ResolveEnglishLexiconPath(metadata, fallbackReporter);
            EnsureCustomKeywords(tokensPath, englishLexiconPath);

            if (!string.IsNullOrEmpty(_keywordsPayload))
            {
                config.KeywordsBuf = _keywordsPayload;
                config.KeywordsBufSize = Encoding.UTF8.GetByteCount(_keywordsPayload);
            }
            else
            {
                var keywordsFile = ResolveKeywordListFile(metadata, fallbackReporter);
                if (!string.IsNullOrEmpty(keywordsFile))
                {
                    config.KeywordsFile = keywordsFile;
                }
            }

            return Task.FromResult(config);
        }

        public void StreamDetect(ReadOnlySpan<float> samples)
        {
            if (IsDisposed || _keywordSpotter == null || _stream == null || samples.Length == 0)
            {
                return;
            }

            // If we are already behind and a worker is active, drop the entire chunk to keep latency bounded.
            if (_dropIfLagging && Volatile.Read(ref _queuedSamples) >= _maxQueuedSamples && Volatile.Read(ref _isDetecting) == 1)
            {
                return;
            }

            // If an incoming chunk is huge, keep only the newest tail to avoid ballooning memory/latency.
            var startIndex = samples.Length + Volatile.Read(ref _queuedSamples) > _maxQueuedSamples * 2
                ? Math.Max(0, samples.Length - _maxQueuedSamples)
                : 0;

            for (int i = startIndex; i < samples.Length; i++)
            {
                _audioQueue.Enqueue(samples[i]);
                Interlocked.Increment(ref _queuedSamples);
            }

            // Bound the queue to avoid unbounded latency/memory.
            while (Volatile.Read(ref _queuedSamples) > _maxQueuedSamples && _audioQueue.TryDequeue(out _))
            {
                Interlocked.Decrement(ref _queuedSamples);
            }

            if (Interlocked.Exchange(ref _isDetecting, 1) == 0)
            {
                _ = runner.RunAsync(ProcessAudioQueue, policy: ExecutionPolicy.Always);
            }
        }

        private Task ProcessAudioQueue(CancellationToken ct)
        {
            if (IsDisposed)
            {
                return Task.CompletedTask;
            }


            const int batchSize = 3200;
            float[] batch = ArrayPool<float>.Shared.Rent(batchSize);

            try
            {
                while (!_audioQueue.IsEmpty && !ct.IsCancellationRequested)
                {
                    int count = 0;
                    while (count < batchSize && _audioQueue.TryDequeue(out float sample))
                    {
                        batch[count++] = sample;
                        Interlocked.Decrement(ref _queuedSamples);
                    }

                    if (count > 0)
                    {
                        ProcessAudioChunk(batch.AsSpan(0, count));
                    }
                }
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                SherpaLog.Exception(ex);
            }
            finally
            {
                ArrayPool<float>.Shared.Return(batch);
                Volatile.Write(ref _isDetecting, 0);
                // If new data arrived mid-drain, kick off another pass without letting the queue grow unchecked.
                if (!_audioQueue.IsEmpty && !ct.IsCancellationRequested && Interlocked.CompareExchange(ref _isDetecting, 1, 0) == 0)
                {
                    _ = runner.RunAsync(ProcessAudioQueue, cancellationToken: ct, policy: ExecutionPolicy.Always);
                }
            }

            return Task.CompletedTask;
        }

        private void ProcessAudioChunk(ReadOnlySpan<float> samples)
        {
            lock (_lockObject)
            {
                if (IsDisposed || _stream == null)
                {
                    return;
                }

                var buffer = SharedBuffer.RentAndCopy(samples);
                try
                {
                    _stream.AcceptWaveform(_sampleRate, buffer);
                }
                finally
                {
                    SharedBuffer.Return(buffer);
                }

                while (_keywordSpotter.IsReady(_stream))
                {
                    _keywordSpotter.Decode(_stream);
                    var result = _keywordSpotter.GetResult(_stream);

                    if (!string.IsNullOrEmpty(result.Keyword))
                    {
                        _keywordSpotter.Reset(_stream);
                        var detectedKeyword = result.Keyword;
                        ExecuteOnMainThread(_keywordDetectedDispatch, detectedKeyword);
                    }
                }
            }
        }

        public async Task<string> DetectAsync(float[] samples, int sampleRate = 0, CancellationToken? ct = null)
        {
            if (_keywordSpotter == null || _stream == null)
            {
                throw new InvalidOperationException("KeywordSpotting is not initialized or has been disposed. Please ensure it is loaded successfully before detecting keywords.");
            }
            if (sampleRate <= 0)
            {
                sampleRate = _sampleRate;
            }

            return await runner.RunAsync((cancellationToken) =>
                {
                    string detectedKeyword = string.Empty;

                    lock (_lockObject)
                    {
                        if (IsDisposed || _stream == null)
                        {

                            return Task.FromResult(string.Empty);
                        }


                        _stream.AcceptWaveform(sampleRate, samples);

                        while (_keywordSpotter.IsReady(_stream))
                        {
                            _keywordSpotter.Decode(_stream);
                            var result = _keywordSpotter.GetResult(_stream);

                            if (!string.IsNullOrEmpty(result.Keyword))
                            {
                                _keywordSpotter.Reset(_stream);
                                detectedKeyword = result.Keyword;
                                break;
                            }
                        }
                    }

                    return Task.FromResult(detectedKeyword);
                }, cancellationToken: ct ?? CancellationToken.None);
        }

        public string DetectSync(float[] samples, int sampleRate = 0)
        {
            if (_keywordSpotter == null || _stream == null || IsDisposed)
            {

                return string.Empty;
            }

            if (sampleRate <= 0)
            {
                sampleRate = _sampleRate;
            }


            lock (_lockObject)
            {
                if (IsDisposed || _stream == null)
                {
                    return string.Empty;
                }


                _stream.AcceptWaveform(sampleRate, samples);

                while (_keywordSpotter.IsReady(_stream))
                {
                    _keywordSpotter.Decode(_stream);
                    var result = _keywordSpotter.GetResult(_stream);

                    if (!string.IsNullOrEmpty(result.Keyword))
                    {
                        _keywordSpotter.Reset(_stream);
                        return result.Keyword;
                    }
                }

                return string.Empty;
            }
        }

        #region  PrivateMethod
        private readonly struct KeywordSpotterTriplet
        {
            public KeywordSpotterTriplet(string encoder, string decoder, string joiner)
            {
                Encoder = encoder;
                Decoder = decoder;
                Joiner = joiner;
            }

            public string Encoder { get; }
            public string Decoder { get; }
            public string Joiner { get; }
        }

        private sealed class ModelVariantSet
        {
            public string Key;
            public List<string> EncoderPaths = new List<string>();
            public List<string> DecoderPaths = new List<string>();
            public List<string> JoinerPaths = new List<string>();
        }

        private static KeywordSpotterTriplet ResolveKeywordSpotterTriplet(
            SherpaONNXModelMetadata metadata,
            bool isMobilePlatform,
            Action<string> fallbackReporter)
        {
            var boundEncoder = ModelFileResolver.ResolveOptionalFileWithBindings(
                metadata,
                fallbackReporter,
                new[] { SherpaONNXModelFileKey.Encoder },
                ModelFileCriteria.FromKeywords("encoder"));
            var boundDecoder = ModelFileResolver.ResolveOptionalFileWithBindings(
                metadata,
                fallbackReporter,
                new[] { SherpaONNXModelFileKey.Decoder },
                ModelFileCriteria.FromKeywords("decoder"));
            var boundJoiner = ModelFileResolver.ResolveOptionalFileWithBindings(
                metadata,
                fallbackReporter,
                new[] { SherpaONNXModelFileKey.Joiner },
                ModelFileCriteria.FromKeywords("joiner"));

            if (!string.IsNullOrEmpty(boundEncoder) &&
                !string.IsNullOrEmpty(boundDecoder) &&
                !string.IsNullOrEmpty(boundJoiner))
            {
                return new KeywordSpotterTriplet(boundEncoder, boundDecoder, boundJoiner);
            }

            var encoderCandidates = ModelFileResolver.FilterValidFiles(
                metadata.GetModelFilePathByKeywords("encoder") ?? Array.Empty<string>(),
                fallbackReporter);
            var decoderCandidates = ModelFileResolver.FilterValidFiles(
                metadata.GetModelFilePathByKeywords("decoder") ?? Array.Empty<string>(),
                fallbackReporter);
            var joinerCandidates = ModelFileResolver.FilterValidFiles(
                metadata.GetModelFilePathByKeywords("joiner") ?? Array.Empty<string>(),
                fallbackReporter);

            var variants = new Dictionary<string, ModelVariantSet>(StringComparer.OrdinalIgnoreCase);
            AddVariantCandidates(variants, encoderCandidates, "encoder");
            AddVariantCandidates(variants, decoderCandidates, "decoder");
            AddVariantCandidates(variants, joinerCandidates, "joiner");

            ModelVariantSet bestVariant = null;
            int bestScore = int.MinValue;

            foreach (var variant in variants.Values)
            {
                if (variant.EncoderPaths.Count == 0 || variant.DecoderPaths.Count == 0 || variant.JoinerPaths.Count == 0)
                {
                    continue;
                }

                var score = ScoreKeywordSpotterVariant(variant.Key);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestVariant = variant;
                }
            }

            if (bestVariant != null)
            {
                return new KeywordSpotterTriplet(
                    SelectPreferredPath(bestVariant.EncoderPaths, isMobilePlatform),
                    SelectPreferredPath(bestVariant.DecoderPaths, isMobilePlatform),
                    SelectPreferredPath(bestVariant.JoinerPaths, isMobilePlatform));
            }

            var int8QuantKeyword = isMobilePlatform ? "int8" : null;
            return new KeywordSpotterTriplet(
                ModelFileResolver.ResolveRequiredFileWithBindings(
                    metadata,
                    "transducer encoder",
                    fallbackReporter,
                    new[] { SherpaONNXModelFileKey.Encoder },
                    ModelFileCriteria.FromKeywords("encoder", "99", int8QuantKeyword),
                    ModelFileCriteria.FromKeywords("encoder", "99"),
                    ModelFileCriteria.FromKeywords("encoder", int8QuantKeyword),
                    ModelFileCriteria.FromKeywords("encoder")),
                ModelFileResolver.ResolveRequiredFileWithBindings(
                    metadata,
                    "transducer decoder",
                    fallbackReporter,
                    new[] { SherpaONNXModelFileKey.Decoder },
                    ModelFileCriteria.FromKeywords("decoder", "99", int8QuantKeyword),
                    ModelFileCriteria.FromKeywords("decoder", "99"),
                    ModelFileCriteria.FromKeywords("decoder", int8QuantKeyword),
                    ModelFileCriteria.FromKeywords("decoder")),
                ModelFileResolver.ResolveRequiredFileWithBindings(
                    metadata,
                    "transducer joiner",
                    fallbackReporter,
                    new[] { SherpaONNXModelFileKey.Joiner },
                    ModelFileCriteria.FromKeywords("joiner", "99", int8QuantKeyword),
                    ModelFileCriteria.FromKeywords("joiner", "99"),
                    ModelFileCriteria.FromKeywords("joiner", int8QuantKeyword),
                    ModelFileCriteria.FromKeywords("joiner")));
        }

        private static void AddVariantCandidates(
            Dictionary<string, ModelVariantSet> variants,
            IEnumerable<string> paths,
            string componentPrefix)
        {
            if (paths == null)
            {
                return;
            }

            foreach (var path in paths)
            {
                var variantKey = GetKeywordSpotterVariantKey(path, componentPrefix);
                if (string.IsNullOrWhiteSpace(variantKey))
                {
                    continue;
                }

                if (!variants.TryGetValue(variantKey, out var variant))
                {
                    variant = new ModelVariantSet { Key = variantKey };
                    variants[variantKey] = variant;
                }

                switch (componentPrefix)
                {
                    case "encoder":
                        variant.EncoderPaths.Add(path);
                        break;
                    case "decoder":
                        variant.DecoderPaths.Add(path);
                        break;
                    case "joiner":
                        variant.JoinerPaths.Add(path);
                        break;
                }
            }
        }

        internal static string GetKeywordSpotterVariantKey(string path, string componentPrefix)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            var fileName = Path.GetFileName(path);
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return string.Empty;
            }

            var normalized = fileName;
            if (normalized.EndsWith(".int8.onnx", StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized.Substring(0, normalized.Length - ".int8.onnx".Length);
            }
            else if (normalized.EndsWith(".onnx", StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized.Substring(0, normalized.Length - ".onnx".Length);
            }

            var prefix = componentPrefix + "-";
            if (normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized.Substring(prefix.Length);
            }

            return normalized;
        }

        internal static int ScoreKeywordSpotterVariant(string variantKey)
        {
            if (string.IsNullOrWhiteSpace(variantKey))
            {
                return int.MinValue;
            }

            var score = 0;
            if (variantKey.IndexOf("epoch-99", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                score += 1000;
            }

            if (variantKey.IndexOf("chunk-16", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                score += 200;
            }
            else if (variantKey.IndexOf("chunk-8", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                score += 100;
            }

            var epochIndex = variantKey.IndexOf("epoch-", StringComparison.OrdinalIgnoreCase);
            if (epochIndex >= 0)
            {
                epochIndex += "epoch-".Length;
                int end = epochIndex;
                while (end < variantKey.Length && char.IsDigit(variantKey[end]))
                {
                    end++;
                }

                if (end > epochIndex &&
                    int.TryParse(variantKey.Substring(epochIndex, end - epochIndex), out var epoch))
                {
                    score += Math.Min(epoch, 999);
                }
            }

            return score;
        }

        private static string SelectPreferredPath(List<string> paths, bool preferInt8)
        {
            if (paths == null || paths.Count == 0)
            {
                return string.Empty;
            }

            string bestPath = null;
            int bestScore = int.MinValue;

            for (int i = 0; i < paths.Count; i++)
            {
                var path = paths[i];
                var fileName = Path.GetFileName(path);
                var hasInt8 = fileName.IndexOf(".int8.", StringComparison.OrdinalIgnoreCase) >= 0;
                var score = hasInt8 == preferInt8 ? 100 : 0;
                if (score > bestScore)
                {
                    bestScore = score;
                    bestPath = path;
                }
            }

            return bestPath ?? paths[0];
        }

        private static string ResolveEnglishLexiconPath(SherpaONNXModelMetadata metadata, Action<string> fallbackReporter)
        {
            return ModelFileResolver.ResolveOptionalFileWithBindings(
                metadata,
                fallbackReporter,
                new[] { SherpaONNXModelFileKey.Lexicon },
                ModelFileCriteria.FromKeywords("en.phone"),
                ModelFileCriteria.FromKeywords("phone"),
                ModelFileCriteria.FromExtensions(".phone"),
                ModelFileCriteria.FromKeywords("lexicon"));
        }

        internal static string ResolveKeywordListFile(SherpaONNXModelMetadata metadata, Action<string> fallbackReporter)
        {
            var resolved = ModelFileResolver.ResolveOptionalFileWithBindings(
                metadata,
                fallbackReporter,
                new[] { SherpaONNXModelFileKey.Keywords },
                ModelFileCriteria.FromKeywords("keywords.txt"),
                ModelFileCriteria.FromKeywords("keywords"));
            if (!string.IsNullOrEmpty(resolved))
            {
                return resolved;
            }

            var fallbackRelativePaths = new[]
            {
                "keywords.txt",
                Path.Combine("test_wavs", "keywords.txt"),
                Path.Combine("test_wavs", "test_keywords.txt"),
            };

            for (int i = 0; i < fallbackRelativePaths.Length; i++)
            {
                var relativePath = fallbackRelativePaths[i];
                var candidate = metadata.GetModelFilePath(relativePath);
                if (string.IsNullOrWhiteSpace(candidate) || !File.Exists(candidate))
                {
                    continue;
                }

                fallbackReporter?.Invoke($"Resolved keywords file via fallback path '{relativePath}'.");
                return candidate;
            }

            return null;
        }

        private void EnsureCustomKeywords(string tokensFilePath, string englishLexiconPath)
        {
            if (_keywordsPayload != null || _keywordConfigs == null || _keywordConfigs.Length == 0)
            {
                return;
            }

            _keywordsPayload = BuildCustomKeywordsPayload(_keywordConfigs, tokensFilePath, englishLexiconPath, out _registedKeywords);
            if (string.IsNullOrWhiteSpace(_keywordsPayload))
            {
                _registedKeywords = Array.Empty<string>();
                _keywordsPayload = null;
            }
        }

        internal static string BuildCustomKeywordsPayload(
            IReadOnlyList<KeywordRegistration> keywordConfigs,
            string tokensFilePath,
            string englishLexiconPath,
            out string[] registeredKeywords)
        {
            registeredKeywords = Array.Empty<string>();

            if (keywordConfigs == null || keywordConfigs.Count == 0)
            {
                return null;
            }

            if (string.IsNullOrEmpty(tokensFilePath) || !File.Exists(tokensFilePath))
            {
                SherpaLog.Warning($"KeywordSpotting: Tokens file '{tokensFilePath}' is missing. Custom keywords will be ignored.");
                return null;
            }

            var tokenSet = LoadTokenSet(tokensFilePath, out int maxTokenLength);
            var englishLexicon = LoadEnglishPhoneLexicon(englishLexiconPath);
            var formattedKeywords = new List<string>(keywordConfigs.Count);

            for (int i = 0; i < keywordConfigs.Count; i++)
            {
                var keywordConfig = keywordConfigs[i];
                var keyword = keywordConfig.Keyword?.Trim();

                if (string.IsNullOrEmpty(keyword))
                {
                    continue;
                }

                try
                {
                    var tokens = ConvertKeywordToTokens(keyword, tokenSet, maxTokenLength, englishLexicon);
                    if (tokens == null || tokens.Count == 0)
                    {
                        SherpaLog.Warning($"KeywordSpotting: Unable to tokenize keyword '{keyword}'. It will be skipped.");
                        continue;
                    }

                    var boosting = SanitizeBoostingScore(keywordConfig.BoostingScore, keyword);
                    var threshold = SanitizeTriggerThreshold(keywordConfig.TriggerThreshold, keyword);
                    var spacedTokens = string.Join(" ", tokens);
                    formattedKeywords.Add(FormattableString.Invariant($"{spacedTokens} :{boosting:0.0###} #{threshold:0.0###} @{keyword}"));
                }
                catch (Exception ex)
                {
                    SherpaLog.Warning($"KeywordSpotting: Exception while processing keyword '{keyword}'. It will be skipped. {ex.Message}");
                }
            }

            if (formattedKeywords.Count == 0)
            {
                return null;
            }

            registeredKeywords = formattedKeywords.ToArray();
            var payload = string.Join("\n", registeredKeywords);
            if (!payload.EndsWith("\n", StringComparison.Ordinal))
            {
                payload += "\n";
            }

            return payload;
        }

        private static List<string> ConvertKeywordToTokens(
            string keyword,
            HashSet<string> tokenSet,
            int maxTokenLength,
            IReadOnlyDictionary<string, string[]> englishLexicon)
        {
            if (string.IsNullOrWhiteSpace(keyword))
            {
                return null;
            }

            var segments = SplitKeywordSegments(keyword);
            if (segments.Count == 0)
            {
                return null;
            }

            var result = new List<string>();

            for (int i = 0; i < segments.Count; i++)
            {
                var segment = segments[i];
                if (segment.Kind == KeywordSegmentKind.Chinese)
                {
                    var pinyin = Pinyin4Net.GetPinyin(segment.Value, PinyinFormat.WITH_TONE_MARK | PinyinFormat.LOWERCASE);
                    var tokens = ConvertPinyinToTokens(pinyin, tokenSet, maxTokenLength);
                    if (tokens == null || tokens.Count == 0)
                    {
                        return null;
                    }

                    result.AddRange(tokens);
                    continue;
                }

                var englishPhones = ConvertEnglishWordToTokens(segment.Value, englishLexicon);
                if (englishPhones == null || englishPhones.Count == 0)
                {
                    return null;
                }

                result.AddRange(englishPhones);
            }

            return result;
        }

        private enum KeywordSegmentKind
        {
            Chinese,
            English
        }

        private readonly struct KeywordSegment
        {
            public KeywordSegment(KeywordSegmentKind kind, string value)
            {
                Kind = kind;
                Value = value;
            }

            public KeywordSegmentKind Kind { get; }
            public string Value { get; }
        }

        private static List<KeywordSegment> SplitKeywordSegments(string keyword)
        {
            var segments = new List<KeywordSegment>();
            if (string.IsNullOrWhiteSpace(keyword))
            {
                return segments;
            }

            var normalized = NormalizeWhitespace(keyword);
            var buffer = new StringBuilder(normalized.Length);
            KeywordSegmentKind? currentKind = null;

            void Flush()
            {
                if (buffer.Length == 0 || currentKind == null)
                {
                    return;
                }

                segments.Add(new KeywordSegment(currentKind.Value, buffer.ToString()));
                buffer.Clear();
            }

            for (int i = 0; i < normalized.Length; i++)
            {
                var ch = normalized[i];
                if (IsChineseCharacter(ch))
                {
                    if (currentKind != KeywordSegmentKind.Chinese)
                    {
                        Flush();
                        currentKind = KeywordSegmentKind.Chinese;
                    }

                    buffer.Append(ch);
                    continue;
                }

                if (IsEnglishKeywordCharacter(ch))
                {
                    if (currentKind != KeywordSegmentKind.English)
                    {
                        Flush();
                        currentKind = KeywordSegmentKind.English;
                    }

                    buffer.Append(ch);
                    continue;
                }

                Flush();
                currentKind = null;
            }

            Flush();
            return segments;
        }

        private static bool IsChineseCharacter(char ch)
        {
            return (ch >= '\u3400' && ch <= '\u4DBF') ||
                   (ch >= '\u4E00' && ch <= '\u9FFF') ||
                   (ch >= '\uF900' && ch <= '\uFAFF');
        }

        private static bool IsEnglishKeywordCharacter(char ch)
        {
            return ch <= sbyte.MaxValue && (char.IsLetterOrDigit(ch) || ch == '\'');
        }

        private static List<string> ConvertEnglishWordToTokens(
            string word,
            IReadOnlyDictionary<string, string[]> englishLexicon)
        {
            if (string.IsNullOrWhiteSpace(word) || englishLexicon == null || englishLexicon.Count == 0)
            {
                return null;
            }

            var normalized = word.Trim().ToUpperInvariant();
            if (TryGetEnglishPhones(normalized, englishLexicon, out var phones))
            {
                return new List<string>(phones);
            }

            normalized = normalized.Replace("'", string.Empty);
            return TryGetEnglishPhones(normalized, englishLexicon, out phones)
                ? new List<string>(phones)
                : null;
        }

        private static bool TryGetEnglishPhones(
            string word,
            IReadOnlyDictionary<string, string[]> englishLexicon,
            out string[] phones)
        {
            phones = null;
            if (string.IsNullOrWhiteSpace(word) || englishLexicon == null)
            {
                return false;
            }

            if (englishLexicon.TryGetValue(word, out phones) && phones != null && phones.Length > 0)
            {
                return true;
            }

            return false;
        }

        internal static Dictionary<string, string[]> LoadEnglishPhoneLexicon(string englishLexiconPath)
        {
            if (string.IsNullOrWhiteSpace(englishLexiconPath) || !File.Exists(englishLexiconPath))
            {
                return null;
            }

            var lexicon = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
            foreach (var rawLine in File.ReadLines(englishLexiconPath))
            {
                if (string.IsNullOrWhiteSpace(rawLine))
                {
                    continue;
                }

                var trimmed = rawLine.Trim();
                if (trimmed.Length == 0 || trimmed[0] == '#')
                {
                    continue;
                }

                var parts = trimmed.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2)
                {
                    continue;
                }

                var word = NormalizeEnglishLexiconWord(parts[0]);
                if (string.IsNullOrEmpty(word) || lexicon.ContainsKey(word))
                {
                    continue;
                }

                var phones = new string[parts.Length - 1];
                Array.Copy(parts, 1, phones, 0, phones.Length);
                lexicon[word] = phones;
            }

            return lexicon;
        }

        private static string NormalizeEnglishLexiconWord(string word)
        {
            if (string.IsNullOrWhiteSpace(word))
            {
                return string.Empty;
            }

            var normalized = word.Trim().ToUpperInvariant();
            var pronunciationIndex = normalized.IndexOf('(');
            return pronunciationIndex > 0 ? normalized.Substring(0, pronunciationIndex) : normalized;
        }

        private static List<string> ConvertPinyinToTokens(string pinyin, HashSet<string> tokenSet, int maxTokenLength)
        {
            if (string.IsNullOrWhiteSpace(pinyin))
            {
                return null;
            }

            pinyin = NormalizeWhitespace(pinyin).Trim();
            if (pinyin.Length == 0)
            {
                return null;
            }

            var syllables = pinyin.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var result = new List<string>();

            for (int i = 0; i < syllables.Length; i++)
            {
                var syllable = syllables[i];
                if (string.IsNullOrEmpty(syllable))
                {
                    continue;
                }

                var normalized = NormalizeToneMarks(syllable)
                    .Replace("'", string.Empty)
                    .Replace("’", string.Empty)
                    .Replace("u:", "ü")
                    .Replace("v", "ü")
                    .Replace("·", string.Empty);

                normalized = normalized.ToLowerInvariant();

                if (TrySegmentSyllable(normalized, tokenSet, maxTokenLength, out var tokens))
                {
                    result.AddRange(tokens);
                    continue;
                }

                var fallback = TryFallbackSegmentation(normalized, tokenSet);
                if (fallback == null)
                {
                    return null;
                }

                result.AddRange(fallback);
            }

            return result;
        }

        private static HashSet<string> LoadTokenSet(string tokensFilePath, out int maxTokenLength)
        {
            var tokens = new HashSet<string>(StringComparer.Ordinal);
            maxTokenLength = 0;

            foreach (var line in File.ReadLines(tokensFilePath))
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                var trimmed = line.Trim();
                if (trimmed.Length == 0 || trimmed[0] == '#')
                {
                    continue;
                }

                if (trimmed.StartsWith("<", StringComparison.Ordinal))
                {
                    continue;
                }

                var spaceIndex = trimmed.IndexOf(' ');
                var token = spaceIndex >= 0 ? trimmed.Substring(0, spaceIndex) : trimmed;
                if (string.IsNullOrEmpty(token))
                {
                    continue;
                }

                tokens.Add(token);
                if (token.Length > maxTokenLength)
                {
                    maxTokenLength = token.Length;
                }
            }

            if (maxTokenLength == 0)
            {
                maxTokenLength = 1;
            }

            return tokens;
        }

        private static bool TrySegmentSyllable(string syllable, HashSet<string> tokenSet, int maxTokenLength, out List<string> segments)
        {
            segments = new List<string>();

            if (string.IsNullOrEmpty(syllable))
            {
                return false;
            }

            var memo = new Dictionary<int, bool>();
            if (TrySegmentRecursive(syllable, 0, tokenSet, maxTokenLength, segments, memo))
            {
                return true;
            }

            segments.Clear();
            return false;
        }

        private static bool TrySegmentRecursive(string syllable, int index, HashSet<string> tokenSet, int maxTokenLength, List<string> current, Dictionary<int, bool> memo)
        {
            if (index == syllable.Length)
            {
                return true;
            }

            if (memo.ContainsKey(index))
            {
                return false;
            }

            int remaining = syllable.Length - index;
            int maxLen = Math.Min(maxTokenLength, remaining);

            for (int len = maxLen; len >= 1; len--)
            {
                var slice = syllable.Substring(index, len);
                if (!tokenSet.Contains(slice))
                {
                    continue;
                }

                current.Add(slice);
                if (TrySegmentRecursive(syllable, index + len, tokenSet, maxTokenLength, current, memo))
                {
                    return true;
                }

                current.RemoveAt(current.Count - 1);
            }

            memo[index] = true;
            return false;
        }

        private static List<string> TryFallbackSegmentation(string syllable, HashSet<string> tokenSet)
        {
            if (tokenSet.Contains(syllable))
            {
                return new List<string> { syllable };
            }

            var fallback = new List<string>(syllable.Length);
            foreach (var rune in syllable)
            {
                var token = rune.ToString();
                if (!tokenSet.Contains(token))
                {
                    return null;
                }

                fallback.Add(token);
            }

            return fallback;
        }

        private static string NormalizeWhitespace(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            var builder = new StringBuilder(value.Length);
            for (int i = 0; i < value.Length; i++)
            {
                var ch = value[i];
                builder.Append(ch == '\u00A0' ? ' ' : ch);
            }

            return builder.ToString();
        }

        private static float SanitizeBoostingScore(float value, string keyword)
        {
            if (value > 0f)
            {
                return value;
            }

            SherpaLog.Warning($"Keyword '{keyword}' has invalid boosting score {value}. Using default {DefaultBoostingScore}.");
            return DefaultBoostingScore;
        }

        private static float SanitizeTriggerThreshold(float value, string keyword)
        {
            if (value > 0f && value <= 1f)
            {
                return value;
            }

            if (value <= 0f)
            {
                SherpaLog.Warning($"Keyword '{keyword}' has invalid trigger threshold {value}. Using default {DefaultTriggerThreshold}.");
                return DefaultTriggerThreshold;
            }

            SherpaLog.Warning($"Keyword '{keyword}' trigger threshold {value} is above 1.0. Clamping to 1.0.");
            return 1f;
        }

        private static string NormalizeToneMarks(string pinyin)
        {
            if (string.IsNullOrEmpty(pinyin))
            {
                return string.Empty;
            }

            Span<char> buffer = stackalloc char[pinyin.Length];
            int count = 0;

            for (int i = 0; i < pinyin.Length; i++)
            {
                char c = pinyin[i];
                buffer[count++] = c switch
                {
                    'ă' => 'ǎ',
                    'Ă' => 'Ǎ',
                    'ĕ' => 'ě',
                    'Ĕ' => 'Ě',
                    'ĭ' => 'ǐ',
                    'Ĭ' => 'Ǐ',
                    'ŏ' => 'ǒ',
                    'Ŏ' => 'Ǒ',
                    'ŭ' => 'ǔ',
                    'Ŭ' => 'Ǔ',
                    _ => c
                };
            }

            return new string(buffer.Slice(0, count));
        }

        #endregion

        protected override void OnDestroy()
        {
            lock (_lockObject)
            {
                while (_audioQueue.TryDequeue(out _)) { }
                _stream?.Dispose();
                _stream = null;
                _keywordSpotter?.Dispose();
                _keywordSpotter = null;
            }
        }
    }
}
