using System;
using System.Collections.Generic;
using System.Linq;
using Eitan.SherpaONNXUnity.Runtime.Utilities;
using UnityEngine;

namespace Eitan.SherpaONNXUnity.Runtime
{
    internal sealed class SherpaONNXRuntimeSettingsSnapshot
    {
        public static readonly SherpaONNXRuntimeSettingsSnapshot Default = new SherpaONNXRuntimeSettingsSnapshot();

        public bool FetchLatestManifest { get; set; } = true;
        public bool AutoDownloadModels { get; set; } = true;
        public bool AutoDeleteCorruptedModels { get; set; } = true;
        public int DownloadAttemptTimeoutSeconds { get; set; } = 600;
        public bool AllowInsecureModelDownload { get; set; }
        public bool ForceModelHashValidation { get; set; }
        public string GithubProxyUrl { get; set; } = string.Empty;
        public string ChecksumCacheDirectory { get; set; } = string.Empty;
        public int ChecksumCacheTtlSeconds { get; set; } = 3600;
        public bool LoggingEnabled { get; set; }
        public SherpaLogLevel LoggingLevel { get; set; } = SherpaLogLevel.Info;
        public bool TraceWithStacks { get; set; } = true;

        public void ApplyEnvironmentDefaults()
        {
            SherpaONNXEnvironment.Set(
                SherpaONNXEnvironment.BuiltinKeys.FetchLatestManifest,
                FetchLatestManifest.ToString().ToLowerInvariant());
            SherpaONNXEnvironment.Set(
                SherpaONNXEnvironment.BuiltinKeys.AutoDownloadModels,
                AutoDownloadModels.ToString().ToLowerInvariant());
            SherpaONNXEnvironment.Set(
                SherpaONNXEnvironment.BuiltinKeys.AutoDeleteCorruptedModels,
                AutoDeleteCorruptedModels.ToString().ToLowerInvariant());
            SherpaONNXEnvironment.Set(
                SherpaONNXEnvironment.BuiltinKeys.DownloadAttemptTimeoutSeconds,
                Mathf.Max(0, DownloadAttemptTimeoutSeconds).ToString(System.Globalization.CultureInfo.InvariantCulture));
            SherpaONNXEnvironment.Set(
                SherpaONNXEnvironment.BuiltinKeys.AllowInsecureModelDownload,
                AllowInsecureModelDownload.ToString().ToLowerInvariant());
            SherpaONNXEnvironment.Set(
                SherpaONNXEnvironment.BuiltinKeys.ForceModelHashValidation,
                ForceModelHashValidation.ToString().ToLowerInvariant());
            SherpaONNXRuntimeSettings.ApplyGithubProxyValue(SherpaONNXRuntimeSettings.ResolveProxyValue(GithubProxyUrl));
            if (string.IsNullOrWhiteSpace(ChecksumCacheDirectory))
            {
                SherpaONNXEnvironment.Remove(SherpaONNXEnvironment.BuiltinKeys.ChecksumCacheDirectory);
            }
            else
            {
                SherpaONNXEnvironment.Set(
                    SherpaONNXEnvironment.BuiltinKeys.ChecksumCacheDirectory,
                    ChecksumCacheDirectory.Trim());
            }

            SherpaONNXEnvironment.Set(
                SherpaONNXEnvironment.BuiltinKeys.ChecksumCacheTtlSeconds,
                Mathf.Max(0, ChecksumCacheTtlSeconds).ToString(System.Globalization.CultureInfo.InvariantCulture));
            SherpaONNXEnvironment.Set(
                SherpaONNXEnvironment.BuiltinKeys.LoggingEnabled,
                LoggingEnabled.ToString().ToLowerInvariant());
            SherpaONNXEnvironment.Set(
                SherpaONNXEnvironment.BuiltinKeys.LoggingLevel,
                LoggingLevel.ToString());
            SherpaONNXEnvironment.Set(
                SherpaONNXEnvironment.BuiltinKeys.LoggingTraceStacks,
                TraceWithStacks.ToString().ToLowerInvariant());

            SherpaLog.Configure(LoggingLevel, LoggingEnabled, TraceWithStacks);
        }
    }

    internal sealed class SherpaONNXCustomModelCatalogSnapshot
    {
        public static readonly SherpaONNXCustomModelCatalogSnapshot Empty = new SherpaONNXCustomModelCatalogSnapshot(
            Array.Empty<SherpaONNXModelMetadata>(),
            Array.Empty<string>());

        public SherpaONNXCustomModelCatalogSnapshot(
            IReadOnlyList<SherpaONNXModelMetadata> localModels,
            IReadOnlyList<string> remoteManifestUrls)
        {
            LocalModels = localModels ?? Array.Empty<SherpaONNXModelMetadata>();
            RemoteManifestUrls = remoteManifestUrls ?? Array.Empty<string>();
        }

        public IReadOnlyList<SherpaONNXModelMetadata> LocalModels { get; }
        public IReadOnlyList<string> RemoteManifestUrls { get; }
    }

