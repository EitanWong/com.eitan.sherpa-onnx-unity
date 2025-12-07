using System;

namespace Eitan.SherpaONNXUnity.Runtime
{
    /// <summary>
    /// Represents a single SherpaONNX log emission for in-memory capture and editor tooling.
    /// </summary>
    public readonly struct SherpaLogEntry
    {
        public SherpaLogEntry(
            DateTime timestampUtc,
            SherpaLogLevel level,
            string category,
            string message,
            string formattedMessage,
            string stackTrace,
            string exceptionType,
            int threadId)
        {
            TimestampUtc = timestampUtc;
            Level = level;
            Category = category ?? string.Empty;
            Message = message ?? string.Empty;
            FormattedMessage = formattedMessage ?? string.Empty;
            StackTrace = stackTrace ?? string.Empty;
            ExceptionType = exceptionType ?? string.Empty;
            ThreadId = threadId;
        }

        public DateTime TimestampUtc { get; }
        public SherpaLogLevel Level { get; }
        public string Category { get; }
        public string Message { get; }
        public string FormattedMessage { get; }
        public string StackTrace { get; }
        public string ExceptionType { get; }
        public int ThreadId { get; }
        public bool HasStackTrace => !string.IsNullOrEmpty(StackTrace);
    }
}
