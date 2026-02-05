#if UNITY_EDITOR

namespace Eitan.SherpaONNXUnity.Editor
{


    using System;
    using System.Collections.Generic;
    using System.IO;
    using Eitan.SherpaONNXUnity.Editor.Localization;
    using Eitan.SherpaONNXUnity.Runtime;
    using UnityEditor;
    using UnityEditor.UIElements;
    using UnityEditorInternal;
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
        private SerializedObject _customModelsObject;
        private ReorderableList _customCatalogList;
        private VisualElement _rootElement;
        private string _runtimeSettingsAssetPath = string.Empty;
        private string _customModelsAssetPath = string.Empty;

        public SherpaONNXSettingsProvider() : base(kPath, SettingsScope.Project) { }

        [SettingsProvider]
        public static SettingsProvider Create() => new SherpaONNXSettingsProvider();

        public override void OnActivate(string searchContext, VisualElement rootElement)
        {
            _rootElement = rootElement;
            EnsureRuntimeSettingsObject();
            EnsureCustomModelsObject();
            SherpaONNXLocalization.LanguageChanged += OnLanguageChanged;
            BuildUi();
        }

        public override void OnDeactivate()
        {
            SherpaONNXLocalization.LanguageChanged -= OnLanguageChanged;
            _rootElement = null;
            _runtimeSettingsObject = null;
            _customModelsObject = null;
        }

