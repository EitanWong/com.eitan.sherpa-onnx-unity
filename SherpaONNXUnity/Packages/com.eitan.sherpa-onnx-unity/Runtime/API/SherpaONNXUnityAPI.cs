

// File: Packages/com.eitan.sherpa-onnx-unity/Runtime/API/SherpaONNXUnityAPI.cs
#nullable enable
using System;
using System.Linq;
using System.Threading.Tasks;
using Eitan.SherpaONNXUnity.Runtime;
using Eitan.SherpaONNXUnity.Runtime.Constants;
using Eitan.SherpaONNXUnity.Runtime.Native;
using Eitan.SherpaONNXUnity.Runtime.Utilities;


/// <summary>
/// Thin, user-friendly facade for common SherpaONNX settings.
/// Keep this API tiny and stable so developers have a simple entrypoint.
/// </summary>
public static class SherpaONNXUnityAPI
{
    /// <summary>
    /// Set a GitHub download acceleration proxy. Examples:
    /// "https://ghfast.top".
    /// Pass null or empty to clear.
    /// </summary>
    public static void SetGithubProxy(string? proxy)
    {
        proxy = proxy?.Trim();
        if (string.IsNullOrEmpty(proxy))
        {
            ClearGithubProxy();
            return;
        }

        // Normalize to end with a single slash for safe joining later.
        if (!proxy.EndsWith("/", StringComparison.Ordinal))
        { proxy += "/"; }

        SherpaONNXEnvironment.Set(SherpaONNXEnvironment.BuiltinKeys.GithubProxy, proxy);
    }

    /// <summary>Remove the configured GitHub proxy, if any.</summary>
    public static void ClearGithubProxy()
    {
        SherpaONNXEnvironment.Remove(SherpaONNXEnvironment.BuiltinKeys.GithubProxy);
    }

    public static async Task<string[]> GetModelIDByTypeAsync(SherpaONNXModuleType type)
    {
        var manifest = await SherpaONNXModelRegistry.Instance.GetManifestAsync();
        return manifest.Filter(m => m.moduleType == type).Select(m => m.modelId).ToArray();
    }

    public static bool IsOnlineModel(string modelID)
    {
        return SherpaUtils.Model.IsOnlineModel(modelID);
    }

    /// <summary>
    /// Delete downloaded checksum.txt cache files to force the next lookup to re-fetch manifests.
    /// </summary>
    public static SherpaChecksumCacheClearResult ClearChecksumCache()
    {
        return SherpaONNXConstants.ClearChecksumCache();
    }

    /// <summary>
    /// Enable or disable automatic model downloads (can also be set via SHERPA_ONNX_AUTO_DOWNLOAD).
    /// When disabled, developers must pre-seed models locally.
    /// </summary>
    public static void SetAutoDownloadModels(bool enabled)
    {
        SherpaONNXEnvironment.Set(SherpaONNXEnvironment.BuiltinKeys.AutoDownloadModels, enabled ? bool.TrueString : bool.FalseString);
    }

    /// <summary>
    /// Returns whether automatic model downloads are enabled.
    /// </summary>
    public static bool GetAutoDownloadModels()
    {
        return SherpaONNXEnvironment.GetBool(SherpaONNXEnvironment.BuiltinKeys.AutoDownloadModels, @default: true);
    }

    /// <summary>
    /// Controls if the latest checksum manifest should always be fetched (SHERPA_ONNX_FETCH_LATEST_MANIFEST).
    /// </summary>
    public static void SetFetchLatestManifest(bool enabled)
    {
        SherpaONNXEnvironment.Set(SherpaONNXEnvironment.BuiltinKeys.FetchLatestManifest, enabled ? bool.TrueString : bool.FalseString);
    }

    /// <summary>
    /// Returns whether automatic manifest refresh is enabled.
    /// </summary>
    public static bool GetFetchLatestManifest()
    {
        return SherpaONNXEnvironment.GetBool(SherpaONNXEnvironment.BuiltinKeys.FetchLatestManifest, @default: true);
    }

    /// <summary>
    /// Overrides the checksum cache directory. Pass null or empty to revert to the default temp location.
    /// </summary>
    public static void SetChecksumCacheDirectory(string? absolutePath)
    {
        var key = SherpaONNXEnvironment.BuiltinKeys.ChecksumCacheDirectory;
        if (string.IsNullOrWhiteSpace(absolutePath))
        {
            SherpaONNXEnvironment.Remove(key);
            return;
        }

        SherpaONNXEnvironment.Set(key, absolutePath.Trim());
    }

    /// <summary>
    /// Returns the configured checksum cache directory (empty string when default).
    /// </summary>
    public static string GetChecksumCacheDirectory()
    {
        return SherpaONNXEnvironment.Get(SherpaONNXEnvironment.BuiltinKeys.ChecksumCacheDirectory);
    }

    /// <summary>
    /// Sets the checksum cache lifetime (seconds). Values below zero are clamped to zero.
    /// </summary>
    public static void SetChecksumCacheTtlSeconds(int seconds)
    {
        var clamped = Math.Max(0, seconds);
        SherpaONNXEnvironment.Set(SherpaONNXEnvironment.BuiltinKeys.ChecksumCacheTtlSeconds, clamped);
    }

    /// <summary>
    /// Returns the current checksum cache lifetime in seconds.
    /// </summary>
    public static int GetChecksumCacheTtlSeconds()
    {
        return SherpaONNXEnvironment.GetInt(SherpaONNXEnvironment.BuiltinKeys.ChecksumCacheTtlSeconds, @default: 3600);
    }

    public static string SherpaONNXLibVersion => VersionInfo.Version;
    public static string SherpaONNXLibGitDate => VersionInfo.GitDate;
    public static string SherpaONNXLibGitSha1 => VersionInfo.GitSha1;

}
