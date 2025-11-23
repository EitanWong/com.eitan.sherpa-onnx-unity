namespace Eitan.SherpaONNXUnity.Runtime.Modules
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using UnityEngine;
    using Eitan.SherpaONNXUnity.Runtime.Native;
    using NativeSpokenLanguageIdentification = Eitan.SherpaONNXUnity.Runtime.Native.SpokenLanguageIdentification;
    using Eitan.SherpaONNXUnity.Runtime.Utilities;
    public class SpokenLanguageIdentification : SherpaONNXModule
    {

        private NativeSpokenLanguageIdentification _slid;
        public int SampleRate { get; private set; }

        public SpokenLanguageIdentification(string modelID, int sampleRate = 16000, SherpaONNXFeedbackReporter reporter = null) : base(modelID, sampleRate, reporter)
        {

        }

        protected override SherpaONNXModuleType ModuleType => SherpaONNXModuleType.SpokenLanguageIdentification;

        protected override async Task<bool> Initialization(SherpaONNXModelMetadata metadata, int sampleRate, bool isMobilePlatform, SherpaONNXFeedbackReporter reporter, CancellationToken ct)
        {
            try
            {
                // ignore the prarmeter sampleRate it's not correct.

                reporter?.Report(new LoadFeedback(metadata, message: $"Start Loading: {metadata.modelId}"));
                var modelType = SherpaUtils.Model.GetSpokenLanguageIdentificationModelType(metadata.modelId);
                this.SampleRate = metadata.sampleRate;

                var sliConfig = CreateSliConfig(modelType, metadata, this.SampleRate, isMobilePlatform, reporter, ct);

                return await runner.RunAsync<bool>(cancellationToken =>
                {
                    try
                    {
                        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, cancellationToken);
                        linkedCts.Token.ThrowIfCancellationRequested();

                        if (IsDisposed) { return Task.FromResult(false); }

                        reporter?.Report(new LoadFeedback(metadata, message: $"Loading SpokenLanguageIdentification model: {metadata.modelId}"));

                        _slid = new NativeSpokenLanguageIdentification(sliConfig);
                        var initialized = IsSuccessInitializad(_slid);
                        if (initialized)
                        {
                            reporter?.Report(new LoadFeedback(metadata, message: $"SpokenLanguageIdentification model loaded successfully: {metadata.modelId}"));
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

        private SpokenLanguageIdentificationConfig CreateSliConfig(SpokenLanguageIdentificationModelType modelType, SherpaONNXModelMetadata metadata, int sampleRate, bool isMobilePlatform, SherpaONNXFeedbackReporter reporter, CancellationToken ct)
        {
            var fallbackReporter = CreateFallbackReporter(metadata, reporter);
            var sliModelConfig = new SpokenLanguageIdentificationConfig { NumThreads = ThreadingUtils.GetAdaptiveThreadCount() };
            var int8QuantKeyword = isMobilePlatform ? "int8" : null;

            switch (modelType)
            {
                case SpokenLanguageIdentificationModelType.Whisper:
                    sliModelConfig.Whisper.Encoder = ModelFileResolver.ResolveRequiredFile(
                        metadata,
                        "Whisper encoder",
                        fallbackReporter,
                        ModelFileCriteria.FromKeywords("encoder", int8QuantKeyword),
                        ModelFileCriteria.FromKeywords("encoder"));
                    sliModelConfig.Whisper.Decoder = ModelFileResolver.ResolveRequiredFile(
                        metadata,
                        "Whisper decoder",
                        fallbackReporter,
                        ModelFileCriteria.FromKeywords("decoder", int8QuantKeyword),
                        ModelFileCriteria.FromKeywords("decoder"));
                    break;
                default:
                    throw new NotSupportedException($"Unsupported SpokenLanguageIdentification model type: {modelType}");
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

            }, cancellationToken: ct ?? CancellationToken.None, policy: ExecutionPolicy.Auto);
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