        private void BuildUi()
        {
            if (_rootElement == null)
            {
                return;
            }

            _rootElement.Clear();
            var buildSettings = SherpaONNXBuildSettings.Instance;
            if (_customCatalogList == null)
            {
                BuildCustomCatalogList();
            }

            _rootElement.style.flexGrow = 1;
            _rootElement.style.flexDirection = FlexDirection.Column;
            _rootElement.style.minHeight = 0;
            var scrollView = new ScrollView
            {
                horizontalScrollerVisibility = ScrollerVisibility.Hidden,
                verticalScrollerVisibility = ScrollerVisibility.Auto
            };
            scrollView.style.flexGrow = 1;
            scrollView.style.flexShrink = 1;
            scrollView.style.minHeight = 0;
            _rootElement.Add(scrollView);

            var paddedContainer = new VisualElement();
            paddedContainer.style.paddingLeft = 10;
            paddedContainer.style.paddingRight = 10;
            paddedContainer.style.flexDirection = FlexDirection.Column;
            paddedContainer.style.flexGrow = 0;
            paddedContainer.style.flexShrink = 0;
            scrollView.Add(paddedContainer);

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
                        SherpaONNXRuntimeSettings.AutoDeleteCorruptedModelsPropertyName,
                        SherpaONNXL10n.Settings.AutoDeleteCorruptedLabel,
                        "Auto-delete corrupted models",
                        SherpaONNXL10n.Settings.AutoDeleteCorruptedTooltip,
                        "When enabled, corrupted model folders are deleted after initialization or verification failures."));

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
                SherpaONNXL10n.Settings.CustomModelsTitle,
                "Custom Models",
                section =>
                {
                    var guide = new HelpBox(
                        SherpaONNXLocalization.Tr(
                            SherpaONNXL10n.Settings.CustomModelsGuide,
                            "Tips: Use Module Type Hint/Model Type Hint only when auto-detection fails. Hints must match enum names (case-insensitive).\n" +
                            "File Bindings map SherpaONNXModelFileKey to files; paths are relative to the model folder unless absolute."),
                        HelpBoxMessageType.Info);
                    guide.style.marginBottom = 6;
                    section.Add(guide);

                    var importRow = new VisualElement();
                    importRow.style.flexDirection = FlexDirection.Row;
                    importRow.style.flexWrap = Wrap.Wrap;
                    importRow.style.marginBottom = 4;

                    var importButton = new Button(ImportCustomModelFolder)
                    {
                        text = SherpaONNXLocalization.Tr(
                            SherpaONNXL10n.Settings.CustomModelsImportButton,
                            "Import Model Folder"),
                        tooltip = SherpaONNXLocalization.Tr(
                            SherpaONNXL10n.Settings.CustomModelsImportTooltip,
                            "Select a model folder under Assets/StreamingAssets/sherpa-onnx/models to auto-fill a custom entry.")
                    };
                    importButton.style.marginRight = 6;
                    importRow.Add(importButton);

                    var exportButton = new Button(ExportCustomManifest)
                    {
                        text = SherpaONNXLocalization.Tr(
                            SherpaONNXL10n.Settings.CustomModelsExportButton,
                            "Export Manifest"),
                        tooltip = SherpaONNXLocalization.Tr(
                            SherpaONNXL10n.Settings.CustomModelsExportTooltip,
                            "Export enabled model entries to a SherpaONNXModelManifest JSON file.")
                    };
                    exportButton.style.marginRight = 6;
                    importRow.Add(exportButton);

                    section.Add(importRow);
                    section.Add(CreateCustomCatalogListElement());

                    var customHelp = new HelpBox(
                        string.Format(
                            SherpaONNXLocalization.Tr(
                                SherpaONNXL10n.Settings.CustomModelsHelp,
                                "Custom models are merged into the catalog at runtime. Custom entries override built-in models when modelId + module match.\nCurrent asset: {0}"),
                            string.IsNullOrEmpty(_customModelsAssetPath)
                                ? SherpaONNXLocalization.Tr(
                                    SherpaONNXL10n.Settings.CustomModelsHelpMissing,
                                    "Asset will be created automatically.")
                                : _customModelsAssetPath),
                        HelpBoxMessageType.None);
                    customHelp.style.marginTop = 6;
                    section.Add(customHelp);
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

        private void EnsureCustomModelsObject()
        {
            if (_customModelsObject != null)
            {
                return;
            }

            var customSettings = SherpaONNXCustomModelSettingsUtility.LoadOrCreateSettingsAsset();
            _customModelsObject = new SerializedObject(customSettings);
            _customModelsAssetPath = AssetDatabase.GetAssetPath(customSettings);
            BuildCustomCatalogList();
        }

        private void BuildCustomCatalogList()
        {
            if (_customModelsObject == null)
            {
                return;
            }

            var entriesProp = _customModelsObject.FindProperty(SherpaONNXCustomModelSettings.EntriesPropertyName);
            _customCatalogList = new ReorderableList(_customModelsObject, entriesProp, draggable: true, displayHeader: true, displayAddButton: true, displayRemoveButton: true);
            _customCatalogList.drawHeaderCallback = rect =>
            {
                var label = SherpaONNXLocalization.Tr(
                    SherpaONNXL10n.Settings.CustomModelsListLabel,
                    "Custom catalog entries");
                var tooltip = SherpaONNXLocalization.Tr(
                    SherpaONNXL10n.Settings.CustomModelsListTooltip,
                    "Add model entries or remote manifest entries. Model entries should include modelId, moduleType, downloadUrl, and downloadFileHash.");
                EditorGUI.LabelField(rect, new GUIContent(label, tooltip));
            };
            _customCatalogList.elementHeightCallback = index =>
            {
                var element = entriesProp.GetArrayElementAtIndex(index);
                return EditorGUI.GetPropertyHeight(element, includeChildren: true);
            };
            _customCatalogList.drawElementCallback = (rect, index, isActive, isFocused) =>
            {
                var element = entriesProp.GetArrayElementAtIndex(index);
                rect.height = EditorGUI.GetPropertyHeight(element, includeChildren: true);
                EditorGUI.PropertyField(rect, element, GUIContent.none, includeChildren: true);
            };
            _customCatalogList.footerHeight = EditorGUIUtility.singleLineHeight + 2f;
        }

        private void ImportCustomModelFolder()
        {
            var dialogTitle = SherpaONNXLocalization.Tr(
                SherpaONNXL10n.Settings.CustomModelsImportDialogTitle,
                "SherpaONNX");
            var folder = EditorUtility.OpenFolderPanel(
                SherpaONNXLocalization.Tr(
                    SherpaONNXL10n.Settings.CustomModelsImportDialogBody,
                    "Select model folder"),
                Application.dataPath,
                string.Empty);

            if (string.IsNullOrWhiteSpace(folder))
            {
                return;
            }

            folder = folder.Replace('\\', '/').TrimEnd('/');
            var modelRoot = "Assets/StreamingAssets/sherpa-onnx/models";
            var relative = FileUtil.GetProjectRelativePath(folder);
            if (string.IsNullOrWhiteSpace(relative))
            {
                EditorUtility.DisplayDialog(
                    dialogTitle,
                    SherpaONNXLocalization.Tr(
                        SherpaONNXL10n.Settings.CustomModelsImportErrorOutsideProject,
                        "Selected folder must be inside this Unity project."),
                    "OK");
                return;
            }

            relative = relative.Replace('\\', '/').TrimEnd('/');
            if (!relative.StartsWith(modelRoot + "/", StringComparison.OrdinalIgnoreCase))
            {
                EditorUtility.DisplayDialog(
                    dialogTitle,
                    string.Format(
                        SherpaONNXLocalization.Tr(
                            SherpaONNXL10n.Settings.CustomModelsImportErrorNotUnderRoot,
                            "Selected folder must be under:\n{0}"),
                        modelRoot),
                    "OK");
                return;
            }

            var segments = relative.Substring(modelRoot.Length).Trim('/').Split('/');
            if (segments.Length < 2)
            {
                EditorUtility.DisplayDialog(
                    dialogTitle,
                    SherpaONNXLocalization.Tr(
                        SherpaONNXL10n.Settings.CustomModelsImportErrorInvalidLayout,
                        "Please select a model folder inside a module folder (e.g., speech-synthesis/your-model-id)."),
                    "OK");
                return;
            }

            var moduleFolder = segments[segments.Length - 2];
            var modelId = segments[segments.Length - 1];
            var moduleType = TryParseModuleTypeFromFolder(moduleFolder, out var parsedType)
                ? parsedType
                : SherpaONNXModuleType.Undefined;

            if (moduleType == SherpaONNXModuleType.Undefined)
            {
                ModuleTypePickerWindow.Show(
                    SherpaONNXLocalization.Tr(
                        SherpaONNXL10n.Settings.CustomModelsImportSelectModuleTitle,
                        "Select Module Type"),
                    selected =>
                    {
                        if (selected == SherpaONNXModuleType.Undefined)
                        {
                            return;
                        }

                        CreateCustomEntryFromImport(dialogTitle, folder, modelId, selected);
                    });
                return;
            }

            CreateCustomEntryFromImport(dialogTitle, folder, modelId, moduleType);
        }

        private void ExportCustomManifest()
        {
            EnsureCustomModelsObject();
            var dialogTitle = SherpaONNXLocalization.Tr(
                SherpaONNXL10n.Settings.CustomModelsExportDialogTitle,
                "SherpaONNX");

            var settings = SherpaONNXCustomModelSettingsUtility.LoadOrCreateSettingsAsset();
            if (settings == null)
            {
                EditorUtility.DisplayDialog(
                    dialogTitle,
                    SherpaONNXLocalization.Tr(
                        SherpaONNXL10n.Settings.CustomModelsExportDialogMissing,
                        "Custom model settings asset not found. Create one at Assets/Resources/SherpaONNXCustomModels.asset."),
                    "OK");
                return;
            }

            var manifest = BuildCustomManifest(settings);
            if (manifest.models == null || manifest.models.Count == 0)
            {
                EditorUtility.DisplayDialog(
                    dialogTitle,
                    SherpaONNXLocalization.Tr(
                        SherpaONNXL10n.Settings.CustomModelsExportDialogEmpty,
                        "No enabled custom model entries found. Add at least one Model entry."),
                    "OK");
                return;
            }

            var defaultDirectory = Path.GetDirectoryName(Application.dataPath) ?? Application.dataPath;
            var outputPath = EditorUtility.SaveFilePanel(
                SherpaONNXLocalization.Tr(
                    SherpaONNXL10n.Settings.CustomModelsExportDialogSaveTitle,
                    "Export SherpaONNX Manifest"),
                defaultDirectory,
                "manifest",
                "json");

            if (string.IsNullOrWhiteSpace(outputPath))
            {
                return;
            }

            var json = JsonUtility.ToJson(manifest, true);
            File.WriteAllText(outputPath, json);
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog(
                dialogTitle,
                string.Format(
                    SherpaONNXLocalization.Tr(
                        SherpaONNXL10n.Settings.CustomModelsExportDialogSaved,
                        "Manifest exported:\n{0}"),
                    outputPath),
                "OK");
        }

        private static SherpaONNXModelManifest BuildCustomManifest(SherpaONNXCustomModelSettings settings)
        {
            var manifest = new SherpaONNXModelManifest();
            if (settings == null || settings.Entries == null)
            {
                return manifest;
            }

            for (int i = 0; i < settings.Entries.Count; i++)
            {
                var entry = settings.Entries[i];
                if (entry == null || !entry.enabled || !entry.IsModel)
                {
                    continue;
                }

                var metadata = entry.ToMetadata();
                if (metadata == null || string.IsNullOrWhiteSpace(metadata.modelId))
                {
                    continue;
                }

                manifest.models.Add(NormalizeMetadata(metadata));
            }

            return manifest;
        }

        private static SherpaONNXModelMetadata NormalizeMetadata(SherpaONNXModelMetadata metadata)
        {
            if (metadata == null)
            {
                return null;
            }

            return new SherpaONNXModelMetadata
            {
                modelId = metadata.modelId?.Trim(),
                moduleType = metadata.moduleType,
                moduleTypeHint = metadata.moduleTypeHint?.Trim(),
                downloadUrl = metadata.downloadUrl?.Trim(),
                downloadFileHash = metadata.downloadFileHash?.Trim(),
                modelTypeHint = metadata.modelTypeHint?.Trim(),
                numberOfSpeakers = metadata.numberOfSpeakers,
                sampleRate = metadata.sampleRate,
                fileBindings = NormalizeBindings(metadata.fileBindings)
            };
        }

        private static List<SherpaONNXModelFileBinding> NormalizeBindings(List<SherpaONNXModelFileBinding> bindings)
        {
            var results = new List<SherpaONNXModelFileBinding>();
            if (bindings == null)
            {
                return results;
            }

            for (int i = 0; i < bindings.Count; i++)
            {
                var binding = bindings[i];
                if (binding == null)
                {
                    continue;
                }

                var path = binding.path?.Trim();
                if (binding.key == SherpaONNXModelFileKey.None || string.IsNullOrWhiteSpace(path))
                {
                    continue;
                }

                results.Add(new SherpaONNXModelFileBinding
                {
                    key = binding.key,
                    path = path
                });
            }

            return results;
        }

        private void CreateCustomEntryFromImport(string dialogTitle, string folder, string modelId, SherpaONNXModuleType moduleType)
        {
            if (string.IsNullOrWhiteSpace(folder) || string.IsNullOrWhiteSpace(modelId))
            {
                return;
            }

            EnsureCustomModelsObject();
            _customModelsObject.Update();

            var entriesProp = _customModelsObject.FindProperty(SherpaONNXCustomModelSettings.EntriesPropertyName);
            var newIndex = entriesProp.arraySize;
            entriesProp.arraySize += 1;
            var entryProp = entriesProp.GetArrayElementAtIndex(newIndex);

            entryProp.FindPropertyRelative("enabled").boolValue = true;
            entryProp.FindPropertyRelative("entryType").enumValueIndex = (int)SherpaONNXCustomCatalogEntryType.Model;
            entryProp.FindPropertyRelative("name").stringValue = modelId;
            entryProp.FindPropertyRelative("modelId").stringValue = modelId;
            entryProp.FindPropertyRelative("moduleType").enumValueIndex = (int)moduleType;
            entryProp.FindPropertyRelative("moduleTypeHint").stringValue = string.Empty;
            entryProp.FindPropertyRelative("downloadUrl").stringValue = string.Empty;
            entryProp.FindPropertyRelative("downloadFileHash").stringValue = string.Empty;
            entryProp.FindPropertyRelative("numberOfSpeakers").intValue = 0;
            entryProp.FindPropertyRelative("sampleRate").intValue = 16000;
            entryProp.FindPropertyRelative("modelTypeHint").stringValue = InferModelTypeHint(moduleType, modelId);

            var bindingsProp = entryProp.FindPropertyRelative("fileBindings");
            if (bindingsProp != null)
            {
                bindingsProp.ClearArray();
                var bindings = CollectBindings(folder);
                for (int i = 0; i < bindings.Count; i++)
                {
                    var binding = bindings[i];
                    var index = bindingsProp.arraySize;
                    bindingsProp.InsertArrayElementAtIndex(index);
                    var bindingProp = bindingsProp.GetArrayElementAtIndex(index);
                    bindingProp.FindPropertyRelative("key").enumValueIndex = (int)binding.key;
                    bindingProp.FindPropertyRelative("path").stringValue = binding.path;
                }
            }

            _customModelsObject.ApplyModifiedProperties();

            var warnings = new List<string>();
            var hasModel = HasBinding(entryProp, SherpaONNXModelFileKey.Model);
            var hasTokens = HasBinding(entryProp, SherpaONNXModelFileKey.Tokens);
            if (!hasModel)
            {
                warnings.Add(SherpaONNXLocalization.Tr(
                    SherpaONNXL10n.Settings.CustomModelsImportWarnMissingModel,
                    "Model file (.onnx) not detected. Please bind it manually."));
            }
            if (!hasTokens)
            {
                warnings.Add(SherpaONNXLocalization.Tr(
                    SherpaONNXL10n.Settings.CustomModelsImportWarnMissingTokens,
                    "Tokens file not detected. Please bind tokens.txt manually if required."));
            }

            var success = SherpaONNXLocalization.Tr(
                SherpaONNXL10n.Settings.CustomModelsImportSuccess,
                "Custom model entry created.");
            if (warnings.Count > 0)
            {
                success += "\n\n" + string.Join("\n", warnings);
            }

            EditorUtility.DisplayDialog(dialogTitle, success, "OK");
            BuildCustomCatalogList();
            BuildUi();
        }

        private static bool TryParseModuleTypeFromFolder(string folderName, out SherpaONNXModuleType moduleType)
        {
            moduleType = SherpaONNXModuleType.Undefined;
            if (string.IsNullOrWhiteSpace(folderName))
            {
                return false;
            }

            var normalized = folderName.Trim().ToLowerInvariant();
            foreach (SherpaONNXModuleType candidate in Enum.GetValues(typeof(SherpaONNXModuleType)))
            {
                if (candidate == SherpaONNXModuleType.Undefined)
                {
                    continue;
                }

                var kebab = System.Text.RegularExpressions.Regex.Replace(candidate.ToString(), @"([a-z])([A-Z])", "$1-$2").ToLowerInvariant();
                if (string.Equals(kebab, normalized, StringComparison.OrdinalIgnoreCase))
                {
                    moduleType = candidate;
                    return true;
                }
            }

            return false;
        }

        private static string InferModelTypeHint(SherpaONNXModuleType moduleType, string modelId)
        {
            if (string.IsNullOrWhiteSpace(modelId))
            {
                return string.Empty;
            }

            var lower = modelId.ToLowerInvariant();
            switch (moduleType)
            {
                case SherpaONNXModuleType.SpeechSynthesis:
                    if (lower.Contains("vits")) return "Vits";
                    if (lower.Contains("matcha")) return "Matcha";
                    if (lower.Contains("kokoro")) return "Kokoro";
                    if (lower.Contains("kitten")) return "KittenTTS";
                    if (lower.Contains("zipvoice")) return "ZipVoice";
                    break;
                case SherpaONNXModuleType.VoiceActivityDetection:
                    if (lower.Contains("silero")) return "SileroVad";
                    if (lower.Contains("ten")) return "TenVad";
                    break;
                case SherpaONNXModuleType.SpokenLanguageIdentification:
                    if (lower.Contains("whisper")) return "Whisper";
                    break;
                case SherpaONNXModuleType.AudioTagging:
                    if (lower.Contains("ced")) return "Ced";
                    if (lower.Contains("zipformer")) return "Zipformer";
                    break;
                default:
                    break;
            }

            return string.Empty;
        }

        private static bool HasBinding(SerializedProperty entryProp, SherpaONNXModelFileKey key)
        {
            var bindingsProp = entryProp.FindPropertyRelative("fileBindings");
            if (bindingsProp == null || !bindingsProp.isArray)
            {
                return false;
            }

            for (int i = 0; i < bindingsProp.arraySize; i++)
            {
                var binding = bindingsProp.GetArrayElementAtIndex(i);
                var keyProp = binding.FindPropertyRelative("key");
                if (keyProp != null && keyProp.enumValueIndex == (int)key)
                {
                    return true;
                }
            }

            return false;
        }

        private static List<(SherpaONNXModelFileKey key, string path)> CollectBindings(string folder)
        {
            var results = new List<(SherpaONNXModelFileKey key, string path)>();
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            {
                return results;
            }

            var seen = new HashSet<SherpaONNXModelFileKey>();
            var entries = Directory.EnumerateFileSystemEntries(folder, "*", SearchOption.AllDirectories);
            foreach (var entry in entries)
            {
                var relative = GetRelativePath(folder, entry);
                if (string.IsNullOrEmpty(relative))
                {
                    continue;
                }

                var isDir = Directory.Exists(entry);
                var key = MapBindingKey(Path.GetFileName(entry), relative, isDir);
                if (key == SherpaONNXModelFileKey.None)
                {
                    continue;
                }

                if (key == SherpaONNXModelFileKey.RuleFsts || key == SherpaONNXModelFileKey.RuleFars)
                {
                    results.Add((key, relative));
                    continue;
                }

                if (seen.Add(key))
                {
                    results.Add((key, relative));
                }
            }

            return results;
        }

        private static string GetRelativePath(string root, string fullPath)
        {
            if (string.IsNullOrEmpty(root) || string.IsNullOrEmpty(fullPath))
            {
                return string.Empty;
            }

            var normalizedRoot = root.Replace('\\', '/').TrimEnd('/') + "/";
            var normalizedPath = fullPath.Replace('\\', '/');
            if (!normalizedPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            return normalizedPath.Substring(normalizedRoot.Length);
        }

        private static SherpaONNXModelFileKey MapBindingKey(string name, string relativePath, bool isDirectory)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return SherpaONNXModelFileKey.None;
            }

            var lowerName = name.ToLowerInvariant();
            var lowerPath = relativePath.ToLowerInvariant();

            if (isDirectory)
            {
                if (lowerName.Contains("dict")) return SherpaONNXModelFileKey.DictDir;
                if (lowerName.Contains("espeak-ng-data") || lowerName == "data") return SherpaONNXModelFileKey.DataDir;
                if (lowerName.Contains("tokenizer") || lowerName.Contains("qwen")) return SherpaONNXModelFileKey.Tokenizer;
                if (lowerName.Contains("voices")) return SherpaONNXModelFileKey.Voices;
                return SherpaONNXModelFileKey.None;
            }

            var ext = Path.GetExtension(lowerName);
            if (ext == ".fst") return SherpaONNXModelFileKey.RuleFsts;
            if (ext == ".far") return SherpaONNXModelFileKey.RuleFars;

            if (lowerName.Contains("tokens")) return SherpaONNXModelFileKey.Tokens;
            if (lowerName.Contains("lexicon")) return SherpaONNXModelFileKey.Lexicon;
            if (lowerName.Contains("vocos") || lowerName.Contains("vocoder")) return SherpaONNXModelFileKey.Vocoder;
            if (lowerName.Contains("acoustic") || lowerName.Contains("matcha")) return SherpaONNXModelFileKey.AcousticModel;
            if (lowerName.Contains("fm") || lowerName.Contains("flow")) return SherpaONNXModelFileKey.FlowMatchingModel;
            if (lowerName.Contains("text")) return SherpaONNXModelFileKey.TextModel;
            if (lowerName.Contains("preprocess")) return SherpaONNXModelFileKey.Preprocessor;
            if (lowerName.Contains("cached")) return SherpaONNXModelFileKey.CachedDecoder;
            if (lowerName.Contains("uncached")) return SherpaONNXModelFileKey.UncachedDecoder;
            if (lowerName.Contains("embedding")) return SherpaONNXModelFileKey.Embedding;
            if (lowerName.Contains("tokenizer")) return SherpaONNXModelFileKey.Tokenizer;
            if (lowerName.Contains("llm")) return SherpaONNXModelFileKey.Llm;
            if (lowerName.Contains("adaptor") || lowerName.Contains("adapter")) return SherpaONNXModelFileKey.EncoderAdaptor;
            if (lowerName.Contains("labels")) return SherpaONNXModelFileKey.Labels;
            if (lowerName.Contains("keywords")) return SherpaONNXModelFileKey.Keywords;
            if (lowerName.Contains("hotwords")) return SherpaONNXModelFileKey.Hotwords;
            if (lowerName.Contains("pinyin")) return SherpaONNXModelFileKey.Pinyin;
            if (lowerName.Contains("silero")) return SherpaONNXModelFileKey.SileroVad;
            if (lowerName.Contains("ten")) return SherpaONNXModelFileKey.TenVad;
            if (lowerName.Contains("tdnn")) return SherpaONNXModelFileKey.Tdnn;
            if (lowerName.Contains("gtcrn")) return SherpaONNXModelFileKey.Gtcrn;
            if (lowerName.Contains("ced")) return SherpaONNXModelFileKey.Ced;
            if (lowerName.Contains("zipformer")) return SherpaONNXModelFileKey.Zipformer;
            if (lowerName.Contains("encoder") || lowerName.Contains("encode")) return SherpaONNXModelFileKey.Encoder;
            if (lowerName.Contains("decoder")) return SherpaONNXModelFileKey.Decoder;
            if (lowerName.Contains("joiner")) return SherpaONNXModelFileKey.Joiner;

            if (ext == ".onnx") return SherpaONNXModelFileKey.Model;

            return SherpaONNXModelFileKey.None;
        }

        private sealed class ModuleTypePickerWindow : EditorWindow
        {
            private static readonly List<SherpaONNXModuleType> s_Options = BuildOptions();
            private static readonly string[] s_Labels = s_Options.ConvertAll(t => t.ToString()).ToArray();
            private Action<SherpaONNXModuleType> _onConfirm;
            private int _selectedIndex;

            public static void Show(string title, Action<SherpaONNXModuleType> onConfirm)
            {
                var window = CreateInstance<ModuleTypePickerWindow>();
                window.titleContent = new GUIContent(title);
                window._onConfirm = onConfirm;
                window._selectedIndex = 0;
                window.minSize = new Vector2(320f, 120f);
                window.maxSize = new Vector2(420f, 160f);
                window.ShowUtility();
            }

            private void OnGUI()
            {
                EditorGUILayout.Space(6);
                EditorGUILayout.LabelField(
                    SherpaONNXLocalization.Tr(
                        SherpaONNXL10n.Settings.CustomModelsImportSelectModuleLabel,
                        "Module Type"),
                    EditorStyles.boldLabel);
                EditorGUILayout.Space(2);
                _selectedIndex = EditorGUILayout.Popup(_selectedIndex, s_Labels);
                EditorGUILayout.Space(8);

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button(
                        SherpaONNXLocalization.Tr(
                            SherpaONNXL10n.Settings.CustomModelsImportSelectModuleConfirm,
                            "Create"),
                        GUILayout.Width(90)))
                    {
                        _onConfirm?.Invoke(s_Options[_selectedIndex]);
                        Close();
                    }
                    if (GUILayout.Button(
                        SherpaONNXLocalization.Tr(
                            SherpaONNXL10n.Common.ButtonCancel,
                            "Cancel"),
                        GUILayout.Width(90)))
                    {
                        Close();
                    }
                }
            }

            private static List<SherpaONNXModuleType> BuildOptions()
            {
                var list = new List<SherpaONNXModuleType>();
                foreach (SherpaONNXModuleType candidate in Enum.GetValues(typeof(SherpaONNXModuleType)))
                {
                    if (candidate == SherpaONNXModuleType.Undefined)
                    {
                        continue;
                    }
                    list.Add(candidate);
                }
                return list;
            }
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

        private PropertyField CreatePropertyField(SerializedObject serializedObject, string propertyName, string labelKey, string labelFallback, string tooltipKey, string tooltipFallback)
        {
            if (serializedObject == null)
            {
                return new PropertyField();
            }

            var prop = serializedObject.FindProperty(propertyName);
            var field = new PropertyField(
                prop,
                SherpaONNXLocalization.Tr(labelKey, labelFallback))
            {
                tooltip = SherpaONNXLocalization.Tr(tooltipKey, tooltipFallback)
            };
            field.Bind(serializedObject);
            field.style.marginBottom = 4;
            field.style.flexShrink = 0;
            return field;
        }

        private IMGUIContainer CreateIMGUIPropertyField(SerializedObject serializedObject, string propertyName, string labelKey, string labelFallback, string tooltipKey, string tooltipFallback)
        {
            var container = new IMGUIContainer(() =>
            {
                if (serializedObject == null)
                {
                    return;
                }

                serializedObject.Update();
                var prop = serializedObject.FindProperty(propertyName);
                if (prop == null)
                {
                    return;
                }

                var content = new GUIContent(
                    SherpaONNXLocalization.Tr(labelKey, labelFallback),
                    SherpaONNXLocalization.Tr(tooltipKey, tooltipFallback));
                EditorGUILayout.PropertyField(prop, content, true);
                serializedObject.ApplyModifiedProperties();
            });

            container.style.marginBottom = 4;
            container.style.flexShrink = 0;
            return container;
        }

        private VisualElement CreateCustomCatalogListElement()
        {
            var container = new IMGUIContainer(() =>
            {
                if (_customModelsObject == null)
                {
                    return;
                }

                _customModelsObject.Update();
                _customCatalogList?.DoLayoutList();
                _customModelsObject.ApplyModifiedProperties();
            });

            container.style.marginBottom = 4;
            container.style.flexShrink = 0;
            return container;
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
                        SherpaONNXRuntimeSettings.AutoDeleteCorruptedModelsPropertyName,
                        SherpaONNXL10n.Settings.AutoDeleteCorruptedLabel,
                        "Auto-delete corrupted models",
                        SherpaONNXL10n.Settings.AutoDeleteCorruptedTooltip,
                        "When enabled, corrupted model folders are deleted after initialization or verification failures.");
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
            card.style.flexDirection = FlexDirection.Column;
            card.style.flexShrink = 0;
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
