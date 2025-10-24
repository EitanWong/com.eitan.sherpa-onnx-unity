// SpeechSynthesis.cs

namespace Eitan.SherpaOnnxUnity.Runtime
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Eitan.SherpaOnnxUnity.Runtime.Utilities;
    using Eitan.SherpaOnnxUnity.Runtime.Native;
    using UnityEngine;

    public class SpeechSynthesis : SherpaOnnxModule
    {

        private OfflineTts _tts;

        protected override SherpaOnnxModuleType ModuleType => SherpaOnnxModuleType.SpeechSynthesis;

        public SpeechSynthesis(string modelID, int sampleRate = -1, SherpaOnnxFeedbackReporter reporter = null)
            : base(modelID, sampleRate, reporter)
        {

        }

        protected override async Task<bool> Initialization(SherpaOnnxModelMetadata metadata, int sampleRate, bool isMobilePlatform, SherpaOnnxFeedbackReporter reporter, CancellationToken ct)
        {
            try
            {
                reporter?.Report(new LoadFeedback(metadata, message: $"Start Loading: {metadata.modelId}"));
                var modelType = Utilities.SherpaUtils.Model.GetSpeechSynthesisModelType(metadata.modelId);
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




        private async Task<OfflineTtsConfig> CreateTtsConfig(SpeechSynthesisModelType modelType, SherpaOnnxModelMetadata metadata, bool isMobilePlatform, SherpaOnnxFeedbackReporter reporter, CancellationToken ct)
        {
            var ttsModelConfig = new OfflineTtsConfig();
            var int8QuantKeyword = isMobilePlatform ? "int8" : null;
            ttsModelConfig.RuleFsts = string.Join(",", metadata.GetModelFilesByExtensionName(".fst"));
            ttsModelConfig.RuleFars = string.Join(",", metadata.GetModelFilesByExtensionName(".far"));
            ttsModelConfig.Model.NumThreads = ThreadingUtils.GetAdaptiveThreadCount();

            switch (modelType)
            {
                case SpeechSynthesisModelType.Vits:
                    ttsModelConfig.Model.Vits.Model = metadata.GetModelFilePathByKeywords("model", "en_US", "vits", "theresa", "eula", ".onnx", int8QuantKeyword)?.First();
                    ttsModelConfig.Model.Vits.Lexicon = metadata.GetModelFilePathByKeywords("lexicon")?.First();
                    ttsModelConfig.Model.Vits.Tokens = metadata.GetModelFilePathByKeywords("tokens.txt")?.First();
                    ttsModelConfig.Model.Vits.DictDir = metadata.GetModelFilePathByKeywords("dict")?.First();
                    ttsModelConfig.Model.Vits.DataDir = metadata.GetModelFilePathByKeywords("espeak-ng-data")?.First();

                    break;
                case SpeechSynthesisModelType.Matcha:
                    var vocoderMetaData = await SherpaOnnxModelRegistry.Instance.GetMetadataAsync("vocos-22khz-univ", ct);
                    if (modelType == SpeechSynthesisModelType.Matcha)
                    {
                        //prepare vocoder
                        await SherpaUtils.Prepare.PrepareAndLoadModelAsync(vocoderMetaData, reporter, ct);
                    }

                    ttsModelConfig.Model.Matcha.AcousticModel = metadata.GetModelFilePathByKeywords("matcha", "model", int8QuantKeyword)?.First();
                    ttsModelConfig.Model.Matcha.Vocoder = vocoderMetaData.GetModelFilePathByKeywords("vocos")?.First();
                    ttsModelConfig.Model.Matcha.Lexicon = metadata.GetModelFilePathByKeywords("lexicon")?.First();
                    ttsModelConfig.Model.Matcha.Tokens = metadata.GetModelFilePathByKeywords("tokens.txt")?.First();
                    ttsModelConfig.Model.Matcha.DictDir = metadata.GetModelFilePathByKeywords("dict")?.First();
                    ttsModelConfig.Model.Matcha.DataDir = metadata.GetModelFilePathByKeywords("espeak-ng-data")?.First();

                    break;
                case SpeechSynthesisModelType.Kokoro:

                    ttsModelConfig.Model.Kokoro.Model = metadata.GetModelFilePathByKeywords("model", "kokoro", int8QuantKeyword)?.First();
                    ttsModelConfig.Model.Kokoro.Voices = metadata.GetModelFilePathByKeywords("voices")?.First();
                    ttsModelConfig.Model.Kokoro.Lexicon = string.Join(",", metadata.GetModelFilePathByKeywords("lexicon"));
                    ttsModelConfig.Model.Kokoro.Tokens = metadata.GetModelFilePathByKeywords("tokens.txt")?.First();
                    ttsModelConfig.Model.Kokoro.DictDir = metadata.GetModelFilePathByKeywords("dict")?.First();
                    ttsModelConfig.Model.Kokoro.DataDir = metadata.GetModelFilePathByKeywords("espeak-ng-data")?.First();
                    break;
                case SpeechSynthesisModelType.KittenTTS:
                    ttsModelConfig.Model.Kitten.Model = metadata.GetModelFilePathByKeywords("model", int8QuantKeyword)?.First();
                    ttsModelConfig.Model.Kitten.Tokens = metadata.GetModelFilePathByKeywords("tokens.txt")?.First();
                    ttsModelConfig.Model.Kitten.Voices = metadata.GetModelFilePathByKeywords("voices")?.First();
                    ttsModelConfig.Model.Kitten.DataDir = metadata.GetModelFilePathByKeywords("espeak-ng-data")?.First();
                    break;
                default:
                    throw new NotSupportedException($"Unsupported TTS model type: {modelType}");
            }

            // UnityEngine.Debug.Log(ttsModelConfig.Model.Vits.Model);
            // UnityEngine.Debug.Log(ttsModelConfig.Model.Vits.Lexicon);
            // UnityEngine.Debug.Log(ttsModelConfig.Model.Vits.Tokens);
            // UnityEngine.Debug.Log(ttsModelConfig.Model.Vits.DictDir);
            // UnityEngine.Debug.Log(ttsModelConfig.Model.Vits.DataDir);

            return ttsModelConfig;
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
            if (_tts == null)
            {
                throw new InvalidOperationException("SpeechSynthesis is not initialized or has been disposed. Please ensure it is loaded successfully before generating speech.");
            }

            return await runner.RunAsync(async (cancellationToken) =>
            {
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, ct ?? CancellationToken.None);
                var combinedToken = linkedCts.Token;
                combinedToken.ThrowIfCancellationRequested();

                OfflineTtsGeneratedAudio generatedAudio = _tts.Generate(text, speed, voiceID);

                if (generatedAudio == null)
                {
                    Debug.LogWarning("TTS generation returned no audio.");
                    return null;
                }

                var tcs = new TaskCompletionSource<AudioClip>();
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
            if (_tts == null)
            {
                throw new InvalidOperationException("SpeechSynthesis is not initialized or has been disposed. Please ensure it is loaded successfully before generating speech.");
            }

            return await runner.RunAsync(async (cancellationToken) =>
            {
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, ct ?? CancellationToken.None);
                var combinedToken = linkedCts.Token;
                combinedToken.ThrowIfCancellationRequested();

                OfflineTtsGeneratedAudio generatedAudio = _tts.GenerateWithCallback(text, speed, voiceID, callback);

                if (generatedAudio == null)
                {
                    Debug.LogWarning("TTS generation returned no audio.");
                    return null;
                }

                var tcs = new TaskCompletionSource<AudioClip>();
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
            if (_tts == null)
            {
                throw new InvalidOperationException("SpeechSynthesis is not initialized or has been disposed. Please ensure it is loaded successfully before generating speech.");
            }

            return await runner.RunAsync(async (cancellationToken) =>
            {
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, ct ?? CancellationToken.None);
                var combinedToken = linkedCts.Token;
                combinedToken.ThrowIfCancellationRequested();

                OfflineTtsGeneratedAudio generatedAudio = _tts.GenerateWithCallbackProgress(text, speed, voiceID, callback);

                if (generatedAudio == null)
                {
                    Debug.LogWarning("TTS generation returned no audio.");
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


        protected override void OnDestroy()
        {
            SafeExecute(() =>
            {
                _tts?.Dispose();
                _tts = null;
            });
        }
    }
}
