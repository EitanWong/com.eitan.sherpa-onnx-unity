#if UNITY_EDITOR

namespace Eitan.SherpaONNXUnity.Editor
{


    using System;
    using System.Collections.Generic;
    using Eitan.SherpaONNXUnity.Editor.Localization;
    using Eitan.SherpaONNXUnity.Runtime;
    using UnityEditor;
    using UnityEditor.UIElements;
    using UnityEngine;
    using UnityEngine.UIElements;

    /// <summary>
    /// Project Settings UI: Edit ▸ Project Settings ▸ SHERPA ONNX
    /// </summary>
    internal sealed class SherpaONNXSettingsProvider : SettingsProvider
    {
        private const string kPath = "Project/SherpaONNX";
        private static readonly SherpaONNXEditorLanguage[] kLanguageOptions =
            (SherpaONNXEditorLanguage[])Enum.GetValues(typeof(SherpaONNXEditorLanguage));

        private SerializedObject _runtimeSettingsObject;
        private VisualElement _rootElement;
        private string _runtimeSettingsAssetPath = string.Empty;

        public SherpaONNXSettingsProvider() : base(kPath, SettingsScope.Project) { }

        [SettingsProvider]
        public static SettingsProvider Create() => new SherpaONNXSettingsProvider();

        public override void OnActivate(string searchContext, VisualElement rootElement)
        {
            _rootElement = rootElement;
            EnsureRuntimeSettingsObject();
            SherpaONNXLocalization.LanguageChanged += OnLanguageChanged;
            BuildUi();
        }

        public override void OnDeactivate()
        {
            SherpaONNXLocalization.LanguageChanged -= OnLanguageChanged;
            _rootElement = null;
            _runtimeSettingsObject = null;
        }

