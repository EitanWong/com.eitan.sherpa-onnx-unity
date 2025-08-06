namespace Eitan.SherpaOnnxUnity.Runtime
{
    /// <summary>
    /// Default implementation of ILogger that uses UnityEngine.Debug.
    /// </summary>
    internal class UnityLogger : ILogger
    {
        private bool _disposed = false;

        public void LogError(string message)
        {
            if (!_disposed)
            {
                UnityEngine.Debug.LogError(message);
            }

        }

        public void LogWarning(string message)
        {
            if (!_disposed)
            {
                UnityEngine.Debug.LogWarning(message);
            }

        }

        public void LogInfo(string message)
        {
            if (!_disposed)
            {
                UnityEngine.Debug.Log(message);
            }

        }

        public void Dispose()
        {
            _disposed = true;
        }
    }
}