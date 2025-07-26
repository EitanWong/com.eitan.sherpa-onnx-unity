using System;

namespace Eitan.SherpaOnnxUnity.Runtime
{
    public class VerifyFeedback : ProgressFeedback
    {
        public string CalculatedHash { get; }
        public string ExpectedHash { get; }

        public VerifyFeedback(SherpaOnnxModelMetadata metadata, string message, string filePath, string calculatedHash = null, string expectedHash = null, float progress = 0, Exception exception = null) : base(metadata, message, filePath, progress, exception)
        {
            this.CalculatedHash = calculatedHash;
            this.ExpectedHash = expectedHash;
        }

        public override void Accept(ISherpaFeedbackHandler handler) => handler.OnFeedback(this);
    }
}
