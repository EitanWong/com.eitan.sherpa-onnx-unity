#if UNITY_EDITOR

namespace Eitan.SherpaONNXUnity.Editor
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Threading;
    using UnityEngine;
    using UnityEditor;
    using UnityEditor.IMGUI.Controls;
    using UnityEngine.Networking;
    using Eitan.SherpaONNXUnity.Runtime;
    using Eitan.SherpaONNXUnity.Editor.Localization;
    using Eitan.SherpaONNXUnity.Runtime.Utilities;

    /// <summary>
    /// SherpaONNX Model Manager - A clean, performant editor window for browsing,
    /// downloading, and managing SherpaONNX models.
    /// </summary>
    public sealed class SherpaONNXModelsEditorWindow : EditorWindow
    {
        #region Constants

        private static class LayoutConstants
        {
            // Toolbar
            public const float ToolbarHeight = 22f;
            public const float SmallButtonWidth = 28f;
            public const float MediumButtonWidth = 60f;

            // Filter bar
            public const float SearchFieldMinWidth = 150f;
            public const float SearchFieldMaxWidth = 300f;
            public const float FilterDropdownWidth = 120f;
            public const float FilterLabelWidth = 50f;
            public const float FilterSpacing = 6f;

            // Model cards
            public const float CardPadding = 10f;
            public const float CardMarginBottom = 4f;
            public const float CardMinHeight = 56f;
            public const float IconSize = 18f;
            public const float StatusBadgeWidth = 85f;
            public const float ActionButtonWidth = 70f;
            public const float MenuButtonWidth = 24f;
            public const float RowSpacing = 4f;
            public const float ElementSpacing = 6f;

            // Progress
            public const float ProgressBarHeight = 16f;

            // Virtualization
            public const float VirtualizationBuffer = 200f;

            // Downloads panel
            public const float DownloadsPanelMaxHeight = 150f;
        }

        private static class ColorPalette
        {
            public static readonly Color BadgeDownloaded = new Color(0.18f, 0.55f, 0.18f, 1f);
            public static readonly Color BadgeNotDownloaded = new Color(0.45f, 0.45f, 0.45f, 1f);
            public static readonly Color BadgeChecking = new Color(0.65f, 0.50f, 0.12f, 1f);
            public static readonly Color BadgeFailed = new Color(0.70f, 0.20f, 0.20f, 1f);
            public static readonly Color MetaTextColor = new Color(0.55f, 0.55f, 0.55f, 1f);
        }

        #endregion

        #region Cached Styles

        private sealed class StyleCache
        {
            public GUIStyle HeaderLabel;
            public GUIStyle CardBackground;
            public GUIStyle ModelNameLabel;
            public GUIStyle MetaLabel;
            public GUIStyle BadgeLabel;
            public GUIStyle ActionButton;
            public GUIStyle ToolbarSearchField;
            public GUIStyle FoldoutHeader;

            private bool _isInitialized;
            private static Texture2D _badgeBackgroundTexture;

            public bool IsInitialized => _isInitialized;

            public void Initialize()
            {
                if (_isInitialized)
                {
                    return;
                }


                if (EditorStyles.boldLabel == null)
                {
                    return; // Not ready yet
                }


                try
                {
                    HeaderLabel = new GUIStyle(EditorStyles.boldLabel)
                    {
                        fontSize = 12,
                        padding = new RectOffset(2, 2, 2, 2)
                    };

                    CardBackground = new GUIStyle("HelpBox")
                    {
                        padding = new RectOffset(
                            (int)LayoutConstants.CardPadding,
                            (int)LayoutConstants.CardPadding,
                            (int)LayoutConstants.CardPadding,
                            (int)LayoutConstants.CardPadding
                        ),
                        margin = new RectOffset(0, 0, 0, (int)LayoutConstants.CardMarginBottom)
                    };

                    ModelNameLabel = new GUIStyle(EditorStyles.boldLabel)
                    {
                        fontSize = 11,
                        wordWrap = false,
                        clipping = TextClipping.Clip,
                        alignment = TextAnchor.MiddleLeft
                    };

                    MetaLabel = new GUIStyle(EditorStyles.miniLabel)
                    {
                        fontSize = 10,
                        normal = { textColor = ColorPalette.MetaTextColor },
                        wordWrap = false,
                        clipping = TextClipping.Clip
                    };

                    BadgeLabel = new GUIStyle(EditorStyles.miniLabel)
                    {
                        alignment = TextAnchor.MiddleCenter,
                        fontSize = 9,
                        fontStyle = FontStyle.Bold,
                        normal = { textColor = Color.white },
                        padding = new RectOffset(4, 4, 2, 2)
                    };

                    ActionButton = new GUIStyle(EditorStyles.miniButton)
                    {
                        fontSize = 10,
                        fixedHeight = 18f,
                        padding = new RectOffset(6, 6, 2, 2)
                    };

                    ToolbarSearchField = new GUIStyle("ToolbarSearchTextField");

                    FoldoutHeader = new GUIStyle(EditorStyles.foldoutHeader)
                    {
                        fontStyle = FontStyle.Bold
                    };

                    _isInitialized = true;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[SherpaONNXModelsEditorWindow] Style initialization failed: {ex.Message}");
                    _isInitialized = false;
                }
            }

            public static Texture2D GetBadgeBackground()
            {
                if (_badgeBackgroundTexture == null)
                {
                    _badgeBackgroundTexture = new Texture2D(1, 1);
                    _badgeBackgroundTexture.SetPixel(0, 0, Color.white);
                    _badgeBackgroundTexture.Apply();
                    _badgeBackgroundTexture.hideFlags = HideFlags.DontSave;
                }
                return _badgeBackgroundTexture;
            }
        }

        #endregion

        #region Cached Icons

        private sealed class IconCache
        {
            private GUIContent _refresh;
            private GUIContent _menu;
            private GUIContent _folder;
            private GUIContent _download;
            private GUIContent _delete;
            private GUIContent _microphone;
            private GUIContent _audioSource;
            private GUIContent _audioMixer;
            private GUIContent _scriptableObject;

            private bool _isInitialized;
            public bool IsInitialized => _isInitialized;

            public GUIContent Refresh => _refresh;
            public GUIContent Menu => _menu;
            public GUIContent Folder => _folder;
            public GUIContent Download => _download;
            public GUIContent Delete => _delete;
            public GUIContent Microphone => _microphone;
            public GUIContent AudioSource => _audioSource;
            public GUIContent AudioMixer => _audioMixer;
            public GUIContent ScriptableObject => _scriptableObject;

            public void Initialize()
            {
                if (_isInitialized)
                {
                    return;
                }


                _refresh = LoadIcon("d_Refresh", "Refresh");
                _menu = LoadIcon("d__Menu", "_Menu", "≡");
                _folder = LoadIcon("d_Folder Icon", "Folder Icon");
                _download = LoadIcon("CloudConnect", "CloudConnect", "↓");
                _delete = LoadIcon("d_TreeEditor.Trash", "TreeEditor.Trash");
                _microphone = LoadIcon("d_Microphone Icon", "Microphone Icon");
                _audioSource = LoadIcon("d_AudioSource Icon", "AudioSource Icon");
                _audioMixer = LoadIcon("d_Audio Mixer", "Audio Mixer");
                _scriptableObject = LoadIcon("d_ScriptableObject Icon", "ScriptableObject Icon");

                _isInitialized = true;
            }

            private static GUIContent LoadIcon(string darkIconName, string lightIconName, string fallbackText = "•")
            {
                // Try dark theme icon first
                var content = EditorGUIUtility.IconContent(darkIconName);
                if (content != null && content.image != null)
                {
                    return new GUIContent(content.image);
                }

                // Try light theme icon
                content = EditorGUIUtility.IconContent(lightIconName);
                if (content != null && content.image != null)
                {
                    return new GUIContent(content.image);
                }

                // Return text fallback
                return new GUIContent(fallbackText);
            }
        }

        #endregion

        #region Localized Content Cache

        private sealed class LocalizedContentCache
        {
            // Window
            public GUIContent WindowTitle;
            public GUIContent HeaderTitle;

            // Toolbar
            public GUIContent RefreshTooltip;
            public GUIContent RescanButton;
            public GUIContent ClearButton;

            // Filters
            public GUIContent SearchPlaceholder;
            public GUIContent CategoryLabel;
            public GUIContent LanguageLabel;
            public GUIContent FilterAll;

            // Actions
            public GUIContent DownloadButton;
            public GUIContent RedownloadButton;
            public GUIContent DeleteButton;
            public GUIContent CancelButton;
            public GUIContent RevealButton;

            // Context menu
            public GUIContent CopyModelIdItem;
            public GUIContent CopyUrlItem;
            public GUIContent RescanStatusItem;

            // Status badges
            public GUIContent StatusReady;
            public GUIContent StatusNotDownloaded;
            public GUIContent StatusChecking;
            public GUIContent StatusFailed;

            // Sections
            public GUIContent ActiveDownloadsHeader;

            // Messages
            public GUIContent NoModelsMessage;
            public GUIContent LoadingMessage;

            // Dialogs
            public string DeleteDialogTitle;
            public string DeleteDialogConfirm;
            public string DeleteDialogCancel;

            private bool _isBuilt;
            public bool IsBuilt => _isBuilt;

            public void Build()
            {
                WindowTitle = new GUIContent(Tr(SherpaONNXL10n.Models.WindowTitle, "SherpaONNX Models"));
                HeaderTitle = new GUIContent(Tr(SherpaONNXL10n.Models.Header, "Model Manager"));

                RefreshTooltip = new GUIContent(string.Empty, Tr(SherpaONNXL10n.Common.TooltipRefresh, "Reload manifest from server"));
                RescanButton = new GUIContent(Tr(SherpaONNXL10n.Common.ButtonRescan, "Rescan"));
                ClearButton = new GUIContent(Tr(SherpaONNXL10n.Common.ButtonClear, "Clear"));

                SearchPlaceholder = new GUIContent(Tr(SherpaONNXL10n.Common.LabelSearch, "Search..."));
                CategoryLabel = new GUIContent(Tr(SherpaONNXL10n.Common.LabelCategory, "Category"));
                LanguageLabel = new GUIContent(Tr(SherpaONNXL10n.Common.LabelLanguage, "Language"));
                FilterAll = new GUIContent(Tr(SherpaONNXL10n.Common.FilterAll, "All"));

                DownloadButton = new GUIContent(Tr(SherpaONNXL10n.Models.ButtonDownload, "Download"));
                RedownloadButton = new GUIContent(Tr(SherpaONNXL10n.Models.ButtonRedownload, "Retry"));
                DeleteButton = new GUIContent(Tr(SherpaONNXL10n.Models.ButtonDelete, "Delete"));
                CancelButton = new GUIContent(Tr(SherpaONNXL10n.Common.ButtonCancel, "Cancel"));
                RevealButton = new GUIContent(Tr(SherpaONNXL10n.Models.ButtonReveal, "Reveal"));

                CopyModelIdItem = new GUIContent(Tr(SherpaONNXL10n.Models.ButtonCopyName, "Copy Model ID"));
                CopyUrlItem = new GUIContent(Tr(SherpaONNXL10n.Models.ButtonCopyUrl, "Copy Download URL"));
                RescanStatusItem = new GUIContent(Tr(SherpaONNXL10n.Models.ContextRescanStatus, "Rescan Status"));

                StatusReady = new GUIContent(Tr(SherpaONNXL10n.Models.StatusDownloaded, "Ready"));
                StatusNotDownloaded = new GUIContent(Tr(SherpaONNXL10n.Models.StatusNotDownloaded, "Not Downloaded"));
                StatusChecking = new GUIContent(Tr(SherpaONNXL10n.Models.StatusChecking, "Checking..."));
                StatusFailed = new GUIContent(Tr(SherpaONNXL10n.Models.StatusVerifyFailed, "Failed"));

                ActiveDownloadsHeader = new GUIContent(Tr(SherpaONNXL10n.Models.LabelActiveDownloads, "Active Downloads"));

                NoModelsMessage = new GUIContent(Tr(SherpaONNXL10n.Models.HelpNoMatches, "No models match the current filters."));
                LoadingMessage = new GUIContent(Tr(SherpaONNXL10n.Models.LoadingRemote, "Loading model manifest..."));

                DeleteDialogTitle = Tr(SherpaONNXL10n.Models.DialogDeleteTitle, "Delete Model");
                DeleteDialogConfirm = Tr(SherpaONNXL10n.Models.DialogDeleteConfirm, "Delete");
                DeleteDialogCancel = Tr(SherpaONNXL10n.Models.DialogDeleteCancel, "Cancel");

                _isBuilt = true;
            }

            private static string Tr(string key, string fallback)
            {
                try
                {
                    return SherpaONNXLocalization.Tr(key, fallback);
                }
                catch
                {
                    return fallback;
                }
            }
        }

        #endregion

        #region Data Models

        private enum ModelStatus
        {
            Unknown,
            Checking,
            Downloaded,
            NotDownloaded,
            Failed
        }

        private enum DownloadPhase
        {
            None,
            Downloading,
            Extracting,
            Verifying,
            Completed,
            Failed,
            Canceled
        }

        private sealed class ModelEntry
        {
            public SherpaONNXModelMetadata Metadata;
            public ModelStatus Status = ModelStatus.Unknown;
            public List<string> Languages;
            public string StatusMessage;
            public float CachedCardHeight;
            public bool IsHeightValid;

            public string ModelId => Metadata?.modelId ?? string.Empty;
            public SherpaONNXModuleType ModuleType => Metadata?.moduleType ?? SherpaONNXModuleType.Undefined;
            public string DownloadUrl => Metadata?.downloadUrl ?? string.Empty;
            public bool HasDownloadUrl => !string.IsNullOrEmpty(DownloadUrl);
        }

        private sealed class DownloadOperation : IDisposable
        {
            public SherpaONNXModelMetadata Metadata;
            public DownloadPhase Phase;
            public float Progress;
            public string StatusText;
            public CancellationTokenSource CancellationSource;
            public UnityWebRequest WebRequest;
            public string ArchivePath;
            public string TempArchivePath;
            public string ModuleDirectory;
            public bool IsCompressed;

            private EditorApplication.CallbackFunction _updateCallback;
            private bool _isDisposed;

            public bool IsCanceled => CancellationSource?.IsCancellationRequested ?? true;

            public bool IsActive => Phase != DownloadPhase.None &&
                                    Phase != DownloadPhase.Completed &&
                                    Phase != DownloadPhase.Failed &&
                                    Phase != DownloadPhase.Canceled;

            public string ModelId => Metadata?.modelId ?? "Unknown";

            public void SetUpdateCallback(EditorApplication.CallbackFunction callback)
            {
                RemoveUpdateCallback();
                _updateCallback = callback;
                if (_updateCallback != null)
                {
                    EditorApplication.update += _updateCallback;
                }
            }

            public void RemoveUpdateCallback()
            {
                if (_updateCallback != null)
                {
                    EditorApplication.update -= _updateCallback;
                    _updateCallback = null;
                }
            }

            public void Dispose()
            {
                if (_isDisposed)
                {
                    return;
                }


                _isDisposed = true;

                RemoveUpdateCallback();

                try { CancellationSource?.Cancel(); } catch { }
                try { CancellationSource?.Dispose(); } catch { }
                CancellationSource = null;

                try { WebRequest?.Abort(); } catch { }
                try { WebRequest?.Dispose(); } catch { }
                WebRequest = null;
            }
        }

        private sealed class FilterState
        {
            public string SearchQuery = string.Empty;
            public int CategoryIndex;
            public int LanguageIndex;

            private string[] _categoryValues = Array.Empty<string>();
            private string[] _languageValues = Array.Empty<string>();
            private GUIContent[] _categoryDisplays = Array.Empty<GUIContent>();
            private GUIContent[] _languageDisplays = Array.Empty<GUIContent>();

            public GUIContent[] CategoryDisplays => _categoryDisplays;
            public GUIContent[] LanguageDisplays => _languageDisplays;
            public string SelectedCategory => CategoryIndex > 0 && CategoryIndex < _categoryValues.Length
                ? _categoryValues[CategoryIndex] : null;
            public string SelectedLanguage => LanguageIndex > 0 && LanguageIndex < _languageValues.Length
                ? _languageValues[LanguageIndex] : null;

            public bool HasActiveFilters => !string.IsNullOrEmpty(SearchQuery) || CategoryIndex > 0 || LanguageIndex > 0;

            public void RebuildOptions(
                List<ModelEntry> allModels,
                Func<SherpaONNXModuleType, string> categoryFormatter,
                Func<string, string> languageFormatter,
                string allLabel)
            {
                // Build category list
                var categoryList = new List<string> { string.Empty };
                var categoryDisplayList = new List<GUIContent> { new GUIContent(allLabel) };

                var moduleTypes = (SherpaONNXModuleType[])Enum.GetValues(typeof(SherpaONNXModuleType));
                foreach (var moduleType in moduleTypes)
                {
                    if (moduleType == SherpaONNXModuleType.Undefined)
                    {
                        continue;
                    }


                    categoryList.Add(moduleType.ToString());
                    categoryDisplayList.Add(new GUIContent(categoryFormatter(moduleType)));
                }

                _categoryValues = categoryList.ToArray();
                _categoryDisplays = categoryDisplayList.ToArray();

                // Build language list from actual model data
                var languageSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var entry in allModels)
                {
                    if (entry.Languages == null)
                    {
                        continue;
                    }


                    foreach (var lang in entry.Languages)
                    {
                        if (!string.IsNullOrEmpty(lang) && lang != "other")
                        {
                            languageSet.Add(lang);
                        }
                    }
                }

                var languageList = new List<string> { string.Empty };
                var languageDisplayList = new List<GUIContent> { new GUIContent(allLabel) };

                var sortedLanguages = new List<string>(languageSet);
                sortedLanguages.Sort(StringComparer.OrdinalIgnoreCase);

                foreach (var lang in sortedLanguages)
                {
                    languageList.Add(lang);
                    languageDisplayList.Add(new GUIContent(languageFormatter(lang)));
                }

                _languageValues = languageList.ToArray();
                _languageDisplays = languageDisplayList.ToArray();

                // Clamp current indices
                CategoryIndex = Mathf.Clamp(CategoryIndex, 0, Mathf.Max(0, _categoryValues.Length - 1));
                LanguageIndex = Mathf.Clamp(LanguageIndex, 0, Mathf.Max(0, _languageValues.Length - 1));
            }

            public void Reset()
            {
                SearchQuery = string.Empty;
                CategoryIndex = 0;
                LanguageIndex = 0;
                GUI.FocusControl(null);
            }

            public bool Matches(ModelEntry entry)
            {
                if (entry?.Metadata == null)
                {
                    return false;
                }

                // Category filter

                var selectedCategory = SelectedCategory;
                if (!string.IsNullOrEmpty(selectedCategory))
                {
                    if (entry.ModuleType.ToString() != selectedCategory)
                    {
                        return false;
                    }
                }

                // Language filter
                var selectedLanguage = SelectedLanguage;
                if (!string.IsNullOrEmpty(selectedLanguage))
                {
                    if (entry.Languages == null || !entry.Languages.Contains(selectedLanguage))
                    {
                        return false;
                    }
                }

                // Search filter
                if (!string.IsNullOrWhiteSpace(SearchQuery))
                {
                    if (entry.ModelId.IndexOf(SearchQuery, StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        #endregion

        #region State Fields

        // Cached resources
        private StyleCache _styles;
        private IconCache _icons;
        private LocalizedContentCache _content;
        private SearchField _searchField;

        // Data
        private readonly List<ModelEntry> _allModels = new List<ModelEntry>();
        private readonly List<ModelEntry> _filteredModels = new List<ModelEntry>();
        private readonly List<DownloadOperation> _activeDownloads = new List<DownloadOperation>();
        private readonly FilterState _filter = new FilterState();
        private readonly List<string> _languageDisplayBuffer = new List<string>(8);
        private readonly HashSet<string> _languageCodeSeen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _languageDisplaySeen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // UI state
        private Vector2 _scrollPosition;
        private bool _showDownloadsPanel = true;
        private bool _isLoadingManifest;
        private bool _needsFilterRebuild = true;
        private bool _needsRepaint;
        private double _lastRepaintTime;

        // Async operations
        private CancellationTokenSource _manifestLoadCts;
        private EditorApplication.CallbackFunction _repaintPumpCallback;

        // Reusable string builder to avoid allocations
        private readonly StringBuilder _stringBuilder = new StringBuilder(256);
        private readonly StringBuilder _languageStringBuilder = new StringBuilder(128);
        private readonly GUIContent _tempMetaContent = new GUIContent();

        // Initialization tracking
        private bool _isFullyInitialized;

        #endregion

        #region Window Lifecycle

        [MenuItem("Window/SherpaONNX/Model Manager", priority = 100)]
        private static void ShowWindow()
        {
            var window = GetWindow<SherpaONNXModelsEditorWindow>();
            window.minSize = new Vector2(450, 350);
            window.Show();
        }

        private void OnEnable()
        {
            InitializeCaches();
            InstallRepaintPump();
            SubscribeToEvents();
            LoadManifestData();
        }

        private void OnDisable()
        {
            UnsubscribeFromEvents();
            UninstallRepaintPump();
            CancelManifestLoad();
            CancelAllDownloads();
        }

        private void OnDestroy()
        {
            OnDisable();
        }

        private void InitializeCaches()
        {
            _styles = new StyleCache();
            _icons = new IconCache();
            _content = new LocalizedContentCache();
            _searchField = new SearchField();
        }

        private void EnsureInitialized()
        {
            if (_styles == null)
            {
                _styles = new StyleCache();
            }

            if (_icons == null)
            {
                _icons = new IconCache();
            }

            if (_content == null)
            {
                _content = new LocalizedContentCache();
            }

            if (_searchField == null)
            {
                _searchField = new SearchField();
            }

            if (!_styles.IsInitialized)
            {
                _styles.Initialize();
            }

            if (!_icons.IsInitialized)
            {
                _icons.Initialize();
            }

            if (!_content.IsBuilt)
            {
                _content.Build();
            }


            if (_styles.IsInitialized && _icons.IsInitialized && _content.IsBuilt)
            {
                if (!_isFullyInitialized)
                {
                    _isFullyInitialized = true;
                    titleContent = _content.WindowTitle ?? new GUIContent("SherpaONNX Models");
                }
            }
        }

        private void SubscribeToEvents()
        {
            SherpaONNXLocalization.LanguageChanged += OnLanguageChanged;
            SherpaONNXModelRegistry.Instance.Initialized += OnRegistryInitialized;
        }

        private void UnsubscribeFromEvents()
        {
            SherpaONNXLocalization.LanguageChanged -= OnLanguageChanged;
            if (SherpaONNXModelRegistry.Instance != null)
            {
                SherpaONNXModelRegistry.Instance.Initialized -= OnRegistryInitialized;
            }
        }

        private void OnLanguageChanged()
        {
            if (_content != null)
            {
                _content.Build();
                titleContent = _content.WindowTitle ?? new GUIContent("SherpaONNX Models");
            }
            _needsFilterRebuild = true;
            InvalidateAllCardHeights();
            RequestRepaint();
        }

        private void OnRegistryInitialized()
        {
            EditorApplication.delayCall += () =>
            {
                _isLoadingManifest = false;
                LoadManifestData();
                RequestRepaint();
            };
        }

        #endregion

        #region Repaint Management

        private void InstallRepaintPump()
        {
            if (_repaintPumpCallback != null)
            {
                return;
            }

            _repaintPumpCallback = () =>
            {
                bool hasActivity = _needsRepaint || HasActiveDownloads();
                if (!hasActivity)
                {
                    return;
                }

                double now = EditorApplication.timeSinceStartup;
                const double minInterval = 1.0 / 30.0; // Max 30 FPS for editor

                if (now - _lastRepaintTime < minInterval)
                {
                    return;
                }


                _lastRepaintTime = now;
                _needsRepaint = false;
                Repaint();
            };

            EditorApplication.update += _repaintPumpCallback;
        }

        private void UninstallRepaintPump()
        {
            if (_repaintPumpCallback != null)
            {
                EditorApplication.update -= _repaintPumpCallback;
                _repaintPumpCallback = null;
            }
        }

        private void RequestRepaint()
        {
            _needsRepaint = true;
        }

        private bool HasActiveDownloads()
        {
            for (int i = 0; i < _activeDownloads.Count; i++)
            {
                if (_activeDownloads[i].IsActive)
                {
                    return true;
                }

            }
            return false;
        }

        private void InvalidateAllCardHeights()
        {
            for (int i = 0; i < _allModels.Count; i++)
            {
                _allModels[i].IsHeightValid = false;
            }
        }

        #endregion

        #region Data Loading

        private void LoadManifestData()
        {
            _allModels.Clear();
            _filteredModels.Clear();

            var registry = SherpaONNXModelRegistry.Instance;
            if (registry == null)
            {
                _isLoadingManifest = false;
                return;
            }

            if (registry.TryGetManifest(out var manifest))
            {
                _isLoadingManifest = false;
                PopulateModelsFromManifest(manifest);
                _needsFilterRebuild = true;
                ScanAllModelStatuses();
            }
            else
            {
                _isLoadingManifest = true;
                RequestRepaint();
                StartManifestLoadAsync();
            }
        }

        private async void StartManifestLoadAsync()
        {
            CancelManifestLoad();
            _manifestLoadCts = new CancellationTokenSource();
            var token = _manifestLoadCts.Token;

            try
            {
                var manifest = await SherpaONNXModelRegistry.Instance.WaitForManifestAsync(token);

                if (token.IsCancellationRequested)
                {
                    return;
                }


                EditorApplication.delayCall += () =>
                {
                    _isLoadingManifest = false;
                    PopulateModelsFromManifest(manifest);
                    _needsFilterRebuild = true;
                    ScanAllModelStatuses();
                    RequestRepaint();
                };
            }
            catch (OperationCanceledException)
            {
                // Expected when window closes during load
            }
            catch (Exception ex)
            {
                EditorApplication.delayCall += () =>
                {
                    _isLoadingManifest = false;
                    Debug.LogWarning($"[SherpaONNXModelsEditorWindow] Failed to load manifest: {ex.Message}");
                    RequestRepaint();
                };
            }
        }

        private void CancelManifestLoad()
        {
            try { _manifestLoadCts?.Cancel(); } catch { }
            try { _manifestLoadCts?.Dispose(); } catch { }
            _manifestLoadCts = null;
        }

        private void PopulateModelsFromManifest(SherpaONNXModelManifest manifest)
        {
            if (manifest?.models == null)
            {
                return;
            }


            foreach (var meta in manifest.models)
            {
                if (meta == null)
                {
                    continue;
                }


                var entry = new ModelEntry
                {
                    Metadata = meta,
                    Languages = ParseLanguagesFromModelId(meta.modelId),
                    Status = ModelStatus.Unknown
                };
                _allModels.Add(entry);
            }
        }

        private void ForceRefreshManifest()
        {
            CancelAllDownloads();
            _allModels.Clear();
            _filteredModels.Clear();
            _isLoadingManifest = true;
            _needsFilterRebuild = true;
            RequestRepaint();

            SherpaONNXModelRegistry.Instance?.Uninitialize();
            EditorApplication.delayCall += StartManifestLoadAsync;
        }

        #endregion

        #region Status Scanning

        private void ScanAllModelStatuses()
        {
            foreach (var entry in _allModels)
            {
                ScanModelStatusAsync(entry);
            }
        }

        private async void ScanModelStatusAsync(ModelEntry entry)
        {
            if (entry?.Metadata == null)
            {
                return;
            }


            entry.Status = ModelStatus.Checking;
            entry.StatusMessage = null;
            entry.IsHeightValid = false;
            RequestRepaint();

            try
            {
                bool isDownloaded = await SherpaUtils.Prepare.CheckIsModelDownloadedAsync(entry.Metadata);
                entry.Status = isDownloaded ? ModelStatus.Downloaded : ModelStatus.NotDownloaded;
            }
            catch (Exception)
            {
                entry.Status = ModelStatus.NotDownloaded;
            }

            entry.IsHeightValid = false;
            RequestRepaint();
        }

        #endregion

        #region Main GUI

        private void OnGUI()
        {
            // Ensure all caches are initialized
            EnsureInitialized();

            if (!_isFullyInitialized || !_styles.IsInitialized)
            {
                DrawFallbackUI();
                return;
            }

            // Check for loading state
            bool isLoading = _isLoadingManifest ||
                             (SherpaONNXModelRegistry.Instance?.IsInitializing ?? false);

            if (isLoading)
            {
                DrawLoadingState();
                return;
            }

            // Rebuild filter options if needed
            if (_needsFilterRebuild)
            {
                RebuildFilterOptions();
                RebuildFilteredModelList();
                _needsFilterRebuild = false;
            }

            // Draw main UI
            DrawToolbar();
            DrawFilterBar();

            EditorGUILayout.Space(4);

            DrawModelList();
            DrawDownloadsPanel();
        }

        private void DrawFallbackUI()
        {
            GUILayout.FlexibleSpace();
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label("Initializing...", EditorStyles.centeredGreyMiniLabel);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            GUILayout.FlexibleSpace();

            // Force repaint until initialized
            if (Event.current.type == EventType.Repaint)
            {
                EditorApplication.delayCall += Repaint;
            }
        }

        private void DrawLoadingState()
        {
            var contentToShow = _content?.LoadingMessage ?? new GUIContent("Loading...");

            GUILayout.FlexibleSpace();

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            EditorGUILayout.BeginVertical(GUILayout.Width(250));

            // Spinner
            int frame = (int)(EditorApplication.timeSinceStartup * 12) % 12;
            var spinnerContent = EditorGUIUtility.IconContent($"WaitSpin{frame:00}");

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (spinnerContent != null && spinnerContent.image != null)
            {
                GUILayout.Label(spinnerContent, GUILayout.Width(32), GUILayout.Height(32));
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(8);

            EditorGUILayout.LabelField(contentToShow, EditorStyles.centeredGreyMiniLabel);

            EditorGUILayout.EndVertical();

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            GUILayout.FlexibleSpace();

            RequestRepaint();
        }

        #endregion

        #region Toolbar

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            // Header title
            GUILayout.Label(_content.HeaderTitle, _styles.HeaderLabel, GUILayout.ExpandWidth(false));

            GUILayout.FlexibleSpace();

            // Refresh button
            var refreshContent = _icons.Refresh ?? GUIContent.none;
            if (!string.IsNullOrEmpty(_content.RefreshTooltip?.tooltip))
            {
                refreshContent = new GUIContent(refreshContent.image, _content.RefreshTooltip.tooltip);
            }

            if (GUILayout.Button(refreshContent, EditorStyles.toolbarButton,
                GUILayout.Width(LayoutConstants.SmallButtonWidth)))
            {
                ForceRefreshManifest();
            }

            // Rescan button
            if (GUILayout.Button(_content.RescanButton, EditorStyles.toolbarButton,
                GUILayout.Width(LayoutConstants.MediumButtonWidth)))
            {
                ScanAllModelStatuses();
            }

            // Clear filters button
            EditorGUI.BeginDisabledGroup(!_filter.HasActiveFilters);
            if (GUILayout.Button(_content.ClearButton, EditorStyles.toolbarButton,
                GUILayout.Width(50)))
            {
                _filter.Reset();
                RebuildFilteredModelList();
            }
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.EndHorizontal();
        }

        #endregion

        #region Filter Bar

        private void DrawFilterBar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            // Search field
            float searchWidth = Mathf.Clamp(
                position.width * 0.3f,
                LayoutConstants.SearchFieldMinWidth,
                LayoutConstants.SearchFieldMaxWidth
            );

            var searchRect = GUILayoutUtility.GetRect(
                searchWidth, searchWidth,
                LayoutConstants.ToolbarHeight, LayoutConstants.ToolbarHeight,
                _styles.ToolbarSearchField
            );

            string newSearch = _searchField.OnToolbarGUI(searchRect, _filter.SearchQuery);
            if (newSearch != _filter.SearchQuery)
            {
                _filter.SearchQuery = newSearch;
                RebuildFilteredModelList();
            }

            GUILayout.Space(LayoutConstants.FilterSpacing);

            // Category filter
            GUILayout.Label(_content.CategoryLabel, EditorStyles.miniLabel,
                GUILayout.Width(LayoutConstants.FilterLabelWidth));

            int newCategoryIndex = EditorGUILayout.Popup(
                _filter.CategoryIndex,
                GetDisplayStrings(_filter.CategoryDisplays),
                EditorStyles.toolbarPopup,
                GUILayout.Width(LayoutConstants.FilterDropdownWidth)
            );

            if (newCategoryIndex != _filter.CategoryIndex)
            {
                _filter.CategoryIndex = newCategoryIndex;
                RebuildFilteredModelList();
            }

            GUILayout.Space(LayoutConstants.FilterSpacing);

            // Language filter
            GUILayout.Label(_content.LanguageLabel, EditorStyles.miniLabel,
                GUILayout.Width(LayoutConstants.FilterLabelWidth));

            int newLanguageIndex = EditorGUILayout.Popup(
                _filter.LanguageIndex,
                GetDisplayStrings(_filter.LanguageDisplays),
                EditorStyles.toolbarPopup,
                GUILayout.Width(LayoutConstants.FilterDropdownWidth)
            );

            if (newLanguageIndex != _filter.LanguageIndex)
            {
                _filter.LanguageIndex = newLanguageIndex;
                RebuildFilteredModelList();
            }

            GUILayout.FlexibleSpace();

            EditorGUILayout.EndHorizontal();
        }

        private string[] GetDisplayStrings(GUIContent[] contents)
        {
            if (contents == null || contents.Length == 0)
            {
                return new[] { "All" };
            }

            var result = new string[contents.Length];
            for (int i = 0; i < contents.Length; i++)
            {
                result[i] = contents[i]?.text ?? string.Empty;
            }
            return result;
        }

        private void RebuildFilterOptions()
        {
            _filter.RebuildOptions(
                _allModels,
                GetModuleTypeDisplayName,
                GetLanguageDisplayName,
                _content?.FilterAll?.text ?? "All"
            );
        }

        private void RebuildFilteredModelList()
        {
            _filteredModels.Clear();

            for (int i = 0; i < _allModels.Count; i++)
            {
                var entry = _allModels[i];
                if (_filter.Matches(entry))
                {
                    _filteredModels.Add(entry);
                }
            }
        }

        #endregion

        #region Model List

        private void DrawModelList()
        {
            if (_filteredModels.Count == 0)
            {
                EditorGUILayout.HelpBox(_content.NoModelsMessage?.text ?? "No models found.", MessageType.Info);
                return;
            }

            // Calculate available height
            float downloadsHeight = HasActiveDownloads() ? LayoutConstants.DownloadsPanelMaxHeight + 20 : 0;
            float availableHeight = position.height - GUILayoutUtility.GetLastRect().yMax - downloadsHeight - 10;
            availableHeight = Mathf.Max(100f, availableHeight);

            float viewWidth = position.width - 20;

            // Begin scroll view
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition, GUILayout.Height(availableHeight));

            // Simple virtualization: calculate visible range
            float scrollY = _scrollPosition.y;
            float viewBottom = scrollY + availableHeight;
            float bufferSize = LayoutConstants.VirtualizationBuffer;

            float accumulatedY = 0;
            int firstVisible = -1;
            int lastVisible = -1;

            // First pass: find visible range
            for (int i = 0; i < _filteredModels.Count; i++)
            {
                var entry = _filteredModels[i];
                float cardHeight = GetCardHeight(entry, viewWidth);
                float cardTop = accumulatedY;
                float cardBottom = accumulatedY + cardHeight;

                if (firstVisible < 0 && cardBottom >= scrollY - bufferSize)
                {
                    firstVisible = i;
                }

                if (cardTop <= viewBottom + bufferSize)
                {
                    lastVisible = i;
                }

                accumulatedY += cardHeight;
            }

            float totalHeight = accumulatedY;

            // Calculate pre-space
            float preSpace = 0;
            if (firstVisible > 0)
            {
                for (int i = 0; i < firstVisible; i++)
                {
                    preSpace += GetCardHeight(_filteredModels[i], viewWidth);
                }
            }

            // Draw pre-space
            if (preSpace > 0)
            {
                GUILayout.Space(preSpace);
            }

            // Draw visible cards
            if (firstVisible >= 0 && lastVisible >= 0)
            {
                for (int i = firstVisible; i <= lastVisible && i < _filteredModels.Count; i++)
                {
                    DrawModelCard(_filteredModels[i], viewWidth);
                }
            }

            // Calculate post-space
            float drawnHeight = 0;
            if (firstVisible >= 0 && lastVisible >= 0)
            {
                for (int i = firstVisible; i <= lastVisible && i < _filteredModels.Count; i++)
                {
                    drawnHeight += GetCardHeight(_filteredModels[i], viewWidth);
                }
            }

            float postSpace = totalHeight - preSpace - drawnHeight;
            if (postSpace > 0)
            {
                GUILayout.Space(postSpace);
            }

            EditorGUILayout.EndScrollView();
        }

        private float GetCardHeight(ModelEntry entry, float viewWidth)
        {
            if (entry.IsHeightValid)
            {
                return entry.CachedCardHeight;
            }

            float height = LayoutConstants.CardPadding * 2; // Padding
            height += EditorGUIUtility.singleLineHeight; // Name row
            height += LayoutConstants.RowSpacing;
            height += EditorGUIUtility.singleLineHeight * 0.9f; // Meta row

            // Progress bar if downloading
            var download = GetActiveDownloadForModel(entry.Metadata);
            if (download != null && download.IsActive)
            {
                height += LayoutConstants.RowSpacing;
                height += LayoutConstants.ProgressBarHeight;
            }

            // Error message
            if (entry.Status == ModelStatus.Failed && !string.IsNullOrEmpty(entry.StatusMessage))
            {
                height += LayoutConstants.RowSpacing;
                height += EditorGUIUtility.singleLineHeight * 2; // Approximate help box height
            }

            height += LayoutConstants.CardMarginBottom;

            entry.CachedCardHeight = height;
            entry.IsHeightValid = true;
            return height;
        }

        private void DrawModelCard(ModelEntry entry, float viewWidth)
        {
            if (entry?.Metadata == null)
            {
                return;
            }


            var download = GetActiveDownloadForModel(entry.Metadata);
            bool isDownloading = download?.IsActive ?? false;

            EditorGUILayout.BeginVertical(_styles.CardBackground);

            // Row 1: Icon + Name + Badge + Actions
            DrawModelCardHeader(entry, isDownloading, viewWidth);

            EditorGUILayout.Space(LayoutConstants.RowSpacing);

            // Row 2: Metadata
            DrawModelCardMetadata(entry);

            // Progress bar if downloading
            if (isDownloading)
            {
                EditorGUILayout.Space(LayoutConstants.RowSpacing);
                DrawProgressBar(download);
            }

            // Error message
            if (entry.Status == ModelStatus.Failed && !string.IsNullOrEmpty(entry.StatusMessage))
            {
                EditorGUILayout.Space(LayoutConstants.RowSpacing);
                EditorGUILayout.HelpBox(entry.StatusMessage, MessageType.Error);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawModelCardHeader(ModelEntry entry, bool isDownloading, float viewWidth)
        {
            EditorGUILayout.BeginHorizontal();

            // Module type icon
            var moduleIcon = GetModuleTypeIcon(entry.ModuleType);
            if (moduleIcon != null && moduleIcon.image != null)
            {
                GUILayout.Label(moduleIcon, GUILayout.Width(LayoutConstants.IconSize),
                    GUILayout.Height(LayoutConstants.IconSize));
            }

            GUILayout.Space(LayoutConstants.ElementSpacing);

            // Calculate available width for name
            float fixedWidth = LayoutConstants.IconSize +
                               LayoutConstants.ElementSpacing * 4 +
                               LayoutConstants.StatusBadgeWidth +
                               LayoutConstants.ActionButtonWidth +
                               LayoutConstants.MenuButtonWidth +
                               LayoutConstants.CardPadding * 2;

            float nameWidth = Mathf.Max(100, viewWidth - fixedWidth);

            // Model name
            GUILayout.Label(entry.ModelId, _styles.ModelNameLabel,
                GUILayout.MaxWidth(nameWidth),
                GUILayout.MinWidth(80));

            GUILayout.FlexibleSpace();

            // Status badge
            DrawStatusBadge(entry.Status);

            GUILayout.Space(LayoutConstants.ElementSpacing);

            // Primary action button
            DrawPrimaryActionButton(entry, isDownloading);

            // Context menu button
            if (GUILayout.Button(_icons.Menu ?? new GUIContent("≡"),
                EditorStyles.iconButton,
                GUILayout.Width(LayoutConstants.MenuButtonWidth)))
            {
                ShowModelContextMenu(entry);
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawStatusBadge(ModelStatus status)
        {
            GUIContent content;
            Color bgColor;

            switch (status)
            {
                case ModelStatus.Downloaded:
                    content = _content.StatusReady;
                    bgColor = ColorPalette.BadgeDownloaded;
                    break;
                case ModelStatus.NotDownloaded:
                    content = _content.StatusNotDownloaded;
                    bgColor = ColorPalette.BadgeNotDownloaded;
                    break;
                case ModelStatus.Checking:
                    content = _content.StatusChecking;
                    bgColor = ColorPalette.BadgeChecking;
                    break;
                case ModelStatus.Failed:
                    content = _content.StatusFailed;
                    bgColor = ColorPalette.BadgeFailed;
                    break;
                default:
                    content = new GUIContent("...");
                    bgColor = ColorPalette.BadgeNotDownloaded;
                    break;
            }

            var rect = GUILayoutUtility.GetRect(
                LayoutConstants.StatusBadgeWidth,
                EditorGUIUtility.singleLineHeight,
                GUILayout.Width(LayoutConstants.StatusBadgeWidth)
            );

            // Draw background
            var prevColor = GUI.color;
            GUI.color = bgColor;
            GUI.DrawTexture(rect, StyleCache.GetBadgeBackground(), ScaleMode.StretchToFill);
            GUI.color = prevColor;

            // Draw text
            GUI.Label(rect, content, _styles.BadgeLabel);
        }

        private void DrawPrimaryActionButton(ModelEntry entry, bool isDownloading)
        {
            if (isDownloading)
            {
                if (GUILayout.Button(_content.CancelButton, _styles.ActionButton,
                    GUILayout.Width(LayoutConstants.ActionButtonWidth)))
                {
                    var download = GetActiveDownloadForModel(entry.Metadata);
                    CancelDownload(download);
                }
            }
            else if (entry.Status == ModelStatus.Failed && entry.HasDownloadUrl)
            {
                if (GUILayout.Button(_content.RedownloadButton, _styles.ActionButton,
                    GUILayout.Width(LayoutConstants.ActionButtonWidth)))
                {
                    StartModelDownload(entry.Metadata);
                }
            }
            else if (entry.Status == ModelStatus.NotDownloaded && entry.HasDownloadUrl)
            {
                if (GUILayout.Button(_content.DownloadButton, _styles.ActionButton,
                    GUILayout.Width(LayoutConstants.ActionButtonWidth)))
                {
                    StartModelDownload(entry.Metadata);
                }
            }
            else if (entry.Status == ModelStatus.Downloaded)
            {
                if (GUILayout.Button(_content.DeleteButton, _styles.ActionButton,
                    GUILayout.Width(LayoutConstants.ActionButtonWidth)))
                {
                    ConfirmAndDeleteModel(entry);
                }
            }
            else
            {
                // Placeholder to maintain layout
                GUILayout.Space(LayoutConstants.ActionButtonWidth + 2);
            }
        }

        private void DrawModelCardMetadata(ModelEntry entry)
        {
            EditorGUILayout.BeginHorizontal();

            // Build metadata string + tooltip with full language list
            _stringBuilder.Clear();
            _stringBuilder.Append(GetModuleTypeDisplayName(entry.ModuleType));
            _stringBuilder.Append("  •  ");
            var languageSummary = FormatLanguageList(entry.Languages);
            _stringBuilder.Append(languageSummary);

            _tempMetaContent.text = _stringBuilder.ToString();
            _tempMetaContent.tooltip = BuildLanguageTooltip(entry.Languages);

            GUILayout.Label(_tempMetaContent, _styles.MetaLabel);

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawProgressBar(DownloadOperation download)
        {
            if (download == null)
            {
                return;
            }


            var rect = EditorGUILayout.GetControlRect(false, LayoutConstants.ProgressBarHeight);
            EditorGUI.ProgressBar(rect, download.Progress, download.StatusText ?? "Working...");
        }

        #endregion

        #region Context Menu

        private void ShowModelContextMenu(ModelEntry entry)
        {
            var menu = new GenericMenu();
            var meta = entry.Metadata;

            // Copy Model ID
            menu.AddItem(_content.CopyModelIdItem, false, () =>
            {
                EditorGUIUtility.systemCopyBuffer = entry.ModelId;
                ShowNotification(new GUIContent($"Copied: {entry.ModelId}"));
            });

            // Copy URL
            if (entry.HasDownloadUrl)
            {
                menu.AddItem(_content.CopyUrlItem, false, () =>
                {
                    EditorGUIUtility.systemCopyBuffer = meta.downloadUrl;
                    ShowNotification(new GUIContent("URL copied to clipboard"));
                });
            }
            else
            {
                menu.AddDisabledItem(_content.CopyUrlItem);
            }

            menu.AddSeparator(string.Empty);

            // Reveal in Explorer/Finder
            var modelFolder = GetModelFolder(meta);
            if (!string.IsNullOrEmpty(modelFolder) && Directory.Exists(modelFolder))
            {
                menu.AddItem(_content.RevealButton, false, () =>
                {
                    EditorUtility.RevealInFinder(modelFolder);
                });
            }
            else
            {
                menu.AddDisabledItem(_content.RevealButton);
            }

            // Rescan status
            menu.AddItem(_content.RescanStatusItem, false, () =>
            {
                ScanModelStatusAsync(entry);
            });

            menu.AddSeparator(string.Empty);

            // Delete
            if (!string.IsNullOrEmpty(modelFolder) && Directory.Exists(modelFolder))
            {
                menu.AddItem(_content.DeleteButton, false, () =>
                {
                    ConfirmAndDeleteModel(entry);
                });
            }
            else
            {
                menu.AddDisabledItem(_content.DeleteButton);
            }

            menu.ShowAsContext();
        }

        #endregion

        #region Downloads Panel

        private void DrawDownloadsPanel()
        {
            // Count active downloads without LINQ
            int activeCount = 0;
            for (int i = 0; i < _activeDownloads.Count; i++)
            {
                if (_activeDownloads[i].IsActive)
                {
                    activeCount++;
                }

            }

            if (activeCount == 0)
            {
                return;
            }


            EditorGUILayout.Space(8);

            _stringBuilder.Clear();
            _stringBuilder.Append(_content.ActiveDownloadsHeader?.text ?? "Active Downloads");
            _stringBuilder.Append(" (");
            _stringBuilder.Append(activeCount);
            _stringBuilder.Append(")");

            _showDownloadsPanel = EditorGUILayout.BeginFoldoutHeaderGroup(
                _showDownloadsPanel,
                _stringBuilder.ToString(),
                _styles.FoldoutHeader
            );

            if (_showDownloadsPanel)
            {
                EditorGUILayout.BeginVertical("box");

                for (int i = 0; i < _activeDownloads.Count; i++)
                {
                    var download = _activeDownloads[i];
                    if (!download.IsActive)
                    {
                        continue;
                    }


                    DrawDownloadRow(download);

                    if (i < _activeDownloads.Count - 1)
                    {
                        EditorGUILayout.Space(4);
                    }
                }

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawDownloadRow(DownloadOperation download)
        {
            EditorGUILayout.BeginVertical();

            // Name and phase
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(download.ModelId, EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            GUILayout.Label(GetDownloadPhaseDisplayName(download.Phase), EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();

            // Progress bar
            var rect = EditorGUILayout.GetControlRect(false, LayoutConstants.ProgressBarHeight);
            EditorGUI.ProgressBar(rect, download.Progress, download.StatusText ?? "Working...");

            // Cancel button
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            EditorGUI.BeginDisabledGroup(download.IsCanceled);
            if (GUILayout.Button(_content.CancelButton, _styles.ActionButton, GUILayout.Width(60)))
            {
                CancelDownload(download);
            }
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        #endregion

        #region Download Operations

        private DownloadOperation GetActiveDownloadForModel(SherpaONNXModelMetadata meta)
        {
            if (meta == null)
            {
                return null;
            }


            for (int i = 0; i < _activeDownloads.Count; i++)
            {
                var download = _activeDownloads[i];
                if (download.Metadata == meta && download.IsActive)
                {
                    return download;
                }
            }
            return null;
        }

        private void StartModelDownload(SherpaONNXModelMetadata meta)
        {
            if (meta == null || string.IsNullOrEmpty(meta.downloadUrl))
            {
                return;
            }


            if (GetActiveDownloadForModel(meta) != null)
            {
                return;
            }

            // Reset entry status

            var entry = FindModelEntry(meta);
            if (entry != null)
            {
                entry.Status = ModelStatus.Checking;
                entry.StatusMessage = null;
                entry.IsHeightValid = false;
            }

            var operation = new DownloadOperation
            {
                Metadata = meta,
                Phase = DownloadPhase.Downloading,
                Progress = 0f,
                StatusText = "Starting...",
                CancellationSource = new CancellationTokenSource()
            };

            _activeDownloads.Add(operation);
            BeginDownloadRequest(operation);
            RequestRepaint();
        }

        private void BeginDownloadRequest(DownloadOperation operation)
        {
            var meta = operation.Metadata;

            // Resolve paths
            string moduleDir, modelDir, fileName;
            bool isCompressed;

            string destPath = SherpaUtils.Prepare.ResolveDownloadFilePath(
                meta, out moduleDir, out modelDir, out fileName, out isCompressed
            );

            operation.ArchivePath = destPath;
            operation.TempArchivePath = destPath + ".download";
            operation.ModuleDirectory = moduleDir;
            operation.IsCompressed = isCompressed;

            // Ensure directory exists
            var downloadDir = Path.GetDirectoryName(destPath);
            if (!string.IsNullOrEmpty(downloadDir) && !Directory.Exists(downloadDir))
            {
                Directory.CreateDirectory(downloadDir);
            }

            // Create request
            var request = UnityWebRequest.Get(meta.downloadUrl);
            request.downloadHandler = new DownloadHandlerFile(operation.TempArchivePath, false);
            operation.WebRequest = request;

            var asyncOp = request.SendWebRequest();

            // Set up update callback
            operation.SetUpdateCallback(() => UpdateDownloadProgress(operation, request, asyncOp));
        }

        private void UpdateDownloadProgress(DownloadOperation operation, UnityWebRequest request, UnityWebRequestAsyncOperation asyncOp)
        {
            if (operation.IsCanceled)
            {
                FinalizeDownload(operation, false, "Canceled", canceled: true);
                return;
            }

            if (!asyncOp.isDone)
            {
                // Update progress (0-70% for download phase)
                float progress = Mathf.Clamp01(request.downloadProgress);
                operation.Progress = progress * 0.7f;

                _stringBuilder.Clear();
                _stringBuilder.Append("Downloading... ");
                _stringBuilder.Append((progress * 100f).ToString("0"));
                _stringBuilder.Append("%");
                operation.StatusText = _stringBuilder.ToString();

                RequestRepaint();
                return;
            }

            // Download complete - remove update callback
            operation.RemoveUpdateCallback();

            if (request.result != UnityWebRequest.Result.Success)
            {
                string error = request.error ?? "Unknown error";
                FinalizeDownload(operation, false, $"Download failed: {error}");
                request.Dispose();
                return;
            }

            request.Dispose();

            ProcessDownloadedArchive(operation);
        }

        private async void ProcessDownloadedArchive(DownloadOperation operation)
        {
            try
            {
                operation.Phase = DownloadPhase.Extracting;
                operation.Progress = 0.72f;
                operation.StatusText = "Saving...";
                RequestRepaint();

                if (!string.IsNullOrEmpty(operation.TempArchivePath) &&
                    !string.Equals(operation.TempArchivePath, operation.ArchivePath, StringComparison.Ordinal))
                {
                    try { if (File.Exists(operation.ArchivePath)) { SherpaFileUtils.Delete(operation.ArchivePath); } } catch { }
                    File.Move(operation.TempArchivePath, operation.ArchivePath);
                }

                if (operation.IsCanceled)
                {
                    EditorApplication.delayCall += () => FinalizeDownload(operation, false, "Canceled", canceled: true);
                    return;
                }

                // Extract if compressed
                if (operation.IsCompressed)
                {
                    var progressHandler = new Progress<DecompressionEventArgs>(args =>
                    {
                        operation.Progress = 0.7f + 0.2f * Mathf.Clamp01(args.Progress);

                        _stringBuilder.Clear();
                        _stringBuilder.Append("Extracting... ");
                        _stringBuilder.Append((args.Progress * 100f).ToString("0"));
                        _stringBuilder.Append("%");
                        operation.StatusText = _stringBuilder.ToString();

                        RequestRepaint();
                    });

                    var result = await SherpaDecompressHelper.DecompressAsync(
                        operation.ArchivePath,
                        operation.ModuleDirectory,
                        progressHandler,
                        cancellationToken: operation.CancellationSource.Token
                    );

                    if (!result.Success)
                    {
                        throw new Exception(result.ErrorMessage ?? "Extraction failed");
                    }

                    // Clean up archive
                    try { SherpaFileUtils.Delete(operation.ArchivePath); } catch { }

                    // Refresh AssetDatabase if in Assets folder
                    string normalizedModulePath = operation.ModuleDirectory.Replace("\\", "/");
                    string normalizedDataPath = Application.dataPath.Replace("\\", "/");

                    if (normalizedModulePath.StartsWith(normalizedDataPath, StringComparison.OrdinalIgnoreCase))
                    {
                        EditorApplication.delayCall += AssetDatabase.Refresh;
                    }
                }

                // Verify phase
                operation.Phase = DownloadPhase.Verifying;
                operation.Progress = 0.95f;
                operation.StatusText = "Verifying...";
                RequestRepaint();

                bool verified = await SherpaUtils.Prepare.CheckIsModelDownloadedAsync(operation.Metadata);

                EditorApplication.delayCall += () =>
                {
                    if (verified)
                    {
                        FinalizeDownload(operation, true, "Completed");
                    }
                    else
                    {
                        FinalizeDownload(operation, false, "Verification failed - files may be corrupt");
                    }
                };
            }
            catch (OperationCanceledException)
            {
                EditorApplication.delayCall += () => FinalizeDownload(operation, false, "Canceled", canceled: true);
            }
            catch (Exception ex)
            {
                EditorApplication.delayCall += () => FinalizeDownload(operation, false, $"Error: {ex.Message}");
            }
        }

        private void FinalizeDownload(DownloadOperation operation, bool success, string message, bool canceled = false)
        {
            operation.Phase = success ? DownloadPhase.Completed : canceled ? DownloadPhase.Canceled : DownloadPhase.Failed;
            operation.Progress = 1f;
            operation.StatusText = message;
            operation.Dispose();

            // Update model entry
            var entry = FindModelEntry(operation.Metadata);
            if (entry != null)
            {
                if (success)
                {
                    entry.Status = ModelStatus.Downloaded;
                    entry.StatusMessage = null;
                }
                else if (canceled || operation.Phase == DownloadPhase.Canceled)
                {
                    entry.Status = ModelStatus.NotDownloaded;
                    entry.StatusMessage = null;
                }
                else
                {
                    entry.Status = ModelStatus.Failed;
                    entry.StatusMessage = message;
                }
                entry.IsHeightValid = false;
            }

            // Clean up archive on failure
            if (!success && !string.IsNullOrEmpty(operation.ArchivePath) && File.Exists(operation.ArchivePath))
            {
                try { SherpaFileUtils.Delete(operation.ArchivePath); } catch { }
            }
            if (!success && !string.IsNullOrEmpty(operation.TempArchivePath) && File.Exists(operation.TempArchivePath))
            {
                try { SherpaFileUtils.Delete(operation.TempArchivePath); } catch { }
            }

            // Remove from list
            _activeDownloads.Remove(operation);

            // Show notification
            _stringBuilder.Clear();
            _stringBuilder.Append(success ? "Downloaded: " : canceled ? "Canceled: " : "Failed: ");
            _stringBuilder.Append(operation.ModelId);
            ShowNotification(new GUIContent(_stringBuilder.ToString()));

            RequestRepaint();
        }

        private void CancelDownload(DownloadOperation operation)
        {
            if (operation == null)
            {
                return;
            }


            operation.Dispose();
            FinalizeDownload(operation, false, "Canceled by user", canceled: true);
        }

        private void CancelAllDownloads()
        {
            for (int i = _activeDownloads.Count - 1; i >= 0; i--)
            {
                _activeDownloads[i].Dispose();
            }
            _activeDownloads.Clear();
        }

        #endregion

        #region File Operations

        private string GetModelFolder(SherpaONNXModelMetadata meta)
        {
            if (meta == null)
            {
                return null;
            }


            string moduleRoot = SherpaPathResolver.GetModuleRootPath(meta.moduleType);
            if (string.IsNullOrEmpty(moduleRoot))
            {
                return null;
            }


            string modelDir = Path.Combine(moduleRoot, meta.modelId);
            return Directory.Exists(modelDir) ? modelDir : null;
        }

        private void ConfirmAndDeleteModel(ModelEntry entry)
        {
            if (entry?.Metadata == null)
            {
                return;
            }


            var meta = entry.Metadata;
            var folder = GetModelFolder(meta);
            if (string.IsNullOrEmpty(folder))
            {
                return;
            }


            bool confirmed = EditorUtility.DisplayDialog(
                _content.DeleteDialogTitle,
                $"Delete local files for '{meta.modelId}'?\n\nPath: {folder}",
                _content.DeleteDialogConfirm,
                _content.DeleteDialogCancel
            );

            if (!confirmed)
            {
                return;
            }


            try
            {
                string normalizedFolder = folder.Replace("\\", "/");
                string normalizedDataPath = Application.dataPath.Replace("\\", "/");

                if (normalizedFolder.StartsWith(normalizedDataPath, StringComparison.OrdinalIgnoreCase))
                {
                    // Inside Assets - use AssetDatabase
                    string relativePath = "Assets" + normalizedFolder.Substring(normalizedDataPath.Length);

                    if (AssetDatabase.IsValidFolder(relativePath))
                    {
                        AssetDatabase.DeleteAsset(relativePath);
                    }
                    else
                    {
                        FileUtil.DeleteFileOrDirectory(relativePath);
                    }
                    AssetDatabase.Refresh();
                }
                else
                {
                    // Outside Assets - direct delete
                    Directory.Delete(folder, true);
                }

                entry.Status = ModelStatus.NotDownloaded;
                entry.StatusMessage = null;
                entry.IsHeightValid = false;

                // Cancel any active download
                var activeDownload = GetActiveDownloadForModel(meta);
                if (activeDownload != null)
                {
                    CancelDownload(activeDownload);
                }

                ShowNotification(new GUIContent($"Deleted: {meta.modelId}"));
            }
            catch (Exception ex)
            {
                ShowNotification(new GUIContent($"Delete failed: {ex.Message}"));
            }

            RequestRepaint();
        }

        #endregion

        #region Utility Methods

        private ModelEntry FindModelEntry(SherpaONNXModelMetadata meta)
        {
            if (meta == null)
            {
                return null;
            }


            for (int i = 0; i < _allModels.Count; i++)
            {
                if (_allModels[i].Metadata == meta)
                {
                    return _allModels[i];
                }
            }
            return null;
        }

        private string GetModuleTypeDisplayName(SherpaONNXModuleType moduleType)
        {
            return moduleType switch
            {
                SherpaONNXModuleType.Undefined => Tr(SherpaONNXL10n.Models.CategoryUndefined, "Undefined"),
                SherpaONNXModuleType.SpeechRecognition => Tr(SherpaONNXL10n.Models.CategorySpeechRecognition, "Speech Recognition"),
                SherpaONNXModuleType.SpeechSynthesis => Tr(SherpaONNXL10n.Models.CategorySpeechSynthesis, "Text to Speech"),
                SherpaONNXModuleType.SourceSeparation => Tr(SherpaONNXL10n.Models.CategorySourceSeparation, "Source Separation"),
                SherpaONNXModuleType.SpeakerIdentification => Tr(SherpaONNXL10n.Models.CategorySpeakerIdentification, "Speaker ID"),
                SherpaONNXModuleType.SpeakerDiarization => Tr(SherpaONNXL10n.Models.CategorySpeakerDiarization, "Diarization"),
                SherpaONNXModuleType.SpokenLanguageIdentification => Tr(SherpaONNXL10n.Models.CategorySpokenLanguageId, "Language ID"),
                SherpaONNXModuleType.AudioTagging => Tr(SherpaONNXL10n.Models.CategoryAudioTagging, "Audio Tagging"),
                SherpaONNXModuleType.VoiceActivityDetection => Tr(SherpaONNXL10n.Models.CategoryVad, "VAD"),
                SherpaONNXModuleType.KeywordSpotting => Tr(SherpaONNXL10n.Models.CategoryKeywordSpotting, "Keyword"),
                SherpaONNXModuleType.AddPunctuation => Tr(SherpaONNXL10n.Models.CategoryAddPunctuation, "Punctuation"),
                SherpaONNXModuleType.SpeechEnhancement => Tr(SherpaONNXL10n.Models.CategorySpeechEnhancement, "Enhancement"),
                _ => moduleType.ToString()
            };
        }

        private string GetLanguageDisplayName(string code)
        {
            if (string.IsNullOrWhiteSpace(code) || code.Equals("other", StringComparison.OrdinalIgnoreCase))
            {
                return Tr(SherpaONNXL10n.Common.LanguageOther, "Other");
            }

            return code.ToLowerInvariant() switch
            {
                "chinese" => Tr(SherpaONNXL10n.Common.LanguageChinese, "Chinese"),
                "cantonese" => Tr(SherpaONNXL10n.Common.LanguageCantonese, "Cantonese"),
                "english" => Tr(SherpaONNXL10n.Common.LanguageEnglish, "English"),
                "japanese" => Tr(SherpaONNXL10n.Common.LanguageJapanese, "Japanese"),
                "korean" => Tr(SherpaONNXL10n.Common.LanguageKorean, "Korean"),
                "thai" => Tr(SherpaONNXL10n.Common.LanguageThai, "Thai"),
                "vietnamese" => Tr(SherpaONNXL10n.Common.LanguageVietnamese, "Vietnamese"),
                "russian" => Tr(SherpaONNXL10n.Common.LanguageRussian, "Russian"),
                "french" => Tr(SherpaONNXL10n.Common.LanguageFrench, "French"),
                "spanish" => Tr(SherpaONNXL10n.Common.LanguageSpanish, "Spanish"),
                "german" => Tr(SherpaONNXL10n.Common.LanguageGerman, "German"),
                "dutch" => Tr(SherpaONNXL10n.Common.LanguageDutch, "Dutch"),
                "danish" => Tr(SherpaONNXL10n.Common.LanguageDanish, "Danish"),
                "czech" => Tr(SherpaONNXL10n.Common.LanguageCzech, "Czech"),
                "catalan" => Tr(SherpaONNXL10n.Common.LanguageCatalan, "Catalan"),
                "italian" => Tr(SherpaONNXL10n.Common.LanguageItalian, "Italian"),
                "portuguese" => Tr(SherpaONNXL10n.Common.LanguagePortuguese, "Portuguese"),
                "arabic" => Tr(SherpaONNXL10n.Common.LanguageArabic, "Arabic"),
                "hindi" => Tr(SherpaONNXL10n.Common.LanguageHindi, "Hindi"),
                "turkish" => Tr(SherpaONNXL10n.Common.LanguageTurkish, "Turkish"),
                "polish" => Tr(SherpaONNXL10n.Common.LanguagePolish, "Polish"),
                "swedish" => Tr(SherpaONNXL10n.Common.LanguageSwedish, "Swedish"),
                "norwegian" => Tr(SherpaONNXL10n.Common.LanguageNorwegian, "Norwegian"),
                "indonesian" => Tr(SherpaONNXL10n.Common.LanguageIndonesian, "Indonesian"),
                "malay" => Tr(SherpaONNXL10n.Common.LanguageMalay, "Malay"),
                "urdu" => Tr(SherpaONNXL10n.Common.LanguageUrdu, "Urdu"),
                "persian" => Tr(SherpaONNXL10n.Common.LanguagePersian, "Persian"),
                "hebrew" => Tr(SherpaONNXL10n.Common.LanguageHebrew, "Hebrew"),
                _ => System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(code)
            };
        }

        private string GetDownloadPhaseDisplayName(DownloadPhase phase)
        {
            return phase switch
            {
                DownloadPhase.Downloading => Tr(SherpaONNXL10n.Models.StatusDownloadPhase, "Downloading"),
                DownloadPhase.Extracting => Tr(SherpaONNXL10n.Models.StatusInstallPhase, "Extracting"),
                DownloadPhase.Verifying => Tr(SherpaONNXL10n.Models.StatusVerifyPhase, "Verifying"),
                DownloadPhase.Completed => "Completed",
                DownloadPhase.Failed => "Failed",
                DownloadPhase.Canceled => "Canceled",
                _ => string.Empty
            };
        }

        private string FormatLanguageList(List<string> languages)
        {
            // Deduplicate while preserving order to avoid showing the same language multiple times.
            if (languages == null || languages.Count == 0)
            {
                return GetLanguageDisplayName("other");
            }

            var displayList = BuildLanguageDisplayList(languages);

            if (displayList.Count == 0)
            {
                return GetLanguageDisplayName("other");
            }

            _languageStringBuilder.Clear();

            int count = Mathf.Min(displayList.Count, 3);
            for (int i = 0; i < count; i++)
            {
                if (i > 0)
                {
                    _languageStringBuilder.Append(", ");
                }


                _languageStringBuilder.Append(displayList[i]);
            }

            if (displayList.Count > 3)
            {
                _languageStringBuilder.Append(" +");
                _languageStringBuilder.Append(displayList.Count - 3);
            }

            return _languageStringBuilder.ToString();
        }

        private string BuildLanguageTooltip(List<string> languages)
        {
            var displayList = BuildLanguageDisplayList(languages);
            if (displayList.Count == 0)
            {
                return GetLanguageDisplayName("other");
            }

            _languageStringBuilder.Clear();
            for (int i = 0; i < displayList.Count; i++)
            {
                if (i > 0)
                {
                    _languageStringBuilder.Append(", ");
                }
                _languageStringBuilder.Append(displayList[i]);
            }
            return _languageStringBuilder.ToString();
        }

        private List<string> BuildLanguageDisplayList(List<string> languages)
        {
            _languageDisplayBuffer.Clear();
            _languageCodeSeen.Clear();
            _languageDisplaySeen.Clear();

            if (languages == null)
            {
                return _languageDisplayBuffer;
            }

            for (int i = 0; i < languages.Count; i++)
            {
                var code = languages[i];
                if (string.IsNullOrWhiteSpace(code))
                {
                    continue;
                }

                if (!_languageCodeSeen.Add(code))
                {
                    continue;
                }

                var display = GetLanguageDisplayName(code)?.Trim();
                if (string.IsNullOrEmpty(display))
                {
                    continue;
                }

                // Also dedupe by translated display string in case different codes map to the same name.
                if (_languageDisplaySeen.Add(display))
                {
                    _languageDisplayBuffer.Add(display);
                }
            }

            return _languageDisplayBuffer;
        }

        private GUIContent GetModuleTypeIcon(SherpaONNXModuleType type)
        {
            return type switch
            {
                SherpaONNXModuleType.SpeechRecognition => _icons.Microphone,
                SherpaONNXModuleType.SpeechSynthesis => _icons.AudioSource,
                SherpaONNXModuleType.VoiceActivityDetection => _icons.AudioMixer,
                _ => _icons.ScriptableObject
            };
        }

        private static string Tr(string key, string fallback)
        {
            try
            {
                return SherpaONNXLocalization.Tr(key, fallback);
            }
            catch
            {
                return fallback;
            }
        }

        #endregion

        #region Language Parser

        private static readonly Dictionary<string, string> LanguageKeywordMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "chinese", "chinese" }, { "mandarin", "chinese" }, { "zh", "chinese" },
            { "cantonese", "cantonese" }, { "yue", "cantonese" },
            { "english", "english" }, { "en", "english" },
            { "japanese", "japanese" }, { "ja", "japanese" },
            { "korean", "korean" }, { "ko", "korean" },
            { "thai", "thai" }, { "th", "thai" },
            { "vietnamese", "vietnamese" }, { "vi", "vietnamese" },
            { "russian", "russian" }, { "ru", "russian" },
            { "french", "french" }, { "fr", "french" },
            { "spanish", "spanish" }, { "es", "spanish" },
            { "german", "german" }, { "de", "german" },
            { "dutch", "dutch" }, { "nl", "dutch" },
            { "italian", "italian" }, { "it", "italian" },
            { "portuguese", "portuguese" }, { "pt", "portuguese" },
            { "arabic", "arabic" }, { "ar", "arabic" },
            { "turkish", "turkish" }, { "tr", "turkish" },
            { "polish", "polish" }, { "pl", "polish" },
            { "swedish", "swedish" }, { "sv", "swedish" },
            { "norwegian", "norwegian" }, { "no", "norwegian" }, { "nb", "norwegian" }, { "nn", "norwegian" },
            { "indonesian", "indonesian" }, { "id", "indonesian" },
            { "malay", "malay" }, { "ms", "malay" },
            { "urdu", "urdu" }, { "ur", "urdu" },
            { "persian", "persian" }, { "fa", "persian" }, { "farsi", "persian" },
            { "hebrew", "hebrew" }, { "he", "hebrew" }, { "iw", "hebrew" },
            { "danish", "danish" }, { "da", "danish" },
            { "czech", "czech" }, { "cs", "czech" },
            { "catalan", "catalan" }, { "ca", "catalan" },
            { "hindi", "hindi" }, { "hi", "hindi" },
            { "bengali", "bengali" }, { "bn", "bengali" },
            { "tamil", "tamil" }, { "ta", "tamil" },
            { "telugu", "telugu" }, { "te", "telugu" },
            { "marathi", "marathi" }, { "mr", "marathi" },
            { "gujarati", "gujarati" }, { "gu", "gujarati" },
            { "kannada", "kannada" }, { "kn", "kannada" },
            { "malayalam", "malayalam" }, { "ml", "malayalam" },
            { "punjabi", "punjabi" }, { "pa", "punjabi" },
            { "nepali", "nepali" }, { "ne", "nepali" },
            { "sinhala", "sinhala" }, { "si", "sinhala" },
            { "lao", "lao" }, { "lo", "lao" },
            { "burmese", "burmese" }, { "my", "burmese" }, { "myanmar", "burmese" },
            { "khmer", "khmer" }, { "km", "khmer" },
            { "mongolian", "mongolian" }, { "mn", "mongolian" },
            { "tagalog", "tagalog" }, { "fil", "tagalog" }, { "tl", "tagalog" },
            { "javanese", "javanese" }, { "jv", "javanese" },
            { "ukrainian", "ukrainian" }, { "uk", "ukrainian" },
            { "romanian", "romanian" }, { "ro", "romanian" },
            { "hungarian", "hungarian" }, { "hu", "hungarian" },
            { "bulgarian", "bulgarian" }, { "bg", "bulgarian" },
            { "croatian", "croatian" }, { "hr", "croatian" },
            { "serbian", "serbian" }, { "sr", "serbian" },
            { "slovak", "slovak" }, { "sk", "slovak" },
            { "slovenian", "slovenian" }, { "sl", "slovenian" },
            { "finnish", "finnish" }, { "fi", "finnish" },
            { "estonian", "estonian" }, { "et", "estonian" },
            { "latvian", "latvian" }, { "lv", "latvian" },
            { "lithuanian", "lithuanian" }, { "lt", "lithuanian" },
            { "swahili", "swahili" }, { "sw", "swahili" },
            { "afrikaans", "afrikaans" }, { "af", "afrikaans" },
            { "zulu", "zulu" }, { "zu", "zulu" },
            { "xhosa", "xhosa" }, { "xh", "xhosa" }
        };

        private static readonly char[] LanguageSeparators = { '-', '_', '.', '/', ' ', '+' };

        private static List<string> ParseLanguagesFromModelId(string modelId)
        {
            var result = new List<string>();

            if (string.IsNullOrWhiteSpace(modelId))
            {
                result.Add("other");
                return result;
            }

            var foundLanguages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string lowerModelId = modelId.ToLowerInvariant();

            // Check segmented parts
            var parts = lowerModelId.Split(LanguageSeparators, StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                var normalized = NormalizeLanguageToken(part);
                if (string.IsNullOrEmpty(normalized))
                {
                    continue;
                }

                if (LanguageKeywordMap.TryGetValue(normalized, out string language))
                {
                    foundLanguages.Add(language);
                }
            }

            if (foundLanguages.Count == 0)
            {
                result.Add("other");
            }
            else
            {
                result.AddRange(foundLanguages);
                result.Sort(StringComparer.OrdinalIgnoreCase);
            }

            return result;
        }

        private static string NormalizeLanguageToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return string.Empty;
            }

            var sb = new StringBuilder(token.Length);
            for (int i = 0; i < token.Length; i++)
            {
                char c = token[i];
                if (char.IsLetter(c))
                {
                    sb.Append(char.ToLowerInvariant(c));
                }
            }

            return sb.ToString();
        }

        #endregion
    }
}

#endif
