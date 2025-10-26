#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading; // add this right below using System.Threading.Tasks;;
using UnityEngine;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine.Networking;
using Eitan.SherpaOnnxUnity.Runtime;
using System;
using System.Threading.Tasks;
using Eitan.SherpaOnnxUnity.Runtime.Utilities;


namespace Eitan.SherpaOnnxUnity.Editor
{
    /// <summary>
    /// Lightweight adapter to surface Prepare/download/install/verify progress into the EditorWindow.
    /// Call Report(progress, message) from any background task to update the UI.
    /// </summary>
    internal sealed class SherpaOnnxFeedbackReporterAdapter
    {
        private readonly Action<float, string> _onReport;

        public SherpaOnnxFeedbackReporterAdapter(Action<float, string> onReport)
        {
            _onReport = onReport;
        }

        public void Report(float progress01, string message)
        {
            // Clamp and forward to the UI updater
            _onReport?.Invoke(Mathf.Clamp01(progress01), message ?? string.Empty);
        }
    }

    public class SherpaOnnxModelsEditorWindow : EditorWindow
    {
        // GUI state
        private string _searchQuery = string.Empty;
        private int _selectedCategoryIndex = 0;
        private int _selectedLanguageIndex = 0;
        private Vector2 _scrollPosition;
        private bool isDownloading = false;
        private bool _needsRepaint = false;
        private bool _isLoadingManifest = false;
        private System.Threading.CancellationTokenSource _loadCts;
        private int _spinnerFrame = 0;

        // Cached popup arrays to avoid ToArray allocations every frame
        private string[] _categoriesArray = System.Array.Empty<string>();
        private string[] _languagesArray = System.Array.Empty<string>();


        // Background repaint pump so progress stays live even when window loses focus
        private EditorApplication.CallbackFunction _backgroundRepaintHandler;
        private double _lastRepaintTime = 0d;

        private SearchField _searchField;

        /// <summary>
        /// Clears all filter fields and resets UI state.
        /// </summary>
        private void ResetFilters()
        {
            _searchQuery = string.Empty;
            _selectedCategoryIndex = 0;
            _selectedLanguageIndex = 0;
            GUI.FocusControl(null);
            _needsRepaint = true;
        }

        /// <summary>
        /// Rebuild cached arrays for popups to avoid per-frame allocations.
        /// Call whenever _categories/_languages lists change.
        /// </summary>
        private void RebuildPopupArrays()
        {
            _categoriesArray = _categories.ToArray();
            _languagesArray = _languages.ToArray();
        }


        // Data caches
        private readonly List<string> _categories = new();
        private readonly List<string> _languages = new();
        private readonly List<ModelEntry> _allEntries = new();
        private readonly List<DownloadTask> _activeDownloads = new();

        // Representation of each model entry in the UI
        private class ModelEntry
        {
            public string Category;
            // public string Language;
            public List<string> Languages = new List<string>();  // 单语种列表（去重）
            public string LanguagesLabel => (Languages != null && Languages.Count > 0)
                ? string.Join(", ", Languages)                   // 仅用于 UI 展示
                : "other";

            public SherpaOnnxModelMetadata Metadata;
            public bool? IsDownloaded; // null = checking, true = downloaded, false = not downloaded
            public bool Expanded;
            public bool VerifyFailed;      // set when post-install verification fails
            public string VerifyMessage;   // optional message for the UI

            // Minimal layout snapshot computed during Layout
            public bool ProgressVisible;
            public float HelpHeight;
        }

        // Representation of an active download
        private class DownloadTask
        {
            public SherpaOnnxModelMetadata Metadata;
            public float Progress;
            public string Status;
            public string CurrentPhase; // "Download", "Install", "Verify"

            // NEW: cancellation & plumbing
            public CancellationTokenSource Cts = new CancellationTokenSource();
            public UnityWebRequest Request;
            public EditorApplication.CallbackFunction UpdateHandler;
            public string DestPath;
            public string ModuleDir;
            public bool IsCompressed;
            public bool IsCanceled;
        }
        [MenuItem("Window/Sherpa Onnx/Model Manager")]
        private static void ShowWindow()
        {
            var window = GetWindow<SherpaOnnxModelsEditorWindow>();
            window.titleContent = new GUIContent("Sherpa ONNX Models");
            window.Show();
        }

        // [MenuItem("Window/Sherpa Onnx/Reset Registry")]
        // private static void ResetRegistryMenu()
        // {
        //     // Clear registry state and open the window with a forced refresh
        //     SherpaOnnxModelRegistry.Instance.Uninitialize();
        //     var window = GetWindow<SherpaOnnxModelsEditorWindow>();
        //     window.titleContent = new GUIContent("Sherpa ONNX Models");
        //     window.Show();
        //     window.ForceRefreshManifest();
        // }

