using System;

namespace Eitan.SherpaONNXUnity.Runtime
{
    public readonly struct PrepareResult
    {
        public PrepareResult(
            bool success,
            PrepareErrorCode errorCode,
            string message,
            Exception exception,
            bool cleanupAttempted)
        {
            Success = success;
            ErrorCode = errorCode;
            Message = message ?? string.Empty;
            Exception = exception;
            CleanupAttempted = cleanupAttempted;
        }

        public bool Success { get; }
        public PrepareErrorCode ErrorCode { get; }
        public string Message { get; }
        public Exception Exception { get; }
        public bool CleanupAttempted { get; }

        public static PrepareResult Ok(string message = "Prepare succeeded.")
        {
            return new PrepareResult(true, PrepareErrorCode.None, message, null, cleanupAttempted: false);
        }

        public static PrepareResult Fail(PrepareErrorCode code, string message, Exception exception = null, bool cleanupAttempted = false)
        {
            return new PrepareResult(false, code, message, exception, cleanupAttempted);
        }
    }
}
