// SpeechSynthesis.cs

namespace Eitan.SherpaOnnxUnity.Runtime
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Eitan.SherpaOnnxUnity.Runtime.Utilities;
    using SherpaOnnx;
    using UnityEngine;

    public class SpeechSynthesis : SherpaOnnxModule
    {

        private SpeechRecognitionModelType _modelType;
        private readonly object _lockObject = new object();
        private OfflineTts _tts;

        protected override SherpaOnnxModuleType ModuleType => SherpaOnnxModuleType.SpeechSynthesis;

        public SpeechSynthesis(string modelID, int sampleRate = 16000, SherpaFeedbackReporter reporter = null)
            : base(modelID, sampleRate, reporter)
        {
        }

        protected override async Task Initialization(SherpaOnnxModelMetadata metadata, int sampleRate, bool isMobilePlatform, SherpaFeedbackReporter reporter, CancellationToken ct)
        {
            try
            {
                reporter?.Report(new LoadFeedback(metadata, message: $"Start Loading: {metadata.modelId}")); 
                var modelType = Utilities.SherpaUtils.Model.GetSpeechSynthesisModelType(metadata.modelId);
                var ttsConfig = await CreateTtsConfig(modelType, metadata, sampleRate, isMobilePlatform,reporter,ct);
                await runner.RunAsync(cancellationToken =>
                {
                    try
                    {
                        reporter?.Report(new LoadFeedback(metadata, message: $"Loading TTS model: {metadata.modelId}"));
                        _tts = new OfflineTts(ttsConfig);
                        if (_tts == null)
                        {
                            throw new Exception($"Failed to initialize TTS model: {metadata.modelId}");
                        }
                        reporter?.Report(new LoadFeedback(metadata, message: $"TTS model loaded successfully: {metadata.modelId}"));
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
        



        private async Task<OfflineTtsConfig> CreateTtsConfig(SpeechSynthesisModelType modelType, SherpaOnnxModelMetadata metadata, int sampleRate, bool isMobilePlatform,SherpaFeedbackReporter reporter, CancellationToken ct)
        {
            var vadModelConfig = new OfflineTtsConfig();
            var int8QuantKeyword = isMobilePlatform ? "int8" : null;
            vadModelConfig.RuleFsts = string.Join(",", metadata.GetModelFilesByExtensionName(".fst"));
            vadModelConfig.RuleFars = string.Join(",", metadata.GetModelFilesByExtensionName(".far"));
            vadModelConfig.Model.NumThreads = 4;

            switch (modelType)
            {
                case SpeechSynthesisModelType.Vits:
                    vadModelConfig.Model.Vits.Model = metadata.GetModelFilePathByKeywords("model", "en_US", "vits", "theresa", "eula", int8QuantKeyword)?.First();
                    vadModelConfig.Model.Vits.Lexicon = metadata.GetModelFilePathByKeywords("lexicon")?.First();
                    vadModelConfig.Model.Vits.Tokens = metadata.GetModelFilePathByKeywords("tokens.txt")?.First();
                    vadModelConfig.Model.Vits.DictDir = metadata.GetModelFilePathByKeywords("dict")?.First();
                    vadModelConfig.Model.Vits.DataDir = metadata.GetModelFilePathByKeywords("espeak-ng-data")?.First();

                    break;
                case SpeechSynthesisModelType.Matcha:
                    var vocoderMetaData = await SherpaOnnxModelRegistry.Instance.GetMetadataAsync("vocos-22khz-univ");
                    if (modelType == SpeechSynthesisModelType.Matcha)
                    {
                        //prepare vocoder
                        await SherpaUtils.Prepare.PrepareModelAsync(vocoderMetaData, reporter, ct);
                    }
                    
                    vadModelConfig.Model.Matcha.AcousticModel = metadata.GetModelFilePathByKeywords("matcha","model", int8QuantKeyword)?.First();
                    vadModelConfig.Model.Matcha.Vocoder = vocoderMetaData.GetModelFilePathByKeywords("vocos")?.First();
                    vadModelConfig.Model.Matcha.Lexicon = metadata.GetModelFilePathByKeywords("lexicon")?.First();
                    vadModelConfig.Model.Matcha.Tokens = metadata.GetModelFilePathByKeywords("tokens.txt")?.First();
                    vadModelConfig.Model.Matcha.DictDir = metadata.GetModelFilePathByKeywords("dict")?.First();
                    vadModelConfig.Model.Matcha.DataDir = metadata.GetModelFilePathByKeywords("espeak-ng-data")?.First();
                    break;
                case SpeechSynthesisModelType.Kokoro:

                    vadModelConfig.Model.Kokoro.Model = metadata.GetModelFilePathByKeywords("model", "kokoro", int8QuantKeyword)?.First();
                    vadModelConfig.Model.Kokoro.Voices = metadata.GetModelFilePathByKeywords("voices")?.First();
                    vadModelConfig.Model.Kokoro.Lexicon = string.Join(",", metadata.GetModelFilePathByKeywords("lexicon"));
                    vadModelConfig.Model.Kokoro.Tokens = metadata.GetModelFilePathByKeywords("tokens.txt")?.First();
                    vadModelConfig.Model.Kokoro.DictDir = metadata.GetModelFilePathByKeywords("dict")?.First();
                    vadModelConfig.Model.Kokoro.DataDir = metadata.GetModelFilePathByKeywords("espeak-ng-data")?.First();
                    break;
                default:
                    throw new NotSupportedException($"Unsupported VAD model type: {modelType}");
            }
            return vadModelConfig;
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
                OfflineTtsGeneratedAudio generatedAudio = _tts.Generate(text, speed, voiceID);

                if (generatedAudio == null)
                { 
                    Debug.LogWarning("TTS generation returned no audio.");
                    return null;
                }

                var tcs = new TaskCompletionSource<AudioClip>();

                void CreateAudioClipOnMainThread()
                {
                    try
                    {
                        var audioClip = AudioClip.Create($"tts_{voiceID}_{text.GetHashCode()}", generatedAudio.NumSamples, 1, generatedAudio.SampleRate, false);
                        audioClip.SetData(generatedAudio.Samples, 0);
                        tcs.SetResult(audioClip);
                    }
                    catch (Exception ex)
                    {
                        tcs.SetException(ex);
                    }
                }

                if (SynchronizationContext.Current != null)
                {
                    SynchronizationContext.Current.Post(_ => CreateAudioClipOnMainThread(), null);
                }
                else
                {
                    tcs.SetException(new InvalidOperationException("Cannot create AudioClip without a Unity SynchronizationContext."));
                }

                return await tcs.Task;
            }, cancellationToken: ct ?? CancellationToken.None);
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
                OfflineTtsGeneratedAudio generatedAudio = _tts.GenerateWithCallback(text, speed, voiceID, callback);

                if (generatedAudio == null)
                { 
                    Debug.LogWarning("TTS generation returned no audio.");
                    return null;
                }

                var tcs = new TaskCompletionSource<AudioClip>();

                void CreateAudioClipOnMainThread()
                {
                    try
                    {
                        var audioClip = AudioClip.Create($"tts_{voiceID}_{text.GetHashCode()}", generatedAudio.NumSamples, 1, generatedAudio.SampleRate, false);
                        audioClip.SetData(generatedAudio.Samples, 0);
                        tcs.SetResult(audioClip);
                    }
                    catch (Exception ex)
                    {
                        tcs.SetException(ex);
                    }
                }

                if (SynchronizationContext.Current != null)
                {
                    SynchronizationContext.Current.Post(_ => CreateAudioClipOnMainThread(), null);
                }
                else
                {
                    tcs.SetException(new InvalidOperationException("Cannot create AudioClip without a Unity SynchronizationContext."));
                }

                return await tcs.Task;
            }, cancellationToken: ct ?? CancellationToken.None);
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
                OfflineTtsGeneratedAudio generatedAudio = _tts.GenerateWithCallbackProgress(text, speed, voiceID, callback);

                if (generatedAudio == null)
                { 
                    Debug.LogWarning("TTS generation returned no audio.");
                    return null;
                }

                var tcs = new TaskCompletionSource<AudioClip>();

                void CreateAudioClipOnMainThread()
                {
                    try
                    {
                        var audioClip = AudioClip.Create($"tts_{voiceID}_{text.GetHashCode()}", generatedAudio.NumSamples, 1, generatedAudio.SampleRate, false);
                        audioClip.SetData(generatedAudio.Samples, 0);
                        tcs.SetResult(audioClip);
                    }
                    catch (Exception ex)
                    {
                        tcs.SetException(ex);
                    }
                }

                if (SynchronizationContext.Current != null)
                {
                    SynchronizationContext.Current.Post(_ => CreateAudioClipOnMainThread(), null);
                }
                else
                {
                    tcs.SetException(new InvalidOperationException("Cannot create AudioClip without a Unity SynchronizationContext."));
                }

                return await tcs.Task;
            }, cancellationToken: ct ?? CancellationToken.None);
        }


        protected override void OnDestroy()
        {
            lock (_lockObject)
            {
                _tts?.Dispose();
                _tts = null;
            }
        }
    }
}
