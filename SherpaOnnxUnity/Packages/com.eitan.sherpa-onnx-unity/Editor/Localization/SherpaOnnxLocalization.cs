#if UNITY_EDITOR

namespace Eitan.SherpaOnnxUnity.Editor.Localization
{
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// Localization entry point for all Sherpa ONNX editor tooling.
    /// Resolves strings from JSON tables stored under Editor/Resources.
    /// </summary>
    internal static class SherpaOnnxLocalization
    {
        private const string ResourceFolder = "SherpaOnnxLocalization";

        private static readonly Dictionary<SherpaOnnxEditorLanguage, Dictionary<string, string>> Cache = new();
        private static SherpaOnnxEditorLanguage _lastBroadcastLanguage;

        static SherpaOnnxLocalization()
        {
            _lastBroadcastLanguage = ResolveEffectiveLanguage();
            EditorApplication.update += PollAutoLanguage;
            AssemblyReloadEvents.beforeAssemblyReload += HandleBeforeDomainReload;
        }

        public static event Action LanguageChanged;

        internal static SherpaOnnxEditorLanguage PreferredLanguage => Preferences.Language;

        internal static SherpaOnnxEditorLanguage EffectiveLanguage => ResolveEffectiveLanguage();

        internal static void SetLanguage(SherpaOnnxEditorLanguage language)
        {
            if (Preferences.Language == language)
            {
                return;
            }

            Preferences.Language = language;
            NotifyLanguageChanged();
        }

        internal static string Tr(string key, string fallback = null)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return fallback ?? string.Empty;
            }

            var language = ResolveEffectiveLanguage();
            if (TryGetEntry(language, key, out var value))
            {
                return value;
            }

            if (language != SherpaOnnxEditorLanguage.English &&
                TryGetEntry(SherpaOnnxEditorLanguage.English, key, out value))
            {
                return value;
            }

            return fallback ?? key;
        }

        internal static string GetLanguageDisplayName(SherpaOnnxEditorLanguage language)
        {
            return language switch
            {
                SherpaOnnxEditorLanguage.Auto => Tr("editor.language.auto", "Auto"),
                SherpaOnnxEditorLanguage.ChineseSimplified => Tr("editor.language.zhHans", "简体中文"),
                _ => Tr("editor.language.en", "English"),
            };
        }

        private static SherpaOnnxLocalizationPreferences Preferences => SherpaOnnxLocalizationPreferences.instance;

        private static void PollAutoLanguage()
        {
            if (Preferences.Language != SherpaOnnxEditorLanguage.Auto)
            {
                return;
            }

            var detected = DetectEditorLanguage();
            if (detected == _lastBroadcastLanguage)
            {
                return;
            }

            _lastBroadcastLanguage = detected;
            NotifyLanguageChanged();
        }

        private static void HandleBeforeDomainReload()
        {
            EditorApplication.update -= PollAutoLanguage;
            AssemblyReloadEvents.beforeAssemblyReload -= HandleBeforeDomainReload;
        }

        private static void NotifyLanguageChanged()
        {
            _lastBroadcastLanguage = ResolveEffectiveLanguage();
            Cache.Clear();
            LanguageChanged?.Invoke();
        }

        private static SherpaOnnxEditorLanguage ResolveEffectiveLanguage()
        {
            if (Preferences.Language != SherpaOnnxEditorLanguage.Auto)
            {
                return Preferences.Language;
            }

            return _lastBroadcastLanguage == default
                ? DetectEditorLanguage()
                : _lastBroadcastLanguage;
        }

        private static bool TryGetEntry(SherpaOnnxEditorLanguage language, string key, out string value)
        {
            var table = GetTable(language);
            if (table != null && table.TryGetValue(key, out value) && !string.IsNullOrEmpty(value))
            {
                return true;
            }

            value = null;
            return false;
        }

        private static Dictionary<string, string> GetTable(SherpaOnnxEditorLanguage language)
        {
            if (Cache.TryGetValue(language, out var cached))
            {
                return cached;
            }

            var loaded = LoadTable(language);
            Cache[language] = loaded;
            return loaded;
        }

        private static Dictionary<string, string> LoadTable(SherpaOnnxEditorLanguage language)
        {
            var resourceId = $"{ResourceFolder}/{GetLanguageCode(language)}";
            var asset = Resources.Load<TextAsset>(resourceId);
            if (asset == null)
            {
                Debug.LogWarning($"SherpaOnnx localization resource '{resourceId}.json' could not be found. Falling back to English.");
                return null;
            }

            return SherpaOnnxLocalizationTable.Parse(asset.text);
        }

        private static string GetLanguageCode(SherpaOnnxEditorLanguage language) =>
            language switch
            {
                SherpaOnnxEditorLanguage.ChineseSimplified => "zh-Hans",
                _ => "en",
            };

        private static SherpaOnnxEditorLanguage DetectEditorLanguage()
        {
            var editorLanguage = ResolveEditorLanguageName();
            if (editorLanguage.IndexOf("Chinese", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return SherpaOnnxEditorLanguage.ChineseSimplified;
            }

            return SherpaOnnxEditorLanguage.English;
        }

        private static string ResolveEditorLanguageName()
        {
            const string defaultLanguage = "English";
            try
            {
                var type = Type.GetType("UnityEditor.LocalizationDatabase, UnityEditor");
                var property = type?.GetProperty(
                    "currentEditorLanguage",
                    BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
                if (property != null && property.GetValue(null) is string value && !string.IsNullOrEmpty(value))
                {
                    return value;
                }
            }
            catch
            {
                // Ignore reflection failures and fall back to defaults.
            }

            return defaultLanguage;
        }
    }
}

#endif