        private void OnEnable()
        {
            // Keep the window repainting on scene changes and while unfocused
            autoRepaintOnSceneChange = true;

            // Ensure data is fresh when (re)opening
            RefreshData();

            // Install a lightweight background repaint pump.
            // This runs even when the EditorWindow is not focused, so progress bars keep moving.
            if (_backgroundRepaintHandler == null)
            {
                _backgroundRepaintHandler = () =>
                {
                    // If any download is active or a repaint was requested, refresh at ~15 FPS
                    if (_needsRepaint || _activeDownloads.Count > 0)
                    {
                        double now = EditorApplication.timeSinceStartup;
                        if (now - _lastRepaintTime >= (1.0 / 15.0))
                        {
                            _lastRepaintTime = now;
                            // Clear the flag *before* repaint to avoid missing rapid updates
                            _needsRepaint = false;
                            Repaint();
                        }
                    }
                };
                EditorApplication.update += _backgroundRepaintHandler;
            }
            // Auto-refresh when the registry finishes initializing
            SherpaOnnxModelRegistry.Instance.Initialized += OnRegistryInitialized;
        }
        private void OnDisable()
        {
            // Remove the background update pump
            if (_backgroundRepaintHandler != null)
            {
                try { EditorApplication.update -= _backgroundRepaintHandler; } catch { }
                _backgroundRepaintHandler = null;
            }
            try { SherpaOnnxModelRegistry.Instance.Initialized -= OnRegistryInitialized; } catch { }
            try { _loadCts?.Cancel(); _loadCts?.Dispose(); _loadCts = null; } catch { }
            CancelAllActiveOperations();
        }
        private void OnDestroy()
        {
            if (_backgroundRepaintHandler != null)
            {
                try { EditorApplication.update -= _backgroundRepaintHandler; } catch { }
                _backgroundRepaintHandler = null;
            }
            try { SherpaOnnxModelRegistry.Instance.Initialized -= OnRegistryInitialized; } catch { }
            try { _loadCts?.Cancel(); _loadCts?.Dispose(); _loadCts = null; } catch { }
            CancelAllActiveOperations();
        }
        /// <summary>
        /// Clears caches and repopulates the list of models, categories and languages.
        /// </summary>
        private void RefreshData()
        {
            _allEntries.Clear();
            _categories.Clear();
            _languages.Clear();

            // The "All" option at index 0
            _categories.Add("All");
            _languages.Add("All");
            _categories.AddRange(Enum.GetNames(typeof(SherpaOnnxModuleType)));
            RebuildPopupArrays();

            var reg = SherpaOnnxModelRegistry.Instance;
            SherpaOnnxModelManifest manifest;
            if (!reg.TryGetManifest(out manifest))
            {
                // Still initializing – show spinner and wait asynchronously (on MAIN thread)
                _isLoadingManifest = true;
                _loadCts?.Cancel();
                _loadCts = new System.Threading.CancellationTokenSource();
                var token = _loadCts.Token;

                // Schedule the async wait from the main thread to avoid creating UnityWebRequest off-thread
                EditorApplication.delayCall += async () =>
                {
                    try
                    {
                        var mf = await reg.WaitForManifestAsync(token);
                        if (token.IsCancellationRequested)
                        {
                            return;
                        }


                        _isLoadingManifest = false;
                        BuildFromManifest(mf);
                        RebuildPopupArrays();
                        KickoffDownloadStatusScan();
                        _needsRepaint = true;
                    }
                    catch (OperationCanceledException) { }
                    catch (Exception ex)
                    {
                        _isLoadingManifest = false;
                        ShowNotification(new GUIContent("Manifest load failed: " + ex.Message));
                        _needsRepaint = true;
                    }
                };

                _needsRepaint = true; // keep the spinner animating
                return;
            }

            // Already initialized – build immediately
            BuildFromManifest(manifest);
            RebuildPopupArrays();
            KickoffDownloadStatusScan();
        }

        /// <summary>
        /// Hard refresh: Uninitialize registry, clear local UI caches, show spinner, and re-fetch manifest on main thread.
        /// Also triggers a full rescan when finished.
        /// </summary>
        private void ForceRefreshManifest()
        {
            // Stop any ongoing downloads/progress UI first
            CancelAllActiveOperations();

            // Reset UI caches and filters
            _allEntries.Clear();
            _categories.Clear();
            _languages.Clear();

            // Rebuild base dropdown items ("All" + categories)
            _categories.Add("All");
            _languages.Add("All");
            _categories.AddRange(Enum.GetNames(typeof(SherpaOnnxModuleType)));
            RebuildPopupArrays();

            // Enter loading state and keep spinner alive
            _isLoadingManifest = true;
            _needsRepaint = true;

            // Reset any previous load waits
            try { _loadCts?.Cancel(); } catch { }
            try { _loadCts?.Dispose(); } catch { }
            _loadCts = new System.Threading.CancellationTokenSource();
            var token = _loadCts.Token;

            // Clear the registry so any in-flight init won't pollute new state
            var reg = SherpaOnnxModelRegistry.Instance;
            reg.Uninitialize();

            // Kick the async re-fetch from the main thread to respect UnityWebRequest threading rules
            EditorApplication.delayCall += async () =>
            {
                try
                {
                    var mf = await reg.WaitForManifestAsync(token);
                    if (token.IsCancellationRequested)
                    {
                        return;
                    }

                    _isLoadingManifest = false;
                    BuildFromManifest(mf);
                    RebuildPopupArrays();
                    KickoffDownloadStatusScan(); // full rescan after refresh
                    _needsRepaint = true;
                }
                catch (OperationCanceledException)
                {
                    // silent: user navigated away / another refresh issued
                }
                catch (Exception ex)
                {
                    _isLoadingManifest = false;
                    ShowNotification(new GUIContent("Manifest reload failed: " + ex.Message));
                    _needsRepaint = true;
                }
            };
        }

        private void BuildFromManifest(SherpaOnnxModelManifest manifest)
        {
            if (manifest == null || manifest.models == null)
            {
                return;
            }


            foreach (var metadata in manifest.models)
            {
                var langs = ParseLanguages(metadata.modelId);
                var entry = new ModelEntry
                {
                    Category = metadata.moduleType.ToString(),
                    Metadata = metadata,
                    Languages = langs,
                    VerifyFailed = false,
                    VerifyMessage = string.Empty,
                };
                _allEntries.Add(entry);

                // Populate language dropdown: single languages only, skip "other"
                foreach (var lang in langs)
                {
                    if (string.IsNullOrEmpty(lang) || lang == "other")
                    {
                        continue;
                    }


                    if (!_languages.Contains(lang))
                    {
                        _languages.Add(lang);
                    }

                }
            }
            // Rebuild cached popup arrays after languages list may have grown
            RebuildPopupArrays();
        }

        private void OnInspectorUpdate()
        {
            if (_needsRepaint)
            {
                _needsRepaint = false;
                Repaint();
            }
        }

        private void KickoffDownloadStatusScan()
        {
            foreach (var entry in _allEntries)
            {
                entry.VerifyFailed = false;
                entry.VerifyMessage = string.Empty;
                entry.IsDownloaded = null; // mark as checking
                var meta = entry.Metadata;
                Task.Run(async () =>
                {
                    bool ok = false;
                    try
                    {
                        ok = await SherpaUtils.Prepare.CheckIsModelDownloadedAsync(meta);
                    }
                    catch
                    {
                        ok = false;
                    }
                    entry.IsDownloaded = ok;
                    _needsRepaint = true;
                });
            }
        }
        private void SetVerifyFailed(SherpaOnnxModelMetadata meta, bool failed, string message = null)
        {
            var entry = _allEntries.FirstOrDefault(e => e.Metadata == meta);
            if (entry == null) { return; }

            entry.VerifyFailed = failed;
            entry.VerifyMessage = failed ? (message ?? "Verification failed. Please re-download.") : string.Empty;
            _needsRepaint = true;
        }
        private void RescanSingle(SherpaOnnxModelMetadata meta)
        {
            var entry = _allEntries.FirstOrDefault(e => e.Metadata == meta);
            if (entry == null)
            {
                return;
            }
            entry.VerifyFailed = false;
            entry.VerifyMessage = string.Empty;
            entry.IsDownloaded = null;
            Task.Run(async () =>
            {
                bool ok = false;
                try
                {
                    ok = await SherpaUtils.Prepare.CheckIsModelDownloadedAsync(meta);
                }
                catch
                {
                    ok = false;
                }
                entry.IsDownloaded = ok;
                _needsRepaint = true;
            });
        }

