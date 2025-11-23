using System;

namespace Eitan.SherpaONNXUnity.Runtime
{
    public class SuccessFeedback : SherpaFeedback
    {
        public SuccessFeedback(SherpaONNXModelMetadata metadata, string message, Exception exception = null) : base(metadata, message, exception)
        {
        }

        public override void Accept(ISherpaFeedbackHandler handler) => handler.OnFeedback(this);
    }
}
