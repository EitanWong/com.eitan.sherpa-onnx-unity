using System;

namespace Eitan.SherpaOnnxUnity.Runtime
{

    public class UncompressFeedback : ProgressFeedback
    {
        public UncompressFeedback(SherpaOnnxModelMetadata metadata, string message, string filePath, float progress = 0, Exception exception = null) : base(metadata, message, filePath, progress, exception)
        {
        }

        public override void Accept(ISherpaFeedbackHandler handler) => handler.OnFeedback(this);
    }

}