        private void BuildUi()
        {
            if (_rootElement == null)
            {
                return;
            }

            _rootElement.Clear();
            var settings = SherpaONNXBuildSettings.Instance;

            var title = new Label(SherpaONNXLocalization.Tr(
                SherpaONNXL10n.Settings.BuildTitle,
                "SherpaONNX Build Settings"));
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginTop = 6;
            title.style.marginBottom = 6;
            _rootElement.Add(title);

            _rootElement.Add(CreateLanguageField());

            var toggle = new Toggle(SherpaONNXLocalization.Tr(
                SherpaONNXL10n.Settings.IncludeModelsLabel,
                "Include downloaded models in desktop builds (Windows/macOS/Linux)"))
            {
                tooltip = SherpaONNXLocalization.Tr(
                    SherpaONNXL10n.Settings.IncludeModelsTooltip,
                    "If enabled, StreamingAssets/sherpa-onnx will be bundled into desktop builds. Default: OFF."),
                value = settings.IncludeModelsInDesktopBuild
            };
            toggle.RegisterValueChangedCallback(evt => settings.IncludeModelsInDesktopBuild = evt.newValue);
            _rootElement.Add(toggle);

            var includeHelp = new HelpBox(
                SherpaONNXLocalization.Tr(
                    SherpaONNXL10n.Settings.IncludeModelsHelp,
                    "OFF (default): Standalone builds skip StreamingAssets/sherpa-onnx for faster iterations.\n" +
                    "ON: include the folder for offline-ready builds.\n" +
                    "Mobile/WebGL/consoles remain excluded because StreamingAssets is read-only."),
                HelpBoxMessageType.Info);
            includeHelp.style.marginTop = 6;
            _rootElement.Add(includeHelp);

            var runtimeTitle = new Label(SherpaONNXLocalization.Tr(
                SherpaONNXL10n.Settings.RuntimeDefaultsTitle,
                "Runtime Environment Defaults"));
            runtimeTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            runtimeTitle.style.marginTop = 10;
            runtimeTitle.style.marginBottom = 4;
            _rootElement.Add(runtimeTitle);

            _rootElement.Add(CreatePropertyField(
                SherpaONNXRuntimeSettings.FetchLatestManifestPropertyName,
                SherpaONNXL10n.Settings.FetchLatestLabel,
                "Fetch latest manifest before loading models",
                SherpaONNXL10n.Settings.FetchLatestTooltip,
                "If disabled, registry lookups rely on cached checksum.txt content."));

            _rootElement.Add(CreatePropertyField(
                SherpaONNXRuntimeSettings.AutoDownloadModelsPropertyName,
                SherpaONNXL10n.Settings.AutoDownloadLabel,
                "Automatically download missing models",
                SherpaONNXL10n.Settings.AutoDownloadTooltip,
                "Disable to enforce offline/manual installations. Verification still runs."));

            _rootElement.Add(CreatePropertyField(
                SherpaONNXRuntimeSettings.ChecksumCacheDirectoryPropertyName,
                SherpaONNXL10n.Settings.CacheDirectoryLabel,
                "Checksum cache directory (optional)",
                SherpaONNXL10n.Settings.CacheDirectoryTooltip,
                "Absolute directory for checksum.txt cache files. Leave blank to use the temporary cache path."));

            _rootElement.Add(CreatePropertyField(
                SherpaONNXRuntimeSettings.ChecksumCacheTtlSecondsPropertyName,
                SherpaONNXL10n.Settings.CacheTtlLabel,
                "Checksum cache TTL (seconds)",
                SherpaONNXL10n.Settings.CacheTtlTooltip,
                "0 disables caching. Default: 3600 seconds (1 hour)."));

            var clearCacheButton = new Button(ClearChecksumCacheWithPrompt)
            {
                text = SherpaONNXLocalization.Tr(
                    SherpaONNXL10n.Settings.CacheClearButton,
                    "Delete cached checksum.txt files"),
                tooltip = SherpaONNXLocalization.Tr(
                    SherpaONNXL10n.Settings.CacheClearTooltip,
                    "Removes downloaded checksum manifests so the next lookup fetches a fresh copy.")
            };
            clearCacheButton.style.marginTop = 4;
            clearCacheButton.style.marginBottom = 6;
            _rootElement.Add(clearCacheButton);

            var runtimeHelp = new HelpBox(
                string.Format(
                    SherpaONNXLocalization.Tr(
                        SherpaONNXL10n.Settings.RuntimeHelp,
                        "Settings are stored under any Resources folder so they ship with builds.\nCurrent asset: {0}"),
                    string.IsNullOrEmpty(_runtimeSettingsAssetPath)
                        ? SherpaONNXLocalization.Tr(
                            SherpaONNXL10n.Settings.RuntimeHelpMissing,
                            "Asset will be created automatically.")
                        : _runtimeSettingsAssetPath),
                HelpBoxMessageType.None);
            runtimeHelp.style.marginTop = 6;
            _rootElement.Add(runtimeHelp);
        }

        private VisualElement CreateLanguageField()
        {
            var label = SherpaONNXLocalization.Tr(
                SherpaONNXL10n.Settings.LanguageLabel,
                "Editor language");
            var tooltip = SherpaONNXLocalization.Tr(
                SherpaONNXL10n.Settings.LanguageTooltip,
                "Auto follows the Unity Editor language. Override to lock Sherpa windows to a specific language.");

            var choices = new List<string>(kLanguageOptions.Length);
            var indexMap = new Dictionary<string, SherpaONNXEditorLanguage>(kLanguageOptions.Length);
            foreach (var lang in kLanguageOptions)
            {
                var display = SherpaONNXLocalization.GetLanguageDisplayName(lang);
                choices.Add(display);
                indexMap[display] = lang;
            }

            var current = SherpaONNXLocalization.PreferredLanguage;
            var currentLabel = SherpaONNXLocalization.GetLanguageDisplayName(current);
            var initialIndex = Mathf.Max(0, choices.IndexOf(currentLabel));

            var popup = new PopupField<string>(label, choices, initialIndex)
            {
                tooltip = tooltip
            };

            popup.RegisterValueChangedCallback(evt =>
            {
                if (evt == null || string.IsNullOrEmpty(evt.newValue))
                {
                    return;
                }

                if (indexMap.TryGetValue(evt.newValue, out var selected))
                {
                    SherpaONNXLocalization.SetLanguage(selected);
                }
            });

            popup.style.marginBottom = 6;
            return popup;
        }