        /// <summary>
        /// Deletes the model folder for a given metadata.
        /// </summary>
        private bool DeleteModelFolder(SherpaOnnxModelMetadata meta, string modelDirOverride = null)
        {
            try
            {
                string moduleRoot = SherpaPathResolver.GetModuleRootPath(meta.moduleType);
                string modelDir = modelDirOverride ?? Path.Combine(moduleRoot, meta.modelId);
                if (!Directory.Exists(modelDir))
                {
                    return false;
                }
                // If inside Assets, delete via AssetDatabase to keep the project in sync
                string dataPath = Application.dataPath.Replace("\\", "/");
                string modelDirNorm = modelDir.Replace("\\", "/");
                if (modelDirNorm.StartsWith(dataPath, StringComparison.OrdinalIgnoreCase))
                {
                    string relative = "Assets" + modelDirNorm.Substring(dataPath.Length);
                    if (AssetDatabase.IsValidFolder(relative))
                    {
                        AssetDatabase.DeleteAsset(relative);
                    }
                    else
                    {
                        FileUtil.DeleteFileOrDirectory(relative);
                    }
                    AssetDatabase.Refresh();
                }
                else
                {
                    Directory.Delete(modelDir, recursive: true);
                }
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to delete model '{meta.modelId}': {e.Message}");
                return false;
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Sherpa ONNX Model Manager", EditorStyles.boldLabel);
            DrawToolbar();

            var reg = SherpaOnnxModelRegistry.Instance;
            SherpaOnnxModelManifest _mfCheck;
            if (_isLoadingManifest || reg.IsInitializing || !reg.TryGetManifest(out _mfCheck))
            {
                DrawLoadingSpinner("Fetching model manifest from GitHub…");
                return;
            }

            DrawModelList();
            DrawActiveDownloads();
        }
        private void DrawLoadingSpinner(string message = "Fetching model manifest…")
        {
            _spinnerFrame = (int)(EditorApplication.timeSinceStartup * 10) % 12;
            var icon = EditorGUIUtility.IconContent($"WaitSpin{_spinnerFrame:00}");
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                GUILayout.Label(icon, GUILayout.Width(20), GUILayout.Height(20));
                GUILayout.Label(message, EditorStyles.wordWrappedLabel);
            }
            _needsRepaint = true; // keep the animation running via the repaint pump
        }

        private void OnRegistryInitialized()
        {
            // Ensure main-thread UI update
            EditorApplication.delayCall += () =>
            {
                _isLoadingManifest = false;
                RefreshData();
                _needsRepaint = true;
            };
        }

        /// <summary>
        /// Responsive toolbar: adapts to window width, keeps controls readable and structured.
        /// </summary>
        private void DrawToolbar()
        {
            float viewWidth = EditorGUIUtility.currentViewWidth;
            bool isNarrow = viewWidth < 680f;
            bool useOverflow = !isNarrow && viewWidth < 780f; // show overflow dropdown on medium widths

            if (_searchField == null)
            {
                _searchField = new SearchField();
            }

            if (!isNarrow)
            {
                using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
                {
                    // Search field (toolbar height)
                    float searchMin = Mathf.Min(180f, viewWidth * 0.60f);
                    Rect searchRect = GUILayoutUtility.GetRect(
                        searchMin, 320f,
                        EditorGUIUtility.singleLineHeight, EditorGUIUtility.singleLineHeight,
                        EditorStyles.toolbarSearchField,
                        GUILayout.ExpandWidth(true)
                    );
                    _searchQuery = _searchField.OnToolbarGUI(searchRect, _searchQuery);
                    GUILayout.Space(4);

                    // Category
                    GUILayout.Label("Category", EditorStyles.miniLabel);
                    _selectedCategoryIndex = EditorGUILayout.Popup(
                        _selectedCategoryIndex,
                        _categoriesArray,
                        EditorStyles.toolbarPopup,
                        GUILayout.MaxWidth(Mathf.Min(260f, viewWidth * 0.25f)),
                        GUILayout.MinWidth(120f),
                        GUILayout.ExpandWidth(false));

                    GUILayout.Space(4);

                    // Language
                    GUILayout.Label("Language", EditorStyles.miniLabel);
                    _selectedLanguageIndex = EditorGUILayout.Popup(
                        _selectedLanguageIndex,
                        _languagesArray,
                        EditorStyles.toolbarPopup,
                        GUILayout.MaxWidth(Mathf.Min(220f, viewWidth * 0.20f)),
                        GUILayout.MinWidth(100f),
                        GUILayout.ExpandWidth(false));

                    GUILayout.FlexibleSpace();

                    if (useOverflow)
                    {
                        if (EditorGUILayout.DropdownButton(new GUIContent("More"), FocusType.Passive, EditorStyles.toolbarDropDown, GUILayout.Width(60)))
                        {
                            var menu = new GenericMenu();
                            menu.AddItem(new GUIContent("Clear"), false, () => { ResetFilters(); });
                            menu.AddItem(new GUIContent("Refresh"), false, () => { ForceRefreshManifest(); });
                            menu.AddItem(new GUIContent("Rescan"), false, () => { KickoffDownloadStatusScan(); });
                            var r = GUILayoutUtility.GetLastRect();
                            menu.DropDown(new Rect(r.x, r.y + r.height, 0, 0));
                            GUIUtility.ExitGUI();
                        }
                    }
                    else
                    {
                        if (GUILayout.Button(new GUIContent("Clear", "Reset search and all filters"), EditorStyles.toolbarButton, GUILayout.Width(56)))
                        {
                            ResetFilters();
                        }

                        if (GUILayout.Button(new GUIContent("Refresh", "Force-reload manifest (Uninitialize + re-fetch)"), EditorStyles.toolbarButton, GUILayout.Width(70)))
                        {
                            ForceRefreshManifest();
                        }

                        if (GUILayout.Button(new GUIContent("Rescan", "Re-check download status for all models"), EditorStyles.toolbarButton, GUILayout.Width(70)))
                        {
                            KickoffDownloadStatusScan();
                        }
                    }
                }
                EditorGUILayout.Space(3);
                return;
            }

            // Narrow layout: stacked rows inside a framed group
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                // Row 1: Search + overflow actions
                using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
                {
                    Rect narrowSearchRect = GUILayoutUtility.GetRect(80f, EditorGUIUtility.singleLineHeight, GUILayout.ExpandWidth(true));
                    _searchQuery = _searchField.OnToolbarGUI(narrowSearchRect, _searchQuery);

                    if (GUILayout.Button(new GUIContent("Clear", "Reset search and all filters"), EditorStyles.toolbarButton, GUILayout.Width(56)))
                    {
                        ResetFilters();
                    }

                    if (GUILayout.Button(new GUIContent("Refresh", "Force-reload manifest (Uninitialize + re-fetch)"), EditorStyles.toolbarButton, GUILayout.Width(70)))
                    {
                        ForceRefreshManifest();
                    }

                    if (GUILayout.Button(new GUIContent("Rescan", "Re-check download status for all models"), EditorStyles.toolbarButton, GUILayout.Width(70)))
                    {
                        KickoffDownloadStatusScan();
                    }
                }

                // Row 2: Filters (expand to available width)
                using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
                {
                    GUILayout.Label("Category", EditorStyles.miniLabel, GUILayout.Width(60));
                    _selectedCategoryIndex = EditorGUILayout.Popup(
                        _selectedCategoryIndex,
                        _categoriesArray,
                        EditorStyles.toolbarPopup,
                        GUILayout.ExpandWidth(true));

                    GUILayout.Space(6);

                    GUILayout.Label("Language", EditorStyles.miniLabel, GUILayout.Width(60));
                    _selectedLanguageIndex = EditorGUILayout.Popup(
                        _selectedLanguageIndex,
                        _languagesArray,
                        EditorStyles.toolbarPopup,
                        GUILayout.ExpandWidth(true));
                }
            }

