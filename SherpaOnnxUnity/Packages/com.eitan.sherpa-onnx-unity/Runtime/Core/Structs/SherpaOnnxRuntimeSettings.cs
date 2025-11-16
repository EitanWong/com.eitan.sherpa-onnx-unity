using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Eitan.SherpaONNXUnity.Runtime
{
    /// <summary>
    /// ScriptableObject that stores default environment values for SherpaONNX.
    /// Serialized under Resources so the data ships with builds and can be read very early.
    /// </summary>
    public sealed class SherpaONNXRuntimeSettings : ScriptableObject
    {
        public const string ResourceName = "SherpaONNXRuntimeSettings";
        public const string AssetPath = "Assets/Resources/" + ResourceName + ".asset";
        internal const string FetchLatestManifestPropertyName = nameof(_fetchLatestManifest);
        internal const string AutoDownloadModelsPropertyName = nameof(_autoDownloadModels);
        internal const string ChecksumCacheDirectoryPropertyName = nameof(_checksumCacheDirectory);
        internal const string ChecksumCacheTtlSecondsPropertyName = nameof(_checksumCacheTtlSeconds);

        [SerializeField]
        [Tooltip("When enabled (default), the manifest download routine will always try to fetch the latest checksum.txt list.")]
        private bool _fetchLatestManifest = true;

        [SerializeField]
        [Tooltip("When disabled, the prepare pipeline skips remote downloads and expects models to exist locally.")]
        private bool _autoDownloadModels = true;

        [SerializeField]
        [Tooltip("Optional absolute path for checksum.txt caching. Leave empty to use the platform-specific temp folder.")]
        private string _checksumCacheDirectory = string.Empty;

        [SerializeField]
        [Tooltip("Cache lifetime for fetched checksum.txt content, in seconds. Use 0 to disable caching entirely.")]
        private int _checksumCacheTtlSeconds = 3600;

        internal static SherpaONNXRuntimeSettings LoadFromResources()
        {
            // Fast path: default root-level asset.
            var direct = Resources.Load<SherpaONNXRuntimeSettings>(ResourceName);
            if (direct != null)
            {
                return direct;
            }

            var discovered = Resources.LoadAll<SherpaONNXRuntimeSettings>(string.Empty);
            if (discovered == null || discovered.Length == 0)
            {
                return null;
            }

            var valid = new List<SherpaONNXRuntimeSettings>(discovered.Length);
            foreach (var candidate in discovered)
            {
                if (candidate != null)
                {
                    valid.Add(candidate);
                }
            }

            if (valid.Count == 0)
            {
                return null;
            }

            if (valid.Count > 1)
            {
                Debug.LogError($"Multiple {nameof(SherpaONNXRuntimeSettings)} assets detected under Resources. Please keep only one asset to avoid ambiguity.");
            }

            valid.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            return valid[0];
        }

        internal void ApplyEnvironmentDefaults()
        {
            SetBool(SherpaONNXEnvironment.BuiltinKeys.FetchLatestManifest, _fetchLatestManifest);
            SetBool(SherpaONNXEnvironment.BuiltinKeys.AutoDownloadModels, _autoDownloadModels);
            SetStringOrClear(
                SherpaONNXEnvironment.BuiltinKeys.ChecksumCacheDirectory,
                _checksumCacheDirectory);

            var ttl = Mathf.Max(0, _checksumCacheTtlSeconds);
            SherpaONNXEnvironment.Set(
                SherpaONNXEnvironment.BuiltinKeys.ChecksumCacheTtlSeconds,
                ttl.ToString(CultureInfo.InvariantCulture));
        }

        private static void SetBool(string key, bool value) =>
            SherpaONNXEnvironment.Set(key, value ? bool.TrueString : bool.FalseString);

        private static void SetStringOrClear(string key, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                SherpaONNXEnvironment.Remove(key);
                return;
            }

            SherpaONNXEnvironment.Set(key, value.Trim());
        }
    }

    internal static class SherpaONNXRuntimeSettingsBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
        private static void ApplyRuntimeDefaults() => Apply();

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        private static void ApplyEditorDefaults() => Apply();
#endif

        private static void Apply()
        {
            var asset = SherpaONNXRuntimeSettings.LoadFromResources();
            asset?.ApplyEnvironmentDefaults();
        }
    }
}
