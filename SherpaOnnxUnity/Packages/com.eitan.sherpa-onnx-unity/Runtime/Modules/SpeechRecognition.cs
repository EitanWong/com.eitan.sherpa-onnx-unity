// SpeechRecognition.cs (Refactored and Optimized)

namespace Eitan.SherpaONNXUnity.Runtime.Core
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Runtime.CompilerServices;

    using Eitan.SherpaONNXUnity.Runtime.Native;
    using Eitan.SherpaONNXUnity.Runtime.Core.Utilities;
    using Eitan.SherpaONNXUnity.Runtime.Core.Utilities.Lexicon;

    public class SpeechRecognition : SherpaONNXModule
    {
        private OnlineRecognizer _onlineRecognizer;
        private OnlineStream _onlineStream;
        private OfflineRecognizer _offlineRecognizer;

        private SpeechRecognitionModelType _modelType;
        private readonly object _lockObject = new object();
        public bool IsOnlineModel { get; private set; }

        protected override SherpaONNXModuleType ModuleType => SherpaONNXModuleType.SpeechRecognition;

        public float Rule1MinTrailingSilence = 2.4f;
        public float Rule2MinTrailingSilence = 1.2f;
        public float Rule3MinUtteranceLength = 30f;


        public SpeechRecognition(string modelID, int sampleRate = 16000, SherpaONNXFeedbackReporter reporter = null)
            : base(modelID, sampleRate, reporter)
        {
            IsOnlineModel = SherpaUtils.Model.IsOnlineModel(modelID);
            _modelType = SherpaUtils.Model.GetSpeechRecognitionModelType(modelID);
        }

        protected override async Task<bool> Initialization(SherpaONNXModelMetadata metadata, int sampleRate, bool isMobilePlatform, SherpaONNXFeedbackReporter reporter, CancellationToken ct)
        {
            try
            {
                reporter?.Report(new LoadFeedback(metadata, message: $"Start Loading: {metadata.modelId}"));

                if (IsOnlineModel)
                {
                    return await LoadOnlineModelAsync(metadata, sampleRate, isMobilePlatform, reporter, ct);
                }
                else
                {

                    return await LoadOfflineModelAsync(metadata, sampleRate, isMobilePlatform, reporter, ct);
                }
            }
            catch (Exception ex)
            {
                reporter?.Report(new FailedFeedback(metadata, ex.Message, exception: ex));
                throw;
            }
        }

        private async Task<bool> LoadOnlineModelAsync(SherpaONNXModelMetadata metadata, int sampleRate, bool isMobilePlatform, SherpaONNXFeedbackReporter reporter, CancellationToken ct)
        {
            var config = CreateOnlineRecognizerConfig(metadata, sampleRate, isMobilePlatform, reporter);

            return await runner.RunAsync<bool>(cancellationToken =>
            {
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, cancellationToken);
                linkedCts.Token.ThrowIfCancellationRequested();

                if (IsDisposed) { return Task.FromResult(false); }

                _onlineRecognizer = new OnlineRecognizer(config);
                var initialized = IsSuccessInitializad(_onlineRecognizer);
                if (initialized)
                {
                    _onlineStream = _onlineRecognizer.CreateStream();
                }
                reporter?.Report(new LoadFeedback(metadata, message: $"Loaded online model: {metadata.modelId}"));
                return Task.FromResult(initialized);
            });
        }

        private async Task<bool> LoadOfflineModelAsync(SherpaONNXModelMetadata metadata, int sampleRate, bool isMobilePlatform, SherpaONNXFeedbackReporter reporter, CancellationToken ct)
        {
            var config = CreateOfflineRecognizerConfig(metadata, sampleRate, isMobilePlatform, reporter);

            return await runner.RunAsync<bool>(cancellationToken =>
             {
                 using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, cancellationToken);
                 linkedCts.Token.ThrowIfCancellationRequested();

                 if (IsDisposed) { return Task.FromResult(false); }

                 _offlineRecognizer = new OfflineRecognizer(config);
                 var initialized = IsSuccessInitializad(_offlineRecognizer);

                 if (initialized)
                 {
                     reporter?.Report(new LoadFeedback(metadata, message: $"Loaded offline model: {metadata.modelId}"));
                 }
                 return Task.FromResult(initialized);
             });
        }

        private OnlineRecognizerConfig CreateOnlineRecognizerConfig(SherpaONNXModelMetadata metadata, int sampleRate, bool isMobilePlatform, SherpaONNXFeedbackReporter reporter)
        {
            var fallbackReporter = CreateFallbackReporter(metadata, reporter);
            var threadCount = ThreadingUtils.GetAdaptiveThreadCount();
            var int8QuantKeyword = isMobilePlatform ? "int8" : null;

            var tokensPath = ModelFileResolver.ResolveRequiredByKeywords(metadata, "token file", fallbackReporter, "tokens", "tokens.txt");

            var config = new OnlineRecognizerConfig
            {
                FeatConfig = { SampleRate = sampleRate, FeatureDim = 80 },
                ModelConfig = {
                    Tokens = tokensPath,
                    NumThreads = threadCount,
                    Debug = 0
                },
                DecodingMethod = "greedy_search",
                MaxActivePaths = 4,
                EnableEndpoint = 1,
                Rule1MinTrailingSilence = Rule1MinTrailingSilence,
                Rule2MinTrailingSilence = Rule2MinTrailingSilence,
                Rule3MinUtteranceLength = Rule3MinUtteranceLength
            };

            switch (_modelType)
            {
                case SpeechRecognitionModelType.Online_Paraformer:
                    config.ModelConfig.Paraformer.Encoder = ModelFileResolver.ResolveRequiredFile(
                        metadata,
                        "Paraformer encoder",
                        fallbackReporter,
                        ModelFileCriteria.FromKeywords("encoder", int8QuantKeyword),
                        ModelFileCriteria.FromKeywords("encoder"));
                    config.ModelConfig.Paraformer.Decoder = ModelFileResolver.ResolveRequiredFile(
                        metadata,
                        "Paraformer decoder",
                        fallbackReporter,
                        ModelFileCriteria.FromKeywords("decoder", int8QuantKeyword),
                        ModelFileCriteria.FromKeywords("decoder"));
                    break;
                case SpeechRecognitionModelType.Online_Transducer:
                    config.DecodingMethod = "modified_beam_search";
                    config.ModelConfig.Transducer.Encoder = ModelFileResolver.ResolveRequiredFile(
                        metadata,
                        "Transducer encoder",
                        fallbackReporter,
                        ModelFileCriteria.FromKeywords("encoder", int8QuantKeyword),
                        ModelFileCriteria.FromKeywords("encoder"));
                    config.ModelConfig.Transducer.Decoder = ModelFileResolver.ResolveRequiredFile(
                        metadata,
                        "Transducer decoder",
                        fallbackReporter,
                        ModelFileCriteria.FromKeywords("decoder", int8QuantKeyword),
                        ModelFileCriteria.FromKeywords("decoder"));
                    config.ModelConfig.Transducer.Joiner = ModelFileResolver.ResolveRequiredFile(
                        metadata,
                        "Transducer joiner",
                        fallbackReporter,
                        ModelFileCriteria.FromKeywords("joiner", int8QuantKeyword),
                        ModelFileCriteria.FromKeywords("joiner"));
                    break;
                case SpeechRecognitionModelType.Online_Ctc:
                    config.DecodingMethod = "greedy_search";
                    config.ModelConfig.Zipformer2Ctc.Model = ModelFileResolver.ResolveRequiredFile(
                        metadata,
                        "CTC model",
                        fallbackReporter,
                        ModelFileCriteria.FromKeywords("model", "ctc", int8QuantKeyword),
                        ModelFileCriteria.FromKeywords("model", "ctc"));
                    break;
                default:
                    throw new NotSupportedException($"Unsupported online model type: {_modelType}");
            }

            return config;
        }

        private OfflineRecognizerConfig CreateOfflineRecognizerConfig(SherpaONNXModelMetadata metadata, int sampleRate, bool isMobilePlatform, SherpaONNXFeedbackReporter reporter)
        {
            var fallbackReporter = CreateFallbackReporter(metadata, reporter);
            var threadCount = ThreadingUtils.GetAdaptiveThreadCount();
            var int8QuantKeyword = isMobilePlatform ? "int8" : null;

            var tokensPath = ModelFileResolver.ResolveRequiredByKeywords(metadata, "token file", fallbackReporter, "tokens", "tokens.txt");

            var config = new OfflineRecognizerConfig
            {
                FeatConfig = { SampleRate = sampleRate, FeatureDim = 80 },
                ModelConfig = {
                    Tokens = tokensPath,
                    NumThreads = threadCount,
                    ModelType = string.Empty

                },
                DecodingMethod = "greedy_search",
                MaxActivePaths = 4,
                RuleFsts = string.Empty
            };

            switch (_modelType)
            {
                case SpeechRecognitionModelType.Offline_Transducer:

                    config.DecodingMethod = "modified_beam_search";
                    config.ModelConfig.Transducer.Encoder = ModelFileResolver.ResolveRequiredFile(
                        metadata,
                        "Transducer encoder",
                        fallbackReporter,
                        ModelFileCriteria.FromKeywords("encoder", int8QuantKeyword),
                        ModelFileCriteria.FromKeywords("encoder"));
                    config.ModelConfig.Transducer.Decoder = ModelFileResolver.ResolveRequiredFile(
                        metadata,
                        "Transducer decoder",
                        fallbackReporter,
                        ModelFileCriteria.FromKeywords("decoder", int8QuantKeyword),
                        ModelFileCriteria.FromKeywords("decoder"));
                    config.ModelConfig.Transducer.Joiner = ModelFileResolver.ResolveRequiredFile(
                        metadata,
                        "Transducer joiner",
                        fallbackReporter,
                        ModelFileCriteria.FromKeywords("joiner", int8QuantKeyword),
                        ModelFileCriteria.FromKeywords("joiner"));
                    if (config.DecodingMethod == "modified_beam_search")
                    {
                        var hotwordsPath = ModelFileResolver.ResolveOptionalByKeywords(metadata, fallbackReporter, "hotwords");
                        if (!string.IsNullOrEmpty(hotwordsPath))
                        {
                            config.HotwordsFile = hotwordsPath;
                        }
                    }
                    break;

                case SpeechRecognitionModelType.Offline_Paraformer:
                    config.ModelConfig.Paraformer.Model = ModelFileResolver.ResolveRequiredFile(
                        metadata,
                        "Paraformer model",
                        fallbackReporter,
                        ModelFileCriteria.FromKeywords("model", int8QuantKeyword),
                        ModelFileCriteria.FromKeywords("model"));
                    break;

                case SpeechRecognitionModelType.Offline_ZipformerCtc:
                    config.ModelConfig.ZipformerCtc.Model = ModelFileResolver.ResolveRequiredFile(
                        metadata,
                        "Zipformer CTC model",
                        fallbackReporter,
                        ModelFileCriteria.FromKeywords("model", int8QuantKeyword),
                        ModelFileCriteria.FromKeywords("model"));
                    break;

                case SpeechRecognitionModelType.Offline_Nemo_Ctc:
                    config.ModelConfig.NeMoCtc.Model = ModelFileResolver.ResolveRequiredFile(
                        metadata,
                        "NeMo CTC model",
                        fallbackReporter,
                        ModelFileCriteria.FromKeywords("model", int8QuantKeyword),
                        ModelFileCriteria.FromKeywords("model"));
                    break;

                case SpeechRecognitionModelType.Dolphin:
                    config.ModelConfig.Dolphin.Model = ModelFileResolver.ResolveRequiredFile(
                        metadata,
                        "Dolphin model",
                        fallbackReporter,
                        ModelFileCriteria.FromKeywords("model", int8QuantKeyword),
                        ModelFileCriteria.FromKeywords("model"));
                    break;

                case SpeechRecognitionModelType.TeleSpeech:
                    config.ModelConfig.TeleSpeechCtc = ModelFileResolver.ResolveRequiredFile(
                        metadata,
                        "TeleSpeech model",
                        fallbackReporter,
                        ModelFileCriteria.FromKeywords("model", int8QuantKeyword),
                        ModelFileCriteria.FromKeywords("model"));
                    break;

                case SpeechRecognitionModelType.Whisper:
                    config.ModelConfig.Whisper.Encoder = ModelFileResolver.ResolveRequiredFile(
                        metadata,
                        "Whisper encoder",
                        fallbackReporter,
                        ModelFileCriteria.FromKeywords("encoder", int8QuantKeyword),
                        ModelFileCriteria.FromKeywords("encoder"));
                    config.ModelConfig.Whisper.Decoder = ModelFileResolver.ResolveRequiredFile(
                        metadata,
                        "Whisper decoder",
                        fallbackReporter,
                        ModelFileCriteria.FromKeywords("decoder", int8QuantKeyword),
                        ModelFileCriteria.FromKeywords("decoder"));
                    config.ModelConfig.Whisper.Language = string.Empty;
                    config.ModelConfig.Whisper.Task = "transcribe";
                    break;

                case SpeechRecognitionModelType.Tdnn:
                    config.ModelConfig.Tdnn.Model = ModelFileResolver.ResolveRequiredFile(
                        metadata,
                        "TDNN model",
                        fallbackReporter,
                        ModelFileCriteria.FromKeywords("tdnn", int8QuantKeyword),
                        ModelFileCriteria.FromKeywords("tdnn"));
                    break;

                case SpeechRecognitionModelType.SenseVoice:

                    config.ModelConfig.SenseVoice.Model = ModelFileResolver.ResolveRequiredFile(
                        metadata,
                        "SenseVoice model",
                        fallbackReporter,
                        ModelFileCriteria.FromKeywords("model", int8QuantKeyword),
                        ModelFileCriteria.FromKeywords("model"));
                    config.ModelConfig.SenseVoice.UseInverseTextNormalization = 1;
                    config.ModelConfig.SenseVoice.Language = "auto";
                    break;

                case SpeechRecognitionModelType.Moonshine:
                    config.ModelConfig.Moonshine.Preprocessor = ModelFileResolver.ResolveRequiredFile(
                        metadata,
                        "Moonshine preprocessor",
                        fallbackReporter,
                        ModelFileCriteria.FromKeywords("preprocess", int8QuantKeyword),
                        ModelFileCriteria.FromKeywords("preprocess"));
                    config.ModelConfig.Moonshine.Encoder = ModelFileResolver.ResolveRequiredFile(
                        metadata,
                        "Moonshine encoder",
                        fallbackReporter,
                        ModelFileCriteria.FromKeywords("encode", int8QuantKeyword),
                        ModelFileCriteria.FromKeywords("encode"));
                    config.ModelConfig.Moonshine.UncachedDecoder = ModelFileResolver.ResolveRequiredFile(
                        metadata,
                        "Moonshine uncached decoder",
                        fallbackReporter,
                        ModelFileCriteria.FromKeywords("uncached_decode", int8QuantKeyword),
                        ModelFileCriteria.FromKeywords("uncached_decode"));
                    config.ModelConfig.Moonshine.CachedDecoder = ModelFileResolver.ResolveRequiredFile(
                        metadata,
                        "Moonshine cached decoder",
                        fallbackReporter,
                        ModelFileCriteria.FromKeywords("cached_decode", int8QuantKeyword),
                        ModelFileCriteria.FromKeywords("cached_decode"));
                    break;

                case SpeechRecognitionModelType.FireRedAsr:
                    config.ModelConfig.FireRedAsr.Encoder = ModelFileResolver.ResolveRequiredFile(
                        metadata,
                        "FireRed ASR encoder",
                        fallbackReporter,
                        ModelFileCriteria.FromKeywords("encoder", int8QuantKeyword),
                        ModelFileCriteria.FromKeywords("encoder"));
                    config.ModelConfig.FireRedAsr.Decoder = ModelFileResolver.ResolveRequiredFile(
                        metadata,
                        "FireRed ASR decoder",
                        fallbackReporter,
                        ModelFileCriteria.FromKeywords("decoder", int8QuantKeyword),
                        ModelFileCriteria.FromKeywords("decoder"));
                    break;
                case SpeechRecognitionModelType.Omnilingual:
                    config.ModelConfig.Omnilingual.Model = ModelFileResolver.ResolveRequiredFile(
                        metadata,
                        "Omnilingual ASR encoder",
                        fallbackReporter,
                        ModelFileCriteria.FromKeywords("model", int8QuantKeyword),
                        ModelFileCriteria.FromKeywords("model"));
                    break;

                default:
                    throw new NotSupportedException($"Unsupported offline model type: {_modelType}");
            }


            return config;
        }

        public Task<string> SpeechTranscriptionAsync(float[] audioSamplesFrame, int sampleRate, CancellationToken cancellationToken = default)
        {
            if (IsDisposed || audioSamplesFrame == null || audioSamplesFrame.Length == 0 || runner.IsDisposed)
            {
                return Task.FromResult(string.Empty);
            }

            return IsOnlineModel ?
              ProcessOnlineTranscriptionAsync(audioSamplesFrame, sampleRate, cancellationToken) :
              ProcessOfflineTranscriptionAsync(audioSamplesFrame, sampleRate, cancellationToken);
        }

        private Task<string> ProcessOnlineTranscriptionAsync(float[] audioSamplesFrame, int sampleRate, CancellationToken cancellationToken)
        {
            if (_onlineRecognizer == null || _onlineStream == null)
            {
                return Task.FromResult(string.Empty);
            }

            lock (_lockObject)
            {
                if (IsDisposed || _onlineStream == null) { return Task.FromResult(string.Empty); }

                _onlineStream.AcceptWaveform(sampleRate, audioSamplesFrame);
            }

            return runner.RunAsync<string>(ct =>
            {
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, cancellationToken);
                var combinedCt = linkedCts.Token;

                if (IsDisposed || _onlineRecognizer == null || _onlineStream == null)
                {
                    return Task.FromResult(string.Empty);
                }

                lock (_lockObject)
                {
                    if (IsDisposed || _onlineStream == null) { return Task.FromResult(string.Empty); }

                    DecodeOnlineStream(combinedCt);
                    var result = _onlineRecognizer.GetResult(_onlineStream);

                    if (_onlineRecognizer.IsEndpoint(_onlineStream))
                    {
                        HandleEndpointDetection(sampleRate, combinedCt);
                        result = _onlineRecognizer.GetResult(_onlineStream);
                        _onlineRecognizer.Reset(_onlineStream);
                    }

                    var text = result?.Text ?? string.Empty;
                    var cased = PostProcessCasing(text);
                    return Task.FromResult(cased);
                }
            });
        }

        private Task<string> ProcessOfflineTranscriptionAsync(float[] audioSamplesFrame, int sampleRate, CancellationToken cancellationToken)
        {
            if (_offlineRecognizer == null)
            {
                return Task.FromResult(string.Empty);
            }

            return runner.RunAsync<string>(ct =>
            {
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, cancellationToken);
                var combinedCt = linkedCts.Token;

                if (IsDisposed || _offlineRecognizer == null)
                {
                    return Task.FromResult(string.Empty);
                }

                // Create new offline stream for each transcription
                string result = string.Empty;
                using (var offlineStream = _offlineRecognizer.CreateStream())
                {
                    offlineStream.AcceptWaveform(sampleRate, audioSamplesFrame);
                    combinedCt.ThrowIfCancellationRequested();
                    _offlineRecognizer.Decode(offlineStream);
                    result = offlineStream.Result.Text;
                    result = PostProcessCasing(result);
                }
                return Task.FromResult(result);
            });
        }

        private void DecodeOnlineStream(CancellationToken cancellationToken)
        {
            while (!IsDisposed && _onlineRecognizer != null && _onlineStream != null && _onlineRecognizer.IsReady(_onlineStream))
            {
                cancellationToken.ThrowIfCancellationRequested();
                _onlineRecognizer.Decode(_onlineStream);
            }
        }

        private void HandleEndpointDetection(int sampleRate, CancellationToken cancellationToken)
        {
            if (IsDisposed || _onlineStream == null) { return; }

            // Add tail padding to ensure final words are processed
            var tailPadding = new float[sampleRate]; // 1 second of silence
            _onlineStream.AcceptWaveform(sampleRate, tailPadding);

            DecodeOnlineStream(cancellationToken);
        }

        // --- English sentence casing post-processor (fast + safe for mixed languages) ---
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool HasAsciiLetter(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return false;
            }


            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                // Fast bounds check: map to uint to avoid branch mispredictions
                if ((uint)(c - 'A') <= ('Z' - 'A') || (uint)(c - 'a') <= ('z' - 'a'))
                {

                    return true;
                }

            }
            return false;
        }

        /// <summary>
        /// Apply English sentence casing only when the text contains ASCII letters.
        /// /// Non-English scripts (CJK, etc.) are returned unchanged. Mixed content is safe:
        /// non-Latin characters are unaffected by the caser.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static string PostProcessCasing(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }
            // If the text has no ASCII letters, skip casing to avoid touching other languages.

            if (!HasAsciiLetter(text))
            {
                return text;
            }

            // Delegate to the high-performance caser (handles punctuation, acronyms, phrases, etc.)

            return EnglishSentenceCaser.ToSentenceCase(text);
        }

        protected override void OnDestroy()
        {
            lock (_lockObject)
            {
                _onlineStream?.Dispose();
                _onlineRecognizer?.Dispose();
                _offlineRecognizer?.Dispose();

                _onlineStream = null;
                _onlineRecognizer = null;
                _offlineRecognizer = null;
            }
        }
    }
}
