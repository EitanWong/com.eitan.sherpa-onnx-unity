using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Eitan.SherpaOnnxUnity.Runtime;
using SherpaOnnx;
using UnityEngine;
namespace Eitan.SherpaOnnxUnity
{
    public class SpokenLanguageIdentification : SherpaOnnxModule
    {

        private SherpaOnnx.SpokenLanguageIdentification _slid;
        public int SampleRate { get; private set; }

        public SpokenLanguageIdentification(string modelID, int sampleRate = 16000, SherpaOnnxFeedbackReporter reporter = null) : base(modelID, sampleRate, reporter)
        {

        }

        protected override SherpaOnnxModuleType ModuleType => SherpaOnnxModuleType.SpokenLanguageIdentification;

        protected override async Task Initialization(SherpaOnnxModelMetadata metadata, int sampleRate, bool isMobilePlatform, SherpaOnnxFeedbackReporter reporter, CancellationToken ct)
        {
            try
            {
                // ignore the prarmeter sampleRate it's not correct.

                reporter?.Report(new LoadFeedback(metadata, message: $"Start Loading: {metadata.modelId}"));
                var modelType = Runtime.Utilities.SherpaUtils.Model.GetSpokenLanguageIdentificationModelType(metadata.modelId);
                this.SampleRate = metadata.SampleRate;

                var sliConfig = CreateSliConfig(modelType, metadata, this.SampleRate, isMobilePlatform, reporter, ct);

                await runner.RunAsync(cancellationToken =>
                {
                    try
                    {
                        reporter?.Report(new LoadFeedback(metadata, message: $"Loading SpokenLanguageIdentification model: {metadata.modelId}"));

                        _slid = new SherpaOnnx.SpokenLanguageIdentification(sliConfig);
                        if (_slid == null)
                        {

                            throw new Exception($"Failed to initialize SpokenLanguageIdentification model: {metadata.modelId}");
                        }

                        // var s = slid.CreateStream();
                        // s.AcceptWaveform(waveReader.SampleRate, waveReader.Samples);
                        // var result = slid.Compute(s);

                        reporter?.Report(new LoadFeedback(metadata, message: $"SpokenLanguageIdentification model loaded successfully: {metadata.modelId}"));
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

        private SpokenLanguageIdentificationConfig CreateSliConfig(SpokenLanguageIdentificationModelType modelType, SherpaOnnxModelMetadata metadata, int sampleRate, bool isMobilePlatform, SherpaOnnxFeedbackReporter reporter, CancellationToken ct)
        {
            var sliModelConfig = new SpokenLanguageIdentificationConfig();
            var int8QuantKeyword = isMobilePlatform ? "int8" : null;

            switch (modelType)
            {
                case SpokenLanguageIdentificationModelType.Whisper:
                    sliModelConfig.Whisper.Encoder = metadata.GetModelFilePathByKeywords("encoder", int8QuantKeyword)?.First();
                    sliModelConfig.Whisper.Decoder = metadata.GetModelFilePathByKeywords("decoder", int8QuantKeyword)?.First();
                    break;
                default:
                    throw new NotSupportedException($"Unsupported VAD model type: {modelType}");
            }
            return sliModelConfig;
        }


        #region Public Method

        public async Task<string> IdentifyAsync(float[] Samples, int SampleRate, CancellationToken? ct = null)
        {
            if (_slid == null)
            {
                throw new InvalidOperationException("SpokenLanguageIdentification is not initialized or has been disposed. Please ensure it is loaded successfully before identify spoken audio.");
            }

            return await runner.RunAsync((cancellationToken) =>
            {
                string result = null;
                using (var s = _slid.CreateStream())
                {
                    s.AcceptWaveform(SampleRate, Samples);
                    result = _slid.Compute(s)?.Lang;
                }
                return Task.FromResult(result);

            }, cancellationToken: ct ?? CancellationToken.None, policy: Runtime.Utilities.ExecutionPolicy.Auto);
        }

        public async Task<string> IdentifyAsync(AudioClip clip, CancellationToken? ct = null)
        {
            float[] samples = new float[clip.samples];
            clip.GetData(samples, 0);
            return await IdentifyAsync(samples, clip.frequency, ct);
        }


        #endregion

        protected override void OnDestroy()
        {
            SafeExecute(() =>
            {
                _slid?.Dispose();
                _slid = null;
            });
        }


    }
}
