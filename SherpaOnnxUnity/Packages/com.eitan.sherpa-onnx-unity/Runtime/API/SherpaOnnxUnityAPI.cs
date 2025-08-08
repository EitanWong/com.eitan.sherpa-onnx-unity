

// File: Packages/com.eitan.sherpa-onnx-unity/Runtime/API/SherpaOnnxUnityAPI.cs
#nullable enable
using System;
using Eitan.SherpaOnnxUnity.Runtime; // For SherpaOnnxEnvironment

/// <summary>
/// Thin, user-friendly facade for common Sherpa ONNX settings.
/// Keep this API tiny and stable so developers have a simple entrypoint.
/// </summary>
public static class SherpaOnnxUnityAPI
{
    /// <summary>
    /// Set a GitHub download acceleration proxy. Examples:
    /// "https://ghfast.sourcegcdn.com/" or "https://mirror.ghproxy.com/".
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

        SherpaOnnxEnvironment.Set(SherpaOnnxEnvironment.BuiltinKeys.GithubProxy, proxy);
    }

    /// <summary>Remove the configured GitHub proxy, if any.</summary>
    public static void ClearGithubProxy()
    {
        SherpaOnnxEnvironment.Remove(SherpaOnnxEnvironment.BuiltinKeys.GithubProxy);
    }

}