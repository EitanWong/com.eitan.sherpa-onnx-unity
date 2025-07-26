namespace Eitan.SherpaOnnxUnity.Runtime
{

    // The Visitor interface defines a Visit method for each concrete feedback type.
    public interface ISherpaFeedbackHandler
    {
        void OnFeedback(PrepareFeedback feedback);
        void OnFeedback(DownloadFeedback feedback);
        void OnFeedback(UncompressFeedback feedback);
        void OnFeedback(VerifyFeedback feedback);
        void OnFeedback(LoadFeedback feedback);
        void OnFeedback(CancelFeedback feedback);
        void OnFeedback(SuccessFeedback feedback);
        void OnFeedback(FailedFeedback feedback);
        void OnFeedback(CleanFeedback feedback);
    }
}