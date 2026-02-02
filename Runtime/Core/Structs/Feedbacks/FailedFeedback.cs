using System;

namespace Eitan.SherpaONNXUnity.Runtime
{

    public class FailedFeedback : SherpaFeedback
    {
        public FailedFeedback(SherpaONNXModelMetadata metadata, string message, Exception exception = null, PrepareErrorCode? errorCode = null) : base(metadata, message, exception)
        {
            ErrorCode = errorCode;
        }

        public PrepareErrorCode? ErrorCode { get; }

        public override void Accept(ISherpaFeedbackHandler handler) => handler.OnFeedback(this);
    }
}
