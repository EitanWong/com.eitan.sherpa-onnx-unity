// SpeechRecognition.cs (Refactored and Optimized)

namespace Eitan.SherpaONNXUnity.Runtime.Modules
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Runtime.CompilerServices;
    using UnityEngine;

    using Eitan.SherpaONNXUnity.Runtime.Native;
    using Eitan.SherpaONNXUnity.Runtime.Utilities;
    using Eitan.SherpaONNXUnity.Runtime.Utilities.Lexicon;
    using System.Collections.Generic;


    public class SpeechRecognition : SherpaONNXModule
    {
        public sealed class Options
        {
            public float Rule1MinTrailingSilence { get; set; } = 2.4f;
            public float Rule2MinTrailingSilence { get; set; } = 1.2f;
            public float Rule3MinUtteranceLength { get; set; } = 30f;
        }

        private OnlineRecognizer _onlineRecognizer;
        private OnlineStream _onlineStream;
        private OfflineRecognizer _offlineRecognizer;

        private SpeechRecognitionModelType _modelType;
        private readonly object _lockObject = new object();
        private int _modelSampleRate;
        private float[] _endpointPaddingBuffer;
        private int _endpointPaddingSampleRate;
        public bool IsOnlineModel { get; private set; }
        private readonly SemaphoreSlim _transcriptionSemaphore = new SemaphoreSlim(1, 1);
        private readonly Options _options;
        private readonly int _maxPendingTranscriptions;
        private readonly bool _dropIfBusy;
        private int _pendingTranscriptions;

        private readonly struct RecognizerConfigContext
        {
            public RecognizerConfigContext(int threadCount, string tokensPath, string int8Keyword, Action<string> fallbackReporter)
            {
                ThreadCount = threadCount;
                TokensPath = tokensPath;
                Int8Keyword = int8Keyword;
                FallbackReporter = fallbackReporter;
            }

            public int ThreadCount { get; }
            public string TokensPath { get; }
            public string Int8Keyword { get; }
            public Action<string> FallbackReporter { get; }
        }

        public enum TranscriptionStatus
        {
            Success,
            NotReady,
            Disposed,
            Cancelled,
            Busy,
            Error
        }

        public readonly struct TranscriptionResult
        {
            public TranscriptionResult(TranscriptionStatus status, string text = "", bool isFinal = false, Exception error = null)
            {
                Status = status;
                Text = text ?? string.Empty;
                IsFinal = isFinal;
                Error = error;
            }

            public TranscriptionStatus Status { get; }
            public string Text { get; }
            public bool IsFinal { get; }
            public Exception Error { get; }
        }

        private static IEnumerable<string> CollectOfflineModelFiles(OfflineRecognizerConfig config)
        {
            var list = new List<string>(8);
            if (!string.IsNullOrEmpty(config.ModelConfig.Paraformer.Model))
            {
                list.Add(config.ModelConfig.Paraformer.Model);
            }


            if (!string.IsNullOrEmpty(config.ModelConfig.Transducer.Encoder))
            {
                list.Add(config.ModelConfig.Transducer.Encoder);
            }


            if (!string.IsNullOrEmpty(config.ModelConfig.Transducer.Decoder))
            {
                list.Add(config.ModelConfig.Transducer.Decoder);
            }

            if (!string.IsNullOrEmpty(config.ModelConfig.Transducer.Joiner))
            {
                list.Add(config.ModelConfig.Transducer.Joiner);
            }

            if (!string.IsNullOrEmpty(config.ModelConfig.NeMoCtc.Model))
            {
                list.Add(config.ModelConfig.NeMoCtc.Model);
            }

            if (!string.IsNullOrEmpty(config.ModelConfig.ZipformerCtc.Model))
            {
                list.Add(config.ModelConfig.ZipformerCtc.Model);
            }


            return list;
        }

        protected override SherpaONNXModuleType ModuleType => SherpaONNXModuleType.SpeechRecognition;

        public SpeechRecognition(string modelID, int sampleRate = 16000, SherpaONNXFeedbackReporter reporter = null, bool startImmediately = true, Options options = null, int maxPendingTranscriptions = 2, bool dropIfBusy = true)
            : base(modelID, sampleRate, reporter, startImmediately)
        {
            IsOnlineModel = SherpaUtils.Model.IsOnlineModel(modelID);
            _modelType = SherpaUtils.Model.GetSpeechRecognitionModelType(modelID);
            _options = options ?? new Options();
            _maxPendingTranscriptions = Math.Max(1, maxPendingTranscriptions);
            _dropIfBusy = dropIfBusy;
        }

        protected override async Task<bool> Initialization(SherpaONNXModelMetadata metadata, int sampleRate, bool isMobilePlatform, SherpaONNXFeedbackReporter reporter, CancellationToken ct)
        {
            try
            {
                reporter?.Report(new LoadFeedback(metadata, message: $"Start Loading: {metadata.modelId}"));

                _modelSampleRate = metadata?.sampleRate > 0 ? metadata.sampleRate : sampleRate;

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
            var context = BuildConfigContext(metadata, sampleRate, isMobilePlatform, reporter);
            var config = CreateOnlineRecognizerConfig(metadata, sampleRate, context);

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
            var context = BuildConfigContext(metadata, sampleRate, isMobilePlatform, reporter);
            var config = CreateOfflineRecognizerConfig(metadata, sampleRate, context);

            return await runner.RunAsync<bool>(cancellationToken =>
             {
                 using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, cancellationToken);
                 linkedCts.Token.ThrowIfCancellationRequested();

                 if (IsDisposed) { return Task.FromResult(false); }

                 try
                 {
                     _offlineRecognizer = new OfflineRecognizer(config);
                     var initialized = IsSuccessInitializad(_offlineRecognizer);

                     if (initialized)
                     {
                         reporter?.Report(new LoadFeedback(metadata, message: $"Loaded offline model: {metadata.modelId}"));
                     }
                     else
                     {
                         reporter?.Report(new FailedFeedback(metadata, message: $"Failed to initialize offline model: {metadata.modelId}"));
                     }

                     return Task.FromResult(initialized);
                 }
                 catch (OperationCanceledException)
                 {
                     throw;
                 }
                 catch (Exception ex)
                 {
                     reporter?.Report(new FailedFeedback(metadata, message: ex.Message, exception: ex));
                     return Task.FromResult(false);
                 }
             });
        }

        private OnlineRecognizerConfig CreateOnlineRecognizerConfig(SherpaONNXModelMetadata metadata, int sampleRate, RecognizerConfigContext context)
        {
            var config = new OnlineRecognizerConfig
            {
                FeatConfig = { SampleRate = sampleRate, FeatureDim = 80 },
                ModelConfig = {
                    Tokens = context.TokensPath,
                    NumThreads = context.ThreadCount,
                    Debug = 0
                },
                DecodingMethod = "greedy_search",
                MaxActivePaths = 4,
                EnableEndpoint = 1,
                Rule1MinTrailingSilence = _options.Rule1MinTrailingSilence,
                Rule2MinTrailingSilence = _options.Rule2MinTrailingSilence,
                Rule3MinUtteranceLength = _options.Rule3MinUtteranceLength
            };

            switch (_modelType)
            {
                case SpeechRecognitionModelType.Online_Paraformer:
                    config.ModelConfig.Paraformer.Encoder = ModelFileResolver.ResolveRequiredFile(
                        metadata,
                        "Paraformer encoder",
                        context.FallbackReporter,
                        ModelFileCriteria.FromKeywords("encoder", context.Int8Keyword),
                        ModelFileCriteria.FromKeywords("encoder"));
                    config.ModelConfig.Paraformer.Decoder = ModelFileResolver.ResolveRequiredFile(
                        metadata,
                        "Paraformer decoder",
                        context.FallbackReporter,
                        ModelFileCriteria.FromKeywords("decoder", context.Int8Keyword),
                        ModelFileCriteria.FromKeywords("decoder"));
                    break;
                case SpeechRecognitionModelType.Online_Transducer:
                    config.DecodingMethod = "modified_beam_search";
                    config.ModelConfig.Transducer.Encoder = ModelFileResolver.ResolveRequiredFile(
                        metadata,
                        "Transducer encoder",
                        context.FallbackReporter,
                        ModelFileCriteria.FromKeywords("encoder", context.Int8Keyword),
                        ModelFileCriteria.FromKeywords("encoder"));
                    config.ModelConfig.Transducer.Decoder = ModelFileResolver.ResolveRequiredFile(
                        metadata,
                        "Transducer decoder",
                        context.FallbackReporter,
                        ModelFileCriteria.FromKeywords("decoder", context.Int8Keyword),
                        ModelFileCriteria.FromKeywords("decoder"));
                    config.ModelConfig.Transducer.Joiner = ModelFileResolver.ResolveRequiredFile(
                        metadata,
                        "Transducer joiner",
                        context.FallbackReporter,
                        ModelFileCriteria.FromKeywords("joiner", context.Int8Keyword),
                        ModelFileCriteria.FromKeywords("joiner"));
                    break;
                case SpeechRecognitionModelType.Online_Ctc:
                    config.DecodingMethod = "greedy_search";
                    config.ModelConfig.Zipformer2Ctc.Model = ModelFileResolver.ResolveRequiredFile(
                        metadata,
                        "CTC model",
                        context.FallbackReporter,
                        ModelFileCriteria.FromKeywords("model", "ctc", context.Int8Keyword),
                        ModelFileCriteria.FromKeywords("model", "ctc"));
                    break;
                default:
                    throw new NotSupportedException($"Unsupported online model type: {_modelType}");
            }

            return config;
        }

        private OfflineRecognizerConfig CreateOfflineRecognizerConfig(SherpaONNXModelMetadata metadata, int sampleRate, RecognizerConfigContext context)
        {
            var config = new OfflineRecognizerConfig
            {
                FeatConfig = { SampleRate = sampleRate, FeatureDim = 80 },
                ModelConfig = {
                    Tokens = context.TokensPath,
                    NumThreads = context.ThreadCount,
                    ModelType = GetOfflineModelTypeString(_modelType)

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
                        context.FallbackReporter,
                        ModelFileCriteria.FromKeywords("encoder", context.Int8Keyword),
                        ModelFileCriteria.FromKeywords("encoder"));
                    config.ModelConfig.Transducer.Decoder = ModelFileResolver.ResolveRequiredFile(
                        metadata,
                        "Transducer decoder",
                        context.FallbackReporter,
                        ModelFileCriteria.FromKeywords("decoder", context.Int8Keyword),
                        ModelFileCriteria.FromKeywords("decoder"));
                    config.ModelConfig.Transducer.Joiner = ModelFileResolver.ResolveRequiredFile(
                        metadata,
                        "Transducer joiner",
                        context.FallbackReporter,
                        ModelFileCriteria.FromKeywords("joiner", context.Int8Keyword),
                        ModelFileCriteria.FromKeywords("joiner"));
                    if (config.DecodingMethod == "modified_beam_search")
                    {
                        var hotwordsPath = ModelFileResolver.ResolveOptionalByKeywords(metadata, context.FallbackReporter, "hotwords");
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
                        context.FallbackReporter,
                        ModelFileCriteria.FromKeywords("model", context.Int8Keyword),
                        ModelFileCriteria.FromKeywords("model"));
                    break;

                case SpeechRecognitionModelType.Offline_ZipformerCtc:
                    config.ModelConfig.ZipformerCtc.Model = ModelFileResolver.ResolveRequiredFile(
                        metadata,
                        "Zipformer CTC model",
                        context.FallbackReporter,
                        ModelFileCriteria.FromKeywords("model", context.Int8Keyword),
                        ModelFileCriteria.FromKeywords("model"));
                    break;

                case SpeechRecognitionModelType.Offline_Nemo_Ctc:
                    config.ModelConfig.NeMoCtc.Model = ModelFileResolver.ResolveRequiredFile(
                        metadata,
                        "NeMo CTC model",
                        context.FallbackReporter,
                        ModelFileCriteria.FromKeywords("model", context.Int8Keyword),
                        ModelFileCriteria.FromKeywords("model"));
                    break;

                case SpeechRecognitionModelType.Dolphin:
                    config.ModelConfig.Dolphin.Model = ModelFileResolver.ResolveRequiredFile(
                        metadata,
                        "Dolphin model",
                        context.FallbackReporter,
                        ModelFileCriteria.FromKeywords("model", context.Int8Keyword),
                        ModelFileCriteria.FromKeywords("model"));
                    break;

                case SpeechRecognitionModelType.TeleSpeech:
                    config.ModelConfig.TeleSpeechCtc = ModelFileResolver.ResolveRequiredFile(
                        metadata,
                        "TeleSpeech model",
                        context.FallbackReporter,
                        ModelFileCriteria.FromKeywords("model", context.Int8Keyword),
                        ModelFileCriteria.FromKeywords("model"));
                    break;

                case SpeechRecognitionModelType.Whisper:
                    config.ModelConfig.Whisper.Encoder = ModelFileResolver.ResolveRequiredFile(
                        metadata,
                        "Whisper encoder",
                        context.FallbackReporter,
                        ModelFileCriteria.FromKeywords("encoder", context.Int8Keyword),
                        ModelFileCriteria.FromKeywords("encoder"));
                    config.ModelConfig.Whisper.Decoder = ModelFileResolver.ResolveRequiredFile(
                        metadata,
                        "Whisper decoder",
                        context.FallbackReporter,
                        ModelFileCriteria.FromKeywords("decoder", context.Int8Keyword),
                        ModelFileCriteria.FromKeywords("decoder"));
                    config.ModelConfig.Whisper.Language = string.Empty;
                    config.ModelConfig.Whisper.Task = "transcribe";
                    break;

                case SpeechRecognitionModelType.Tdnn:
                    config.ModelConfig.Tdnn.Model = ModelFileResolver.ResolveRequiredFile(
                        metadata,
                        "TDNN model",
                        context.FallbackReporter,
                        ModelFileCriteria.FromKeywords("tdnn", context.Int8Keyword),
                        ModelFileCriteria.FromKeywords("tdnn"));
                    break;

                case SpeechRecognitionModelType.SenseVoice:

                    config.ModelConfig.SenseVoice.Model = ModelFileResolver.ResolveRequiredFile(
                        metadata,
                        "SenseVoice model",
                        context.FallbackReporter,
                        ModelFileCriteria.FromKeywords("model", context.Int8Keyword),
                        ModelFileCriteria.FromKeywords("model"));
                    config.ModelConfig.SenseVoice.UseInverseTextNormalization = 1;
                    config.ModelConfig.SenseVoice.Language = "auto";
                    break;

                case SpeechRecognitionModelType.Moonshine:
                    config.ModelConfig.Moonshine.Preprocessor = ModelFileResolver.ResolveRequiredFile(
                        metadata,
                        "Moonshine preprocessor",
                        context.FallbackReporter,
                        ModelFileCriteria.FromKeywords("preprocess", context.Int8Keyword),
                        ModelFileCriteria.FromKeywords("preprocess"));
                    config.ModelConfig.Moonshine.Encoder = ModelFileResolver.ResolveRequiredFile(
                        metadata,
                        "Moonshine encoder",
                        context.FallbackReporter,
                        ModelFileCriteria.FromKeywords("encode", context.Int8Keyword),
                        ModelFileCriteria.FromKeywords("encode"));
                    config.ModelConfig.Moonshine.UncachedDecoder = ModelFileResolver.ResolveRequiredFile(
                        metadata,
                        "Moonshine uncached decoder",
                        context.FallbackReporter,
                        ModelFileCriteria.FromKeywords("uncached_decode", context.Int8Keyword),
                        ModelFileCriteria.FromKeywords("uncached_decode"));
                    config.ModelConfig.Moonshine.CachedDecoder = ModelFileResolver.ResolveRequiredFile(
                        metadata,
                        "Moonshine cached decoder",
                        context.FallbackReporter,
                        ModelFileCriteria.FromKeywords("cached_decode", context.Int8Keyword),
                        ModelFileCriteria.FromKeywords("cached_decode"));
                    break;

                case SpeechRecognitionModelType.FireRedAsr:
                    config.ModelConfig.FireRedAsr.Encoder = ModelFileResolver.ResolveRequiredFile(
                        metadata,
                        "FireRed ASR encoder",
                        context.FallbackReporter,
                        ModelFileCriteria.FromKeywords("encoder", context.Int8Keyword),
                        ModelFileCriteria.FromKeywords("encoder"));
                    config.ModelConfig.FireRedAsr.Decoder = ModelFileResolver.ResolveRequiredFile(
                        metadata,
                        "FireRed ASR decoder",
                        context.FallbackReporter,
                        ModelFileCriteria.FromKeywords("decoder", context.Int8Keyword),
                        ModelFileCriteria.FromKeywords("decoder"));
                    break;
                case SpeechRecognitionModelType.Omnilingual:
                    config.ModelConfig.Omnilingual.Model = ModelFileResolver.ResolveRequiredFile(
                        metadata,
                        "Omnilingual ASR encoder",
                        context.FallbackReporter,
                        ModelFileCriteria.FromKeywords("model", context.Int8Keyword),
                        ModelFileCriteria.FromKeywords("model"));
                    break;

                default:
                    throw new NotSupportedException($"Unsupported offline model type: {_modelType}");
            }


            return config;
        }

        private static string GetOfflineModelTypeString(SpeechRecognitionModelType modelType)
        {
            switch (modelType)
            {
                case SpeechRecognitionModelType.Offline_Transducer: return "transducer";
                case SpeechRecognitionModelType.Offline_Paraformer: return "paraformer";
                case SpeechRecognitionModelType.Offline_ZipformerCtc: return "zipformer_ctc";
                case SpeechRecognitionModelType.Offline_Nemo_Ctc: return "nemo_ctc";
                case SpeechRecognitionModelType.Dolphin: return "dolphin";
                case SpeechRecognitionModelType.TeleSpeech: return "telespeech_ctc";
                case SpeechRecognitionModelType.Whisper: return "whisper";
                case SpeechRecognitionModelType.Tdnn: return "tdnn";
                case SpeechRecognitionModelType.SenseVoice: return "sensevoice";
                case SpeechRecognitionModelType.Moonshine: return "moonshine";
                case SpeechRecognitionModelType.FireRedAsr: return "fire_red_asr";
                case SpeechRecognitionModelType.Omnilingual: return "omnilingual";
                default: throw new NotSupportedException($"Unsupported offline model type: {modelType}");
            }
        }

        public async Task<TranscriptionResult> TranscribeAsync(float[] audioSamplesFrame, int sampleRate, CancellationToken cancellationToken = default)
        {
            if (IsDisposed || runner.IsDisposed)
            {
                return new TranscriptionResult(TranscriptionStatus.Disposed);
            }

            if (!Initialized)
            {
                return new TranscriptionResult(TranscriptionStatus.NotReady);
            }

            if (audioSamplesFrame == null || audioSamplesFrame.Length == 0 || sampleRate <= 0)
            {
                return new TranscriptionResult(TranscriptionStatus.NotReady);
            }

            var expectedSampleRate = _modelSampleRate > 0 ? _modelSampleRate : sampleRate;
            if (expectedSampleRate > 0 && sampleRate != expectedSampleRate)
            {
                SherpaLog.Warning($"[{nameof(SpeechRecognition)}] Sample rate mismatch. Expected {expectedSampleRate} Hz for model '{ModelId}', but received {sampleRate} Hz.");
                return new TranscriptionResult(TranscriptionStatus.Error, error: new InvalidOperationException($"Sample rate mismatch: expected {expectedSampleRate} Hz"));
            }

            CancellationTokenSource linkedCts = null;
            bool acquired = false;
            bool countedPending = false;
            try
            {
                linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

                if (_dropIfBusy)
                {
                    acquired = _transcriptionSemaphore.Wait(0);
                    if (!acquired)
                    {
                        return new TranscriptionResult(TranscriptionStatus.Busy);
                    }
                }
                else
                {
                    await _transcriptionSemaphore.WaitAsync(linkedCts.Token).ConfigureAwait(false);
                    acquired = true;
                }

                var pending = Interlocked.Increment(ref _pendingTranscriptions);
                countedPending = true;
                if (_dropIfBusy && pending > _maxPendingTranscriptions)
                {
                    return new TranscriptionResult(TranscriptionStatus.Busy);
                }

                if (IsDisposed || runner.IsDisposed)
                {
                    return new TranscriptionResult(TranscriptionStatus.Disposed);
                }

                return IsOnlineModel
                    ? await ProcessOnlineTranscriptionAsync(audioSamplesFrame, expectedSampleRate, linkedCts.Token).ConfigureAwait(false)
                    : await ProcessOfflineTranscriptionAsync(audioSamplesFrame, expectedSampleRate, linkedCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException oce)
            {
                return new TranscriptionResult(TranscriptionStatus.Cancelled, error: oce);
            }
            catch (Exception ex)
            {
                return new TranscriptionResult(TranscriptionStatus.Error, error: ex);
            }
            finally
            {
                if (acquired)
                {
                    _transcriptionSemaphore.Release();
                }

                if (countedPending)
                {
                    Interlocked.Decrement(ref _pendingTranscriptions);
                }
                linkedCts?.Dispose();
            }
        }

        public async Task<string> SpeechTranscriptionAsync(float[] audioSamplesFrame, int sampleRate, CancellationToken cancellationToken = default)
        {
            var result = await TranscribeAsync(audioSamplesFrame, sampleRate, cancellationToken).ConfigureAwait(false);
            switch (result.Status)
            {
                case TranscriptionStatus.Success:
                    return result.Text ?? string.Empty;
                case TranscriptionStatus.Cancelled:
                    throw result.Error as OperationCanceledException ?? new OperationCanceledException("Transcription was cancelled.", result.Error, cancellationToken);
                case TranscriptionStatus.Error:
                    if (result.Error != null)
                    {
                        throw result.Error;
                    }
                    throw new InvalidOperationException("Transcription failed for an unknown reason.");
                case TranscriptionStatus.Busy:
                    SherpaLog.Warning($"[{nameof(SpeechRecognition)}] Dropped transcription request because the recognizer is busy.");
                    return string.Empty;
                case TranscriptionStatus.NotReady:
                case TranscriptionStatus.Disposed:
                default:
                    return string.Empty;
            }
        }

        private Task<TranscriptionResult> ProcessOnlineTranscriptionAsync(float[] audioSamplesFrame, int sampleRate, CancellationToken cancellationToken)
        {
            if (_onlineRecognizer == null || _onlineStream == null)
            {
                return Task.FromResult(new TranscriptionResult(TranscriptionStatus.NotReady));
            }

            lock (_lockObject)
            {
                if (IsDisposed || _onlineStream == null) { return Task.FromResult(new TranscriptionResult(TranscriptionStatus.Disposed)); }

                _onlineStream.AcceptWaveform(sampleRate, audioSamplesFrame);
            }

            return runner.RunAsync<TranscriptionResult>(ct =>
            {
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, cancellationToken);
                var combinedCt = linkedCts.Token;

                if (IsDisposed || _onlineRecognizer == null || _onlineStream == null)
                {
                    return Task.FromResult(new TranscriptionResult(TranscriptionStatus.Disposed));
                }

                lock (_lockObject)
                {
                    if (IsDisposed || _onlineStream == null) { return Task.FromResult(new TranscriptionResult(TranscriptionStatus.Disposed)); }

                    var isFinal = false;
                    DecodeOnlineStream(combinedCt);
                    var result = _onlineRecognizer.GetResult(_onlineStream);

                    if (_onlineRecognizer.IsEndpoint(_onlineStream))
                    {
                        isFinal = true;
                        HandleEndpointDetection(sampleRate, combinedCt);
                        _onlineStream.InputFinished();
                        DecodeOnlineStream(combinedCt);
                        result = _onlineRecognizer.GetResult(_onlineStream);
                        _onlineRecognizer.Reset(_onlineStream);
                    }

                    var text = result?.Text ?? string.Empty;
                    var cased = PostProcessCasing(text);
                    return Task.FromResult(new TranscriptionResult(TranscriptionStatus.Success, cased, isFinal));
                }
            });
        }

        private RecognizerConfigContext BuildConfigContext(SherpaONNXModelMetadata metadata, int sampleRate, bool isMobilePlatform, SherpaONNXFeedbackReporter reporter)
        {
            var fallbackReporter = CreateFallbackReporter(metadata, reporter);
            var threadCount = ThreadingUtils.GetAdaptiveThreadCount();
            var int8QuantKeyword = isMobilePlatform ? "int8" : null;
            var tokensPath = ModelFileResolver.ResolveRequiredByKeywords(metadata, "token file", fallbackReporter, "tokens", "tokens.txt");

            return new RecognizerConfigContext(threadCount, tokensPath, int8QuantKeyword, fallbackReporter);
        }

        private Task<TranscriptionResult> ProcessOfflineTranscriptionAsync(float[] audioSamplesFrame, int sampleRate, CancellationToken cancellationToken)
        {
            if (_offlineRecognizer == null)
            {
                return Task.FromResult(new TranscriptionResult(TranscriptionStatus.NotReady));
            }

            return runner.RunAsync<TranscriptionResult>(ct =>
            {
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, cancellationToken);
                var combinedCt = linkedCts.Token;

                if (IsDisposed || _offlineRecognizer == null)
                {
                    return Task.FromResult(new TranscriptionResult(TranscriptionStatus.Disposed));
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
                return Task.FromResult(new TranscriptionResult(TranscriptionStatus.Success, result, isFinal: true));
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

            var tailPadding = EnsureEndpointPaddingBuffer(sampleRate);
            _onlineStream.AcceptWaveform(sampleRate, tailPadding);

            DecodeOnlineStream(cancellationToken);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float[] EnsureEndpointPaddingBuffer(int sampleRate)
        {
            if (_endpointPaddingBuffer == null || _endpointPaddingSampleRate != sampleRate || _endpointPaddingBuffer.Length < sampleRate)
            {
                _endpointPaddingBuffer = new float[sampleRate];
                _endpointPaddingSampleRate = sampleRate;
            }

            return _endpointPaddingBuffer;
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
            _transcriptionSemaphore.Dispose();
        }
    }
}
