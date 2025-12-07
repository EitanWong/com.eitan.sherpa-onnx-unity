using UnityEngine;

namespace Eitan.SherpaONNXUnity.Runtime
{
    /// <summary>
    /// ILogger implementation that forwards messages to Unity's console.
    /// </summary>
    internal sealed class UnityLogger : ILogger
    {
        private bool _disposed;

        public void LogError(string message)
        {
            if (_disposed) { return; }
            UnityEngine.Debug.LogError(message);
        }

        public void LogWarning(string message)
        {
            if (_disposed) { return; }
            UnityEngine.Debug.LogWarning(message);
        }

        public void LogInfo(string message)
        {
            if (_disposed) { return; }
            UnityEngine.Debug.Log(message);
        }

        public void Dispose()
        {
            _disposed = true;
        }
    }
}
