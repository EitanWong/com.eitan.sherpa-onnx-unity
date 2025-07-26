using System;


namespace Eitan.SherpaOnnxUnity.Runtime
{

    /// <summary>
    /// Interface for logging operations.
    /// </summary>
    public interface ILogger : IDisposable
    {
        /// <summary>
        /// Logs an error message.
        /// </summary>
        /// <param name="message">The error message to log.</param>
        void LogError(string message);

        /// <summary>
        /// Logs a warning message.
        /// </summary>
        /// <param name="message">The warning message to log.</param>
        void LogWarning(string message);

        /// <summary>
        /// Logs an informational message.
        /// </summary>
        /// <param name="message">The informational message to log.</param>
        void LogInfo(string message);
    } 

}