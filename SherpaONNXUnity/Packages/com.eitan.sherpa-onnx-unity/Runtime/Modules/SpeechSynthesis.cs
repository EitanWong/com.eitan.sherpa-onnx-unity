// SpeechSynthesis.cs

namespace Eitan.SherpaONNXUnity.Runtime.Modules
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Eitan.SherpaONNXUnity.Runtime.Utilities;

    using Eitan.SherpaONNXUnity.Runtime.Native;
    using UnityEngine;

    public class SpeechSynthesis : SherpaONNXModule
    {

        private OfflineTts _tts;
        private int _activeGenerationCount;
        private TaskCompletionSource<bool> _shutdownCompletionSource;

        protected override SherpaONNXModuleType ModuleType => SherpaONNXModuleType.SpeechSynthesis;

        public SpeechSynthesis(string modelID, int sampleRate = -1, SherpaONNXFeedbackReporter reporter = null)
            : base(modelID, sampleRate, reporter)
        {

        }

        protected override async Task<bool> Initialization(SherpaONNXModelMetadata metadata, int sampleRate, bool isMobilePlatform, SherpaONNXFeedbackReporter reporter, CancellationToken ct)
        {
            try
            {
                reporter?.Report(new LoadFeedback(metadata, message: $"Start Loading: {metadata.modelId}"));
                TryReportAndroid32BitRuntimeRisk(metadata, reporter, "SpeechSynthesis");
                var modelType = Utilities.SherpaUtils.Model.ResolveSpeechSynthesisModelType(metadata);
                var ttsConfig = await CreateTtsConfig(modelType, metadata, isMobilePlatform, reporter, ct);

                return await runner.RunAsync<bool>(cancellationToken =>
                {
                    try
                    {

                        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, cancellationToken);
                        linkedCts.Token.ThrowIfCancellationRequested();

                        if (IsDisposed) { return Task.FromResult(false); }

                        reporter?.Report(new LoadFeedback(metadata, message: $"Loading TTS model: {metadata.modelId}"));
                        _tts = new OfflineTts(ttsConfig);
                        var initialized = IsSuccessInitializad(_tts);
                        if (initialized)
                        {
                            reporter?.Report(new LoadFeedback(metadata, message: $"TTS model loaded successfully: {metadata.modelId}"));
                        }
                        return Task.FromResult(initialized);
                    }
                    catch (System.Exception ex)
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

        private async Task<OfflineTtsConfig> CreateTtsConfig(SpeechSynthesisModelType modelType, SherpaONNXModelMetadata metadata, bool isMobilePlatform, SherpaONNXFeedbackReporter reporter, CancellationToken ct)
        {
            var fallbackReporter = CreateFallbackReporter(metadata, reporter);
            var ttsModelConfig = new OfflineTtsConfig();
            var int8QuantKeyword = isMobilePlatform ? "int8" : null;

            var ruleFsts = ModelFileResolver.FilterValidFiles(metadata.GetModelFilesByExtensionName(".fst"), fallbackReporter);
            ttsModelConfig.RuleFsts = string.Join(",", ruleFsts);

            var ruleFars = ModelFileResolver.FilterValidFiles(metadata.GetModelFilesByExtensionName(".far"), fallbackReporter);
            ttsModelConfig.RuleFars = string.Join(",", ruleFars);

            ttsModelConfig.Model.NumThreads = ThreadingUtils.GetAdaptiveThreadCount();

            switch (modelType)
            {
                case SpeechSynthesisModelType.Vits:
                    {
                        var vitsModel = ModelFileResolver.ResolveRequiredFile(
                            metadata,
                            "VITS acoustic model",
                            fallbackReporter,
                            ModelFileCriteria.FromKeywords("model", "en_US", "vits", "theresa", "eula", ".onnx", int8QuantKeyword),
                            ModelFileCriteria.FromKeywords("model", "en_US", "vits", "theresa", "eula", ".onnx"));

                        ttsModelConfig.Model.Vits.Model = vitsModel;
                        ttsModelConfig.Model.Vits.Tokens = ModelFileResolver.ResolveRequiredByKeywords(metadata, "VITS tokens", fallbackReporter, "tokens.txt");

                        var lexiconPath = ModelFileResolver.ResolveOptionalByKeywords(metadata, fallbackReporter, "lexicon");
                        if (!string.IsNullOrEmpty(lexiconPath))
                        {
                            ttsModelConfig.Model.Vits.Lexicon = lexiconPath;
                        }

                        var dictDir = ModelFileResolver.ResolveOptionalDirectoryByKeywords(metadata, fallbackReporter, "dict");
                        if (!string.IsNullOrEmpty(dictDir))
                        {
                            ttsModelConfig.Model.Vits.DictDir = dictDir;
                        }

                        var dataDir = ModelFileResolver.ResolveOptionalDirectoryByKeywords(metadata, fallbackReporter, "espeak-ng-data");
                        if (!string.IsNullOrEmpty(dataDir))
                        {
                            ttsModelConfig.Model.Vits.DataDir = dataDir;
                        }
                        break;
                    }

                case SpeechSynthesisModelType.Matcha:
                    {
                        var vocoderMetadata = await SherpaONNXModelRegistry.Instance.GetMetadataAsync("vocos-22khz-univ", ct);

                        var vocoderPrepare = await SherpaUtils.Prepare.PrepareAndLoadModelWithResultAsync(vocoderMetadata, reporter, ct).ConfigureAwait(false);
                        if (!vocoderPrepare.Success)
                        {
                            if (vocoderPrepare.ErrorCode == PrepareErrorCode.Cancelled)
                            {
                                throw new OperationCanceledException("Vocoder preparation canceled.", ct);
                            }

                            throw new InvalidOperationException($"Vocoder prepare failed ({vocoderPrepare.ErrorCode}): {vocoderPrepare.Message}");
                        }
                        var vocoderFallback = CreateFallbackReporter(vocoderMetadata, reporter);

                        ttsModelConfig.Model.Matcha.AcousticModel = ModelFileResolver.ResolveRequiredFile(
                            metadata,
                            "Matcha acoustic model",
                            fallbackReporter,
                            ModelFileCriteria.FromKeywords("matcha", "model", int8QuantKeyword),
                            ModelFileCriteria.FromKeywords("matcha", "model"));
                        ttsModelConfig.Model.Matcha.Vocoder = ModelFileResolver.ResolveRequiredFile(
                            vocoderMetadata,
                            "Matcha vocoder",
                            vocoderFallback,
                            ModelFileCriteria.FromKeywords("vocos"));
                        ttsModelConfig.Model.Matcha.Tokens = ModelFileResolver.ResolveRequiredByKeywords(metadata, "Matcha tokens", fallbackReporter, "tokens.txt");

                        var matchaLexicon = ModelFileResolver.ResolveOptionalByKeywords(metadata, fallbackReporter, "lexicon");
                        if (!string.IsNullOrEmpty(matchaLexicon))
                        {
                            ttsModelConfig.Model.Matcha.Lexicon = matchaLexicon;
                        }

                        var matchaDictDir = ModelFileResolver.ResolveOptionalDirectoryByKeywords(metadata, fallbackReporter, "dict");
                        if (!string.IsNullOrEmpty(matchaDictDir))
                        {
                            ttsModelConfig.Model.Matcha.DictDir = matchaDictDir;
                        }

                        var matchaDataDir = ModelFileResolver.ResolveOptionalDirectoryByKeywords(metadata, fallbackReporter, "espeak-ng-data");
                        if (!string.IsNullOrEmpty(matchaDataDir))
                        {
                            ttsModelConfig.Model.Matcha.DataDir = matchaDataDir;
                        }
                        break;
                    }

                case SpeechSynthesisModelType.Kokoro:
                    {
                        ttsModelConfig.Model.Kokoro.Model = ModelFileResolver.ResolveRequiredFile(
                            metadata,
                            "Kokoro model",
                            fallbackReporter,
                            ModelFileCriteria.FromKeywords("model", "kokoro", int8QuantKeyword),
                            ModelFileCriteria.FromKeywords("model", "kokoro"));
                        var kokoroVoices = ModelFileResolver.ResolveOptionalByKeywords(metadata, fallbackReporter, "voices");
                        if (!string.IsNullOrEmpty(kokoroVoices))
                        {
                            ttsModelConfig.Model.Kokoro.Voices = kokoroVoices;
                        }

                        var lexiconPaths = ModelFileResolver.FilterValidFiles(metadata.GetModelFilePathByKeywords("lexicon") ?? Array.Empty<string>(), fallbackReporter);
                        if (lexiconPaths.Length > 0)
                        {
                            ttsModelConfig.Model.Kokoro.Lexicon = string.Join(",", lexiconPaths);
                        }
                        ttsModelConfig.Model.Kokoro.Tokens = ModelFileResolver.ResolveRequiredByKeywords(metadata, "Kokoro tokens", fallbackReporter, "tokens.txt");

                        var kokoroDictDir = ModelFileResolver.ResolveOptionalDirectoryByKeywords(metadata, fallbackReporter, "dict");
                        if (!string.IsNullOrEmpty(kokoroDictDir))
                        {
                            ttsModelConfig.Model.Kokoro.DictDir = kokoroDictDir;
                        }

                        var kokoroDataDir = ModelFileResolver.ResolveOptionalDirectoryByKeywords(metadata, fallbackReporter, "espeak-ng-data");
                        if (!string.IsNullOrEmpty(kokoroDataDir))
                        {
                            ttsModelConfig.Model.Kokoro.DataDir = kokoroDataDir;
                        }
                        break;
                    }

                case SpeechSynthesisModelType.KittenTTS:
                    {
                        ttsModelConfig.Model.Kitten.Model = ModelFileResolver.ResolveRequiredFile(
                            metadata,
                            "Kitten acoustic model",
                            fallbackReporter,
                            ModelFileCriteria.FromKeywords("model", int8QuantKeyword),
                            ModelFileCriteria.FromKeywords("model"));
                        ttsModelConfig.Model.Kitten.Tokens = ModelFileResolver.ResolveRequiredByKeywords(metadata, "Kitten tokens", fallbackReporter, "tokens.txt");

                        var kittenVoices = ModelFileResolver.ResolveOptionalByKeywords(metadata, fallbackReporter, "voices");
                        if (!string.IsNullOrEmpty(kittenVoices))
                        {
                            ttsModelConfig.Model.Kitten.Voices = kittenVoices;
                        }

                        var kittenDataDir = ModelFileResolver.ResolveOptionalDirectoryByKeywords(metadata, fallbackReporter, "espeak-ng-data");
                        if (!string.IsNullOrEmpty(kittenDataDir))
                        {
                            ttsModelConfig.Model.Kitten.DataDir = kittenDataDir;
                        }
                        break;
                    }

                case SpeechSynthesisModelType.ZipVoice:
                    {
                        ttsModelConfig.Model.ZipVoice.Decoder = ModelFileResolver.ResolveRequiredFile(
                            metadata,
                            "ZipVoice flow matching model",
                            fallbackReporter,
                            ModelFileCriteria.FromKeywords("fm_decoder", int8QuantKeyword),
                            ModelFileCriteria.FromKeywords("decoder", int8QuantKeyword),
                            ModelFileCriteria.FromKeywords("fm_decoder"));
                        ttsModelConfig.Model.ZipVoice.Encoder = ModelFileResolver.ResolveRequiredFile(
                            metadata,
                            "ZipVoice text model",
                            fallbackReporter,
                            ModelFileCriteria.FromKeywords("text_encoder", int8QuantKeyword),
                            ModelFileCriteria.FromKeywords("encoder", int8QuantKeyword),
                            ModelFileCriteria.FromKeywords("text_encoder"));
                        ttsModelConfig.Model.ZipVoice.Vocoder = ModelFileResolver.ResolveRequiredByKeywords(metadata, "ZipVoice vocoder", fallbackReporter, "vocos_24khz.onnx");
                        ttsModelConfig.Model.ZipVoice.Tokens = ModelFileResolver.ResolveRequiredByKeywords(metadata, "ZipVoice tokens", fallbackReporter, "tokens.txt");

                        var pinyinDict = ModelFileResolver.ResolveOptionalByKeywords(metadata, fallbackReporter, "pinyin.raw");
                        if (!string.IsNullOrEmpty(pinyinDict))
                        {
                            ttsModelConfig.Model.ZipVoice.Lexicon = pinyinDict;
                        }

                        var zipVoiceDataDir = ModelFileResolver.ResolveOptionalDirectoryByKeywords(metadata, fallbackReporter, "espeak-ng-data");
                        if (!string.IsNullOrEmpty(zipVoiceDataDir))
                        {
                            ttsModelConfig.Model.ZipVoice.DataDir = zipVoiceDataDir;
                        }
                        break;
                    }

                case SpeechSynthesisModelType.Pocket: // generated by ai， not verified
                    {
                        ttsModelConfig.Model.Pocket.LmFlow = ModelFileResolver.ResolveRequiredFile(
                            metadata,
                            "Pocket LM flow model",
                            fallbackReporter,
                            ModelFileCriteria.FromKeywords("lm_flow", int8QuantKeyword),
                            ModelFileCriteria.FromKeywords("lm-flow", int8QuantKeyword),
                            ModelFileCriteria.FromKeywords("lm_flow"),
                            ModelFileCriteria.FromKeywords("lm-flow"));
                        ttsModelConfig.Model.Pocket.LmMain = ModelFileResolver.ResolveRequiredFile(
                            metadata,
                            "Pocket LM main model",
                            fallbackReporter,
                            ModelFileCriteria.FromKeywords("lm_main", int8QuantKeyword),
                            ModelFileCriteria.FromKeywords("lm-main", int8QuantKeyword),
                            ModelFileCriteria.FromKeywords("lm_main"),
                            ModelFileCriteria.FromKeywords("lm-main"));
                        ttsModelConfig.Model.Pocket.Encoder = ModelFileResolver.ResolveRequiredFile(
                            metadata,
                            "Pocket encoder model",
                            fallbackReporter,
                            ModelFileCriteria.FromKeywords("encoder", int8QuantKeyword),
                            ModelFileCriteria.FromKeywords("encoder"));
                        ttsModelConfig.Model.Pocket.Decoder = ModelFileResolver.ResolveRequiredFile(
                            metadata,
                            "Pocket decoder model",
                            fallbackReporter,
                            ModelFileCriteria.FromKeywords("decoder", int8QuantKeyword),
                            ModelFileCriteria.FromKeywords("decoder"));
                        ttsModelConfig.Model.Pocket.TextConditioner = ModelFileResolver.ResolveRequiredFile(
                            metadata,
                            "Pocket text conditioner model",
                            fallbackReporter,
                            ModelFileCriteria.FromKeywords("text_conditioner", int8QuantKeyword),
                            ModelFileCriteria.FromKeywords("text-conditioner", int8QuantKeyword),
                            ModelFileCriteria.FromKeywords("text_conditioner"),
                            ModelFileCriteria.FromKeywords("text-conditioner"));
                        ttsModelConfig.Model.Pocket.VocabJson = ModelFileResolver.ResolveRequiredFile(
                            metadata,
                            "Pocket vocab json",
                            fallbackReporter,
                            ModelFileCriteria.FromKeywords("vocab", ".json"),
                            ModelFileCriteria.FromKeywords("vocab"));
                        ttsModelConfig.Model.Pocket.TokenScoresJson = ModelFileResolver.ResolveRequiredFile(
                            metadata,
                            "Pocket token scores json",
                            fallbackReporter,
                            ModelFileCriteria.FromKeywords("token_scores", ".json"),
                            ModelFileCriteria.FromKeywords("token-scores", ".json"),
                            ModelFileCriteria.FromKeywords("token_scores"),
                            ModelFileCriteria.FromKeywords("token-scores"));
                        break;
                    }

                case SpeechSynthesisModelType.Supertonic: // generated by ai， not verified
                    {
                        ttsModelConfig.Model.Supertonic.DurationPredictor = ModelFileResolver.ResolveRequiredFile(
                            metadata,
                            "Supertonic duration predictor model",
                            fallbackReporter,
                            ModelFileCriteria.FromKeywords("duration_predictor", int8QuantKeyword),
                            ModelFileCriteria.FromKeywords("duration-predictor", int8QuantKeyword),
                            ModelFileCriteria.FromKeywords("duration_predictor"),
                            ModelFileCriteria.FromKeywords("duration-predictor"));
                        ttsModelConfig.Model.Supertonic.TextEncoder = ModelFileResolver.ResolveRequiredFile(
                            metadata,
                            "Supertonic text encoder model",
                            fallbackReporter,
                            ModelFileCriteria.FromKeywords("text_encoder", int8QuantKeyword),
                            ModelFileCriteria.FromKeywords("text-encoder", int8QuantKeyword),
                            ModelFileCriteria.FromKeywords("encoder", int8QuantKeyword),
                            ModelFileCriteria.FromKeywords("text_encoder"),
                            ModelFileCriteria.FromKeywords("text-encoder"),
                            ModelFileCriteria.FromKeywords("encoder"));
                        ttsModelConfig.Model.Supertonic.VectorEstimator = ModelFileResolver.ResolveRequiredFile(
                            metadata,
                            "Supertonic vector estimator model",
                            fallbackReporter,
                            ModelFileCriteria.FromKeywords("vector_estimator", int8QuantKeyword),
                            ModelFileCriteria.FromKeywords("vector-estimator", int8QuantKeyword),
                            ModelFileCriteria.FromKeywords("vector_estimator"),
                            ModelFileCriteria.FromKeywords("vector-estimator"));
                        ttsModelConfig.Model.Supertonic.Vocoder = ModelFileResolver.ResolveRequiredFile(
                            metadata,
                            "Supertonic vocoder model",
                            fallbackReporter,
                            ModelFileCriteria.FromKeywords("vocoder", int8QuantKeyword),
                            ModelFileCriteria.FromKeywords("vocos", int8QuantKeyword),
                            ModelFileCriteria.FromKeywords("vocoder"),
                            ModelFileCriteria.FromKeywords("vocos"));
                        ttsModelConfig.Model.Supertonic.TtsJson = ModelFileResolver.ResolveRequiredFile(
                            metadata,
                            "Supertonic TTS json",
                            fallbackReporter,
                            ModelFileCriteria.FromKeywords("tts", ".json"),
                            ModelFileCriteria.FromKeywords("tts_json", ".json"),
                            ModelFileCriteria.FromKeywords("tts-json", ".json"),
                            ModelFileCriteria.FromKeywords("tts"),
                            ModelFileCriteria.FromKeywords("tts_json"),
                            ModelFileCriteria.FromKeywords("tts-json"));
                        ttsModelConfig.Model.Supertonic.UnicodeIndexer = ModelFileResolver.ResolveRequiredFile(
                            metadata,
                            "Supertonic unicode indexer",
                            fallbackReporter,
                            ModelFileCriteria.FromKeywords("unicode_indexer", ".json"),
                            ModelFileCriteria.FromKeywords("unicode-indexer", ".json"),
                            ModelFileCriteria.FromKeywords("unicode_indexer"),
                            ModelFileCriteria.FromKeywords("unicode-indexer"));
                        ttsModelConfig.Model.Supertonic.VoiceStyle = ModelFileResolver.ResolveRequiredFile(
                            metadata,
                            "Supertonic voice style",
                            fallbackReporter,
                            ModelFileCriteria.FromKeywords("voice_style", ".bin"),
                            ModelFileCriteria.FromKeywords("voice-style", ".bin"),
                            ModelFileCriteria.FromKeywords("voice_style"),
                            ModelFileCriteria.FromKeywords("voice-style"));
                        break;
                    }

                default:
                    throw new NotSupportedException($"Unsupported TTS model type: {modelType}");
            }

            return ttsModelConfig;
        }

        private void EnsureReadyForGeneration()
        {
            if (IsDisposed || runner == null || runner.IsDisposed)
            {
                throw new ObjectDisposedException(nameof(SpeechSynthesis), "SpeechSynthesis has been disposed or is shutting down.");
            }

            if (_tts == null)
            {
                throw new InvalidOperationException("SpeechSynthesis is not initialized or has already been disposed. Please ensure it is loaded successfully before generating speech.");
            }
        }

        private GenerationScope EnterGenerationScope()
        {
            return new GenerationScope(this);
        }

        private void SignalGenerationCompleted()
        {
            var remaining = Interlocked.Decrement(ref _activeGenerationCount);
            if (remaining < 0)
            {
                Interlocked.Exchange(ref _activeGenerationCount, 0);
                return;
            }

            if (remaining == 0)
            {
                var completion = Volatile.Read(ref _shutdownCompletionSource);
                completion?.TrySetResult(true);
            }
        }

        private sealed class GenerationScope : IDisposable
        {
            private readonly SpeechSynthesis _owner;
            private bool _disposed;

            public OfflineTts Tts { get; }

            public GenerationScope(SpeechSynthesis owner)
            {
                _owner = owner ?? throw new ArgumentNullException(nameof(owner));

                OfflineTts ttsRef = null;
                owner.SafeExecute(() =>
                {
                    if (owner.IsDisposed || owner.runner == null || owner.runner.IsDisposed)
                    {
                        throw new ObjectDisposedException(nameof(SpeechSynthesis), "SpeechSynthesis has been disposed or is shutting down.");
                    }

                    if (owner._tts == null)
                    {
                        throw new InvalidOperationException("SpeechSynthesis is not initialized or has already been disposed. Please ensure it is loaded successfully before generating speech.");
                    }

                    ttsRef = owner._tts;
                    Interlocked.Increment(ref owner._activeGenerationCount);
                });

                Tts = ttsRef ?? throw new InvalidOperationException("Failed to acquire OfflineTts instance for generation.");
            }

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                _owner.SignalGenerationCompleted();
            }
        }

        /// <summary>
        /// Generates speech from text asynchronously and returns an AudioClip.
        /// This is the simplest generation method with no callbacks.
        /// </summary>
        /// <param name="text">The text to synthesize.</param>
        /// <param name="voiceID">The speaker ID.</param>
        /// <param name="speed">The speech speed.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>A Task that represents the asynchronous operation. The value of the TResult parameter contains the generated AudioClip.</returns>
        public async Task<AudioClip> GenerateAsync(string text, int voiceID, float speed = 1f, CancellationToken? ct = null)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return await Task.FromResult<AudioClip>(null);
            }

            EnsureReadyForGeneration();

            return await runner.RunAsync(async (cancellationToken) =>
            {
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, ct ?? CancellationToken.None);
                var combinedToken = linkedCts.Token;
                combinedToken.ThrowIfCancellationRequested();

                using var generationScope = EnterGenerationScope();
                combinedToken.ThrowIfCancellationRequested();
                var tts = generationScope.Tts;

                OfflineTtsGeneratedAudio generatedAudio = tts.Generate(text, speed, voiceID);

                if (generatedAudio == null)
                {
                    SherpaLog.Warning("TTS generation returned no audio.");
                    return null;
                }

                var tcs = new TaskCompletionSource<AudioClip>(TaskCreationOptions.RunContinuationsAsynchronously);
                using var registration = combinedToken.Register(() => tcs.TrySetCanceled(), useSynchronizationContext: false);

                void CreateAudioClipOnMainThread()
                {
                    try
                    {
                        var audioClip = AudioClip.Create($"tts_{voiceID}_{text.GetHashCode()}", generatedAudio.NumSamples, 1, generatedAudio.SampleRate, false);
                        audioClip.SetData(generatedAudio.Samples, 0);
                        tcs.TrySetResult(audioClip);
                    }
                    catch (Exception ex)
                    {
                        tcs.TrySetException(ex);
                    }
                }

                ExecuteOnMainThread(_ => CreateAudioClipOnMainThread());

                var clip = await tcs.Task.ConfigureAwait(false);
                combinedToken.ThrowIfCancellationRequested();
                return clip;
            }, cancellationToken: ct ?? CancellationToken.None, policy: Utilities.ExecutionPolicy.Auto);
        }

        /// <summary>
        /// Generates speech from text asynchronously using simple callback and returns an AudioClip.
        /// WARNING: The callback is invoked from a background thread. If you need to interact with Unity objects or UI,
        /// marshal the callback execution to the main thread using UnityMainThreadDispatcher or similar.
        /// </summary>
        /// <param name="text">The text to synthesize.</param>
        /// <param name="voiceID">The speaker ID.</param>
        /// <param name="speed">The speech speed.</param>
        /// <param name="callback">Simple callback invoked from background thread.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>A Task that represents the asynchronous operation. The value of the TResult parameter contains the generated AudioClip.</returns>
        public async Task<AudioClip> GenerateWithCallbackAsync(string text, int voiceID, float speed, OfflineTtsCallback callback, CancellationToken? ct = null)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return await Task.FromResult<AudioClip>(null);
            }

            EnsureReadyForGeneration();

            return await runner.RunAsync(async (cancellationToken) =>
            {
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, ct ?? CancellationToken.None);
                var combinedToken = linkedCts.Token;
                combinedToken.ThrowIfCancellationRequested();

                using var generationScope = EnterGenerationScope();
                combinedToken.ThrowIfCancellationRequested();
                var tts = generationScope.Tts;

                OfflineTtsGeneratedAudio generatedAudio = tts.GenerateWithCallback(text, speed, voiceID, callback);

                if (generatedAudio == null)
                {
                    SherpaLog.Warning("TTS generation returned no audio.");
                    return null;
                }

                var tcs = new TaskCompletionSource<AudioClip>(TaskCreationOptions.RunContinuationsAsynchronously);
                using var registration = combinedToken.Register(() => tcs.TrySetCanceled(), useSynchronizationContext: false);

                void CreateAudioClipOnMainThread()
                {
                    try
                    {
                        var audioClip = AudioClip.Create($"tts_{voiceID}_{text.GetHashCode()}", generatedAudio.NumSamples, 1, generatedAudio.SampleRate, false);
                        audioClip.SetData(generatedAudio.Samples, 0);
                        tcs.TrySetResult(audioClip);
                    }
                    catch (Exception ex)
                    {
                        tcs.TrySetException(ex);
                    }
                }

                ExecuteOnMainThread(_ => CreateAudioClipOnMainThread());

                var clip = await tcs.Task.ConfigureAwait(false);
                combinedToken.ThrowIfCancellationRequested();
                return clip;
            }, cancellationToken: ct ?? CancellationToken.None, policy: Utilities.ExecutionPolicy.Auto);
        }

        /// <summary>
        /// Generates speech from text asynchronously using progress callback and returns an AudioClip.
        /// WARNING: The callback is invoked from a background thread. If you need to interact with Unity objects or UI,
        /// marshal the callback execution to the main thread using UnityMainThreadDispatcher or similar.
        /// </summary>
        /// <param name="text">The text to synthesize.</param>
        /// <param name="voiceID">The speaker ID.</param>
        /// <param name="speed">The speech speed.</param>
        /// <param name="callback">Progress callback invoked from background thread.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>A Task that represents the asynchronous operation. The value of the TResult parameter contains the generated AudioClip.</returns>
        public async Task<AudioClip> GenerateWithProgressCallbackAsync(string text, int voiceID, float speed, OfflineTtsCallbackProgress callback, CancellationToken? ct = null)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return await Task.FromResult<AudioClip>(null);
            }

            EnsureReadyForGeneration();

            return await runner.RunAsync(async (cancellationToken) =>
            {
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, ct ?? CancellationToken.None);
                var combinedToken = linkedCts.Token;
                combinedToken.ThrowIfCancellationRequested();

                using var generationScope = EnterGenerationScope();
                combinedToken.ThrowIfCancellationRequested();
                var tts = generationScope.Tts;

                OfflineTtsGeneratedAudio generatedAudio = tts.GenerateWithCallbackProgress(text, speed, voiceID, callback);

                if (generatedAudio == null)
                {
                    SherpaLog.Warning("TTS generation returned no audio.");
                    return null;
                }

                var tcs = new TaskCompletionSource<AudioClip>(TaskCreationOptions.RunContinuationsAsynchronously);
                using var registration = combinedToken.Register(() => tcs.TrySetCanceled(), useSynchronizationContext: false);

                void CreateAudioClipOnMainThread()
                {
                    try
                    {
                        if (generatedAudio != null)
                        {
                            var audioClip = AudioClip.Create($"tts_{voiceID}_{text.GetHashCode()}", generatedAudio.NumSamples, 1, generatedAudio.SampleRate, false);
                            audioClip.SetData(generatedAudio.Samples, 0);
                            tcs.TrySetResult(audioClip);
                        }
                    }
                    catch (Exception ex)
                    {
                        tcs.TrySetException(ex);
                    }
                }

                ExecuteOnMainThread(_ => CreateAudioClipOnMainThread());

                var clip = await tcs.Task.ConfigureAwait(false);
                combinedToken.ThrowIfCancellationRequested();
                return clip;
            }, cancellationToken: ct ?? CancellationToken.None, policy: Utilities.ExecutionPolicy.Auto);
        }

        public async Task<AudioClip> GenerateZeroShotAsync(string text, string promptText, float[] promptSamples, int promptSampleRate, float speed = 1, int numSteps = 4, CancellationToken? ct = null)
        {

            if (string.IsNullOrWhiteSpace(text))
            {
                return await Task.FromResult<AudioClip>(null);
            }

            EnsureReadyForGeneration();

            return await runner.RunAsync(async (cancellationToken) =>
            {
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, ct ?? CancellationToken.None);
                var combinedToken = linkedCts.Token;
                combinedToken.ThrowIfCancellationRequested();

                using var generationScope = EnterGenerationScope();
                combinedToken.ThrowIfCancellationRequested();
                var tts = generationScope.Tts;

                var generationConfig = new OfflineTtsGenerationConfig
                {
                    Speed = speed,
                    ReferenceAudio = promptSamples,
                    ReferenceSampleRate = promptSampleRate,
                    ReferenceText = promptText ?? string.Empty,
                    NumSteps = numSteps
                };

                OfflineTtsGeneratedAudio generatedAudio = tts.GenerateWithConfig(text, generationConfig, null);

                if (generatedAudio == null)
                {
                    SherpaLog.Warning("TTS generation returned no audio.");
                    return null;
                }

                var tcs = new TaskCompletionSource<AudioClip>();
                using var registration = combinedToken.Register(() => tcs.TrySetCanceled(), useSynchronizationContext: false);

                void CreateAudioClipOnMainThread()
                {
                    try
                    {
                        if (generatedAudio != null)
                        {
                            var audioClip = AudioClip.Create($"tts_zeroshot_{text.GetHashCode()}", generatedAudio.NumSamples, 1, generatedAudio.SampleRate, false);
                            audioClip.SetData(generatedAudio.Samples, 0);
                            tcs.TrySetResult(audioClip);
                        }
                    }
                    catch (Exception ex)
                    {
                        tcs.TrySetException(ex);
                    }
                }

                ExecuteOnMainThread(_ => CreateAudioClipOnMainThread());

                var clip = await tcs.Task.ConfigureAwait(false);
                combinedToken.ThrowIfCancellationRequested();
                return clip;
            }, cancellationToken: ct ?? CancellationToken.None, policy: Utilities.ExecutionPolicy.Auto);
        }
        public async Task<AudioClip> GenerateZeroShotAsync(string text, string promptText, AudioClip promptClip, float speed = 1, int numSteps = 4, CancellationToken? ct = null)
        {
            var promptSamples = new float[promptClip.samples];
            promptClip.GetData(promptSamples, 0);
            int promptSampleRates = promptClip.frequency;
            return await GenerateZeroShotAsync(text, promptText, promptSamples, promptSampleRates, speed, numSteps, ct);
        }
        protected override void OnDestroy()
        {
            OfflineTts ttsInstance = null;
            TaskCompletionSource<bool> shutdownCompletion = null;

            SafeExecute(() =>
            {
                if (_tts == null)
                {
                    return;
                }

                ttsInstance = _tts;
                _tts = null;

                shutdownCompletion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                _shutdownCompletionSource = shutdownCompletion;

                if (Volatile.Read(ref _activeGenerationCount) == 0)
                {
                    shutdownCompletion.TrySetResult(true);
                }
            });

            if (ttsInstance == null)
            {
                return;
            }

            var completionTask = shutdownCompletion?.Task;
            if (completionTask == null || completionTask.IsCompleted)
            {
                DisposeTtsSafely(ttsInstance);
                return;
            }

            _ = completionTask.ContinueWith(_ => DisposeTtsSafely(ttsInstance), TaskScheduler.Default);
        }

        private static void DisposeTtsSafely(OfflineTts ttsInstance)
        {
            if (ttsInstance == null)
            {
                return;
            }

            try
            {
                ttsInstance.Dispose();
            }
            catch (Exception ex)
            {
                SherpaLog.Exception(ex);
            }
        }
    }
}
