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

        //支持自定义唤醒词功能 具体要参考https://k2-fsa.github.io/sherpa/onnx/kws/index.html#what-is-open-vocabulary-keyword-spotting 用pinyin库将文字转为拼音，英文需要使用bpe模型进行分词，暂不支持
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

                        _keywordSpotter = new KeywordSpotter(config);
                        var initialized = IsSuccessInitializad(_keywordSpotter);
                        if (initialized)
                        {

                            if (!string.IsNullOrEmpty(_keywordsPayload))
                            {
                                _stream = _keywordSpotter.CreateStream(_keywordsPayload);
                            }
                            else
                            {
                                _stream = _keywordSpotter.CreateStream();
                            }

                            if (_keywordSpotter == null || _stream == null)
                            {
                                throw new Exception($"Failed to initialize KWS model: {metadata.modelId}");
                            }

                            reporter?.Report(new LoadFeedback(metadata, message: $"KWS model loaded successfully: {metadata.modelId}"));
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

            config.ModelConfig.Transducer.Encoder = ModelFileResolver.ResolveRequiredFile(
                metadata,
                "transducer encoder",
                fallbackReporter,
                ModelFileCriteria.FromKeywords("encoder", "99", int8QuantKeyword),
                ModelFileCriteria.FromKeywords("encoder", "99"));
            config.ModelConfig.Transducer.Decoder = ModelFileResolver.ResolveRequiredFile(
                metadata,
                "transducer decoder",
                fallbackReporter,
                ModelFileCriteria.FromKeywords("decoder", "99", int8QuantKeyword),
                ModelFileCriteria.FromKeywords("decoder", "99"));
            config.ModelConfig.Transducer.Joiner = ModelFileResolver.ResolveRequiredFile(
                metadata,
                "transducer joiner",
                fallbackReporter,
                ModelFileCriteria.FromKeywords("joiner", "99", int8QuantKeyword),
                ModelFileCriteria.FromKeywords("joiner", "99"));
            var tokensPath = ModelFileResolver.ResolveRequiredByKeywords(metadata, "tokens.txt", fallbackReporter, "tokens.txt");
            config.ModelConfig.Tokens = tokensPath;

            EnsureCustomKeywords(tokensPath);

            if (!string.IsNullOrEmpty(_keywordsPayload))
            {
                config.KeywordsBuf = _keywordsPayload;
                config.KeywordsBufSize = Encoding.UTF8.GetByteCount(_keywordsPayload);
            }
            else
            {
                var keywordsFile = ModelFileResolver.ResolveOptionalByKeywords(metadata, fallbackReporter, "keywords.txt");
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
        private void EnsureCustomKeywords(string tokensFilePath)
        {
            if (_keywordsPayload != null || _keywordConfigs == null || _keywordConfigs.Length == 0)
            {
                return;
            }

            if (string.IsNullOrEmpty(tokensFilePath) || !File.Exists(tokensFilePath))
            {
                SherpaLog.Warning($"KeywordSpotting: Tokens file '{tokensFilePath}' is missing. Custom keywords will be ignored.");
                _registedKeywords = Array.Empty<string>();
                _keywordsPayload = null;
                return;
            }

            var tokenSet = LoadTokenSet(tokensFilePath, out int maxTokenLength);
            var format = PinyinFormat.WITH_TONE_MARK | PinyinFormat.LOWERCASE;
            var formattedKeywords = new List<string>(_keywordConfigs.Length);

            for (int i = 0; i < _keywordConfigs.Length; i++)
            {
                var keywordConfig = _keywordConfigs[i];
                var keyword = keywordConfig.Keyword?.Trim();

                if (string.IsNullOrEmpty(keyword))
                {
                    continue;
                }

                try
                {
                    var pinyin = Pinyin4Net.GetPinyin(keyword, format);
                    var tokens = ConvertPinyinToTokens(pinyin, tokenSet, maxTokenLength);

                    if (tokens == null || tokens.Count == 0)
                    {
                        SherpaLog.Warning($"KeywordSpotting: Unable to tokenize keyword '{keyword}'. It will be skipped.");
                        continue;
                    }

                    var boosting = SanitizeBoostingScore(keywordConfig.BoostingScore, keyword);
                    var threshold = SanitizeTriggerThreshold(keywordConfig.TriggerThreshold, keyword);
                    var spacedPinyin = string.Join(" ", tokens);
                    formattedKeywords.Add(FormattableString.Invariant($"{spacedPinyin} :{boosting:0.0###} #{threshold:0.0###} @{keyword}"));
                }
                catch (Exception ex)
                {
                    SherpaLog.Warning($"KeywordSpotting: Exception while processing keyword '{keyword}'. It will be skipped. {ex.Message}");
                }
            }

            if (formattedKeywords.Count == 0)
            {
                _registedKeywords = Array.Empty<string>();
                _keywordsPayload = null;
                return;
            }

            _registedKeywords = formattedKeywords.ToArray();
            _keywordsPayload = string.Join("\n", _registedKeywords);

            if (!_keywordsPayload.EndsWith("\n", StringComparison.Ordinal))
            {
                _keywordsPayload += "\n";
            }
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
