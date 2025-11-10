using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Eitan.SherpaOnnxUnity.Runtime
{
    /// <summary>
    /// ScriptableObject that stores default environment values for SherpaOnnx.
    /// Serialized under Resources so the data ships with builds and can be read very early.
    /// </summary>
    public sealed class SherpaOnnxRuntimeSettings : ScriptableObject
    {
        public const string ResourceName = "SherpaOnnxRuntimeSettings";
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

        internal static SherpaOnnxRuntimeSettings LoadFromResources()
        {
            // Fast path: default root-level asset.
            var direct = Resources.Load<SherpaOnnxRuntimeSettings>(ResourceName);
            if (direct != null)
            {
                return direct;
            }

            var discovered = Resources.LoadAll<SherpaOnnxRuntimeSettings>(string.Empty);
            if (discovered == null || discovered.Length == 0)
            {
                return null;
            }

            var valid = new List<SherpaOnnxRuntimeSettings>(discovered.Length);
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
                Debug.LogError($"Multiple {nameof(SherpaOnnxRuntimeSettings)} assets detected under Resources. Please keep only one asset to avoid ambiguity.");
            }

            valid.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            return valid[0];
        }

        internal void ApplyEnvironmentDefaults()
        {
            SetBool(SherpaOnnxEnvironment.BuiltinKeys.FetchLatestManifest, _fetchLatestManifest);
            SetBool(SherpaOnnxEnvironment.BuiltinKeys.AutoDownloadModels, _autoDownloadModels);
            SetStringOrClear(
                SherpaOnnxEnvironment.BuiltinKeys.ChecksumCacheDirectory,
                _checksumCacheDirectory);

            var ttl = Mathf.Max(0, _checksumCacheTtlSeconds);
            SherpaOnnxEnvironment.Set(
                SherpaOnnxEnvironment.BuiltinKeys.ChecksumCacheTtlSeconds,
                ttl.ToString(CultureInfo.InvariantCulture));
        }

        private static void SetBool(string key, bool value) =>
            SherpaOnnxEnvironment.Set(key, value ? bool.TrueString : bool.FalseString);

        private static void SetStringOrClear(string key, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                SherpaOnnxEnvironment.Remove(key);
                return;
            }

            SherpaOnnxEnvironment.Set(key, value.Trim());
        }
    }

    internal static class SherpaOnnxRuntimeSettingsBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
        private static void ApplyRuntimeDefaults() => Apply();

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        private static void ApplyEditorDefaults() => Apply();
#endif

        private static void Apply()
        {
            var asset = SherpaOnnxRuntimeSettings.LoadFromResources();
            asset?.ApplyEnvironmentDefaults();
        }
    }
}
