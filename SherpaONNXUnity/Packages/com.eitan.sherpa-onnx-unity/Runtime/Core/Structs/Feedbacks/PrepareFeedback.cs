using System;

namespace Eitan.SherpaONNXUnity.Runtime
{

    public class PrepareFeedback : SherpaFeedback
    {
        public PrepareFeedback(SherpaONNXModelMetadata metadata, string message, Exception exception = null) : base(metadata, message, exception)
        {
        }

        public override void Accept(ISherpaFeedbackHandler handler) => handler.OnFeedback(this);

    }

}
