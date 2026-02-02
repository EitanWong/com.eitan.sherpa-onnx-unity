using System;
using System.Threading;
using System.Threading.Tasks;

namespace Eitan.SherpaONNXUnity.Runtime
{
    public sealed class PrepareOptions
    {
        public Func<PrepareContext, SherpaONNXFeedbackReporter, int, CancellationToken, Task<bool>> VerifyExistingAsync { get; set; }
        public Func<PrepareContext, SherpaONNXFeedbackReporter, int, CancellationToken, Task<PrepareErrorCode>> DownloadAsync { get; set; }
        public Func<PrepareContext, SherpaONNXFeedbackReporter, int, CancellationToken, Task<bool>> ExtractAsync { get; set; }
        public Func<PrepareContext, string[], SherpaONNXFeedbackReporter, CancellationToken, Task> CleanupAsync { get; set; }
    }
}