        public override void OnGUI(string searchContext)
        {
            // IMGUI fallback
            var settings = SherpaONNXBuildSettings.Instance;
            DrawLanguagePopup();

            EditorGUI.BeginChangeCheck();
            var newVal = EditorGUILayout.ToggleLeft(
                new GUIContent(
                    SherpaONNXLocalization.Tr(SherpaONNXL10n.Settings.IncludeModelsLabel,
                        "Include downloaded models in desktop builds (Windows/macOS/Linux)"),
                    SherpaONNXLocalization.Tr(SherpaONNXL10n.Settings.IncludeModelsTooltip,
                        "If enabled, StreamingAssets/sherpa-onnx will be bundled into desktop builds.")),
                settings.IncludeModelsInDesktopBuild);
            if (EditorGUI.EndChangeCheck())
            {
                settings.IncludeModelsInDesktopBuild = newVal;
            }


            EditorGUILayout.HelpBox(
                SherpaONNXLocalization.Tr(
                    SherpaONNXL10n.Settings.IncludeModelsHelp,
                    "OFF (default): desktop builds ignore StreamingAssets/sherpa-onnx.\nON: include that folder."),
                MessageType.Info);

            EnsureRuntimeSettingsObject();
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(
                SherpaONNXLocalization.Tr(
                    SherpaONNXL10n.Settings.RuntimeDefaultsTitle,
                    "Runtime Environment Defaults"),
                EditorStyles.boldLabel);

            _runtimeSettingsObject.Update();
            DrawRuntimeProperty(
                SherpaONNXRuntimeSettings.FetchLatestManifestPropertyName,
                SherpaONNXL10n.Settings.FetchLatestLabel,
                "Fetch latest manifest before loading models",
                SherpaONNXL10n.Settings.FetchLatestTooltip,
                "If disabled, registry lookups rely on cached checksum.txt content.");
            DrawRuntimeProperty(
                SherpaONNXRuntimeSettings.AutoDownloadModelsPropertyName,
                SherpaONNXL10n.Settings.AutoDownloadLabel,
                "Automatically download missing models",
                SherpaONNXL10n.Settings.AutoDownloadTooltip,
                "Disable this to keep manual/offline installations untouched.");
            DrawRuntimeProperty(
                SherpaONNXRuntimeSettings.ChecksumCacheDirectoryPropertyName,
                SherpaONNXL10n.Settings.CacheDirectoryLabel,
                "Checksum cache directory (optional)",
                SherpaONNXL10n.Settings.CacheDirectoryTooltip,
                "Absolute directory path. Leave empty to use the system temp directory.");
            DrawRuntimeProperty(
                SherpaONNXRuntimeSettings.ChecksumCacheTtlSecondsPropertyName,
                SherpaONNXL10n.Settings.CacheTtlLabel,
                "Checksum cache TTL (seconds)",
                SherpaONNXL10n.Settings.CacheTtlTooltip,
                "Use 0 to disable caching entirely.");

            if (GUILayout.Button(new GUIContent(
                    SherpaONNXLocalization.Tr(
                        SherpaONNXL10n.Settings.CacheClearButton,
                        "Delete cached checksum.txt files"),
                    SherpaONNXLocalization.Tr(
                        SherpaONNXL10n.Settings.CacheClearTooltip,
                        "Removes downloaded checksum manifests so the next lookup fetches a fresh copy."))))
            {
                ClearChecksumCacheWithPrompt();
            }

            _runtimeSettingsObject.ApplyModifiedProperties();

            EditorGUILayout.HelpBox(
                string.Format(
                    SherpaONNXLocalization.Tr(
                        SherpaONNXL10n.Settings.RuntimeHelp,
                        "Settings are stored under any Resources folder so they ship with builds.\nCurrent asset: {0}"),
                    string.IsNullOrEmpty(_runtimeSettingsAssetPath)
                        ? SherpaONNXLocalization.Tr(
                            SherpaONNXL10n.Settings.RuntimeHelpMissing,
                            "Asset will be created automatically.")
                        : _runtimeSettingsAssetPath),
                MessageType.None);
        }

