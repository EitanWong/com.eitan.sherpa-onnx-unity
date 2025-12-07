using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using UnityEngine;

namespace Eitan.SherpaONNXUnity.Runtime
{
    /// <summary>
    /// Log level order: Off &lt; Error &lt; Warning &lt; Info &lt; Verbose &lt; Trace.
    /// Trace includes call stacks to aid deep debugging of initialization and model calls.
    /// </summary>
    [Serializable]
    public enum SherpaLogLevel
    {
        Off = 0,
        Error = 1,
        Warning = 2,
        Info = 3,
        Verbose = 4,
        Trace = 5
    }

    /// <summary>
    /// Centralized logger for the SherpaONNX Unity package.
    /// Controlled by <see cref="SherpaONNXRuntimeSettings"/> and environment overrides.
    /// </summary>
    public static class SherpaLog
    {
        private static readonly ILogger _logger = new UnityLogger();
        private static volatile bool _enabled = true;
        private static volatile SherpaLogLevel _level = SherpaLogLevel.Info;
        private static volatile bool _includeStackTracesForTrace = true;
        private const int MaxCapturedEntries = 512;
        private static readonly object _entriesLock = new object();
        private static readonly List<SherpaLogEntry> _capturedEntries = new List<SherpaLogEntry>(MaxCapturedEntries);
        private static readonly ConcurrentQueue<SherpaLogEntry> _pendingEntries = new ConcurrentQueue<SherpaLogEntry>();

        /// <summary>
        /// Raised for every SherpaLog emission (after formatting). Safe for editor tooling to subscribe.
        /// </summary>
        public static event Action<SherpaLogEntry> EntryCaptured;

        public static bool Enabled => _enabled;
        public static SherpaLogLevel Level => _level;
        internal static bool TraceEnabled => _enabled && _level >= SherpaLogLevel.Trace;

        /// <summary>
        /// Configure the logger directly (bypassing environment).
        /// </summary>
        public static void Configure(
            SherpaLogLevel level,
            bool enabled = true,
            bool includeStackTracesForTrace = true)
        {
            _level = level;
            _enabled = enabled;
            _includeStackTracesForTrace = includeStackTracesForTrace;
        }

        /// <summary>
        /// Reads configuration from SherpaONNXEnvironment keys.
        /// </summary>
        public static void ConfigureFromEnvironment()
        {
            var enabled = SherpaONNXEnvironment.GetBool(
                SherpaONNXEnvironment.BuiltinKeys.LoggingEnabled,
                @default: true);
            var levelText = SherpaONNXEnvironment.Get(
                SherpaONNXEnvironment.BuiltinKeys.LoggingLevel,
                SherpaLogLevel.Info.ToString());
            var includeStacks = SherpaONNXEnvironment.GetBool(
                SherpaONNXEnvironment.BuiltinKeys.LoggingTraceStacks,
                @default: true);

            Configure(ParseLevel(levelText, SherpaLogLevel.Info), enabled, includeStacks);
        }

        /// <summary>
        /// Parse user-provided text into a log level. Accepts short aliases like "warn" or "debug".
        /// </summary>
        public static SherpaLogLevel ParseLevel(string raw, SherpaLogLevel fallback)
        {
            if (Enum.TryParse(raw, ignoreCase: true, out SherpaLogLevel parsed))
            {
                return parsed;
            }

            switch (raw?.Trim().ToLowerInvariant())
            {
                case "err":
                case "error":
                    return SherpaLogLevel.Error;
                case "warn":
                case "warning":
                    return SherpaLogLevel.Warning;
                case "info":
                case "log":
                    return SherpaLogLevel.Info;
                case "debug":
                case "verbose":
                    return SherpaLogLevel.Verbose;
                case "trace":
                    return SherpaLogLevel.Trace;
                case "off":
                case "none":
                    return SherpaLogLevel.Off;
                default:
                    return fallback;
            }
        }

        public static void Error(
            string message,
            Exception exception = null,
            string category = null,
            bool includeStackTrace = false) =>
            LogInternal(SherpaLogLevel.Error, message, exception, category, includeStackTrace);

        public static void Warning(
            string message,
            Exception exception = null,
            string category = null,
            bool includeStackTrace = false) =>
            LogInternal(SherpaLogLevel.Warning, message, exception, category, includeStackTrace);

        public static void Info(
            string message,
            Exception exception = null,
            string category = null,
            bool includeStackTrace = false) =>
            LogInternal(SherpaLogLevel.Info, message, exception, category, includeStackTrace);

        public static void Verbose(
            string message,
            Exception exception = null,
            string category = null,
            bool includeStackTrace = false) =>
            LogInternal(SherpaLogLevel.Verbose, message, exception, category, includeStackTrace);

        /// <summary>
        /// Trace always includes a call stack to aid deep debugging when enabled.
        /// </summary>
        public static void Trace(
            string message,
            Exception exception = null,
            string category = null,
            bool includeStackTrace = true) =>
            LogInternal(
                SherpaLogLevel.Trace,
                message,
                exception,
                category,
                includeStackTrace || _includeStackTracesForTrace);

