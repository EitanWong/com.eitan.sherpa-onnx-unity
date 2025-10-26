using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Eitan.SherpaOnnxUnity.Runtime;
using Eitan.SherpaOnnxUnity.Runtime.Utilities;
using Eitan.SherpaOnnxUnity.Runtime.Native;

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

        protected override async Task<bool> Initialization(SherpaOnnxModelMetadata metadata, int sampleRate, bool isMobilePlatform, SherpaOnnxFeedbackReporter reporter, CancellationToken ct)
        {
            try
            {
                reporter?.Report(new LoadFeedback(metadata, message: $"Start Loading: {metadata.modelId}"));

                _sampleRate = sampleRate;
                var config = CreatePunctuationConfig(metadata, isMobilePlatform, reporter);

                return await runner.RunAsync<bool>(cancellationToken =>
                  {
                      try
                      {

                          using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, cancellationToken);
                          linkedCts.Token.ThrowIfCancellationRequested();

                          if (IsDisposed) { return Task.FromResult(false); }

                          reporter?.Report(new LoadFeedback(metadata, message: $"Loading Punctuation model: {metadata.modelId}"));
                          _punct = new OfflinePunctuation(config);
                          var initialized = IsSuccessInitializad(_punct);
                          if (initialized)
                          {
                              reporter?.Report(new LoadFeedback(metadata, message: $"Punctuation model loaded successfully: {metadata.modelId}"));
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

        private OfflinePunctuationConfig CreatePunctuationConfig(SherpaOnnxModelMetadata metadata, bool isMobilePlatform, SherpaOnnxFeedbackReporter reporter)
        {
            var fallbackReporter = CreateFallbackReporter(metadata, reporter);
            var config = new OfflinePunctuationConfig();
            config.Model.NumThreads = ThreadingUtils.GetAdaptiveThreadCount();
            var int8QuantKeyword = isMobilePlatform ? "int8" : null;

            config.Model.CtTransformer = ModelFileResolver.ResolveRequiredFile(
                metadata,
                "CT-Transformer model",
                fallbackReporter,
                ModelFileCriteria.FromKeywords("model", int8QuantKeyword),
                ModelFileCriteria.FromKeywords("model"),
                ModelFileCriteria.FromExtensions(".onnx"));

            return config;
        }

        #region Public Method

        public async Task<string> AddPunctuationAsync(string text, CancellationToken? ct = null)
        {

            if (_punct == null)
            {
                throw new InvalidOperationException("Punctuation is not initialized or has been disposed. Please ensure it is loaded successfully before adding punctuation.");
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            return await runner.RunAsync(cancellationToken =>
            {
                var sanitizedInput = text.Trim();
                return Task.FromResult(_punct.AddPunct(sanitizedInput));
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
