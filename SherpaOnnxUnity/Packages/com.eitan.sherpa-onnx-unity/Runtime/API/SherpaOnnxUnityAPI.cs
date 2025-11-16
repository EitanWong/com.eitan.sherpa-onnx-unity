

// File: Packages/com.eitan.sherpa-onnx-unity/Runtime/API/SherpaONNXUnityAPI.cs
#nullable enable
using System;
using System.Linq;
using System.Threading.Tasks;
using Eitan.SherpaONNXUnity.Runtime;
using Eitan.SherpaONNXUnity.Runtime.Constants;
using Eitan.SherpaONNXUnity.Runtime.Core.Utilities;


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
}
