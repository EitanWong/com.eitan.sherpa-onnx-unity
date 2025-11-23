// Assets/Editor/BuildVersionSanitizer.cs
#if UNITY_EDITOR
using System;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

/// <summary>
/// Cross‑platform version sanitizer for Unity builds.
/// - Normalizes PlayerSettings.bundleVersion (short version) for ALL platforms to a numeric triplet (e.g., 1.2.3).
/// - Applies platform-specific build number rules:
///   * iOS / tvOS / macOS: CFBundleVersion must be numbers and dots (≤18 chars). Uses buildNumber.
///   * Android: ensures bundleVersionCode ≥ 1 and within Play limit.
/// - Optional CI overrides via env vars:
///   * UNITY_IOS_BUILD_NUMBER (also used for tvOS/macOS)
///   * UNITY_ANDROID_VERSION_CODE
/// </summary>
public class BuildVersionSanitizer : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;

    // Match first 1~3 numeric segments like 1 / 1.2 / 1.2.3 ; ignores pre-release or metadata
    private static readonly Regex ShortVerRegex = new Regex(@"^\d+(?:\.\d+){0,2}", RegexOptions.Compiled);

    // Apple build number: up to three numeric segments, numbers and dots only, ≤18 chars
    private static readonly Regex AppleBuildRegex = new Regex(@"^\d+(?:\.\d+){0,2}$", RegexOptions.Compiled);

    private const int AppleShortVerMaxLen = 18;
    private const int AndroidVersionCodeMax = 2100000000; // Play upper bound for 32-bit int

    public void OnPreprocessBuild(BuildReport report)
    {
        // 0) Normalize short version (bundleVersion) for ALL platforms
        var raw = PlayerSettings.bundleVersion; // may be semantic version like 1.0.0-preview.1
        var shortVer = SanitizeShortVersion(raw);
        PlayerSettings.bundleVersion = shortVer;

        // 1) Platform-specific adjustments
        switch (report.summary.platform)
        {
            case BuildTarget.iOS:
                SanitizeAppleBuildNumber("iOS");
                break;
            case BuildTarget.tvOS:
                SanitizeAppleBuildNumber("tvOS");
                break;
            case BuildTarget.StandaloneOSX:
                SanitizeAppleBuildNumber("macOS");
                break;
            case BuildTarget.Android:
                SanitizeAndroidVersionCode();
                break;
            default:
                // Other platforms (Windows/Linux/WebGL/etc.) generally accept bundleVersion as-is.
                break;
        }
    }

    // --- Helpers ---
    private static string SanitizeShortVersion(string raw)
    {
        var m = ShortVerRegex.Match(raw ?? string.Empty);
        var s = m.Success ? m.Value : "1.0.0";
        // Unity enforces for iOS/tvOS/macOS: numeric+dot, ≤ 18 chars; safe to clamp for all
        if (s.Length > AppleShortVerMaxLen)
        {
            s = s.Substring(0, AppleShortVerMaxLen);
        }

        if (s.EndsWith("."))
        {
            s = s.TrimEnd('.');
        }

        if (string.IsNullOrEmpty(s))
        {
            s = "1.0.0";
        }


        return s;
    }

    private static void SanitizeAppleBuildNumber(string nestedType)
    {
        // Allow CI to override
        var env = Environment.GetEnvironmentVariable("UNITY_IOS_BUILD_NUMBER");
        var current = GetAppleBuildNumber(nestedType);
        var candidate = !string.IsNullOrEmpty(env) ? env : current;

        if (string.IsNullOrEmpty(candidate) || !AppleBuildRegex.IsMatch(candidate) || candidate.Length > AppleShortVerMaxLen)
        {
            candidate = "1"; // default minimal valid build number
        }
        SetAppleBuildNumber(nestedType, candidate);
    }

    private static string GetAppleBuildNumber(string nestedType)
    {
        var t = typeof(PlayerSettings).GetNestedType(nestedType, BindingFlags.Public);
        var prop = t?.GetProperty("buildNumber", BindingFlags.Public | BindingFlags.Static);
        return prop?.GetValue(null) as string ?? string.Empty;
    }

    private static void SetAppleBuildNumber(string nestedType, string value)
    {
        var t = typeof(PlayerSettings).GetNestedType(nestedType, BindingFlags.Public);
        var prop = t?.GetProperty("buildNumber", BindingFlags.Public | BindingFlags.Static);
        prop?.SetValue(null, value);
    }

    private static void SanitizeAndroidVersionCode()
    {
        // Prefer CI-provided code if present
        var env = Environment.GetEnvironmentVariable("UNITY_ANDROID_VERSION_CODE");
        if (int.TryParse(env, out var envCode))
        {
            PlayerSettings.Android.bundleVersionCode = ClampAndroidVersionCode(envCode);
            return;
        }

        var code = PlayerSettings.Android.bundleVersionCode;
        if (code < 1)
        {
            code = 1;
        }
        PlayerSettings.Android.bundleVersionCode = ClampAndroidVersionCode(code);
    }

    private static int ClampAndroidVersionCode(int code)
    {
        if (code < 1)
        {
            code = 1;
        }


        if (code > AndroidVersionCodeMax)
        {
            code = AndroidVersionCodeMax;
        }


        return code;
    }
}
#endif
