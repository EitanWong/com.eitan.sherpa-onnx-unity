using Eitan.SherpaONNXUnity.Runtime.Utilities;

namespace Eitan.SherpaONNXUnity.Runtime
{
    /// <summary>
    /// Applies serialized runtime settings snapshots to the process environment and logging system.
    /// </summary>
    public static class SherpaONNXRuntimeSettingsApplier
    {
        public static void ApplyFromResources(bool forceReload = false)
        {
            // Capture Unity state on the main thread before any background work touches these values.
            ThreadingUtils.PrimeUnityInfo();
            SherpaPathResolver.PrimeUnityPaths();

            if (forceReload)
            {
                SherpaONNXRuntimeResourceProvider.ReloadFromResources();
            }
            else
            {
                SherpaONNXRuntimeResourceProvider.PreloadFromResources();
            }

            var snapshot = SherpaONNXRuntimeResourceProvider.GetRuntimeSettingsSnapshot();
            snapshot.ApplyEnvironmentDefaults();

            SherpaONNXRuntimeSettings.ApplyEnvironmentOverridesFromProcess();
            // Always honor process overrides for logging even when no asset exists.
            SherpaLog.ConfigureFromEnvironment();
        }

#if UNITY_EDITOR
        public static void ReloadAndApplyInEditor()
        {
            ApplyFromResources(forceReload: true);
        }
#endif
    }
}
