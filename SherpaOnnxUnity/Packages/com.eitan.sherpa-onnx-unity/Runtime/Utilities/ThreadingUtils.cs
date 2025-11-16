namespace Eitan.SherpaONNXUnity.Runtime.Core.Utilities
{
    using UnityEngine;

    /// <summary>
    /// Provides a conservative, environment-aware thread budget for SherpaONNX models.
    /// Keeps headroom for Unity, the OS, and thermal constraints on mobile hardware.
    /// </summary>
    public static class ThreadingUtils
    {
        private const int MaxDesktopThreads = 16;
        private const int MaxMobileThreads = 6;

        public static int GetAdaptiveThreadCount(int minimum = 1, int? maximumOverride = null)
        {
            int logicalCores = Mathf.Max(1, UnityEngine.Device.SystemInfo.processorCount);
            bool isMobile = Application.isMobilePlatform;
            bool isBatchMode = Application.isBatchMode;
            int memoryMb = Mathf.Max(0, UnityEngine.Device.SystemInfo.systemMemorySize);

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
    }
}