            EditorGUILayout.Space(3);
        }

        /// <summary>
        /// Draws the scrollable list of model entries with occlusion culling (virtualization) based on scroll position and window size.
        /// </summary>
        private void DrawModelList()
        {
            // Determine viewport after toolbar; this operates reliably since DrawModelList()
            // is called after header + toolbar have been laid out.
            float prevBottom = GUILayoutUtility.GetLastRect().yMax;
            float viewHeight = Mathf.Max(64f, position.height - prevBottom - 6f);
            float viewWidth = Mathf.Max(200f, position.width - 20f); // leave space for scrollbar

            // Precompute total content height + the first and last visible items using a buffered window.
            // The buffer reduces redraw pop-in during fast scrolling.
            const float buffer = 400f;
            float startY = Mathf.Max(0f, _scrollPosition.y - buffer);
            float endY = _scrollPosition.y + viewHeight + buffer;

            float totalHeight = 0f;
            float topPadding = 0f;
            float drawnHeight = 0f;
            bool hasAny = false;

            // Identify first visible item index and compute top padding.
            int firstIndex = -1;
            // We iterate twice at most: once to find the visible range, once to finalize total height if we break early.
            for (int i = 0; i < _allEntries.Count; i++)
            {
                var entry = _allEntries[i];
                if (!IsVisible(entry))
                {
                    continue; // filtered-out entries do not contribute to height or scroll range
                }

                float h = CalcEntryHeight(entry, viewWidth);
                float next = totalHeight + h;

                if (firstIndex < 0 && next >= startY)
                {
                    firstIndex = i;
                    topPadding = totalHeight;
                    hasAny = true;
                }

                // If we've passed the end of the buffered window, we can stop early, but we still need total height.
                if (next >= endY && hasAny)
                {
                    // Count the current item before finishing the rest to keep scroll math stable.
                    totalHeight = next;
                    // Finish computing total by scanning the remainder without drawing.
                    for (int j = i + 1; j < _allEntries.Count; j++)
                    {
                        var e2 = _allEntries[j];
                        if (!IsVisible(e2)) { continue; }
                        totalHeight += CalcEntryHeight(e2, viewWidth);
                    }
                    break;
                }
                totalHeight = next;
            }

            // If nothing matched filters, draw an empty scroll area with minimal height.
            if (!hasAny)
            {
                // Ensure totalHeight reflects the sum for scrollbars (even if zero).
                totalHeight = 0f;
                foreach (var e in _allEntries)
                {
                    if (!IsVisible(e)) { continue; }
                    totalHeight += CalcEntryHeight(e, viewWidth);
                }
                _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
                if (Mathf.Approximately(totalHeight, 0f))
                {
                    EditorGUILayout.HelpBox("No models match the current filters.", MessageType.Info);
                }
                else
                {
                    GUILayout.Space(totalHeight);
                }
                EditorGUILayout.EndScrollView();
                return;
            }

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            // Top spacer to skip all items above the first visible one.
            GUILayout.Space(topPadding);

            // Draw visible window until we pass endY
            float currentY = topPadding;
            for (int i = firstIndex; i < _allEntries.Count; i++)
            {
                var entry = _allEntries[i];
                if (!IsVisible(entry)) { continue; }

                float h = CalcEntryHeight(entry, viewWidth);

                if (currentY > endY) { break; } // we've drawn beyond the buffered window

                DrawEntry(entry);
                drawnHeight += h;
                currentY += h;
            }

            float bottomPadding = Mathf.Max(0f, totalHeight - topPadding - drawnHeight);
            GUILayout.Space(bottomPadding);

            EditorGUILayout.EndScrollView();
        }

        /// <summary>
        /// Compute a tiny layout snapshot during Layout so both Layout and Repaint use the same structure.
        /// </summary>
        private void UpdateLayoutSnapshot(ModelEntry entry, float viewWidth)
        {
            if (Event.current.type != EventType.Layout)
            {
                return;
            }

            // Whether to show progress area this frame
            entry.ProgressVisible = _activeDownloads.Any(x => x.Metadata == entry.Metadata);

            // Pre-compute helpbox height for verification message
            bool showVerify = entry.VerifyFailed && !string.IsNullOrEmpty(entry.VerifyMessage);
            if (showVerify)
            {
                float usableWidth = Mathf.Max(60f, viewWidth - 40f);
                entry.HelpHeight = EditorStyles.helpBox.CalcHeight(new GUIContent(entry.VerifyMessage), usableWidth);
            }
            else
            {
                entry.HelpHeight = 0f;
            }
        }

