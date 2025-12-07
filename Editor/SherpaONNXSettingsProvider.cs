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
            var buildSettings = SherpaONNXBuildSettings.Instance;

            var paddedContainer = new VisualElement();
            paddedContainer.style.paddingLeft = 10;
            paddedContainer.style.paddingRight = 10;
            paddedContainer.style.flexDirection = FlexDirection.Column;
            _rootElement.Add(paddedContainer);

            var header = new Label(SherpaONNXLocalization.Tr(
                SherpaONNXL10n.Settings.HeaderTitle,
                "SherpaONNX"));
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.fontSize = 19;
            header.style.marginBottom = 10;
            paddedContainer.Add(header);

            paddedContainer.Add(CreateSectionCard(
                SherpaONNXL10n.Settings.VersionTitle,
                "SherpaONNX Native Library",
                section =>
                {
                    section.Add(CreateVersionInfoRow(
                        SherpaONNXL10n.Settings.VersionLabel,
                        "Version",
                        SherpaONNXUnityAPI.SherpaONNXLibVersion));
                    section.Add(CreateVersionInfoRow(
                        SherpaONNXL10n.Settings.GitDateLabel,
                        "Git Date",
                        SherpaONNXUnityAPI.SherpaONNXLibGitDate));
                    section.Add(CreateVersionInfoRow(
                        SherpaONNXL10n.Settings.GitShaLabel,
                        "Git Commit",
                        SherpaONNXUnityAPI.SherpaONNXLibGitSha1));
                }));

            paddedContainer.Add(CreateSectionCard(
                SherpaONNXL10n.Settings.LanguageLabel,
                "Editor language",
                section => section.Add(CreateLanguageField())));

            paddedContainer.Add(CreateSectionCard(
                SherpaONNXL10n.Settings.BuildTitle,
                "SherpaONNX Build Settings",
                section =>
                {
                    var toggle = new Toggle(SherpaONNXLocalization.Tr(
                        SherpaONNXL10n.Settings.IncludeModelsLabel,
                        "Include downloaded models in desktop builds (Windows/macOS/Linux)"))
                    {
                        tooltip = SherpaONNXLocalization.Tr(
                            SherpaONNXL10n.Settings.IncludeModelsTooltip,
                            "If enabled, StreamingAssets/sherpa-onnx will be bundled into desktop builds. Default: OFF."),
                        value = buildSettings.IncludeModelsInDesktopBuild
                    };
                    toggle.RegisterValueChangedCallback(evt => buildSettings.IncludeModelsInDesktopBuild = evt.newValue);
                    section.Add(toggle);

                    var includeHelp = new HelpBox(
                        SherpaONNXLocalization.Tr(
                            SherpaONNXL10n.Settings.IncludeModelsHelp,
                            "OFF (default): Standalone builds skip StreamingAssets/sherpa-onnx for faster iterations.\n" +
                            "ON: include the folder for offline-ready builds.\n" +
                            "Mobile/WebGL/consoles remain excluded because StreamingAssets is read-only."),
                        HelpBoxMessageType.Info);
                    includeHelp.style.marginTop = 4;
                    section.Add(includeHelp);
                }));

            paddedContainer.Add(CreateSectionCard(
                SherpaONNXL10n.Settings.RuntimeDefaultsTitle,
                "Runtime Environment Defaults",
                section =>
                {
                    section.Add(CreatePropertyField(
                        SherpaONNXRuntimeSettings.FetchLatestManifestPropertyName,
                        SherpaONNXL10n.Settings.FetchLatestLabel,
                        "Fetch latest manifest before loading models",
                        SherpaONNXL10n.Settings.FetchLatestTooltip,
                        "If disabled, registry lookups rely on cached checksum.txt content."));

                    section.Add(CreatePropertyField(
                        SherpaONNXRuntimeSettings.AutoDownloadModelsPropertyName,
                        SherpaONNXL10n.Settings.AutoDownloadLabel,
                        "Automatically download missing models",
                        SherpaONNXL10n.Settings.AutoDownloadTooltip,
                        "Disable to enforce offline/manual installations. Verification still runs."));

                    section.Add(CreatePropertyField(
                        SherpaONNXRuntimeSettings.GithubProxyUrlPropertyName,
                        SherpaONNXL10n.Settings.GithubProxyLabel,
                        "GitHub proxy URL (optional)",
                        SherpaONNXL10n.Settings.GithubProxyTooltip,
                        "Base URL prepended to github.com downloads, e.g., https://ghfast.top/. Leave empty to disable."));

                    section.Add(CreatePropertyField(
                        SherpaONNXRuntimeSettings.ChecksumCacheDirectoryPropertyName,
                        SherpaONNXL10n.Settings.CacheDirectoryLabel,
                        "Checksum cache directory (optional)",
                        SherpaONNXL10n.Settings.CacheDirectoryTooltip,
                        "Absolute directory for checksum.txt cache files. Leave blank to use the temporary cache path."));

                    section.Add(CreatePropertyField(
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
                    clearCacheButton.style.marginTop = 6;
                    clearCacheButton.style.marginBottom = 2;
                    section.Add(clearCacheButton);

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
                    section.Add(runtimeHelp);
                }));

            paddedContainer.Add(CreateSectionCard(
                SherpaONNXL10n.Settings.LoggingTitle,
                "Logging",
                section =>
                {
                    var loggingEnabledField = CreatePropertyField(
                        SherpaONNXRuntimeSettings.LoggingEnabledPropertyName,
                        SherpaONNXL10n.Settings.LoggingEnabledLabel,
                        "Enable SherpaONNX logging",
                        SherpaONNXL10n.Settings.LoggingEnabledTooltip,
                        "Master switch for SherpaONNX logs in play mode and builds.");
                    section.Add(loggingEnabledField);

                    var loggingDetails = new VisualElement();
                    loggingDetails.style.marginLeft = 4;
                    loggingDetails.Add(CreatePropertyField(
                        SherpaONNXRuntimeSettings.LoggingLevelPropertyName,
                        SherpaONNXL10n.Settings.LoggingLevelLabel,
                        "Minimum log level",
                        SherpaONNXL10n.Settings.LoggingLevelTooltip,
                        "Trace emits detailed call stacks for initialization and model calls."));

                    loggingDetails.Add(CreatePropertyField(
                        SherpaONNXRuntimeSettings.LoggingTraceStacksPropertyName,
                        SherpaONNXL10n.Settings.LoggingTraceLabel,
                        "Trace level includes call stacks",
                        SherpaONNXL10n.Settings.LoggingTraceTooltip,
                        "When enabled, every Trace entry prints the managed call stack to simplify debugging."));

                    section.Add(loggingDetails);
                    ApplyLoggingVisibility(loggingDetails, loggingEnabledField);
                }));
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
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.Space(4);
                DrawSettingsHeaderIMGUI();
                DrawVersionInfoIMGUI();
                DrawLanguageSectionIMGUI();
                DrawBuildSettingsIMGUI(settings);

                EnsureRuntimeSettingsObject();
                _runtimeSettingsObject.Update();
                DrawRuntimeDefaultsIMGUI();
                DrawLoggingSettingsIMGUI();
                _runtimeSettingsObject.ApplyModifiedProperties();
                EditorGUILayout.Space(4);
            }
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

        private void ApplyLoggingVisibility(VisualElement loggingDetails, VisualElement toggleField)
        {
            if (loggingDetails == null || toggleField == null)
            {
                return;
            }

            void SyncVisibility()
            {
                var prop = _runtimeSettingsObject?.FindProperty(SherpaONNXRuntimeSettings.LoggingEnabledPropertyName);
                var enabled = prop != null && prop.boolValue;
                loggingDetails.style.display = enabled ? DisplayStyle.Flex : DisplayStyle.None;
            }

            toggleField.RegisterCallback<SerializedPropertyChangeEvent>(_ => SyncVisibility());
            SyncVisibility();
        }

        private VisualElement CreateVersionInfoRow(string labelKey, string labelFallback, string rawValue)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.flexWrap = Wrap.Wrap;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 1;

            var label = new Label(string.Format(
                "{0}:",
                SherpaONNXLocalization.Tr(labelKey, labelFallback)));
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.marginRight = 4;
            label.style.minWidth = 110;
            row.Add(label);

            var valueLabel = new Label(NormalizeVersionInfoValue(rawValue));
            valueLabel.style.flexGrow = 1;
            valueLabel.style.whiteSpace = WhiteSpace.Normal;
            row.Add(valueLabel);

            return row;
        }

        private void DrawVersionInfoIMGUI()
        {
            DrawIMGUISection(
                SherpaONNXL10n.Settings.VersionTitle,
                "SherpaONNX Native Library",
                () =>
                {
                    DrawVersionInfoField(
                        SherpaONNXL10n.Settings.VersionLabel,
                        "Version",
                        SherpaONNXUnityAPI.SherpaONNXLibVersion);
                    DrawVersionInfoField(
                        SherpaONNXL10n.Settings.GitDateLabel,
                        "Git Date",
                        SherpaONNXUnityAPI.SherpaONNXLibGitDate);
                    DrawVersionInfoField(
                        SherpaONNXL10n.Settings.GitShaLabel,
                        "Git Commit",
                        SherpaONNXUnityAPI.SherpaONNXLibGitSha1);
                });
        }

        private void DrawLanguageSectionIMGUI()
        {
            DrawIMGUISection(
                SherpaONNXL10n.Settings.LanguageLabel,
                "Editor language",
                DrawLanguagePopup);
        }

        private void DrawBuildSettingsIMGUI(SherpaONNXBuildSettings settings)
        {
            DrawIMGUISection(
                SherpaONNXL10n.Settings.BuildTitle,
                "SherpaONNX Build Settings",
                () =>
                {
                    EditorGUI.BeginChangeCheck();
                    var newValue = EditorGUILayout.ToggleLeft(
                        new GUIContent(
                            SherpaONNXLocalization.Tr(SherpaONNXL10n.Settings.IncludeModelsLabel,
                                "Include downloaded models in desktop builds (Windows/macOS/Linux)"),
                            SherpaONNXLocalization.Tr(SherpaONNXL10n.Settings.IncludeModelsTooltip,
                                "If enabled, StreamingAssets/sherpa-onnx will be bundled into desktop builds.")),
                        settings.IncludeModelsInDesktopBuild);
                    if (EditorGUI.EndChangeCheck())
                    {
                        settings.IncludeModelsInDesktopBuild = newValue;
                    }

                    EditorGUILayout.HelpBox(
                        SherpaONNXLocalization.Tr(
                            SherpaONNXL10n.Settings.IncludeModelsHelp,
                            "OFF (default): desktop builds ignore StreamingAssets/sherpa-onnx.\nON: include that folder."),
                        MessageType.Info);
                });
        }

        private void DrawRuntimeDefaultsIMGUI()
        {
            DrawIMGUISection(
                SherpaONNXL10n.Settings.RuntimeDefaultsTitle,
                "Runtime Environment Defaults",
                () =>
                {
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
                        SherpaONNXRuntimeSettings.GithubProxyUrlPropertyName,
                        SherpaONNXL10n.Settings.GithubProxyLabel,
                        "GitHub proxy URL (optional)",
                        SherpaONNXL10n.Settings.GithubProxyTooltip,
                        "Base URL prepended to github.com downloads, e.g., https://ghfast.top/. Leave empty to disable.");
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
                });
        }

        private void DrawLoggingSettingsIMGUI()
        {
            DrawIMGUISection(
                SherpaONNXL10n.Settings.LoggingTitle,
                "Logging",
                () =>
                {
                    DrawRuntimeProperty(
                        SherpaONNXRuntimeSettings.LoggingEnabledPropertyName,
                        SherpaONNXL10n.Settings.LoggingEnabledLabel,
                        "Enable SherpaONNX logging",
                        SherpaONNXL10n.Settings.LoggingEnabledTooltip,
                        "Master switch for SherpaONNX logs in play mode and builds.");

                    var enabledProp = _runtimeSettingsObject.FindProperty(SherpaONNXRuntimeSettings.LoggingEnabledPropertyName);
                    var loggingEnabled = enabledProp != null && enabledProp.boolValue;

                    EditorGUI.BeginDisabledGroup(!loggingEnabled);
                    DrawRuntimeProperty(
                        SherpaONNXRuntimeSettings.LoggingLevelPropertyName,
                        SherpaONNXL10n.Settings.LoggingLevelLabel,
                        "Minimum log level",
                        SherpaONNXL10n.Settings.LoggingLevelTooltip,
                        "Trace emits detailed call stacks for initialization and model calls.");
                    DrawRuntimeProperty(
                        SherpaONNXRuntimeSettings.LoggingTraceStacksPropertyName,
                        SherpaONNXL10n.Settings.LoggingTraceLabel,
                        "Trace level includes call stacks",
                        SherpaONNXL10n.Settings.LoggingTraceTooltip,
                        "When enabled, every Trace entry prints the managed call stack.");
                    EditorGUI.EndDisabledGroup();
                });
        }

        private void DrawVersionInfoField(string labelKey, string labelFallback, string rawValue)
        {
            var label = SherpaONNXLocalization.Tr(labelKey, labelFallback);
            EditorGUILayout.LabelField(label, NormalizeVersionInfoValue(rawValue));
        }

        private void DrawIMGUISection(string titleKey, string titleFallback, Action body)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(
                    SherpaONNXLocalization.Tr(titleKey, titleFallback),
                    EditorStyles.boldLabel);
                EditorGUI.indentLevel++;
                body?.Invoke();
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.Space();
        }

        private static string NormalizeVersionInfoValue(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? SherpaONNXLocalization.Tr(SherpaONNXL10n.Models.StatusUnknown, "unknown")
                : value;
        }

        private VisualElement CreateSectionCard(string titleKey, string titleFallback, Action<VisualElement> bodyBuilder)
        {
            var card = new VisualElement();
            ApplyCardStyle(card);

            var title = new Label(SherpaONNXLocalization.Tr(titleKey, titleFallback));
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 4;
            card.Add(title);

            var body = new VisualElement();
            body.style.flexDirection = FlexDirection.Column;
            card.Add(body);

            bodyBuilder?.Invoke(body);
            return card;
        }

        private static void ApplyCardStyle(VisualElement card)
        {
            card.style.marginBottom = 6;
            card.style.paddingTop = 4;
            card.style.paddingBottom = 6;
            card.style.paddingLeft = 4;
            card.style.paddingRight = 4;
        }


        private void DrawSettingsHeaderIMGUI()
        {
            var headerLabel = SherpaONNXLocalization.Tr(
                SherpaONNXL10n.Settings.HeaderTitle,
                "SherpaONNX");
            var style = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 19
            };
            EditorGUILayout.LabelField(headerLabel, style);
            EditorGUILayout.Space();
        }
    }

}
#endif
