#if UNITY_EDITOR

namespace Eitan.SherpaONNXUnity.Editor.Localization
{
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using Eitan.SherpaONNXUnity.Runtime;
    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// Localization entry point for all SherpaONNX editor tooling.
    /// Resolves strings from JSON tables stored under Editor/Resources.
    /// </summary>
    internal static class SherpaONNXLocalization
    {
        private const string ResourceFolder = "SherpaONNXLocalization";

        private static readonly Dictionary<SherpaONNXEditorLanguage, Dictionary<string, string>> Cache = new();
        private static SherpaONNXEditorLanguage _lastBroadcastLanguage;

        static SherpaONNXLocalization()
        {
            _lastBroadcastLanguage = ResolveEffectiveLanguage();
            EditorApplication.update += PollAutoLanguage;
            AssemblyReloadEvents.beforeAssemblyReload += HandleBeforeDomainReload;
        }

        public static event Action LanguageChanged;

        internal static SherpaONNXEditorLanguage PreferredLanguage => Preferences.Language;

        internal static SherpaONNXEditorLanguage EffectiveLanguage => ResolveEffectiveLanguage();

        internal static void SetLanguage(SherpaONNXEditorLanguage language)
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

            if (language != SherpaONNXEditorLanguage.English &&
                TryGetEntry(SherpaONNXEditorLanguage.English, key, out value))
            {
                return value;
            }

            return fallback ?? key;
        }

        internal static string GetLanguageDisplayName(SherpaONNXEditorLanguage language)
        {
            return language switch
            {
                SherpaONNXEditorLanguage.Auto => Tr("editor.language.auto", "Auto"),
                SherpaONNXEditorLanguage.ChineseSimplified => Tr("editor.language.zhHans", "简体中文"),
                _ => Tr("editor.language.en", "English"),
            };
        }

        private static SherpaONNXLocalizationPreferences Preferences => SherpaONNXLocalizationPreferences.instance;

        private static void PollAutoLanguage()
        {
            if (Preferences.Language != SherpaONNXEditorLanguage.Auto)
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

        private static SherpaONNXEditorLanguage ResolveEffectiveLanguage()
        {
            if (Preferences.Language != SherpaONNXEditorLanguage.Auto)
            {
                return Preferences.Language;
            }

            return _lastBroadcastLanguage == default
                ? DetectEditorLanguage()
                : _lastBroadcastLanguage;
        }

        private static bool TryGetEntry(SherpaONNXEditorLanguage language, string key, out string value)
        {
            var table = GetTable(language);
            if (table != null && table.TryGetValue(key, out value) && !string.IsNullOrEmpty(value))
            {
                return true;
            }

            value = null;
            return false;
        }

        private static Dictionary<string, string> GetTable(SherpaONNXEditorLanguage language)
        {
            if (Cache.TryGetValue(language, out var cached))
            {
                return cached;
            }

            var loaded = LoadTable(language);
            Cache[language] = loaded;
            return loaded;
        }

        private static Dictionary<string, string> LoadTable(SherpaONNXEditorLanguage language)
        {
            var resourceId = $"{ResourceFolder}/{GetLanguageCode(language)}";
            var asset = Resources.Load<TextAsset>(resourceId);
            if (asset == null)
            {
                SherpaLog.Warning($"SherpaONNX localization resource '{resourceId}.json' could not be found. Falling back to English.", category: "Localization");
                return null;
            }

            return SherpaONNXLocalizationTable.Parse(asset.text);
        }

        private static string GetLanguageCode(SherpaONNXEditorLanguage language) =>
            language switch
            {
                SherpaONNXEditorLanguage.ChineseSimplified => "zh-Hans",
                _ => "en",
            };

        private static SherpaONNXEditorLanguage DetectEditorLanguage()
        {
            var editorLanguage = ResolveEditorLanguageName();
            if (editorLanguage.IndexOf("Chinese", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return SherpaONNXEditorLanguage.ChineseSimplified;
            }

            return SherpaONNXEditorLanguage.English;
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

                if (property != null)
                {
                    SystemLanguage language = (SystemLanguage)property.GetValue(null);
                    return language.ToString();
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