        /// <summary>
        /// Predicts the vertical space that a `ModelEntry` will occupy in the list, given the current window width.
        /// Keep this in sync with `DrawEntry` layout to ensure smooth virtualization.
        /// </summary>
        private float CalcEntryHeight(ModelEntry entry, float viewWidth)
        {
            float h = 0f;
            float line = EditorGUIUtility.singleLineHeight;
            // bool hasFiles = entry.Metadata?.modelFileNames != null && entry.Metadata.modelFileNames.Length > 0;

            // Compute minimal layout snapshot (only during Layout)
            UpdateLayoutSnapshot(entry, viewWidth);
            bool showProgress = entry.ProgressVisible;

            // Approximate padding/margins from the "box" container
            h += 6f;                 // top inner padding

            // Header row
            h += line;               // foldout or bold label

            // Meta row (Category + Language)
            h += line;

            // Files count (only if there are files)
            // if (hasFiles)
            // {
            //     h += line;
            // }

            // URL row
            h += line;

            // Expanded file list
            // if (hasFiles && entry.Expanded)
            // {
            //     int n = entry.Metadata.modelFileNames.Length;
            //     h += 2f + n * line;
            // }

            // Actions row
            h += line;

            // Verification message: always emit one control; height 0 when hidden
            if (entry.HelpHeight > 0f)
            {
                h += entry.HelpHeight;
            }

            // Progress UI: always emit two controls; height 0 when hidden
            h += showProgress ? 18f : 0f;   // progress bar rect
            h += showProgress ? line : 0f;  // Cancel button row

            // Bottom spacing between entries
            h += 10f;

            return h;
        }

