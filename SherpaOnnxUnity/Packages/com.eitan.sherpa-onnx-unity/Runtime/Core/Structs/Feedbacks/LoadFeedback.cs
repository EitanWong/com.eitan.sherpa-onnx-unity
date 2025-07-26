using System;

namespace Eitan.SherpaOnnxUnity.Runtime
{
    public class LoadFeedback : SherpaFeedback
    {
        public LoadFeedback(SherpaOnnxModelMetadata metadata, string message, Exception exception = null) : base(metadata, message, exception)
        {
        }

        public override void Accept(ISherpaFeedbackHandler handler) => handler.OnFeedback(this);
    }
}