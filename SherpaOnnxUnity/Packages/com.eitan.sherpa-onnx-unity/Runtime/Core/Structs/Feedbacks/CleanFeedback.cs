using System;

namespace Eitan.SherpaONNXUnity.Runtime
{

    public class CleanFeedback : FileFeedback
    {
        public CleanFeedback(SherpaONNXModelMetadata metadata, string filePath, string message, Exception exception = null) : base(metadata, message, filePath, exception)
        {
        }

        public override void Accept(ISherpaFeedbackHandler handler) => handler.OnFeedback(this);
    }
}