        /// <summary>
        /// Draw a single entry row (compact). Hides Files row and foldout when there are no files.
        /// </summary>
        private void DrawEntry(ModelEntry entry)
        {
            // bool hasFiles = entry.Metadata?.modelFileNames != null && entry.Metadata.modelFileNames.Length > 0;

            // Compute minimal snapshot for this frame
            float viewWidth = Mathf.Max(200f, position.width - 20f);
            UpdateLayoutSnapshot(entry, viewWidth);

            EditorGUILayout.BeginVertical("box");

            // Header row: Only show foldout if there are files; otherwise show a bold label.
            EditorGUILayout.BeginHorizontal();
            // if (hasFiles)
            // {
            //     entry.Expanded = EditorGUILayout.Foldout(entry.Expanded, entry.Metadata.modelId, true);
            // }
            // else
            // {
            GUILayout.Label(entry.Metadata.modelId, EditorStyles.boldLabel);
            entry.Expanded = false; // ensure not expanded when no files
            // }
            GUILayout.FlexibleSpace();
            DrawDownloadStatusPill(entry);
            EditorGUILayout.EndHorizontal();

            // Meta row: Category & Language as compact labels
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Category: " + entry.Category, EditorStyles.miniLabel);

            // Language list label (already comma-joined)
            EditorGUILayout.LabelField("Language: " + entry.LanguagesLabel, EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();

            // Files count row (only if files exist)
            // if (hasFiles)
            // {
            //     EditorGUILayout.LabelField("Files: " + entry.Metadata.modelFileNames.Length, EditorStyles.miniLabel);
            // }

            // Download URL display
            var url = entry.Metadata.downloadUrl;
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("URL: ", EditorStyles.miniLabel, GUILayout.Width(40));
                if (string.IsNullOrEmpty(url))
                {
                    EditorGUILayout.LabelField("—", EditorStyles.miniLabel);
                }
                else
                {
                    EditorGUILayout.SelectableLabel(url, EditorStyles.miniLabel, GUILayout.Height(EditorGUIUtility.singleLineHeight));
                }
            }

            // Expandable file list (only if has files)
            // if (hasFiles && entry.Expanded)
            // {
            //     EditorGUI.indentLevel++;
            //     var files = entry.Metadata.modelFileNames;
            //     for (int i = 0; i < files.Length; i++)
            //     {
            //         EditorGUILayout.LabelField(files[i], EditorStyles.miniLabel);
            //     }
            //     EditorGUI.indentLevel--;
            //     EditorGUILayout.Space(2);
            // }

            // Actions row
            EditorGUILayout.BeginHorizontal();
            bool hasUrlInMetadata = !string.IsNullOrEmpty(entry.Metadata.downloadUrl);
            bool isDownloadingThis = _activeDownloads.Any(d => d.Metadata == entry.Metadata);

            // Push all operation buttons to the RIGHT
            GUILayout.FlexibleSpace();

            // Copy Name button
            if (GUILayout.Button(new GUIContent("Copy Name", "Copy modelId to clipboard"), GUILayout.Width(90)))
            {
                EditorGUIUtility.systemCopyBuffer = entry.Metadata.modelId;
                ShowNotification(new GUIContent($"Copied: {entry.Metadata.modelId}"));
            }

            // Copy URL button
            if (GUILayout.Button(new GUIContent("Copy URL", "Copy download URL to clipboard"), GUILayout.Width(80)))
            {
                if (!string.IsNullOrEmpty(entry.Metadata.downloadUrl))
                {
                    EditorGUIUtility.systemCopyBuffer = entry.Metadata.downloadUrl;
                    ShowNotification(new GUIContent("Copied URL"));
                }
                else
                {
                    ShowNotification(new GUIContent("No URL"));
                }
            }

            // Reveal button
            string moduleRoot = SherpaPathResolver.GetModuleRootPath(entry.Metadata.moduleType);
            string modelDir = Path.Combine(moduleRoot, entry.Metadata.modelId);
            EditorGUI.BeginDisabledGroup(!Directory.Exists(modelDir));
            if (GUILayout.Button(new GUIContent("Reveal", "Reveal model folder in Finder/Explorer"), GUILayout.Width(70)))
            {
                EditorUtility.RevealInFinder(modelDir);
            }
            EditorGUI.EndDisabledGroup();

            // Rescan button
            if (GUILayout.Button(new GUIContent("Rescan", "Re-check whether this model is fully downloaded & verified"), GUILayout.Width(70)))
            {
                RescanSingle(entry.Metadata);
            }

            // Download button
            EditorGUI.BeginDisabledGroup(!hasUrlInMetadata || isDownloadingThis || entry.IsDownloaded == true);
            string dlLabel = entry.VerifyFailed ? "Re-download" : "Download";
            var dlContent = hasUrlInMetadata
                ? new GUIContent(dlLabel, entry.VerifyFailed
                    ? "Verification failed previously. Click to re-download."
                    : "Download model archive")
                : new GUIContent("No URL", "No download URL in metadata");

            if (GUILayout.Button(dlContent, GUILayout.Width(90)))
            {
                if (hasUrlInMetadata)
                {
                    StartDownload(entry.Metadata);
                }
            }
            EditorGUI.EndDisabledGroup();

            // Delete button
            bool canDelete = Directory.Exists(modelDir);
            EditorGUI.BeginDisabledGroup(!canDelete);
            if (GUILayout.Button(new GUIContent("Delete", "Delete local model files"), GUILayout.Width(70)))
            {
                bool confirm = EditorUtility.DisplayDialog("Delete Model",
                    $"Are you sure you want to delete local files for '{entry.Metadata.modelId}'?",
                    "Delete", "Cancel");
                if (confirm)
                {
                    if (DeleteModelFolder(entry.Metadata, modelDir))
                    {
                        ShowNotification(new GUIContent($"Deleted: {entry.Metadata.modelId}"));
                        _activeDownloads.RemoveAll(d => d.Metadata == entry.Metadata);
                        _needsRepaint = true;
                        RescanSingle(entry.Metadata);
                    }
                    else
                    {
                        ShowNotification(new GUIContent($"Delete failed: {entry.Metadata.modelId}"));
                    }
                }
            }
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.EndHorizontal();

            // Verification message: always emit one control; height 0 when hidden
            Rect verifyRect = EditorGUILayout.GetControlRect(false, entry.HelpHeight);
            if (entry.HelpHeight > 0f)
            {
                EditorGUI.HelpBox(verifyRect, entry.VerifyMessage, MessageType.Error);
            }

            // Progress UI: always emit two controls; height 0 when hidden
            var currentDownload = _activeDownloads.FirstOrDefault(d => d.Metadata == entry.Metadata);
            bool showProgress = entry.ProgressVisible;
            float prog = currentDownload?.Progress ?? 0f;
            string status = currentDownload?.Status ?? string.Empty;

            // Progress bar line
            Rect pRect = EditorGUILayout.GetControlRect(false, showProgress ? 18f : 0f);
            if (showProgress)
            {
                EditorGUI.ProgressBar(pRect, Mathf.Clamp01(prog), string.IsNullOrEmpty(status) ? "Working…" : status);
            }

            // Cancel row (draw manually to keep control count constant without extra GUILayout containers)
            Rect cancelRect = EditorGUILayout.GetControlRect(false, showProgress ? EditorGUIUtility.singleLineHeight : 0f);
            if (showProgress)
            {
                Rect btnRect = new Rect(cancelRect.xMax - 70f, cancelRect.y, 70f, cancelRect.height);
                bool canCancel = currentDownload != null && !currentDownload.IsCanceled;
                using (new EditorGUI.DisabledScope(!canCancel))
                {
                    if (GUI.Button(btnRect, "Cancel") && currentDownload != null)
                    {
                        CancelDownload(currentDownload, "Canceled by user");
                    }
                }
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
        }

        private void DrawDownloadStatusPill(ModelEntry entry)
        {
            string text;
            Color col;
            if (entry.VerifyFailed)
            {
                text = "Verify Failed";
                col = new Color(0.85f, 0.2f, 0.2f); // stronger red
            }
            else if (entry.IsDownloaded == null)
            {
                text = "Checking…";
                col = new Color(1.0f, 0.65f, 0f);
            }
            else if (entry.IsDownloaded == true)
            {
                text = "Downloaded";
                col = new Color(0.25f, 0.75f, 0.25f);
            }
            else
            {
                text = "Not Downloaded";
                col = new Color(0.75f, 0.3f, 0.3f);
            }
            var prev = GUI.color;
            GUI.color = col;
            GUILayout.Label("■", GUILayout.Width(12));
            GUI.color = prev;

            EditorGUILayout.LabelField(text, EditorStyles.miniBoldLabel, GUILayout.Width(110));
        }

        /// <summary>
        /// Draws a summary list of active downloads with progress bars.
        /// </summary>
        private void DrawActiveDownloads()
        {
            if (_activeDownloads.Count == 0)
            {
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Active Downloads", EditorStyles.boldLabel);
            foreach (var download in _activeDownloads.ToArray())
            {
                EditorGUILayout.LabelField(download.Metadata.modelId + (string.IsNullOrEmpty(download.CurrentPhase) ? string.Empty : $"  •  {download.CurrentPhase}"));
                Rect rect = EditorGUILayout.GetControlRect(false, 18f);
                EditorGUI.ProgressBar(rect, download.Progress, download.Status);

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();
                    EditorGUI.BeginDisabledGroup(download.IsCanceled);
                    if (GUILayout.Button("Cancel", GUILayout.Width(70)))
                    {
                        CancelDownload(download, "Canceled by user");
                    }
                    EditorGUI.EndDisabledGroup();
                }
            }
        }

        /// <summary>
        /// Finalize a download task: remove from active list, trigger repaint, and optionally notify.
        /// Ensures we stop showing the progress UI once a download is finished or failed.
        /// </summary>
        private void CompleteDownload(DownloadTask task, bool success, string notifyMessage = null)
        {
            // Ensure UI changes occur on the main thread
            EditorApplication.delayCall += () =>
            {
                // Remove any matching active downloads for this metadata
                _activeDownloads.RemoveAll(d => d.Metadata == task.Metadata);
                isDownloading = false;
                _needsRepaint = true;

                if (!string.IsNullOrEmpty(notifyMessage))
                {
                    ShowNotification(new GUIContent(notifyMessage));
                }
            };
        }
        private void CancelAllActiveOperations()
        {
            foreach (var d in _activeDownloads.ToList())
            {
                CancelDownload(d, "Window closed");
            }
            _activeDownloads.Clear();
            isDownloading = false;
            _needsRepaint = true;
        }

        private void CancelDownload(DownloadTask task, string reason = "Canceled by user")
        {
            try
            {
                task.IsCanceled = true;
                try { task.Cts?.Cancel(); } catch { }
                try { task.Request?.Abort(); } catch { }

                if (task.UpdateHandler != null)
                {
                    try { EditorApplication.update -= task.UpdateHandler; } catch { }
                    task.UpdateHandler = null;
                }

                // Clean any partial archive
                if (!string.IsNullOrEmpty(task.DestPath) && File.Exists(task.DestPath))
                {
                    try { SherpaFileUtils.Delete(task.DestPath); } catch { }
                }

                task.Status = reason;
                task.Progress = 1f;

                // Neutral UI state for cancel (not a red error)
                SetVerifyFailed(task.Metadata, false);
            }
            finally
            {
                // Always prune footer row & notify
                CompleteDownload(task, success: false, notifyMessage: $"Canceled: {task.Metadata.modelId}");
                // And refresh the pill/progress on the list row
                RescanSingle(task.Metadata);
            }
        }
        /// <summary>
        /// Determines whether a model entry should be displayed based on the current filters.
        /// </summary>
        private bool IsVisible(ModelEntry entry)
        {
            // Category filter
            if (_selectedCategoryIndex > 0 && entry.Category != _categories[_selectedCategoryIndex])
            {
                return false;
            }
            // Language filter

            if (_selectedLanguageIndex > 0)
            {
                var selected = _languages[_selectedLanguageIndex];
                if (entry.Languages == null || !entry.Languages.Contains(selected))
                { return false; }
            }
            // Search filter

            if (!string.IsNullOrWhiteSpace(_searchQuery) &&
                !entry.Metadata.modelId.ToLowerInvariant().Contains(_searchQuery.ToLowerInvariant()))
            {

                return false;
            }


            return true;
        }
        /// <summary>
        /// 从 modelId 解析受支持语言，返回**单语种**的规范小写名列表；
        /// 永不返回复合字符串；若未命中则返回 ["other"]。
        /// 规范集：chinese, cantonese, english, japanese, korean, thai, vietnamese, russian,
        ///        french, spanish, german, dutch, danish, czech, catalan, arabic
        /// </summary>
        private static List<string> ParseLanguages(string id)
        {
            var order = new List<string> {
        "chinese","cantonese","english","japanese","korean","thai","vietnamese","russian",
        "french","spanish","german","dutch","danish","czech","catalan","arabic"
    };
            var canon = new HashSet<string>(order);
            var found = new List<string>();

            if (string.IsNullOrWhiteSpace(id))
            {

                return new List<string> { "other" };
            }


            string s = id.ToLowerInvariant();

            void Add(string label)
            {
                if (canon.Contains(label) && !found.Contains(label))
                {
                    found.Add(label);
                }

            }

            // 直观关键词（避免两字母误伤，例如 "ar"）
            if (s.Contains("chinese") || s.Contains("mandarin"))
            {
                Add("chinese");
            }


            if (s.Contains("cantonese"))
            {
                Add("cantonese");
            }


            if (s.Contains("english"))
            {
                Add("english");
            }


            if (s.Contains("japanese"))
            {
                Add("japanese");
            }


            if (s.Contains("korean"))
            {
                Add("korean");
            }


            if (s.Contains("thai"))
            {
                Add("thai");
            }


            if (s.Contains("vietnamese"))
            {
                Add("vietnamese");
            }


            if (s.Contains("russian"))
            {
                Add("russian");
            }


            if (s.Contains("french"))
            {
                Add("french");
            }


            if (s.Contains("spanish"))
            {
                Add("spanish");
            }


            if (s.Contains("german"))
            {
                Add("german");
            }


            if (s.Contains("dutch"))
            {
                Add("dutch");
            }

            if (s.Contains("danish"))
            {
                Add("danish");
            }

            if (s.Contains("czech"))
            {
                Add("czech");
            }

            if (s.Contains("catalan"))
            {
                Add("catalan");
            }

            if (s.Contains("arabic"))
            {
                Add("arabic");
            }

            // 代码/locale 扫描（支持 zh/en/ja/ko/th/vi/ru/fr/es/de/nl/da/cs/ca/ar/yue 等）

            char[] seps = new[] { '-', '_', '.', '/', ' ', '+' };
            var parts = s.Split(seps, StringSplitOptions.RemoveEmptyEntries);
            foreach (var p in parts)
            {
                if (p == "zh" || p.StartsWith("zh"))
                {
                    Add("chinese");
                }

                else if (p == "yue" || p.Contains("cant"))
                {
                    Add("cantonese");
                }

                else if (p == "en" || p.StartsWith("en"))
                {
                    Add("english");
                }

                else if (p == "ja" || p.StartsWith("ja"))
                {
                    Add("japanese");
                }

                else if (p == "ko" || p.StartsWith("ko"))
                {
                    Add("korean");
                }

                else if (p == "th" || p.StartsWith("th"))
                {
                    Add("thai");
                }

                else if (p == "vi" || p.StartsWith("vi"))
                {
                    Add("vietnamese");
                }

                else if (p == "ru" || p.StartsWith("ru"))
                {
                    Add("russian");
                }

                else if (p == "fr" || p.StartsWith("fr"))
                {
                    Add("french");
                }

                else if (p == "es" || p.StartsWith("es"))
                {
                    Add("spanish");
                }

                else if (p == "de" || p.StartsWith("de"))
                {
                    Add("german");
                }

                else if (p == "nl" || p.StartsWith("nl"))
                {
                    Add("dutch");
                }

                else if (p == "da" || p.StartsWith("da"))
                {
                    Add("danish");
                }

                else if (p == "cs" || p.StartsWith("cs"))
                {
                    Add("czech");
                }

                else if (p == "ca" || p.StartsWith("ca"))
                {
                    Add("catalan");
                }

                else if (p == "ar" || p.StartsWith("ar"))
                {
                    Add("arabic");
                }

            }

            if (found.Count == 0)
            {

                return new List<string> { "other" };
            }

            // 稳定顺序（按 order 排）

            found.Sort((a, b) => order.IndexOf(a).CompareTo(order.IndexOf(b)));
            return found;
        }
        /// <summary>
        /// Initiates a download for the given model metadata if a download URL is present.
        /// </summary>
        private void StartDownload(SherpaOnnxModelMetadata metadata)
        {
            if (string.IsNullOrEmpty(metadata.downloadUrl))
            {
                return;
            }


            if (isDownloading)
            {
                return; // Avoid duplicate downloads
            }

            SetVerifyFailed(metadata, false);
            var task = new DownloadTask { Metadata = metadata, Progress = 0f, Status = "Starting...", CurrentPhase = "Download" };
            _activeDownloads.Add(task);
            isDownloading = true;
            StartDownloadProgressUpdate(task); // Start the download manually
        }

        private void StartDownloadProgressUpdate(DownloadTask task)
        {
            string url = task.Metadata.downloadUrl;

            // Resolve the expected archive path using the runtime’s Prepare logic
            bool isCompressed;
            string moduleDir, modelDir, fileName;
            string destPath = SherpaUtils.Prepare.ResolveDownloadFilePath(task.Metadata, out moduleDir, out modelDir, out fileName, out isCompressed);
            task.DestPath = destPath;
            task.ModuleDir = moduleDir;
            task.IsCompressed = isCompressed;
            // Ensure the download directory exists
            string downloadDir = Path.GetDirectoryName(destPath);
            if (!string.IsNullOrEmpty(downloadDir))
            {
                Directory.CreateDirectory(downloadDir);
            }

            var reporter = new SherpaOnnxFeedbackReporterAdapter((p, msg) =>
            {
                task.Progress = p;
                task.Status = msg;
                _needsRepaint = true;
            });

            var request = UnityWebRequest.Get(url);
            task.Request = request;
            var operation = request.SendWebRequest();
            EditorApplication.CallbackFunction updateHandler = null;
            updateHandler = () =>
            {
                // Fast path: external cancel
                if (task.Cts.IsCancellationRequested)
                {
                    CancelDownload(task, "Canceled");
                    return;
                }

                if (operation.isDone)
                {
                    try
                    {
                        if (task.Cts.IsCancellationRequested)
                        {
                            CancelDownload(task, "Canceled");
                            return;
                        }

                        if (request.result == UnityWebRequest.Result.Success)
                        {
                            var data = request.downloadHandler.data;

                            // Offload disk write so the editor doesn't stall
                            Task.Run(() => File.WriteAllBytes(destPath, data))
                            .ContinueWith(t =>
                            {
                                if (task.Cts.IsCancellationRequested)
                                {
                                    EditorApplication.delayCall += () => CancelDownload(task, "Canceled");
                                    return;
                                }

                                if (t.IsFaulted)
                                {
                                    var msg = "Save Error: " + (t.Exception?.GetBaseException().Message ?? "unknown");
                                    SetVerifyFailed(task.Metadata, true, msg);
                                    task.Progress = 1f;
                                    task.Status = msg;
                                    CompleteDownload(task, success: false, notifyMessage: $"Download failed: {task.Metadata.modelId}");
                                    RescanSingle(task.Metadata);
                                }
                                else
                                {
                                    EditorApplication.delayCall += () =>
                                    {
                                        ContinueInstallAndVerify(task, destPath, moduleDir, isCompressed, reporter, task.Cts.Token);
                                    };
                                }
                            });
                        }
                        else
                        {
                            if (task.Cts.IsCancellationRequested || task.IsCanceled)
                            {
                                CancelDownload(task, "Canceled");
                            }
                            else
                            {
                                string err = "Error: " + request.error;
                                SetVerifyFailed(task.Metadata, true, err);
                                task.Progress = 1f;
                                task.Status = err;
                                CompleteDownload(task, success: false, notifyMessage: $"Download failed: {task.Metadata.modelId}");
                                RescanSingle(task.Metadata);
                            }
                        }
                    }
                    finally
                    {
                        if (task.UpdateHandler != null)
                        {
                            EditorApplication.update -= task.UpdateHandler;
                            task.UpdateHandler = null;
                        }
                        request.Dispose();
                        _needsRepaint = true;
                    }
                    return;
                }

                // Progress 0..70% during network
                float p = Mathf.Clamp01(operation.progress);
                task.CurrentPhase = "Download";
                task.Progress = 0.7f * p;
                task.Status = $"Downloading... {(p * 100f):0}%";
                _needsRepaint = true;
            };

            task.UpdateHandler = updateHandler;
            EditorApplication.update += updateHandler;
        }

        private async void ContinueInstallAndVerify(
            DownloadTask task,
            string archivePath,
            string moduleDirectory,
            bool isCompressed,
            SherpaOnnxFeedbackReporterAdapter reporter,
            CancellationToken token)
        {
            if (token.IsCancellationRequested) { CancelDownload(task, "Canceled"); return; }
            // INSTALL (decompress if archive)
            try
            {
                task.CurrentPhase = "Install";
                reporter.Report(0.72f, "Preparing install...");

                if (isCompressed)
                {
                    var progressAdapter = new Progress<DecompressionEventArgs>(args =>
                    {
                        // Map 0..1 -> 0.70..0.90
                        float mapped = 0.7f + 0.2f * Mathf.Clamp01(args.Progress);
                        reporter.Report(mapped, $"Extracting... {(args.Progress * 100f):0}%  (Elapsed {args.ElapsedTime})");
                    });
                    var result = await SherpaDecompressHelper.DecompressAsync(
                        archivePath,
                        moduleDirectory,
                        progressAdapter,
                        cancellationToken: token);

                    if (!result.Success)
                    {
                        throw new InvalidOperationException(result.ErrorMessage ?? "Extraction failed");
                    }

                    // Clean up the archive after successful extraction
                    try { SherpaFileUtils.Delete(archivePath); } catch { /* ignore */ }

                    // Refresh only if extracting inside Assets/
                    if (moduleDirectory.Replace("\\", "/").StartsWith(Application.dataPath.Replace("\\", "/"), StringComparison.OrdinalIgnoreCase))
                    {
                        AssetDatabase.Refresh();
                    }
                }
                else
                {
                    // Non-compressed payload; nothing to extract
                    reporter.Report(0.9f, "Install skipped (no archive), verifying...");
                }
            }
            catch (OperationCanceledException)
            {
                task.CurrentPhase = "Install";
                task.Status = "Canceled";
                CancelDownload(task, "Canceled");
                _needsRepaint = true;
                return;
            }
            catch (Exception e)
            {
                task.CurrentPhase = "Install";
                task.Progress = 1f;
                task.Status = "Install Error: " + e.Message;

                // Mark verification as failed for UI list, hide progress, and notify
                SetVerifyFailed(task.Metadata, true, "Install Error: " + e.Message);
                CompleteDownload(task, success: false, notifyMessage: $"Install failed: {task.Metadata.modelId}");
                _needsRepaint = true;
                return;
            }

            if (token.IsCancellationRequested) { CancelDownload(task, "Canceled"); return; }
            // VERIFY
            task.CurrentPhase = "Verify";
            reporter.Report(0.92f, "Verifying files...");

            bool verified = false;
            try
            {
                verified = await SherpaUtils.Prepare.CheckIsModelDownloadedAsync(task.Metadata);
            }
            catch
            {
                verified = false;
            }

            if (verified)
            {
                SetVerifyFailed(task.Metadata, false);
                reporter.Report(1f, "Completed");

                // Hide the download status section and notify success
                CompleteDownload(task, success: true, notifyMessage: $"Downloaded ✓ {task.Metadata.modelId}");
            }
            else
            {
                SetVerifyFailed(task.Metadata, true, "Verification failed. Click Re-download to try again.");
                reporter.Report(1f, "Verify Failed");

                // Hide the download status section and notify failure
                CompleteDownload(task, success: false, notifyMessage: $"Verify failed: {task.Metadata.modelId}");
            }

            // Refresh the single entry so the pill reads "Downloaded" when appropriate
            RescanSingle(task.Metadata);
            _needsRepaint = true;
        }
    }
}
#endif