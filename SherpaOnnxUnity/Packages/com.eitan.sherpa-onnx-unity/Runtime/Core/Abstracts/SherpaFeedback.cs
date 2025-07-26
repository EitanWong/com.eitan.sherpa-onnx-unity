
using System;

namespace Eitan.SherpaOnnxUnity.Runtime
{
    // A base class for any feedback that involves a progress percentage.
    public abstract class SherpaFeedback : IFeedback
    {
        // public string ModelID { get; set; }
        public readonly SherpaOnnxModelMetadata Metadata;
        public string Message { get; protected set; }
        public Exception Exception { get; private set; }

        public SherpaFeedback(SherpaOnnxModelMetadata metadata, string message, Exception exception = null)
        {
            this.Metadata = metadata;
            this.Message = $"[{GetType().Name}]:{message}";
            this.Exception = exception;
        }

        public abstract void Accept(ISherpaFeedbackHandler handler);
    }
}