        private void ClearChecksumCacheWithPrompt()
        {
            var result = SherpaONNXUnityAPI.ClearChecksumCache();
            var cachePath = string.IsNullOrWhiteSpace(result.CacheDirectory)
                ? SherpaONNXLocalization.Tr(SherpaONNXL10n.Models.StatusUnknown, "unknown")
                : result.CacheDirectory;
            const string dialogTitle = "SherpaONNX";
            const string ok = "OK";

            if (result.HasErrors)
            {
                var errorDetails = (result.Errors != null && result.Errors.Count > 0)
                    ? string.Join("\n", result.Errors)
                    : SherpaONNXLocalization.Tr(SherpaONNXL10n.Models.StatusUnknown, "unknown");
                var message = string.Format(
                    SherpaONNXLocalization.Tr(
                        SherpaONNXL10n.Settings.CacheClearError,
                        "Deleted {0} file(s), but {1} failed:\n{2}"),
                    result.DeletedFiles,
                    result.FailedFiles,
                    errorDetails);
                EditorUtility.DisplayDialog(dialogTitle, message, ok);
                return;
            }

            if (!result.DirectoryFound || !result.AnyDeleted)
            {
                var emptyMessage = string.Format(
                    SherpaONNXLocalization.Tr(
                        SherpaONNXL10n.Settings.CacheClearEmpty,
                        "No cached checksum.txt files were found under:\n{0}"),
                    cachePath);
                EditorUtility.DisplayDialog(dialogTitle, emptyMessage, ok);
                return;
            }

            var successMessage = string.Format(
                SherpaONNXLocalization.Tr(
                    SherpaONNXL10n.Settings.CacheClearSuccess,
                    "Deleted {0} cached checksum file(s) from:\n{1}"),
                result.DeletedFiles,
                cachePath);
            EditorUtility.DisplayDialog(dialogTitle, successMessage, ok);
        }

        private void DrawLanguagePopup()
        {
            var label = SherpaONNXLocalization.Tr(
                SherpaONNXL10n.Settings.LanguageLabel,
                "Editor language");
            var tooltip = SherpaONNXLocalization.Tr(
                SherpaONNXL10n.Settings.LanguageTooltip,
                "Auto follows the Unity Editor language.");

            var displayNames = new string[kLanguageOptions.Length];
            var selectedIndex = 0;
            for (var i = 0; i < kLanguageOptions.Length; i++)
            {
                var lang = kLanguageOptions[i];
                displayNames[i] = SherpaONNXLocalization.GetLanguageDisplayName(lang);
                if (lang == SherpaONNXLocalization.PreferredLanguage)
                {
                    selectedIndex = i;
                }
            }

            EditorGUI.BeginChangeCheck();
            var newIndex = EditorGUILayout.Popup(new GUIContent(label, tooltip), selectedIndex, displayNames);
            if (EditorGUI.EndChangeCheck() && newIndex >= 0 && newIndex < kLanguageOptions.Length)
            {
                SherpaONNXLocalization.SetLanguage(kLanguageOptions[newIndex]);
            }
        }

        private void EnsureRuntimeSettingsObject()
        {
            if (_runtimeSettingsObject != null)
            {
                return;
            }

            var runtimeSettings = SherpaONNXRuntimeSettingsUtility.LoadOrCreateSettingsAsset();
            _runtimeSettingsObject = new SerializedObject(runtimeSettings);
            _runtimeSettingsAssetPath = AssetDatabase.GetAssetPath(runtimeSettings);
        }

        private void OnLanguageChanged()
        {
            BuildUi();
        }

        private void DrawRuntimeProperty(string propertyName, string labelKey, string labelFallback, string tooltipKey, string tooltipFallback)
        {
            var content = new GUIContent(
                SherpaONNXLocalization.Tr(labelKey, labelFallback),
                SherpaONNXLocalization.Tr(tooltipKey, tooltipFallback));
            EditorGUILayout.PropertyField(_runtimeSettingsObject.FindProperty(propertyName), content);
        }

        private PropertyField CreatePropertyField(string propertyName, string labelKey, string labelFallback, string tooltipKey, string tooltipFallback)
        {
            var prop = _runtimeSettingsObject.FindProperty(propertyName);
            var field = new PropertyField(
                prop,
                SherpaONNXLocalization.Tr(labelKey, labelFallback))
            {
                tooltip = SherpaONNXLocalization.Tr(tooltipKey, tooltipFallback)
            };
            field.Bind(_runtimeSettingsObject);
            field.style.marginBottom = 4;
            return field;
        }
    }

}
#endif