    internal static class SherpaONNXRuntimeResourceProvider
    {
        private static readonly object CacheLock = new object();
        private static SherpaONNXRuntimeSettingsSnapshot s_runtimeSettings = SherpaONNXRuntimeSettingsSnapshot.Default;
        private static SherpaONNXCustomModelCatalogSnapshot s_customCatalog = SherpaONNXCustomModelCatalogSnapshot.Empty;
        private static bool s_preloaded;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            lock (CacheLock)
            {
                s_runtimeSettings = SherpaONNXRuntimeSettingsSnapshot.Default;
                s_customCatalog = SherpaONNXCustomModelCatalogSnapshot.Empty;
                s_preloaded = false;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
        private static void PreloadBeforeSplashScreen()
        {
            PreloadFromResources();
        }

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        private static void PreloadInEditor()
        {
            if (!Application.isPlaying)
            {
                PreloadFromResources();
            }
        }
#endif

        public static void PreloadFromResources()
        {
            lock (CacheLock)
            {
                if (s_preloaded)
                {
                    return;
                }

                s_runtimeSettings = BuildRuntimeSettingsSnapshot(SherpaONNXRuntimeSettings.LoadFromResources()) ?? SherpaONNXRuntimeSettingsSnapshot.Default;
                s_customCatalog = BuildCustomCatalogSnapshot(SherpaONNXCustomModelSettings.LoadFromResources()) ?? SherpaONNXCustomModelCatalogSnapshot.Empty;
                s_preloaded = true;
            }
        }

        public static SherpaONNXRuntimeSettingsSnapshot GetRuntimeSettingsSnapshot()
        {
            EnsurePreloaded();
            return s_runtimeSettings;
        }

        public static SherpaONNXCustomModelCatalogSnapshot GetCustomModelCatalogSnapshot()
        {
            EnsurePreloaded();
            return s_customCatalog;
        }

        private static void EnsurePreloaded()
        {
            if (s_preloaded)
            {
                return;
            }

            if (!UnityMainThreadScheduler.IsMainThread)
            {
                SherpaLog.Warning("Runtime resources were requested before main-thread preload completed. Falling back to defaults until preload runs.", category: "Settings");
                return;
            }

            PreloadFromResources();
        }

        private static SherpaONNXRuntimeSettingsSnapshot BuildRuntimeSettingsSnapshot(SherpaONNXRuntimeSettings settings)
        {
            if (settings == null)
            {
                return null;
            }

            return new SherpaONNXRuntimeSettingsSnapshot
            {
                FetchLatestManifest = settings.FetchLatestManifest,
                AutoDownloadModels = settings.AutoDownloadModels,
                AutoDeleteCorruptedModels = settings.AutoDeleteCorruptedModels,
                DownloadAttemptTimeoutSeconds = settings.DownloadAttemptTimeoutSeconds,
                AllowInsecureModelDownload = settings.AllowInsecureModelDownload,
                ForceModelHashValidation = settings.ForceModelHashValidation,
                GithubProxyUrl = settings.GithubProxyUrl ?? string.Empty,
                ChecksumCacheDirectory = settings.ChecksumCacheDirectory ?? string.Empty,
                ChecksumCacheTtlSeconds = settings.ChecksumCacheTtlSeconds,
                LoggingEnabled = settings.LoggingEnabled,
                LoggingLevel = settings.LoggingLevel,
                TraceWithStacks = settings.TraceWithStacks
            };
        }

        private static SherpaONNXCustomModelCatalogSnapshot BuildCustomCatalogSnapshot(SherpaONNXCustomModelSettings settings)
        {
            if (settings == null)
            {
                return null;
            }

            var localModels = settings.EnumerateEnabledModels()
                .Where(metadata => metadata != null && !string.IsNullOrWhiteSpace(metadata.modelId))
                .Select(CloneAndNormalizeMetadata)
                .Where(metadata => metadata != null)
                .ToArray();

            var remoteManifestUrls = settings.EnumerateEnabledRemoteManifestUrls()
                .Where(url => !string.IsNullOrWhiteSpace(url))
                .Select(url => url.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            return new SherpaONNXCustomModelCatalogSnapshot(localModels, remoteManifestUrls);
        }

        private static SherpaONNXModelMetadata CloneAndNormalizeMetadata(SherpaONNXModelMetadata metadata)
        {
            if (metadata == null)
            {
                return null;
            }

            var clone = new SherpaONNXModelMetadata
            {
                modelId = metadata.modelId?.Trim(),
                moduleType = metadata.moduleType,
                moduleTypeHint = metadata.moduleTypeHint?.Trim(),
                downloadUrl = metadata.downloadUrl?.Trim(),
                downloadFileHash = metadata.downloadFileHash?.Trim(),
                modelTypeHint = metadata.modelTypeHint?.Trim(),
                numberOfSpeakers = metadata.numberOfSpeakers,
                sampleRate = metadata.sampleRate
            };

            if (metadata.fileBindings != null)
            {
                foreach (var binding in metadata.fileBindings)
                {
                    if (binding == null || binding.key == SherpaONNXModelFileKey.None || string.IsNullOrWhiteSpace(binding.path))
                    {
                        continue;
                    }

                    clone.fileBindings.Add(new SherpaONNXModelFileBinding
                    {
                        key = binding.key,
                        path = binding.path.Trim()
                    });
                }
            }

            return string.IsNullOrWhiteSpace(clone.modelId) ? null : clone;
        }
    }
}
