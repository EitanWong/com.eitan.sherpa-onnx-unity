using System;

namespace Eitan.SherpaOnnxUnity.Runtime
{
    // A base class for any feedback that involves a progress percentage.
    public abstract class ProgressFeedback : FileFeedback
    {
        public float Progress { get; set; } // Progress of the current task (0.0 to 1.0)
        protected ProgressFeedback(SherpaOnnxModelMetadata metadata,string message,string filePath, float progress=0, Exception exception = null) : base(metadata,  message, filePath,  exception)
        {
            this.Progress = progress;
        }


    }

}