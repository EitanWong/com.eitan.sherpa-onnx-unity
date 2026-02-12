using System;
using System.Threading;
using Eitan.SherpaONNXUnity.Runtime.Constants;

namespace Eitan.SherpaONNXUnity.Runtime.Utilities
{
    internal static class SherpaPathResolver
    {
        private static readonly object PrimeLock = new object();
        private static bool s_Primed;
        private static string s_PersistentDataPath;
        private static string s_StreamingAssetsPath;

        /// <summary>
        /// Capture Unity path values on the main thread so background tasks never touch Unity APIs.
        /// </summary>
        public static void PrimeUnityPaths()
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

                if (!IsUnityMainThread())
                {
                    throw new InvalidOperationException("SherpaPathResolver.PrimeUnityPaths must be called from the Unity main thread before background work uses Application paths.");
                }

                string persistentPath;
                string streamingAssetsPath;
                try
                {
                    persistentPath = UnityEngine.Application.persistentDataPath;
                    streamingAssetsPath = UnityEngine.Application.streamingAssetsPath;
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException("SherpaPathResolver.PrimeUnityPaths must be called from the Unity main thread before background work uses Application paths.", ex);
                }

                if (string.IsNullOrEmpty(persistentPath) || string.IsNullOrEmpty(streamingAssetsPath))
                {
                    throw new InvalidOperationException("Unity application paths are unavailable. Ensure Unity main thread initialization completed before model loading.");
                }

                s_PersistentDataPath = persistentPath;
                s_StreamingAssetsPath = streamingAssetsPath;
                s_Primed = true;
            }
        }

        internal static string GetPersistentDataPath()
        {
            EnsurePrimedIfMainThread();
            if (string.IsNullOrEmpty(s_PersistentDataPath))
            {
                throw new InvalidOperationException("Application.persistentDataPath is unavailable. Ensure it is read on the Unity main thread before background tasks use model paths.");
            }
            return s_PersistentDataPath;
        }
        private static string GetStreamingAssetsPath()
        {
            EnsurePrimedIfMainThread();
            if (string.IsNullOrEmpty(s_StreamingAssetsPath))
            {
                throw new InvalidOperationException("Application.streamingAssetsPath is unavailable. Ensure it is read on the Unity main thread before background tasks use model paths.");
            }
            return s_StreamingAssetsPath;
        }

        private static void EnsurePrimedIfMainThread()
        {
            if (!s_Primed && IsUnityMainThread())
            {
                PrimeUnityPaths();
            }
        }

        private static bool IsUnityMainThread()
        {
            var ctx = SynchronizationContext.Current;
            if (ctx == null)
            {
                return false;
            }

            var typeName = ctx.GetType().Name;
            return string.Equals(typeName, "UnitySynchronizationContext", StringComparison.Ordinal) ||
                   string.Equals(typeName, "UnitySynchronizationContext", StringComparison.OrdinalIgnoreCase);
        }

        public static string GetModelRootPath(string modelID)
        {
            //check the modelId if it's Empty

            if (string.IsNullOrEmpty(modelID))
            {
                throw new System.Exception("The modelID can't be Null or Empty");
            }

            var moduleType = SherpaUtils.Model.GetModuleTypeByModelId(modelID);
            return System.IO.Path.Combine(GetModuleRootPath(moduleType), modelID);
        }

        public static string GetModuleRootPath(SherpaONNXModuleType moduleType)
        {
            var ModuleName = System.Text.RegularExpressions.Regex.Replace(moduleType.ToString(), @"([a-z])([A-Z])", "$1-$2").ToLower();

            var modelPathFolder = System.IO.Path.Combine(SherpaONNXConstants.RootDirectoryName, SherpaONNXConstants.ModelRootDirectoryName);

#if UNITY_EDITOR
            return System.IO.Path.Combine(GetStreamingAssetsPath(), modelPathFolder, ModuleName);
#elif UNITY_ANDROID
            return System.IO.Path.Combine(GetPersistentDataPath(), modelPathFolder, ModuleName);
#elif UNITY_IOS
            return System.IO.Path.Combine(GetPersistentDataPath(), modelPathFolder, ModuleName);
#else
            return System.IO.Path.Combine(GetStreamingAssetsPath(), modelPathFolder, ModuleName);
#endif
        }

    }

}
