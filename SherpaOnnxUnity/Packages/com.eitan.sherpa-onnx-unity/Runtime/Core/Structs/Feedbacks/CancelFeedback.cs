using System;

namespace Eitan.SherpaOnnxUnity.Runtime
{
    public class CancelFeedback : SherpaFeedback
    {
        public CancelFeedback(SherpaOnnxModelMetadata metadata, string message, Exception exception = null) : base(metadata, message, exception)
        {
        }

        public override void Accept(ISherpaFeedbackHandler handler)=> handler.OnFeedback(this);
    
    }
}