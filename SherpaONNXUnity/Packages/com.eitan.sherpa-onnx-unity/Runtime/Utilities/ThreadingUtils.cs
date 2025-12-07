namespace Eitan.SherpaONNXUnity.Runtime.Utilities
{
    using System;
    using System.Threading;
    using UnityEngine;

    /// <summary>
    /// Provides a conservative, environment-aware thread budget for SherpaONNX models.
    /// Keeps headroom for Unity, the OS, and thermal constraints on mobile hardware.
    /// </summary>
    public static class ThreadingUtils
    {
        private const int MaxDesktopThreads = 16;
        private const int MaxMobileThreads = 6;

        private static readonly object PrimeLock = new object();
        private static bool s_Primed;
        private static int s_CachedCores = -1;
        private static int s_CachedMemoryMb = -1;
        private static bool s_CachedIsMobile;
        private static bool s_CachedIsBatch;
        private static bool s_CachedIsEditor;

        public static int GetAdaptiveThreadCount(int minimum = 1, int? maximumOverride = null)
        {
            // Avoid invoking Unity SystemInfo off the main thread. If we have not been primed, fall back to
            // Environment values which are safe everywhere.
            GetPlatformSnapshot(out int logicalCores, out int memoryMb, out bool isMobile, out bool isBatchMode, out _);

            int reservedCores = isMobile
                ? Mathf.Max(1, Mathf.CeilToInt(logicalCores * 0.45f))
                : Mathf.Max(1, Mathf.CeilToInt(logicalCores * 0.25f));

            if (isBatchMode && logicalCores >= 4)
            {
                reservedCores = Mathf.Max(1, reservedCores - 1);
            }

            int usableCores = Mathf.Max(1, logicalCores - reservedCores);

            float utilization = isMobile ? 0.6f : (logicalCores >= 16 ? 0.6f : 0.75f);

            if (memoryMb > 0 && memoryMb < 4000)
            {
                utilization = Mathf.Min(utilization, 0.6f);
            }

            int recommended = Mathf.Clamp(Mathf.CeilToInt(usableCores * utilization), minimum, usableCores);

            int hardMax = maximumOverride ?? (isMobile ? MaxMobileThreads : MaxDesktopThreads);
            if (hardMax > 0)
            {
                recommended = Mathf.Min(recommended, hardMax);
            }

            if ((recommended & 1) != 0)
            {
                recommended = Mathf.Max(minimum, recommended - 1);
            }

            return Mathf.Clamp(recommended, minimum, logicalCores);
        }

        /// <summary>
        /// Call on the Unity main thread (e.g., in Awake) to capture thread-safe SystemInfo values.
        /// </summary>
        public static void PrimeUnityInfo()
        {
            if (s_Primed)
            {
                return;
            }

            lock (PrimeLock)
            {
                if (s_Primed)
                {
                    return;
                }

                try
                {
                    s_CachedCores = Mathf.Max(1, UnityEngine.SystemInfo.processorCount);
                    s_CachedMemoryMb = Mathf.Max(0, UnityEngine.SystemInfo.systemMemorySize);
                    s_CachedIsMobile = Application.isMobilePlatform;
                    s_CachedIsBatch = Application.isBatchMode;
                    s_CachedIsEditor = Application.isEditor;
                }
                catch (Exception ex)
                {
                    s_CachedCores = Math.Max(1, Environment.ProcessorCount);
                    s_CachedMemoryMb = -1;
                    s_CachedIsMobile = false;
                    s_CachedIsBatch = false;
                    s_CachedIsEditor = false;

                    try
                    {
                        SherpaLog.Warning($"[ThreadingUtils] Failed to prime Unity SystemInfo, falling back to Environment: {ex.Message}");
                    }
                    catch
                    {
                        // Ignore logging errors when not in Unity context.
                    }
                }
                finally
                {
                    s_Primed = true;
                }
            }
        }

        /// <summary>
        /// Retrieves platform info without touching Unity SystemInfo unless it has been primed on the main thread.
        /// </summary>
        public static void GetPlatformSnapshot(out int logicalCores, out int memoryMb, out bool isMobile, out bool isBatchMode, out bool isEditor)
        {
            if (!s_Primed && IsUnityMainThread())
            {
                // Safe to prime when invoked from main thread.
                PrimeUnityInfo();
            }

            logicalCores = s_Primed ? s_CachedCores : Math.Max(1, Environment.ProcessorCount);
            memoryMb = s_Primed ? s_CachedMemoryMb : 0;
            isMobile = s_Primed && s_CachedIsMobile;
            isBatchMode = s_Primed && s_CachedIsBatch;
            isEditor = s_Primed && s_CachedIsEditor;
        }

        private static bool IsUnityMainThread()
        {
            var ctx = SynchronizationContext.Current;
            if (ctx == null)
            {
                return false;
            }

            // Avoid referencing UnitySynchronizationContext directly (it's internal); compare by name.
            var typeName = ctx.GetType().Name;
            return string.Equals(typeName, "UnitySynchronizationContext", StringComparison.Ordinal) ||
                   string.Equals(typeName, "UnitySynchronizationContext", StringComparison.OrdinalIgnoreCase);
        }
    }
}
