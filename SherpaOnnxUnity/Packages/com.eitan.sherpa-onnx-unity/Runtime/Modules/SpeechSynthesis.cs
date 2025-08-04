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
        OfflineTts _tts;

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
                    _tts = new OfflineTts(ttsConfig);
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

            switch (modelType)
            {
                case SpeechSynthesisModelType.Vits:
                    vadModelConfig.Model.Vits.Model = metadata.GetModelFilePathByKeywords("model", "en_US", "vits", "theresa", "eula", int8QuantKeyword).First();
                    vadModelConfig.Model.Vits.Lexicon = metadata.GetModelFilePathByKeywords("lexicon").First();
                    vadModelConfig.Model.Vits.Tokens = metadata.GetModelFilePathByKeywords("tokens.txt").First();
                    vadModelConfig.Model.Vits.DictDir = metadata.GetModelFilePathByKeywords("dict").First();
                    vadModelConfig.Model.Vits.DataDir = metadata.GetModelFilePathByKeywords("espeak-ng-data").First();

                    break;
                case SpeechSynthesisModelType.Matcha:
                    var vocoderMetaData = await SherpaOnnxModelRegistry.Instance.GetMetadataAsync("vocos-22khz-univ");
                    if (modelType == SpeechSynthesisModelType.Matcha)
                    {
                        //prepare vocoder
                        await SherpaUtils.Prepare.PrepareModelAsync(vocoderMetaData,reporter,ct);
                    }
                    vadModelConfig.Model.Matcha.AcousticModel = metadata.GetModelFilePathByKeywords("matcha", int8QuantKeyword).First();
                    vadModelConfig.Model.Matcha.Vocoder = vocoderMetaData.GetModelFilePathByKeywords("vocos").First();
                    vadModelConfig.Model.Matcha.Lexicon = metadata.GetModelFilePathByKeywords("lexicon").First();
                    vadModelConfig.Model.Matcha.Tokens = metadata.GetModelFilePathByKeywords("tokens.txt").First();
                    vadModelConfig.Model.Matcha.DictDir = metadata.GetModelFilePathByKeywords("dict").First();
                    vadModelConfig.Model.Matcha.DataDir = metadata.GetModelFilePathByKeywords("espeak-ng-data").First();
                    break;
                case SpeechSynthesisModelType.Kokoro:

                    vadModelConfig.Model.Kokoro.Model = metadata.GetModelFilePathByKeywords("model", "kokoro", int8QuantKeyword).First();
                    vadModelConfig.Model.Kokoro.Lexicon = string.Join(",", metadata.GetModelFilePathByKeywords("lexicon"));
                    vadModelConfig.Model.Kokoro.Tokens = metadata.GetModelFilePathByKeywords("tokens.txt").First();
                    vadModelConfig.Model.Kokoro.DictDir = metadata.GetModelFilePathByKeywords("dict").First();
                    vadModelConfig.Model.Kokoro.DataDir = metadata.GetModelFilePathByKeywords("espeak-ng-data").First();
                    break;
                default:
                    throw new NotSupportedException($"Unsupported VAD model type: {modelType}");
            }
            return vadModelConfig;
        }

        /// <summary>
        /// Generates speech from text asynchronously and returns an AudioClip.
        /// This method is suitable for use with async/await in Unity.
        /// </summary>
        /// <param name="text">The text to synthesize.</param>
        /// <param name="voiceID">The speaker ID.</param>
        /// <param name="speed">The speech speed.</param>
        /// <param name="callback">Progress callback.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>A Task that represents the asynchronous operation. The value of the TResult parameter contains the generated AudioClip.</returns>
        public async Task<AudioClip> GenerateAsync(string text, int voiceID, float speed = 1f, OfflineTtsCallbackProgress callback = null, CancellationToken? ct = null)
        {
            return await runner.RunAsync(async (cancellationToken) =>
            {
                OfflineTtsGeneratedAudio generatedAudio = _tts.GenerateWithCallbackProgress(text, speed, voiceID, callback);

                if (generatedAudio == null)
                { 
                    Debug.LogWarning("TTS generation returned no audio.");
                    return null;
                }

                AudioClip audioClip = null;
                var autoResetEvent = new System.Threading.AutoResetEvent(false);

                void CreateAudioClip()
                {
                    audioClip = AudioClip.Create($"tts_{voiceID}_{text.GetHashCode()}", generatedAudio.NumSamples, 1, generatedAudio.SampleRate, false);
                    audioClip.SetData(generatedAudio.Samples, 0);
                    autoResetEvent.Set();
                }

                if (SynchronizationContext.Current != null)
                {
                    SynchronizationContext.Current.Post(_ => CreateAudioClip(), null);
                }
                else
                {
                    // Fallback for non-Unity threads
                    CreateAudioClip();
                }

                await Task.Run(() => autoResetEvent.WaitOne());

                return audioClip;
            }, cancellationToken: ct ?? CancellationToken.None);
        }

        /// <summary>
        /// Generates speech from text and invokes a callback with the resulting AudioClip.
        /// This method is suitable for users who prefer a callback pattern over async/await.
        /// </summary>
        /// <param name="text">The text to synthesize.</param>
        /// <param name="voiceID">The speaker ID.</param>
        /// <param name="speed">The speech speed.</param>
        /// <param name="onCompleted">Callback invoked with the generated AudioClip when synthesis is complete.</param>
        /// <param name="onError">Callback invoked if an error occurs during synthesis.</param>
        public void Generate(string text, int voiceID, float speed, System.Action<AudioClip> onCompleted, System.Action<Exception> onError = null)
        {
            _ = GenerateAndCallback(text, voiceID, speed, onCompleted, onError);
        }

        private async Task GenerateAndCallback(string text, int voiceID, float speed, System.Action<AudioClip> onCompleted, System.Action<Exception> onError)
        {
            try
            {
                AudioClip clip = await GenerateAsync(text, voiceID, speed);
                // If called from Unity's main thread, this callback will be on the main thread.
                onCompleted?.Invoke(clip);
            }
            catch (Exception e)
            {
                Debug.LogError($"TTS generation failed: {e.Message}");
                // If called from Unity's main thread, this callback will be on the main thread.
                onError?.Invoke(e);
            }
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