        public static void Exception(Exception exception, string category = null, string message = null)
        {
            if (exception == null)
            {
                return;
            }

            var composed = string.IsNullOrEmpty(message)
                ? exception.Message
                : $"{message}: {exception.Message}";
            LogInternal(SherpaLogLevel.Error, composed, exception, category, includeStackTrace: true);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void LogInternal(
            SherpaLogLevel level,
            string message,
            Exception exception,
            string category,
            bool includeStackTrace)
        {
            if (!_enabled || level == SherpaLogLevel.Off || level > _level)
            {
                return;
            }

            if (string.IsNullOrEmpty(message) && exception == null)
            {
                return;
            }

            var stackTrace = (includeStackTrace || ShouldAppendTrace(level, exception))
                ? GetStackTrace(exception)
                : string.Empty;

            var text = BuildMessage(
                level,
                message ?? string.Empty,
                category,
                exception,
                stackTrace);

            CaptureEntry(new SherpaLogEntry(
                DateTime.UtcNow,
                level,
                category,
                message ?? string.Empty,
                text,
                stackTrace,
                exception?.GetType().Name ?? string.Empty,
                Thread.CurrentThread.ManagedThreadId));

            Dispatch(level, text);
        }

        private static void Dispatch(SherpaLogLevel level, string message)
        {
            try
            {
                switch (level)
                {
                    case SherpaLogLevel.Error:
                        _logger.LogError(message);
                        break;
                    case SherpaLogLevel.Warning:
                        _logger.LogWarning(message);
                        break;
                    default:
                        _logger.LogInfo(message);
                        break;
                }
            }
            catch (Exception ex)
            {
                try { UnityEngine.Debug.LogException(ex); } catch { }
            }
        }

        private static bool ShouldAppendTrace(SherpaLogLevel level, Exception exception) =>
            (exception != null && !string.IsNullOrEmpty(exception.StackTrace)) ||
            (_includeStackTracesForTrace && _level == SherpaLogLevel.Trace);

        private static string BuildMessage(
            SherpaLogLevel level,
            string message,
            string category,
            Exception exception,
            string stackTrace)
        {
            var sb = new StringBuilder(512);
            sb.Append("[SherpaONNX]");
            sb.Append('[').Append(level.ToString()).Append(']');

            if (!string.IsNullOrEmpty(category))
            {
                sb.Append('[').Append(category).Append(']');
            }

            sb.Append(' ').Append(message);

            if (exception != null)
            {
                sb.Append(" (").Append(exception.GetType().Name).Append(": ").Append(exception.Message).Append(')');
            }

            if (!string.IsNullOrEmpty(stackTrace))
            {
                sb.AppendLine();
                sb.Append(stackTrace);
            }

            return sb.ToString();
        }

        private static void CaptureEntry(SherpaLogEntry entry)
        {
            lock (_entriesLock)
            {
                _capturedEntries.Add(entry);
                if (_capturedEntries.Count > MaxCapturedEntries)
                {
                    var overflow = _capturedEntries.Count - MaxCapturedEntries;
                    _capturedEntries.RemoveRange(0, overflow);
                }
            }

            if (EntryCaptured != null)
            {
                _pendingEntries.Enqueue(entry);
                FlushPendingEntries();
            }
            else
            {
                while (_pendingEntries.TryDequeue(out _)) { }
            }
        }

        /// <summary>
        /// Returns up to <paramref name="maxEntries"/> most recent log entries (newest last).
        /// </summary>
        public static IReadOnlyList<SherpaLogEntry> GetRecentEntries(int maxEntries = 200)
        {
            lock (_entriesLock)
            {
                if (_capturedEntries.Count == 0)
                {
                    return Array.Empty<SherpaLogEntry>();
                }

                var count = Math.Min(maxEntries, _capturedEntries.Count);
                var start = Math.Max(0, _capturedEntries.Count - count);
                return _capturedEntries.GetRange(start, count);
            }
        }

        internal static void ClearCapturedEntries()
        {
            lock (_entriesLock)
            {
                _capturedEntries.Clear();
            }
        }

        private static void FlushPendingEntries()
        {
            if (EntryCaptured == null || _pendingEntries.IsEmpty)
            {
                return;
            }

            while (_pendingEntries.TryDequeue(out var entry))
            {
                try
                {
                    EntryCaptured?.Invoke(entry);
                }
                catch (Exception ex)
                {
                    try { UnityEngine.Debug.LogException(ex); } catch { }
                }
            }
        }

        private static string GetStackTrace(Exception exception)
        {
            try
            {
                if (exception != null && !string.IsNullOrEmpty(exception.StackTrace))
                {
                    return exception.StackTrace;
                }

                var trace = new StackTrace(skipFrames: 2, fNeedFileInfo: true);
                return trace.ToString();
            }
            catch
            {
                return Environment.StackTrace;
            }
        }
    }
}
