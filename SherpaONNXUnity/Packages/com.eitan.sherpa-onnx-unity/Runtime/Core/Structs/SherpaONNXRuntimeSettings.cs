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
        internal const string AutoDeleteCorruptedModelsPropertyName = nameof(_autoDeleteCorruptedModels);
        internal const string GithubProxyUrlPropertyName = nameof(_githubProxyUrl);
        internal const string ChecksumCacheDirectoryPropertyName = nameof(_checksumCacheDirectory);
        internal const string ChecksumCacheTtlSecondsPropertyName = nameof(_checksumCacheTtlSeconds);
        internal const string LoggingEnabledPropertyName = nameof(_loggingEnabled);
        internal const string LoggingLevelPropertyName = nameof(_loggingLevel);
        internal const string LoggingTraceStacksPropertyName = nameof(_traceWithStacks);
        internal const string GithubProxyEnvironmentVariable = "SHERPA_ONNX_GITHUB_PROXY";
        internal const string FetchLatestManifestEnvironmentVariable = "SHERPA_ONNX_FETCH_LATEST_MANIFEST";
        internal const string AutoDownloadModelsEnvironmentVariable = "SHERPA_ONNX_AUTO_DOWNLOAD";
        internal const string AutoDeleteCorruptedModelsEnvironmentVariable = "SHERPA_ONNX_AUTO_DELETE_CORRUPTED_MODELS";
        internal const string ChecksumCacheDirectoryEnvironmentVariable = "SHERPA_ONNX_CHECKSUM_CACHE_DIR";
        internal const string ChecksumCacheTtlSecondsEnvironmentVariable = "SHERPA_ONNX_CHECKSUM_CACHE_TTL_SECONDS";
        internal const string LoggingEnabledEnvironmentVariable = "SHERPA_ONNX_LOGGING_ENABLED";
        internal const string LoggingLevelEnvironmentVariable = "SHERPA_ONNX_LOGGING_LEVEL";
        internal const string LoggingTraceStacksEnvironmentVariable = "SHERPA_ONNX_LOGGING_TRACE_STACKS";

        [SerializeField]
        [Tooltip("When enabled (default), the manifest download routine will always try to fetch the latest checksum.txt list.")]
        private bool _fetchLatestManifest = true;

        [SerializeField]
        [Tooltip("When disabled, the prepare pipeline skips remote downloads and expects models to exist locally.")]
        private bool _autoDownloadModels = true;

        [SerializeField]
        [Tooltip("When enabled (default), corrupted model artifacts are deleted after initialization or verification failures.")]
        private bool _autoDeleteCorruptedModels = true;

        [SerializeField]
        [Tooltip("Optional proxy (e.g., https://ghfast.top/) prepended to github.com downloads. Environment variable SHERPA_ONNX_GITHUB_PROXY takes priority.")]
        private string _githubProxyUrl = string.Empty;

        [SerializeField]
        [Tooltip("Optional absolute path for checksum.txt caching. Leave empty to use the platform-specific temp folder.")]
        private string _checksumCacheDirectory = string.Empty;

        [SerializeField]
        [Tooltip("Cache lifetime for fetched checksum.txt content, in seconds. Use 0 to disable caching entirely.")]
        private int _checksumCacheTtlSeconds = 3600;

        [SerializeField]
        [Tooltip("Master switch for SherpaONNX logging output (runtime and editor play mode).")]
        private bool _loggingEnabled = false;

        [SerializeField]
        [Tooltip("Minimum log level to emit. Trace will include detailed call stacks for initialization and model calls.")]
        private SherpaLogLevel _loggingLevel = SherpaLogLevel.Info;

        [SerializeField]
        [Tooltip("When enabled, Trace level entries include managed call stacks for every log message.")]
        private bool _traceWithStacks = true;

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
                SherpaLog.Error($"Multiple {nameof(SherpaONNXRuntimeSettings)} assets detected under Resources. Please keep only one asset to avoid ambiguity.", category: "Settings");
            }

            valid.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            return valid[0];
        }

        internal void ApplyEnvironmentDefaults()
        {
            SetBool(SherpaONNXEnvironment.BuiltinKeys.FetchLatestManifest, _fetchLatestManifest);
            SetBool(SherpaONNXEnvironment.BuiltinKeys.AutoDownloadModels, _autoDownloadModels);
            SetBool(SherpaONNXEnvironment.BuiltinKeys.AutoDeleteCorruptedModels, _autoDeleteCorruptedModels);
            ApplyGithubProxyValue(ResolveProxyValue(_githubProxyUrl));
            SetStringOrClear(
                SherpaONNXEnvironment.BuiltinKeys.ChecksumCacheDirectory,
                _checksumCacheDirectory);

            var ttl = Mathf.Max(0, _checksumCacheTtlSeconds);
            SherpaONNXEnvironment.Set(
                SherpaONNXEnvironment.BuiltinKeys.ChecksumCacheTtlSeconds,
                ttl.ToString(CultureInfo.InvariantCulture));

            SetBool(SherpaONNXEnvironment.BuiltinKeys.LoggingEnabled, _loggingEnabled);
            SherpaONNXEnvironment.Set(
                SherpaONNXEnvironment.BuiltinKeys.LoggingLevel,
                _loggingLevel.ToString());
            SetBool(SherpaONNXEnvironment.BuiltinKeys.LoggingTraceStacks, _traceWithStacks);

            SherpaLog.Configure(_loggingLevel, _loggingEnabled, _traceWithStacks);
        }

        internal static void ApplyEnvironmentOverridesFromProcess()
        {
            ApplyBoolEnvironmentOverride(
                FetchLatestManifestEnvironmentVariable,
                SherpaONNXEnvironment.BuiltinKeys.FetchLatestManifest);
            ApplyBoolEnvironmentOverride(
                AutoDownloadModelsEnvironmentVariable,
                SherpaONNXEnvironment.BuiltinKeys.AutoDownloadModels);
            ApplyBoolEnvironmentOverride(
                AutoDeleteCorruptedModelsEnvironmentVariable,
                SherpaONNXEnvironment.BuiltinKeys.AutoDeleteCorruptedModels);
            ApplyBoolEnvironmentOverride(
                LoggingEnabledEnvironmentVariable,
                SherpaONNXEnvironment.BuiltinKeys.LoggingEnabled);
            ApplyBoolEnvironmentOverride(
                LoggingTraceStacksEnvironmentVariable,
                SherpaONNXEnvironment.BuiltinKeys.LoggingTraceStacks);

            ApplyStringEnvironmentOverride(
                LoggingLevelEnvironmentVariable,
                SherpaONNXEnvironment.BuiltinKeys.LoggingLevel);

            ApplyIntEnvironmentOverride(
                ChecksumCacheTtlSecondsEnvironmentVariable,
                SherpaONNXEnvironment.BuiltinKeys.ChecksumCacheTtlSeconds,
                minimum: 0);

            ApplyStringEnvironmentOverride(
                ChecksumCacheDirectoryEnvironmentVariable,
                SherpaONNXEnvironment.BuiltinKeys.ChecksumCacheDirectory,
                trim: true,
                clearWhenEmpty: true);

            var proxyValue = Environment.GetEnvironmentVariable(GithubProxyEnvironmentVariable);
            if (!string.IsNullOrWhiteSpace(proxyValue))
            {
                ApplyGithubProxyValue(proxyValue);
            }
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

        private static void ApplyBoolEnvironmentOverride(string envKey, string targetKey)
        {
            var raw = Environment.GetEnvironmentVariable(envKey);
            if (string.IsNullOrWhiteSpace(raw))
            {
                return;
            }

            if (TryParseBool(raw, out var value))
            {
                SetBool(targetKey, value);
            }
        }

        private static void ApplyIntEnvironmentOverride(string envKey, string targetKey, int minimum = int.MinValue)
        {
            var raw = Environment.GetEnvironmentVariable(envKey);
            if (string.IsNullOrWhiteSpace(raw))
            {
                return;
            }

            if (int.TryParse(raw.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var value))
            {
                if (value < minimum)
                {
                    value = minimum;
                }

                SherpaONNXEnvironment.Set(targetKey, value.ToString(CultureInfo.InvariantCulture));
            }
        }

        private static void ApplyStringEnvironmentOverride(
            string envKey,
            string targetKey,
            bool trim = true,
            bool clearWhenEmpty = false)
        {
            var raw = Environment.GetEnvironmentVariable(envKey);
            if (raw == null)
            {
                return;
            }

            var value = trim ? raw.Trim() : raw;
            if (string.IsNullOrEmpty(value) && clearWhenEmpty)
            {
                SherpaONNXEnvironment.Remove(targetKey);
                return;
            }

            if (!string.IsNullOrEmpty(value))
            {
                SherpaONNXEnvironment.Set(targetKey, value);
            }
        }

        private static bool TryParseBool(string raw, out bool value)
        {
            if (bool.TryParse(raw, out value))
            {
                return true;
            }

            switch (raw.Trim().ToLowerInvariant())
            {
                case "1":
                case "yes":
                case "y":
                case "on":
                    value = true;
                    return true;
                case "0":
                case "no":
                case "n":
                case "off":
                    value = false;
                    return true;
                default:
                    value = false;
                    return false;
            }
        }

        internal static string ResolveProxyValue(string serializedValue)
        {
            var env = Environment.GetEnvironmentVariable(GithubProxyEnvironmentVariable);
            if (!string.IsNullOrWhiteSpace(env))
            {
                return env;
            }

            return serializedValue;
        }

        internal static void ApplyGithubProxyValue(string proxyValue)
        {
            var normalized = NormalizeProxy(proxyValue);
            if (string.IsNullOrEmpty(normalized))
            {
                SherpaONNXEnvironment.Remove(SherpaONNXEnvironment.BuiltinKeys.GithubProxy);
                return;
            }

            SherpaONNXEnvironment.Set(SherpaONNXEnvironment.BuiltinKeys.GithubProxy, normalized);
        }

        private static string NormalizeProxy(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return string.Empty;
            }

            var value = raw.Trim();
            if (!value.EndsWith("/", StringComparison.Ordinal))
            {
                value += "/";
            }

            return value;
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
            if (asset != null)
            {
                asset.ApplyEnvironmentDefaults();
            }
            else
            {
                // Allow environment-only configuration even when no asset exists yet.
                SherpaONNXRuntimeSettings.ApplyGithubProxyValue(SherpaONNXRuntimeSettings.ResolveProxyValue(string.Empty));
            }

            SherpaONNXRuntimeSettings.ApplyEnvironmentOverridesFromProcess();
            // Always honor environment overrides for logging even when no asset exists.
            SherpaLog.ConfigureFromEnvironment();
        }
    }
}
