using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Eitan.SherpaOnnxUnity.Runtime;
using SherpaOnnx;

namespace Eitan.SherpaOnnxUnity
{
    public class Punctuation : SherpaOnnxModule
    {
        private OfflinePunctuation _punct;

        private int _sampleRate;

        protected override SherpaOnnxModuleType ModuleType => SherpaOnnxModuleType.AddPunctuation;

        public Punctuation(string modelID, int sampleRate = 16000, SherpaOnnxFeedbackReporter reporter = null) : base(modelID, sampleRate, reporter)
        {

        }

        protected override async Task Initialization(SherpaOnnxModelMetadata metadata, int sampleRate, bool isMobilePlatform, SherpaOnnxFeedbackReporter reporter, CancellationToken ct)
        {
            try
            {
                reporter?.Report(new LoadFeedback(metadata, message: $"Start Loading: {metadata.modelId}"));

                _sampleRate = sampleRate;
                var config = CreatePunctuationConfig(metadata, isMobilePlatform);

                await runner.RunAsync(cancellationToken =>
                {
                    try
                    {
                        reporter?.Report(new LoadFeedback(metadata, message: $"Loading Punctuation model: {metadata.modelId}"));
                        _punct = new OfflinePunctuation(config);
                        if (_punct == null)
                        {
                            throw new Exception($"Failed to initialize Punctuation model: {metadata.modelId}");
                        }

                        reporter?.Report(new LoadFeedback(metadata, message: $"Punctuation model loaded successfully: {metadata.modelId}"));
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

        private OfflinePunctuationConfig CreatePunctuationConfig(SherpaOnnxModelMetadata metadata, bool isMobilePlatform)
        {
            var config = new OfflinePunctuationConfig();

            var int8QuantKeyword = isMobilePlatform ? "int8" : null;

            // Configure GTCRN model
            var ctTransformerModelPath = metadata.GetModelFilePathByKeywords("model", int8QuantKeyword)?.FirstOrDefault();

            if (!string.IsNullOrEmpty(ctTransformerModelPath))
            {
                config.Model.NumThreads = 1;
                config.Model.CtTransformer = ctTransformerModelPath;
            }
            else
            {
                // Fallback to any .onnx model file
                var modelPath = metadata.GetModelFilesByExtensionName(".onnx")?.FirstOrDefault();
                if (!string.IsNullOrEmpty(modelPath))
                {
                    config.Model.NumThreads = 1;
                    config.Model.CtTransformer = modelPath;
                }
                else
                {
                    throw new InvalidOperationException($"No suitable Ct Transformer model found for {metadata.modelId}");
                }
            }

            return config;
        }

        #region Public Method

        public async Task<string> AddPunctuationAsync(string text, CancellationToken? ct = null)
        {

            if (_punct == null)
            {
                throw new InvalidOperationException("Punctuation is not initialized or has been disposed. Please ensure it is loaded successfully before adding punctuation.");
            }

            return await runner.RunAsync(cancellationToken =>
            {
                return Task.FromResult(_punct.AddPunct(text));
            }, cancellationToken: ct ?? CancellationToken.None, policy: Runtime.Utilities.ExecutionPolicy.Auto);
        }

        #endregion

        protected override void OnDestroy()
        {
            SafeExecute(() =>
            {
                _punct?.Dispose();
                _punct = null;
            });
        }
    }
}
