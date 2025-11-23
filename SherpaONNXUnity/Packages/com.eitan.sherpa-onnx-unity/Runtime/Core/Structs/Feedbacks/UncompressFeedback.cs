using System;

namespace Eitan.SherpaONNXUnity.Runtime
{

    public class DecompressFeedback : ProgressFeedback
    {
        public DecompressFeedback(SherpaONNXModelMetadata metadata, string message, string filePath, float progress = 0, Exception exception = null) : base(metadata, message, filePath, progress, exception)
        {
        }

        public override void Accept(ISherpaFeedbackHandler handler) => handler.OnFeedback(this);
    }

}
