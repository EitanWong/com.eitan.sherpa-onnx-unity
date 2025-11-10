#if UNITY_EDITOR

namespace Eitan.SherpaOnnxUnity.Editor
{


    using System;
    using System.Collections.Generic;
    using Eitan.SherpaOnnxUnity.Editor.Localization;
    using Eitan.SherpaOnnxUnity.Runtime;
    using UnityEditor;
    using UnityEditor.UIElements;
    using UnityEngine;
    using UnityEngine.UIElements;

    /// <summary>
    /// Project Settings UI: Edit ▸ Project Settings ▸ SHERPA ONNX
    /// </summary>
    internal sealed class SherpaOnnxSettingsProvider : SettingsProvider
    {
        private const string kPath = "Project/Sherpa Onnx";
        private static readonly SherpaOnnxEditorLanguage[] kLanguageOptions =
            (SherpaOnnxEditorLanguage[])Enum.GetValues(typeof(SherpaOnnxEditorLanguage));

        private SerializedObject _runtimeSettingsObject;
        private VisualElement _rootElement;
        private string _runtimeSettingsAssetPath = string.Empty;

        public SherpaOnnxSettingsProvider() : base(kPath, SettingsScope.Project) { }

        [SettingsProvider]
        public static SettingsProvider Create() => new SherpaOnnxSettingsProvider();

        public override void OnActivate(string searchContext, VisualElement rootElement)
        {
            _rootElement = rootElement;
            EnsureRuntimeSettingsObject();
            SherpaOnnxLocalization.LanguageChanged += OnLanguageChanged;
            BuildUi();
        }

        public override void OnDeactivate()
        {
            SherpaOnnxLocalization.LanguageChanged -= OnLanguageChanged;
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
            var settings = SherpaOnnxBuildSettings.Instance;

            var title = new Label(SherpaOnnxLocalization.Tr(
                SherpaOnnxI18n.Settings.BuildTitle,
                "Sherpa Onnx Build Settings"));
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginTop = 6;
            title.style.marginBottom = 6;
            _rootElement.Add(title);

            _rootElement.Add(CreateLanguageField());

            var toggle = new Toggle(SherpaOnnxLocalization.Tr(
                SherpaOnnxI18n.Settings.IncludeModelsLabel,
                "Include downloaded models in desktop builds (Windows/macOS/Linux)"))
            {
                tooltip = SherpaOnnxLocalization.Tr(
                    SherpaOnnxI18n.Settings.IncludeModelsTooltip,
                    "If enabled, StreamingAssets/sherpa-onnx will be bundled into desktop builds. Default: OFF."),
                value = settings.IncludeModelsInDesktopBuild
            };
            toggle.RegisterValueChangedCallback(evt => settings.IncludeModelsInDesktopBuild = evt.newValue);
            _rootElement.Add(toggle);

            var includeHelp = new HelpBox(
                SherpaOnnxLocalization.Tr(
                    SherpaOnnxI18n.Settings.IncludeModelsHelp,
                    "OFF (default): Standalone builds skip StreamingAssets/sherpa-onnx for faster iterations.\n" +
                    "ON: include the folder for offline-ready builds.\n" +
                    "Mobile/WebGL/consoles remain excluded because StreamingAssets is read-only."),
                HelpBoxMessageType.Info);
            includeHelp.style.marginTop = 6;
            _rootElement.Add(includeHelp);

            var runtimeTitle = new Label(SherpaOnnxLocalization.Tr(
                SherpaOnnxI18n.Settings.RuntimeDefaultsTitle,
                "Runtime Environment Defaults"));
            runtimeTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            runtimeTitle.style.marginTop = 10;
            runtimeTitle.style.marginBottom = 4;
            _rootElement.Add(runtimeTitle);

            _rootElement.Add(CreatePropertyField(
                SherpaOnnxRuntimeSettings.FetchLatestManifestPropertyName,
                SherpaOnnxI18n.Settings.FetchLatestLabel,
                "Fetch latest manifest before loading models",
                SherpaOnnxI18n.Settings.FetchLatestTooltip,
                "If disabled, registry lookups rely on cached checksum.txt content."));

            _rootElement.Add(CreatePropertyField(
                SherpaOnnxRuntimeSettings.AutoDownloadModelsPropertyName,
                SherpaOnnxI18n.Settings.AutoDownloadLabel,
                "Automatically download missing models",
                SherpaOnnxI18n.Settings.AutoDownloadTooltip,
                "Disable to enforce offline/manual installations. Verification still runs."));

            _rootElement.Add(CreatePropertyField(
                SherpaOnnxRuntimeSettings.ChecksumCacheDirectoryPropertyName,
                SherpaOnnxI18n.Settings.CacheDirectoryLabel,
                "Checksum cache directory (optional)",
                SherpaOnnxI18n.Settings.CacheDirectoryTooltip,
                "Absolute directory for checksum.txt cache files. Leave blank to use the temporary cache path."));

            _rootElement.Add(CreatePropertyField(
                SherpaOnnxRuntimeSettings.ChecksumCacheTtlSecondsPropertyName,
                SherpaOnnxI18n.Settings.CacheTtlLabel,
                "Checksum cache TTL (seconds)",
                SherpaOnnxI18n.Settings.CacheTtlTooltip,
                "0 disables caching. Default: 3600 seconds (1 hour)."));

            var runtimeHelp = new HelpBox(
                string.Format(
                    SherpaOnnxLocalization.Tr(
                        SherpaOnnxI18n.Settings.RuntimeHelp,
                        "Settings are stored under any Resources folder so they ship with builds.\nCurrent asset: {0}"),
                    string.IsNullOrEmpty(_runtimeSettingsAssetPath)
                        ? SherpaOnnxLocalization.Tr(
                            SherpaOnnxI18n.Settings.RuntimeHelpMissing,
                            "Asset will be created automatically.")
                        : _runtimeSettingsAssetPath),
                HelpBoxMessageType.None);
            runtimeHelp.style.marginTop = 6;
            _rootElement.Add(runtimeHelp);
        }

