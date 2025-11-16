using System;

namespace Eitan.SherpaONNXUnity.Runtime
{
    // A base class for any feedback that involves a progress percentage.
    public abstract class FileFeedback : SherpaFeedback
    {
        public string FilePath { get; set; } // Progress of the current task (0.0 to 1.0)
        protected FileFeedback(SherpaONNXModelMetadata metadata, string message, string filePath, Exception exception = null) : base(metadata, message, exception)
        {
            this.FilePath = filePath;
        }


    }

}