        private VisualElement CreateLanguageField()
        {
            var label = SherpaOnnxLocalization.Tr(
                SherpaOnnxI18n.Settings.LanguageLabel,
                "Editor language");
            var tooltip = SherpaOnnxLocalization.Tr(
                SherpaOnnxI18n.Settings.LanguageTooltip,
                "Auto follows the Unity Editor language. Override to lock Sherpa windows to a specific language.");

            var choices = new List<string>(kLanguageOptions.Length);
            var indexMap = new Dictionary<string, SherpaOnnxEditorLanguage>(kLanguageOptions.Length);
            foreach (var lang in kLanguageOptions)
            {
                var display = SherpaOnnxLocalization.GetLanguageDisplayName(lang);
                choices.Add(display);
                indexMap[display] = lang;
            }

            var current = SherpaOnnxLocalization.PreferredLanguage;
            var currentLabel = SherpaOnnxLocalization.GetLanguageDisplayName(current);
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
                    SherpaOnnxLocalization.SetLanguage(selected);
                }
            });

            popup.style.marginBottom = 6;
            return popup;
        }

        public override void OnGUI(string searchContext)
        {
            // IMGUI fallback
            var settings = SherpaOnnxBuildSettings.Instance;
            DrawLanguagePopup();

            EditorGUI.BeginChangeCheck();
            var newVal = EditorGUILayout.ToggleLeft(
                new GUIContent(
                    SherpaOnnxLocalization.Tr(SherpaOnnxI18n.Settings.IncludeModelsLabel,
                        "Include downloaded models in desktop builds (Windows/macOS/Linux)"),
                    SherpaOnnxLocalization.Tr(SherpaOnnxI18n.Settings.IncludeModelsTooltip,
                        "If enabled, StreamingAssets/sherpa-onnx will be bundled into desktop builds.")),
                settings.IncludeModelsInDesktopBuild);
            if (EditorGUI.EndChangeCheck())
            {
                settings.IncludeModelsInDesktopBuild = newVal;
            }


            EditorGUILayout.HelpBox(
                SherpaOnnxLocalization.Tr(
                    SherpaOnnxI18n.Settings.IncludeModelsHelp,
                    "OFF (default): desktop builds ignore StreamingAssets/sherpa-onnx.\nON: include that folder."),
                MessageType.Info);

            EnsureRuntimeSettingsObject();
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(
                SherpaOnnxLocalization.Tr(
                    SherpaOnnxI18n.Settings.RuntimeDefaultsTitle,
                    "Runtime Environment Defaults"),
                EditorStyles.boldLabel);

            _runtimeSettingsObject.Update();
            DrawRuntimeProperty(
                SherpaOnnxRuntimeSettings.FetchLatestManifestPropertyName,
                SherpaOnnxI18n.Settings.FetchLatestLabel,
                "Fetch latest manifest before loading models",
                SherpaOnnxI18n.Settings.FetchLatestTooltip,
                "If disabled, registry lookups rely on cached checksum.txt content.");
            DrawRuntimeProperty(
                SherpaOnnxRuntimeSettings.AutoDownloadModelsPropertyName,
                SherpaOnnxI18n.Settings.AutoDownloadLabel,
                "Automatically download missing models",
                SherpaOnnxI18n.Settings.AutoDownloadTooltip,
                "Disable this to keep manual/offline installations untouched.");
            DrawRuntimeProperty(
                SherpaOnnxRuntimeSettings.ChecksumCacheDirectoryPropertyName,
                SherpaOnnxI18n.Settings.CacheDirectoryLabel,
                "Checksum cache directory (optional)",
                SherpaOnnxI18n.Settings.CacheDirectoryTooltip,
                "Absolute directory path. Leave empty to use the system temp directory.");
            DrawRuntimeProperty(
                SherpaOnnxRuntimeSettings.ChecksumCacheTtlSecondsPropertyName,
                SherpaOnnxI18n.Settings.CacheTtlLabel,
                "Checksum cache TTL (seconds)",
                SherpaOnnxI18n.Settings.CacheTtlTooltip,
                "Use 0 to disable caching entirely.");
            _runtimeSettingsObject.ApplyModifiedProperties();

            EditorGUILayout.HelpBox(
                string.Format(
                    SherpaOnnxLocalization.Tr(
                        SherpaOnnxI18n.Settings.RuntimeHelp,
                        "Settings are stored under any Resources folder so they ship with builds.\nCurrent asset: {0}"),
                    string.IsNullOrEmpty(_runtimeSettingsAssetPath)
                        ? SherpaOnnxLocalization.Tr(
                            SherpaOnnxI18n.Settings.RuntimeHelpMissing,
                            "Asset will be created automatically.")
                        : _runtimeSettingsAssetPath),
                MessageType.None);
        }

        private void DrawLanguagePopup()
        {
            var label = SherpaOnnxLocalization.Tr(
                SherpaOnnxI18n.Settings.LanguageLabel,
                "Editor language");
            var tooltip = SherpaOnnxLocalization.Tr(
                SherpaOnnxI18n.Settings.LanguageTooltip,
                "Auto follows the Unity Editor language.");

            var displayNames = new string[kLanguageOptions.Length];
            var selectedIndex = 0;
            for (var i = 0; i < kLanguageOptions.Length; i++)
            {
                var lang = kLanguageOptions[i];
                displayNames[i] = SherpaOnnxLocalization.GetLanguageDisplayName(lang);
                if (lang == SherpaOnnxLocalization.PreferredLanguage)
                {
                    selectedIndex = i;
                }
            }

            EditorGUI.BeginChangeCheck();
            var newIndex = EditorGUILayout.Popup(new GUIContent(label, tooltip), selectedIndex, displayNames);
            if (EditorGUI.EndChangeCheck() && newIndex >= 0 && newIndex < kLanguageOptions.Length)
            {
                SherpaOnnxLocalization.SetLanguage(kLanguageOptions[newIndex]);
            }
        }

        private void EnsureRuntimeSettingsObject()
        {
            if (_runtimeSettingsObject != null)
            {
                return;
            }

            var runtimeSettings = SherpaOnnxRuntimeSettingsUtility.LoadOrCreateSettingsAsset();
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
                SherpaOnnxLocalization.Tr(labelKey, labelFallback),
                SherpaOnnxLocalization.Tr(tooltipKey, tooltipFallback));
            EditorGUILayout.PropertyField(_runtimeSettingsObject.FindProperty(propertyName), content);
        }

        private PropertyField CreatePropertyField(string propertyName, string labelKey, string labelFallback, string tooltipKey, string tooltipFallback)
        {
            var prop = _runtimeSettingsObject.FindProperty(propertyName);
            var field = new PropertyField(
                prop,
                SherpaOnnxLocalization.Tr(labelKey, labelFallback))
            {
                tooltip = SherpaOnnxLocalization.Tr(tooltipKey, tooltipFallback)
            };
            field.Bind(_runtimeSettingsObject);
            field.style.marginBottom = 4;
            return field;
        }
    }

}
#endif
