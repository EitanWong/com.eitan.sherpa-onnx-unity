#if UNITY_EDITOR

namespace Eitan.SherpaONNXUnity.Editor
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Text;
    using Eitan.SherpaONNXUnity.Editor.Localization;
    using Eitan.SherpaONNXUnity.Runtime;
    using Eitan.SherpaONNXUnity.Runtime.Utilities;
    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// Production-grade profiler window for SherpaONNX diagnostics. Deterministic layout,
    /// zero per-frame allocations in hot paths, and cached visualization resources.
    /// </summary>
    internal sealed class SherpaONNXProfilerWindow : EditorWindow
    {
        #region Constants

        private const int MaxLogEntries = 500;
        private const float RefreshInterval = 0.5f;
        private const float LiveRefreshInterval = 0.25f;
        private const int HistogramBuckets = 60;
        private const int HistogramWindowSeconds = 60;
        private const int PerfGraphCapacity = 512;
        private const int PerfGraphTracks = 5;

        private const float CardPadding = 8f;
        private const float SectionSpacing = 12f;
        private const float ElementSpacing = 4f;
        private const float MinCardWidth = 100f;
        private const float NarrowWindowThreshold = 500f;
        private const float VeryNarrowThreshold = 400f;
        private const float TabMinWidth = 80f;
        private const float IconSize = 16f;
        private const float SmallIconSize = 14f;
        private const float StatusIndicatorSize = 10f;
        private const float ToolbarHeight = 22f;
        private const float BadgeHeight = 18f;

        #endregion

        #region Cached Styles

        private static class Styles
        {
            private static bool s_Initialized;
            private static bool s_LoggedStyleInitIssue;

            public static GUIStyle HeaderLabel { get; private set; }
            public static GUIStyle CardBox { get; private set; }
            public static GUIStyle MonoLabel { get; private set; }
            public static GUIStyle CenteredLabel { get; private set; }
            public static GUIStyle CenteredMiniLabel { get; private set; }
            public static GUIStyle RichTextLabel { get; private set; }
            public static GUIStyle TabButton { get; private set; }
            public static GUIStyle TabButtonActive { get; private set; }
            public static GUIStyle MiniButton { get; private set; }
            public static GUIStyle StackTraceArea { get; private set; }

            public static readonly Color BackgroundDark = new Color(0.15f, 0.15f, 0.15f, 1f);
            public static readonly Color BackgroundMedium = new Color(0.22f, 0.22f, 0.22f, 1f);
            public static readonly Color BackgroundLight = new Color(0.28f, 0.28f, 0.28f, 1f);
            public static readonly Color AccentBlue = new Color(0.3f, 0.6f, 0.9f, 1f);
            public static readonly Color AccentGreen = new Color(0.3f, 0.8f, 0.5f, 1f);
            public static readonly Color AccentYellow = new Color(0.95f, 0.75f, 0.25f, 1f);
            public static readonly Color AccentRed = new Color(0.9f, 0.35f, 0.35f, 1f);
            public static readonly Color AccentPurple = new Color(0.6f, 0.5f, 0.9f, 1f);
            public static readonly Color TextMuted = new Color(0.6f, 0.6f, 0.6f, 1f);

            public static bool EnsureInitialized()
            {
                if (s_Initialized)
                {
                    return true;
                }

                try
                {
                    var label = EditorStyles.label;
                    var boldLabel = EditorStyles.boldLabel;
                    var centeredMini = EditorStyles.centeredGreyMiniLabel;
                    var toolbarButton = EditorStyles.toolbarButton;
                    var miniButton = EditorStyles.miniButton;
                    var textArea = EditorStyles.textArea;
                    var standardFont = EditorStyles.standardFont;

                    if (label == null || boldLabel == null || centeredMini == null || toolbarButton == null || miniButton == null || textArea == null)
                    {
                        return false;
                    }

                    HeaderLabel = new GUIStyle(boldLabel)
                    {
                        fontSize = 13,
                        margin = new RectOffset(4, 4, 8, 4)
                    };

                    CardBox = new GUIStyle("HelpBox")
                    {
                        padding = new RectOffset(8, 8, 6, 6),
                        margin = new RectOffset(2, 2, 2, 2)
                    };

                    MonoLabel = new GUIStyle(label)
                    {
                        font = standardFont ?? label.font,
                        fontSize = 11,
                        richText = true
                    };

                    CenteredLabel = new GUIStyle(label)
                    {
                        alignment = TextAnchor.MiddleCenter
                    };

                    CenteredMiniLabel = new GUIStyle(centeredMini)
                    {
                        fontSize = 9,
                        wordWrap = true,
                        alignment = TextAnchor.UpperCenter
                    };

                    RichTextLabel = new GUIStyle(label)
                    {
                        richText = true,
                        wordWrap = true,
                        fontSize = 11
                    };

                    TabButton = new GUIStyle(toolbarButton)
                    {
                        fixedHeight = 26,
                        fontSize = 11,
                        padding = new RectOffset(10, 10, 4, 4)
                    };

                    TabButtonActive = new GUIStyle(TabButton)
                    {
                        fontStyle = FontStyle.Bold
                    };

                    MiniButton = new GUIStyle(miniButton)
                    {
                        padding = new RectOffset(6, 6, 2, 2),
                        fixedHeight = 18
                    };

                    StackTraceArea = new GUIStyle(textArea)
                    {
                        fontSize = 9,
                        wordWrap = true,
                        richText = false
                    };

                    s_Initialized = true;
                    s_LoggedStyleInitIssue = false;
                    return true;
                }
                catch (Exception)
                {
                    if (!s_LoggedStyleInitIssue)
                    {
                        // Debug.LogWarning($"SherpaONNX Profiler: editor styles not ready. Will retry. ({ex.Message})");
                        s_LoggedStyleInitIssue = true;
                    }

                    return false;
                }
            }

            public static Color GetLogLevelColor(SherpaLogLevel level)
            {
                return level switch
                {
                    SherpaLogLevel.Error => AccentRed,
                    SherpaLogLevel.Warning => AccentYellow,
                    SherpaLogLevel.Info => AccentBlue,
                    SherpaLogLevel.Verbose => AccentGreen,
                    SherpaLogLevel.Trace => AccentPurple,
                    _ => TextMuted
                };
            }
        }

        #endregion

        #region Cached Layout

        private static class LayoutCache
        {
            public static readonly GUILayoutOption[] ToolbarIcon = { GUILayout.Width(24f), GUILayout.Height(ToolbarHeight) };
            public static readonly GUILayoutOption[] ToolbarWide = { GUILayout.MaxWidth(90f), GUILayout.Height(ToolbarHeight) };
            public static readonly GUILayoutOption[] ToolbarLabel = { GUILayout.Width(55f) };
            public static readonly GUILayoutOption[] TabButton = { GUILayout.MinWidth(TabMinWidth) };
            public static readonly GUILayoutOption[] StatusBadge = { GUILayout.Width(60f), GUILayout.Height(BadgeHeight) };
            public static readonly GUILayoutOption[] SmallIcon = { GUILayout.Width(SmallIconSize), GUILayout.Height(SmallIconSize) };
            public static readonly GUILayoutOption[] Icon = { GUILayout.Width(IconSize), GUILayout.Height(IconSize) };
            public static readonly GUILayoutOption[] StatCard = { GUILayout.MinWidth(MinCardWidth), GUILayout.ExpandWidth(true) };
            public static readonly GUILayoutOption[] MetricLabel = { GUILayout.Width(100f) };
            public static readonly GUILayoutOption[] MetricValue = { GUILayout.Width(80f) };
            public static readonly GUILayoutOption[] SearchField = { GUILayout.Width(160f) };
        }

        #endregion

        #region Cached Icons

        private static class Icons
        {
            private static bool s_Initialized;

            public static GUIContent Refresh { get; private set; }
            public static GUIContent Clear { get; private set; }
            public static GUIContent Copy { get; private set; }
            public static GUIContent Info { get; private set; }
            public static GUIContent Warning { get; private set; }
            public static GUIContent Error { get; private set; }
            public static GUIContent Play { get; private set; }
            public static GUIContent Pause { get; private set; }
            public static GUIContent Record { get; private set; }

            public static void EnsureInitialized()
            {
                if (s_Initialized)
                {
                    return;
                }

                Refresh = LoadIcon("d_Refresh", "Refresh");
                Clear = LoadIcon("d_winbtn_win_close", "Clear");
                Copy = LoadIcon("Clipboard", "Copy");
                Info = LoadIcon("console.infoicon.sml", "Info");
                Warning = LoadIcon("console.warnicon.sml", "Warning");
                Error = LoadIcon("console.erroricon.sml", "Error");
                Play = LoadIcon("d_PlayButton", "Play");
                Pause = LoadIcon("d_PauseButton", "Pause");
                Record = LoadIcon("d_Record Off", "Record");

                s_Initialized = true;
            }

            private static GUIContent LoadIcon(string iconName, string fallbackText)
            {
                try
                {
                    var content = EditorGUIUtility.IconContent(iconName);
                    if (content != null && content.image != null)
                    {
                        return content;
                    }
                }
                catch
                {
                    // Ignore - fallback below
                }

                return new GUIContent(fallbackText);
            }

            public static GUIContent GetLogLevelIcon(SherpaLogLevel level)
            {
                return level switch
                {
                    SherpaLogLevel.Error => Error,
                    SherpaLogLevel.Warning => Warning,
                    _ => Info
                };
            }
        }

        #endregion

        #region Cached Content

        private static class Content
        {
            public static readonly GUIContent RefreshButton = new GUIContent();
            public static readonly GUIContent LiveToggle = new GUIContent();
            public static readonly GUIContent CopyButton = new GUIContent();

            public static readonly GUIContent TabOverview = new GUIContent();
            public static readonly GUIContent TabModules = new GUIContent();
            public static readonly GUIContent TabLogs = new GUIContent();
            public static readonly GUIContent TabPerformance = new GUIContent();

            public static readonly GUIContent AutoScrollToggle = new GUIContent();
            public static readonly GUIContent PauseToggle = new GUIContent();
            public static readonly GUIContent StacksToggle = new GUIContent();
            public static readonly GUIContent ClearSearchButton = new GUIContent("×");

            public static readonly GUIContent CopySmall = new GUIContent();
            public static readonly GUIContent RevealButton = new GUIContent();

            public static readonly GUIContent[] LogLevelBadges =
            {
                new GUIContent("ERR"),
                new GUIContent("WRN"),
                new GUIContent("INF"),
                new GUIContent("VRB"),
                new GUIContent("TRC")
            };

            public static void UpdateLocalization()
            {
                RefreshButton.text = " " + Tr(SherpaONNXL10n.Profiler.ButtonRefresh, "Refresh");
                RefreshButton.tooltip = Tr(SherpaONNXL10n.Profiler.TooltipRefresh, "Re-scan modules");

                LiveToggle.tooltip = Tr(SherpaONNXL10n.Profiler.ToggleLive, "Live refresh");
                CopyButton.tooltip = Tr(SherpaONNXL10n.Profiler.TooltipCopyDiagnostics, "Copy diagnostics");

                TabOverview.text = Tr(SherpaONNXL10n.Profiler.TabOverview, "Overview");
                TabModules.text = Tr(SherpaONNXL10n.Profiler.TabModules, "Modules");
                TabLogs.text = Tr(SherpaONNXL10n.Profiler.TabLogs, "Logs");
                TabPerformance.text = Tr(SherpaONNXL10n.Profiler.TabPerformance, "Performance");

                AutoScrollToggle.tooltip = Tr(SherpaONNXL10n.Profiler.LogToggleAutoScroll, "Auto-scroll");
                PauseToggle.tooltip = Tr(SherpaONNXL10n.Profiler.LogTogglePause, "Pause");
                StacksToggle.text = Tr(SherpaONNXL10n.Profiler.LogToggleStacks, "Stacks");

                CopySmall.text = Tr(SherpaONNXL10n.Profiler.ButtonCopy, "Copy");
                RevealButton.text = Tr(SherpaONNXL10n.Profiler.ButtonReveal, "Reveal");
            }

            public static void UpdateIcons()
            {
                Icons.EnsureInitialized();

                RefreshButton.image = Icons.Refresh.image;
                LiveToggle.image = Icons.Record.image;
                CopyButton.image = Icons.Copy.image;
                AutoScrollToggle.image = Icons.Play.image;
                PauseToggle.image = Icons.Pause.image;
            }
        }

        #endregion

        #region Enums and Data Structures

        private enum Tab { Overview, Modules, Logs, Performance }
        private enum ModuleStatus { Pending, Initializing, Ready, Error, Disposed }
        private enum PerfTrack { AvgDuration, LastDuration, ActiveTasks, LogVolume, ErrorModules }

        private struct StatCardData
        {
            public GUIContent Title;
            public GUIContent Value;
            public Color Accent;
        }

        private struct ModuleViewData
        {
            public ModuleStatus Status;
            public Color StatusColor;
            public GUIContent StatusLabel;
            public GUIContent ModelId;
            public GUIContent ModuleType;
            public GUIContent ActiveTasks;
            public GUIContent TotalStarted;
            public GUIContent Completed;
            public GUIContent AvgDuration;
            public GUIContent LastDuration;
            public GUIContent ErrorMessage;
            public DateTime LastStatusUtc;
            public bool HasError;
            public bool Disposed;
        }

        private struct IssueViewData
        {
            public GUIContent Title;
            public GUIContent Message;
            public GUIContent Time;
            public Color AccentColor;
        }

        private struct LogRenderCache
        {
            public SherpaLogEntry Entry;
            public GUIContent Time;
            public GUIContent LevelLabel;
            public GUIContent Category;
            public GUIContent Message;
            public GUIContent Thread;
            public GUIContent Stack;
        }

        #endregion

        #region State Fields

        private Tab _currentTab = Tab.Overview;
        private Vector2 _mainScrollPosition;
        private Vector2 _logScrollPosition;
        private double _lastUpdateTime;
        private bool _liveRefresh = true;
        private DateTime _lastDiagnosticsUpdateUtc;
        private bool _contentNeedsUpdate = true;

        private readonly SherpaLogEntry[] _logEntries = new SherpaLogEntry[MaxLogEntries];
        private readonly LogRenderCache[] _logRenderCache = new LogRenderCache[MaxLogEntries];
        private readonly int[] _filteredLogIndices = new int[MaxLogEntries];
        private readonly ConcurrentQueue<SherpaLogEntry> _pendingLogs = new ConcurrentQueue<SherpaLogEntry>();
        private readonly DateTime[] _recentLogTimestamps = new DateTime[MaxLogEntries];
        private int _logStartIndex;
        private int _logCount;
        private int _filteredLogCount;
        private int _recentLogStartIndex;
        private int _recentLogCount;
        private SherpaLogLevel _minLogLevel = SherpaLogLevel.Trace;
        private string _logSearchQuery = string.Empty;
        private bool _autoScroll = true;
        private bool _isPaused;
        private bool _showStackTraces;
        private bool _logsNeedRefresh = true;
        private readonly int[] _logLevelCounts = new int[5];

        private readonly int[] _logHistogram = new int[HistogramBuckets];
        private SherpaRollingGraph _performanceGraph;
        private readonly float[] _performanceSamples = new float[PerfGraphTracks];
        private readonly GraphTrackConfig[] _performanceTrackConfigs = new GraphTrackConfig[PerfGraphTracks];
        private readonly GUIContent[] _perfTrackLabels = new GUIContent[PerfGraphTracks];
        private GraphScaleMode _graphScaleMode = GraphScaleMode.Unified;
        private bool _graphLogScale;
        private Vector2 _graphFixedRange = new Vector2(0f, 100f);
        private int _requestedVisibleSamples = 256;

        private IReadOnlyList<SherpaONNXModule.ModuleDiagnostics> _cachedDiagnostics = Array.Empty<SherpaONNXModule.ModuleDiagnostics>();
        private int _cachedTotalModules;
        private int _cachedActiveModules;
        private int _cachedPendingModules;
        private int _cachedErrorModules;
        private int _cachedTotalTasks;
        private int _cachedCompletedTasks;
        private float _cachedAvgDuration;
        private float _cachedLastDuration;
        private static int s_profilerWindowRefCount;
        private static bool s_profilerProfilingWasEnabled;
        private string _cachedTimeLabel = "--:--:--";
        private int _cachedTimeLabelSecond = -1;
        private string _cachedModulesHeader = string.Empty;
        private int _cachedModulesHeaderCount = -1;
        private string _loggingOnText = string.Empty;
        private string _loggingOffText = string.Empty;

        private readonly StatCardData[] _overviewStats = new StatCardData[5];
        private readonly GUIContent _modulesStatusContent = new GUIContent();
        private readonly GUIContent _errorStatusContent = new GUIContent();
        private readonly GUIContent _logCountContent = new GUIContent();
        private readonly GUIContent _loggingStatusContent = new GUIContent();
        private readonly GUIContent _timeContent = new GUIContent();

        private ModuleViewData[] _moduleViews = new ModuleViewData[16];
        private int _moduleViewCount;

        private IssueViewData[] _issueCache = new IssueViewData[6];
        private int _issueCount;

        private readonly StringBuilder _sharedStringBuilder = new StringBuilder(1024);

        #endregion

        #region Lifecycle

        [MenuItem("Window/SherpaONNX/SherpaONNX Profiler", priority = 2000)]
        private static void ShowWindow()
        {
            var window = GetWindow<SherpaONNXProfilerWindow>();
            window.titleContent = new GUIContent(
                Tr(SherpaONNXL10n.Profiler.WindowTitle, "SherpaONNX Profiler"),
                EditorGUIUtility.IconContent("d_Profiler.Memory")?.image
            );
            window.minSize = new Vector2(480, 320);
            window.Show();
        }

        private void OnEnable()
        {
            InitializeContent();
            InitializeCaches();
            InitializePerformanceGraph();

            if (s_profilerWindowRefCount == 0)
            {
                s_profilerProfilingWasEnabled = TaskRunner.IsProfilingEnabled;
                if (!s_profilerProfilingWasEnabled)
                {
                    TaskRunner.SetProfilingEnabled(true);
                }
            }
            s_profilerWindowRefCount++;

            SherpaONNXLocalization.LanguageChanged += OnLanguageChanged;
            EditorApplication.update += OnEditorUpdate;
            SherpaLog.EntryCaptured += OnLogCaptured;

            LoadInitialLogs();
            RefreshDiagnostics();
            UpdateOverviewStatsCache();
            UpdateStatusBarContent();

            _lastUpdateTime = EditorApplication.timeSinceStartup;
        }

        private void OnDisable()
        {
            s_profilerWindowRefCount = Mathf.Max(0, s_profilerWindowRefCount - 1);
            if (s_profilerWindowRefCount == 0 && !s_profilerProfilingWasEnabled)
            {
                TaskRunner.SetProfilingEnabled(false);
            }

            SherpaONNXLocalization.LanguageChanged -= OnLanguageChanged;
            EditorApplication.update -= OnEditorUpdate;
            SherpaLog.EntryCaptured -= OnLogCaptured;

            _performanceGraph?.Dispose();
            _performanceGraph = null;
        }

        private void InitializeContent()
        {
            Styles.EnsureInitialized();
            Icons.EnsureInitialized();
            Content.UpdateLocalization();
            Content.UpdateIcons();
            UpdateCachedLocalizedStrings();
        }

        private void InitializeCaches()
        {
            for (int i = 0; i < MaxLogEntries; i++)
            {
                if (_logRenderCache[i].Time == null)
                {
                    _logRenderCache[i].Time = new GUIContent();
                    _logRenderCache[i].LevelLabel = new GUIContent();
                    _logRenderCache[i].Category = new GUIContent();
                    _logRenderCache[i].Message = new GUIContent();
                    _logRenderCache[i].Thread = new GUIContent();
                    _logRenderCache[i].Stack = new GUIContent();
                }
            }

            for (int i = 0; i < _overviewStats.Length; i++)
            {
                if (_overviewStats[i].Title == null)
                {
                    _overviewStats[i].Title = new GUIContent();
                    _overviewStats[i].Value = new GUIContent();
                }
            }

            if (_modulesStatusContent.text == null)
            {
                _modulesStatusContent.text = string.Empty;
            }

            EnsureModuleViewCapacity(_moduleViews.Length);
            EnsureIssueCapacity(_issueCache.Length);
        }

        private void InitializePerformanceGraph()
        {
            _performanceGraph?.Dispose();
            _performanceGraph = new SherpaRollingGraph(PerfGraphTracks, PerfGraphCapacity, Mathf.Min(PerfGraphCapacity, 320));

            _performanceTrackConfigs[(int)PerfTrack.AvgDuration] = new GraphTrackConfig
            {
                Name = "Avg Duration (ms)",
                SeriesType = GraphSeriesType.Line,
                Color = Styles.AccentBlue,
                FillColor = Styles.AccentBlue,
                Visible = true,
                Smoothing = 0.28f,
                RangePadding = 0.08f,
                Baseline = 0f
            };

            _performanceTrackConfigs[(int)PerfTrack.LastDuration] = new GraphTrackConfig
            {
                Name = "Last Duration (ms)",
                SeriesType = GraphSeriesType.Points,
                Color = Styles.AccentPurple,
                FillColor = Styles.AccentPurple,
                Visible = true,
                Smoothing = 0.5f,
                RangePadding = 0.08f,
                Baseline = 0f
            };

            _performanceTrackConfigs[(int)PerfTrack.ActiveTasks] = new GraphTrackConfig
            {
                Name = "Active Tasks",
                SeriesType = GraphSeriesType.Area,
                Color = Styles.AccentGreen,
                FillColor = new Color(Styles.AccentGreen.r, Styles.AccentGreen.g, Styles.AccentGreen.b, 0.2f),
                Visible = true,
                Smoothing = 0.35f,
                RangePadding = 0.15f,
                Baseline = 0f
            };

            _performanceTrackConfigs[(int)PerfTrack.LogVolume] = new GraphTrackConfig
            {
                Name = "Logs/sec",
                SeriesType = GraphSeriesType.Bar,
                Color = Styles.AccentYellow,
                FillColor = Styles.AccentYellow,
                Visible = true,
                Smoothing = 0.4f,
                RangePadding = 0.2f,
                Baseline = 0f
            };

            _performanceTrackConfigs[(int)PerfTrack.ErrorModules] = new GraphTrackConfig
            {
                Name = "Error Modules",
                SeriesType = GraphSeriesType.Points,
                Color = Styles.AccentRed,
                FillColor = Styles.AccentRed,
                Visible = true,
                Smoothing = 0.65f,
                RangePadding = 0.3f,
                Baseline = 0f,
                UseFixedRange = true,
                FixedRange = new Vector2(0f, 5f)
            };

            for (int i = 0; i < PerfGraphTracks; i++)
            {
                _performanceGraph.SetTrackConfig(i, _performanceTrackConfigs[i]);
                if (_perfTrackLabels[i] == null)
                {
                    _perfTrackLabels[i] = new GUIContent();
                }
                _perfTrackLabels[i].text = _performanceTrackConfigs[i].Name ?? $"Track {i + 1}";
            }
        }

        private void OnLanguageChanged()
        {
            Content.UpdateLocalization();
            UpdateCachedLocalizedStrings();
            _contentNeedsUpdate = true;
            Repaint();
        }

        private void OnEditorUpdate()
        {
            ProcessPendingLogs();

            var now = EditorApplication.timeSinceStartup;
            var interval = _liveRefresh ? LiveRefreshInterval : RefreshInterval;

            if (now - _lastUpdateTime >= interval)
            {
                _lastUpdateTime = now;
                RefreshDiagnostics();
                UpdateOverviewStatsCache();
                UpdateStatusBarContent();
                UpdatePerformanceGraphSamples();
                Repaint();
            }
        }

        #endregion

        #region Main GUI

        private void OnGUI()
        {
            if (!Styles.EnsureInitialized())
            {
                return;
            }
            Icons.EnsureInitialized();

            if (_contentNeedsUpdate)
            {
                Content.UpdateLocalization();
                Content.UpdateIcons();
                _contentNeedsUpdate = false;
            }

            ProcessPendingLogs();

            DrawToolbar();
            DrawTabBar();
            DrawMainContent();
            DrawStatusBar();
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button(Content.RefreshButton, EditorStyles.toolbarButton, LayoutCache.ToolbarWide))
                {
                    RefreshDiagnostics();
                    UpdateOverviewStatsCache();
                    UpdateStatusBarContent();
                    _logsNeedRefresh = true;
                }

                GUILayout.Space(ElementSpacing);

                var newLive = GUILayout.Toggle(_liveRefresh, Content.LiveToggle, EditorStyles.toolbarButton, LayoutCache.ToolbarIcon);
                if (newLive != _liveRefresh)
                {
                    _liveRefresh = newLive;
                    _lastUpdateTime = 0;
                }

                GUILayout.FlexibleSpace();

                UpdateTimeLabelCache();
                _timeContent.text = _cachedTimeLabel;
                GUILayout.Label(_timeContent, EditorStyles.miniLabel, LayoutCache.ToolbarLabel);

                GUILayout.Space(ElementSpacing);

                if (GUILayout.Button(Content.CopyButton, EditorStyles.toolbarButton, LayoutCache.ToolbarIcon))
                {
                    CopyAllDiagnosticsToClipboard();
                }
            }
        }

        private void DrawTabBar()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(ElementSpacing);

                DrawTabButton(Tab.Overview, Content.TabOverview);
                DrawTabButton(Tab.Modules, Content.TabModules);
                DrawTabButton(Tab.Logs, Content.TabLogs);
                DrawTabButton(Tab.Performance, Content.TabPerformance);

                GUILayout.FlexibleSpace();
            }

            var underlineRect = GUILayoutUtility.GetRect(0, 2, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(underlineRect, Styles.BackgroundDark);
        }

        private void DrawTabButton(Tab tab, GUIContent content)
        {
            var isActive = _currentTab == tab;
            var style = isActive ? Styles.TabButtonActive : Styles.TabButton;

            if (GUILayout.Button(content, style, LayoutCache.TabButton))
            {
                if (_currentTab != tab)
                {
                    _currentTab = tab;
                    _mainScrollPosition = Vector2.zero;
                }
            }

            if (isActive)
            {
                var lastRect = GUILayoutUtility.GetLastRect();
                var indicatorRect = new Rect(lastRect.x, lastRect.yMax - 2, lastRect.width, 2);
                EditorGUI.DrawRect(indicatorRect, Styles.AccentBlue);
            }
        }

        private void DrawMainContent()
        {
            using (var scroll = new EditorGUILayout.ScrollViewScope(_mainScrollPosition))
            {
                _mainScrollPosition = scroll.scrollPosition;

                GUILayout.Space(CardPadding);

                switch (_currentTab)
                {
                    case Tab.Overview:
                        DrawOverviewTab();
                        break;
                    case Tab.Modules:
                        DrawModulesTab();
                        break;
                    case Tab.Logs:
                        DrawLogsTab();
                        break;
                    case Tab.Performance:
                        DrawPerformanceTab();
                        break;
                }

                GUILayout.Space(CardPadding);
            }
        }

        private void DrawStatusBar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label(_modulesStatusContent, EditorStyles.miniLabel);

                GUILayout.Space(SectionSpacing);

                if (_cachedErrorModules > 0)
                {
                    var oldColor = GUI.contentColor;
                    GUI.contentColor = Styles.AccentRed;
                    GUILayout.Label(_errorStatusContent, EditorStyles.miniLabel);
                    GUI.contentColor = oldColor;
                }

                GUILayout.FlexibleSpace();

                GUILayout.Label(_logCountContent, EditorStyles.miniLabel);

                GUILayout.Space(CardPadding);

                var loggingEnabled = SherpaLog.Enabled;
                _loggingStatusContent.text = loggingEnabled ? _loggingOnText : _loggingOffText;

                var oldContentColor = GUI.contentColor;
                GUI.contentColor = loggingEnabled ? Styles.AccentGreen : Styles.TextMuted;
                GUILayout.Label(_loggingStatusContent, EditorStyles.miniLabel);
                GUI.contentColor = oldContentColor;
            }
        }

        #endregion

        #region Overview Tab

        private void DrawOverviewTab()
        {
            DrawQuickStatsSection();
            GUILayout.Space(SectionSpacing);
            DrawRecentIssuesSection();
            GUILayout.Space(SectionSpacing);
            DrawActivityGraphSection();
        }

        private void DrawQuickStatsSection()
        {
            EditorGUILayout.LabelField(Tr(SherpaONNXL10n.Profiler.OverviewTitle, "Quick Overview"), Styles.HeaderLabel);

            var windowWidth = position.width;
            var isNarrow = windowWidth < NarrowWindowThreshold;

            if (isNarrow)
            {
                for (int i = 0; i < _overviewStats.Length; i++)
                {
                    DrawStatCardVertical(_overviewStats[i]);
                }
            }
            else
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    for (int i = 0; i < _overviewStats.Length; i++)
                    {
                        DrawStatCard(_overviewStats[i]);
                    }
                }
            }
        }

        private void DrawStatCard(in StatCardData data)
        {
            using (new EditorGUILayout.VerticalScope(Styles.CardBox, LayoutCache.StatCard))
            {
                GUILayout.Space(4);

                var oldColor = GUI.contentColor;
                GUI.contentColor = data.Accent;

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();
                    GUILayout.Label(data.Value, EditorStyles.boldLabel);
                    GUILayout.FlexibleSpace();
                }

                GUI.contentColor = oldColor;

                GUILayout.Space(2);

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();
                    GUILayout.Label(data.Title, Styles.CenteredMiniLabel);
                    GUILayout.FlexibleSpace();
                }

                GUILayout.Space(4);
            }
        }

        private void DrawStatCardVertical(in StatCardData data)
        {
            using (new EditorGUILayout.HorizontalScope(Styles.CardBox))
            {
                GUILayout.Label(data.Title, LayoutCache.MetricLabel);
                GUILayout.FlexibleSpace();

                var oldColor = GUI.contentColor;
                GUI.contentColor = data.Accent;
                GUILayout.Label(data.Value, EditorStyles.boldLabel, LayoutCache.MetricValue);
                GUI.contentColor = oldColor;
            }
        }

        private void DrawRecentIssuesSection()
        {
            if (_issueCount == 0)
            {
                using (new EditorGUILayout.HorizontalScope(Styles.CardBox))
                {
                    GUILayout.Label(Icons.Info, LayoutCache.Icon);
                    GUILayout.Space(ElementSpacing);
                    GUILayout.Label(Tr(SherpaONNXL10n.Profiler.IssuesNone, "No issues detected. All systems operational."));
                }
                return;
            }

            EditorGUILayout.LabelField(Tr(SherpaONNXL10n.Profiler.IssuesTitle, "Recent Issues"), Styles.HeaderLabel);

            for (int i = 0; i < _issueCount; i++)
            {
                DrawIssueCard(ref _issueCache[i]);
            }
        }

        private void DrawIssueCard(ref IssueViewData issue)
        {
            using (new EditorGUILayout.HorizontalScope(Styles.CardBox))
            {
                var indicatorRect = GUILayoutUtility.GetRect(3, EditorGUIUtility.singleLineHeight * 2);
                EditorGUI.DrawRect(new Rect(indicatorRect.x, indicatorRect.y, 3, indicatorRect.height), issue.AccentColor);

                GUILayout.Space(CardPadding);

                using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true)))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.Label(issue.Title, EditorStyles.boldLabel);
                        GUILayout.FlexibleSpace();
                        GUILayout.Label(issue.Time, EditorStyles.miniLabel);
                    }

                    GUILayout.Label(issue.Message, Styles.RichTextLabel);
                }

                if (GUILayout.Button(Content.CopySmall, Styles.MiniButton, GUILayout.Width(45)))
                {
                    GUIUtility.systemCopyBuffer = $"{issue.Title.text}: {issue.Message.text}";
                    ShowNotification(new GUIContent(Tr(SherpaONNXL10n.Profiler.ToastCopied, "Copied")));
                }
            }
        }

        private void DrawActivityGraphSection()
        {
            EditorGUILayout.LabelField(Tr(SherpaONNXL10n.Profiler.ActivityTitle, "Log Activity (Last 60s)"), Styles.HeaderLabel);

            var graphRect = GUILayoutUtility.GetRect(0, 70, GUILayout.ExpandWidth(true));
            graphRect.x += ElementSpacing;
            graphRect.width = Mathf.Max(1f, graphRect.width - ElementSpacing * 2f);

            if (!IsRepaintEvent())
            {
                return;
            }

            EditorGUI.DrawRect(graphRect, Styles.BackgroundDark);

            var maxValue = 1;
            for (int i = 0; i < HistogramBuckets; i++)
            {
                var bucketValue = _logHistogram[i];
                if (bucketValue > maxValue)
                {
                    maxValue = bucketValue;
                }
            }

            var barWidth = Mathf.Max(1f, (graphRect.width - 10f) / HistogramBuckets);
            var graphHeight = graphRect.height - 18f;
            var barBaseX = graphRect.x + 5f;
            var barBaseY = graphRect.yMax - 14f;

            for (int i = 0; i < HistogramBuckets; i++)
            {
                var value = _logHistogram[i];
                if (value <= 0)
                {
                    continue;
                }

                var normalizedHeight = value / (float)maxValue;
                var barHeight = normalizedHeight * graphHeight;

                var barRect = new Rect(
                    barBaseX + i * barWidth,
                    barBaseY - barHeight,
                    barWidth - 1f,
                    barHeight
                );

                var barColor = Color.Lerp(Styles.AccentBlue, Styles.AccentGreen, normalizedHeight);
                EditorGUI.DrawRect(barRect, barColor);
            }

            GUI.Label(new Rect(graphRect.x + 2f, graphRect.yMax - 14f, 35f, 14f), "-60s", EditorStyles.miniLabel);
            GUI.Label(new Rect(graphRect.xMax - 30f, graphRect.yMax - 14f, 28f, 14f), "now", EditorStyles.miniLabel);
        }

        #endregion

        #region Modules Tab

        private void DrawModulesTab()
        {
            if (_moduleViewCount == 0)
            {
                DrawEmptyState(
                    Tr(SherpaONNXL10n.Profiler.ModulesEmptyTitle, "No Active Modules"),
                    Tr(SherpaONNXL10n.Profiler.ModulesEmptyBody, "No SherpaONNX modules are currently running.")
                );
                return;
            }

            CacheModulesHeader(_moduleViewCount);
            EditorGUILayout.LabelField(_cachedModulesHeader, Styles.HeaderLabel);

            for (int i = 0; i < _moduleViewCount; i++)
            {
                DrawModuleCard(ref _moduleViews[i]);
                GUILayout.Space(ElementSpacing);
            }
        }

        private void DrawModuleCard(ref ModuleViewData module)
        {
            using (new EditorGUILayout.VerticalScope(Styles.CardBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    DrawStatusIndicator(module.StatusColor);
                    GUILayout.Space(CardPadding);

                    using (new EditorGUILayout.VerticalScope())
                    {
                        GUILayout.Label(module.ModelId, EditorStyles.boldLabel);
                        GUILayout.Label(module.ModuleType, EditorStyles.miniLabel);
                    }

                    GUILayout.FlexibleSpace();

                    DrawStatusBadge(module.StatusLabel, module.StatusColor);
                }

                GUILayout.Space(CardPadding);

                var isNarrow = position.width < NarrowWindowThreshold;

                if (isNarrow)
                {
                    DrawMetricRow(Tr(SherpaONNXL10n.Profiler.StatActiveTasks, "Active Tasks"), module.ActiveTasks);
                    DrawMetricRow(Tr(SherpaONNXL10n.Profiler.MetricCompleted, "Completed"), module.Completed);
                    DrawMetricRow(Tr(SherpaONNXL10n.Profiler.MetricAvgDuration, "Avg Duration"), module.AvgDuration);
                }
                else
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        DrawMetricColumn(Tr(SherpaONNXL10n.Profiler.StatActiveTasks, "Active"), module.ActiveTasks);
                        DrawMetricColumn(Tr(SherpaONNXL10n.Profiler.MetricTotalStarted, "Started"), module.TotalStarted);
                        DrawMetricColumn(Tr(SherpaONNXL10n.Profiler.MetricCompleted, "Completed"), module.Completed);
                        DrawMetricColumn(Tr(SherpaONNXL10n.Profiler.MetricAvgDuration, "Avg (ms)"), module.AvgDuration);
                        DrawMetricColumn(Tr(SherpaONNXL10n.Profiler.MetricLastDuration, "Last (ms)"), module.LastDuration);
                    }
                }

                if (module.HasError)
                {
                    GUILayout.Space(ElementSpacing);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.Label(Icons.Error, LayoutCache.SmallIcon);
                        GUILayout.Space(ElementSpacing);
                        GUILayout.Label(module.ErrorMessage, Styles.RichTextLabel);
                    }
                }
            }
        }

        private void DrawStatusIndicator(Color color)
        {
            var rect = GUILayoutUtility.GetRect(StatusIndicatorSize, StatusIndicatorSize,
                GUILayout.Width(StatusIndicatorSize), GUILayout.Height(StatusIndicatorSize));

            rect.y += (EditorGUIUtility.singleLineHeight - StatusIndicatorSize) / 2f;
            EditorGUI.DrawRect(rect, color);
        }

        private void DrawStatusBadge(GUIContent label, Color color)
        {
            var badgeRect = GUILayoutUtility.GetRect(60f, BadgeHeight, LayoutCache.StatusBadge);
            EditorGUI.DrawRect(badgeRect, new Color(color.r, color.g, color.b, 0.2f));

            var oldColor = GUI.contentColor;
            GUI.contentColor = color;
            GUI.Label(badgeRect, label, Styles.CenteredLabel);
            GUI.contentColor = oldColor;
        }

        private void DrawMetricColumn(string label, GUIContent value)
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.MinWidth(60f), GUILayout.ExpandWidth(true)))
            {
                GUILayout.Label(value, EditorStyles.boldLabel);
                GUILayout.Label(label, EditorStyles.miniLabel);
            }
        }

        private void DrawMetricRow(string label, GUIContent value)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(label, LayoutCache.MetricLabel);
                GUILayout.FlexibleSpace();
                GUILayout.Label(value, EditorStyles.boldLabel);
            }
        }

        #endregion

        #region Logs Tab

        private void DrawLogsTab()
        {
            DrawLogToolbar();
            GUILayout.Space(ElementSpacing);
            RefreshFilteredLogs();
            DrawLogLevelSummary();
            GUILayout.Space(ElementSpacing);
            DrawLogList();
        }

        private void DrawLogToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("Level:", EditorStyles.miniLabel, GUILayout.Width(38));

                var newLevel = (SherpaLogLevel)EditorGUILayout.EnumPopup(_minLogLevel, GUILayout.Width(80));
                if (newLevel != _minLogLevel)
                {
                    _minLogLevel = newLevel;
                    _logsNeedRefresh = true;
                }

                GUILayout.Space(CardPadding);

                var searchWidth = Mathf.Min(position.width * 0.25f, 160f);
                var newSearch = EditorGUILayout.TextField(_logSearchQuery, EditorStyles.toolbarSearchField, GUILayout.Width(searchWidth));
                if (!string.Equals(newSearch, _logSearchQuery, StringComparison.Ordinal))
                {
                    _logSearchQuery = newSearch;
                    _logsNeedRefresh = true;
                }

                if (!string.IsNullOrEmpty(_logSearchQuery))
                {
                    if (GUILayout.Button(Content.ClearSearchButton, EditorStyles.toolbarButton, GUILayout.Width(18)))
                    {
                        _logSearchQuery = string.Empty;
                        _logsNeedRefresh = true;
                        GUI.FocusControl(null);
                    }
                }

                GUILayout.FlexibleSpace();

                _autoScroll = GUILayout.Toggle(_autoScroll, Content.AutoScrollToggle, EditorStyles.toolbarButton, LayoutCache.ToolbarIcon);
                _isPaused = GUILayout.Toggle(_isPaused, Content.PauseToggle, EditorStyles.toolbarButton, LayoutCache.ToolbarIcon);
                _showStackTraces = GUILayout.Toggle(_showStackTraces, Content.StacksToggle, EditorStyles.toolbarButton, GUILayout.Width(50));

                GUILayout.Space(ElementSpacing);

                if (GUILayout.Button(Content.CopyButton, EditorStyles.toolbarButton, LayoutCache.ToolbarIcon))
                {
                    CopyLogsToClipboard();
                }

                if (GUILayout.Button(Icons.Clear, EditorStyles.toolbarButton, LayoutCache.ToolbarIcon))
                {
                    ClearLogs();
                }
            }
        }

        private void DrawLogLevelSummary()
        {
            ComputeLogLevelCounts();

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawLogLevelBadge(Content.LogLevelBadges[0], _logLevelCounts[0], Styles.AccentRed);
                DrawLogLevelBadge(Content.LogLevelBadges[1], _logLevelCounts[1], Styles.AccentYellow);
                DrawLogLevelBadge(Content.LogLevelBadges[2], _logLevelCounts[2], Styles.AccentBlue);
                DrawLogLevelBadge(Content.LogLevelBadges[3], _logLevelCounts[3], Styles.AccentGreen);
                DrawLogLevelBadge(Content.LogLevelBadges[4], _logLevelCounts[4], Styles.AccentPurple);
                GUILayout.FlexibleSpace();
            }
        }

        private void DrawLogLevelBadge(GUIContent label, int count, Color color)
        {
            var rect = GUILayoutUtility.GetRect(55f, BadgeHeight);
            EditorGUI.DrawRect(rect, new Color(color.r, color.g, color.b, 0.2f));

            var oldColor = GUI.contentColor;
            GUI.contentColor = color;
            _sharedStringBuilder.Clear();
            _sharedStringBuilder.Append(label.text);
            _sharedStringBuilder.Append(": ");
            _sharedStringBuilder.Append(count);
            GUI.Label(rect, _sharedStringBuilder.ToString(), Styles.CenteredLabel);
            GUI.contentColor = oldColor;
        }

        private void DrawLogList()
        {
            if (!SherpaLog.Enabled)
            {
                DrawEmptyState(
                    Tr(SherpaONNXL10n.Profiler.LoggingDisabledTitle, "Logging Disabled"),
                    Tr(SherpaONNXL10n.Profiler.LoggingDisabledBody, "Enable logging in settings to capture entries.")
                );
                return;
            }

            if (_filteredLogCount == 0)
            {
                DrawEmptyState(
                    Tr(SherpaONNXL10n.Profiler.LogEmptyTitle, "No Logs"),
                    Tr(SherpaONNXL10n.Profiler.LogEmptyBody, "No entries match current filters.")
                );
                return;
            }

            var listHeight = Mathf.Max(180f, position.height - 220f);

            using (var scroll = new EditorGUILayout.ScrollViewScope(_logScrollPosition, GUILayout.Height(listHeight)))
            {
                _logScrollPosition = scroll.scrollPosition;

                for (int i = 0; i < _filteredLogCount; i++)
                {
                    var bufferIndex = _filteredLogIndices[i];
                    DrawLogEntry(ref _logRenderCache[bufferIndex]);
                }
            }

            if (_autoScroll && !_isPaused)
            {
                _logScrollPosition.y = float.MaxValue;
            }
        }

        private void DrawLogEntry(ref LogRenderCache cache)
        {
            var levelColor = Styles.GetLogLevelColor(cache.Entry.Level);

            using (new EditorGUILayout.VerticalScope(Styles.CardBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label(Icons.GetLogLevelIcon(cache.Entry.Level), LayoutCache.SmallIcon);
                    GUILayout.Label(cache.Time, EditorStyles.miniLabel, GUILayout.Width(72));

                    var oldColor = GUI.contentColor;
                    GUI.contentColor = levelColor;
                    GUILayout.Label(cache.LevelLabel, EditorStyles.miniLabel, GUILayout.Width(35));
                    GUI.contentColor = oldColor;

                    if (!string.IsNullOrEmpty(cache.Category.text))
                    {
                        var categoryWidth = Mathf.Min(position.width * 0.15f, 100f);
                        GUILayout.Label(cache.Category, EditorStyles.miniLabel, GUILayout.Width(categoryWidth));
                    }

                    GUILayout.FlexibleSpace();
                    GUILayout.Label(cache.Thread, EditorStyles.miniLabel, GUILayout.Width(34));
                }

                if (!string.IsNullOrEmpty(cache.Message.text))
                {
                    GUILayout.Label(cache.Message, Styles.RichTextLabel);
                }

                if (_showStackTraces && cache.Entry.HasStackTrace && !string.IsNullOrEmpty(cache.Stack.text))
                {
                    GUILayout.Space(ElementSpacing);
                    EditorGUILayout.TextArea(cache.Stack.text, Styles.StackTraceArea, GUILayout.MaxHeight(80));
                }
            }
        }

        #endregion

        #region Performance Tab

        private void DrawPerformanceTab()
        {
            EditorGUILayout.LabelField(Tr(SherpaONNXL10n.Profiler.PerformanceTitle, "Performance Metrics"), Styles.HeaderLabel);

            using (new EditorGUILayout.VerticalScope(Styles.CardBox))
            {
                DrawPerformanceGraphToolbar();

                var graphRect = GUILayoutUtility.GetRect(0, 160, GUILayout.ExpandWidth(true));
                var targetSamples = Mathf.Clamp((int)graphRect.width, 32, PerfGraphCapacity);
                _requestedVisibleSamples = targetSamples;
                _performanceGraph?.SetVisibleSamples(_requestedVisibleSamples);
                _performanceGraph?.Draw(graphRect, _graphScaleMode, _graphLogScale, _graphFixedRange);
            }

            GUILayout.Space(ElementSpacing);
            DrawPerformanceLegend();

            GUILayout.Space(SectionSpacing);
            DrawPerformanceTable();
        }

        private void DrawPerformanceGraphToolbar()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("Scaling", EditorStyles.miniLabel, GUILayout.Width(52));
                var newScale = (GraphScaleMode)EditorGUILayout.EnumPopup(_graphScaleMode, GUILayout.Width(120));
                if (newScale != _graphScaleMode)
                {
                    _graphScaleMode = newScale;
                }

                GUILayout.Space(ElementSpacing);
                var newLog = GUILayout.Toggle(_graphLogScale, "Log", Styles.MiniButton, GUILayout.Width(48));
                if (newLog != _graphLogScale)
                {
                    _graphLogScale = newLog;
                }

                if (_graphScaleMode == GraphScaleMode.Fixed)
                {
                    GUILayout.Space(ElementSpacing);
                    GUILayout.Label("Range", EditorStyles.miniLabel, GUILayout.Width(42));
                    _graphFixedRange.x = EditorGUILayout.FloatField(_graphFixedRange.x, GUILayout.Width(60));
                    _graphFixedRange.y = EditorGUILayout.FloatField(_graphFixedRange.y, GUILayout.Width(60));

                    if (_graphFixedRange.y <= _graphFixedRange.x)
                    {
                        _graphFixedRange.y = _graphFixedRange.x + 0.001f;
                    }
                }

                GUILayout.FlexibleSpace();
            }
        }

        private void DrawPerformanceLegend()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                for (int i = 0; i < PerfGraphTracks; i++)
                {
                    var cfg = _performanceTrackConfigs[i];
                    var label = _perfTrackLabels[i];
                    if (label == null)
                    {
                        continue;
                    }

                    var rect = GUILayoutUtility.GetRect(90f, 20f, GUILayout.Width(160));
                    var toggleRect = new Rect(rect.x + 20f, rect.y, rect.width - 22f, rect.height);
                    var newVisible = GUI.Toggle(toggleRect, cfg.Visible, label, Styles.MiniButton);
                    var indicatorRect = new Rect(rect.x + 4f, rect.y + 4f, 12f, 12f);
                    EditorGUI.DrawRect(indicatorRect, cfg.Color);

                    if (newVisible != cfg.Visible)
                    {
                        cfg.Visible = newVisible;
                        _performanceTrackConfigs[i] = cfg;
                        _performanceGraph.SetTrackConfig(i, cfg);
                    }
                }

                GUILayout.FlexibleSpace();
            }
        }

        private void DrawPerformanceTable()
        {
            EditorGUILayout.LabelField(Tr(SherpaONNXL10n.Profiler.PerformanceTableTitle, "Module Performance"), Styles.HeaderLabel);

            if (_moduleViewCount == 0)
            {
                DrawEmptyState(
                    Tr(SherpaONNXL10n.Profiler.PerformanceEmptyTitle, "No Data"),
                    Tr(SherpaONNXL10n.Profiler.PerformanceEmptyBody, "No performance data available.")
                );
                return;
            }

            var isVeryNarrow = position.width < VeryNarrowThreshold;

            using (new EditorGUILayout.HorizontalScope(Styles.CardBox))
            {
                GUILayout.Label("Module", EditorStyles.boldLabel, GUILayout.MinWidth(100), GUILayout.ExpandWidth(true));

                if (!isVeryNarrow)
                {
                    GUILayout.Label("Runs", EditorStyles.boldLabel, GUILayout.Width(50));
                    GUILayout.Label("Done", EditorStyles.boldLabel, GUILayout.Width(50));
                }

                GUILayout.Label("Avg (ms)", EditorStyles.boldLabel, GUILayout.Width(60));
                GUILayout.Label("Active", EditorStyles.boldLabel, GUILayout.Width(45));
            }

            for (int i = 0; i < _moduleViewCount; i++)
            {
                ref var view = ref _moduleViews[i];

                using (new EditorGUILayout.HorizontalScope(Styles.CardBox))
                {
                    GUILayout.Label(view.ModelId, GUILayout.MinWidth(100), GUILayout.ExpandWidth(true));

                    if (!isVeryNarrow)
                    {
                        GUILayout.Label(view.TotalStarted, GUILayout.Width(50));
                        GUILayout.Label(view.Completed, GUILayout.Width(50));
                    }

                    GUILayout.Label(view.AvgDuration, GUILayout.Width(60));
                    GUILayout.Label(view.ActiveTasks, GUILayout.Width(45));
                }
            }
        }

        #endregion

        #region Data Management

        private void RefreshDiagnostics()
        {
            try
            {
                var diagnostics = SherpaONNXModule.GetLiveModuleDiagnostics();
                _cachedDiagnostics = diagnostics != null
                    ? (IReadOnlyList<SherpaONNXModule.ModuleDiagnostics>)diagnostics
                    : Array.Empty<SherpaONNXModule.ModuleDiagnostics>();
            }
            catch
            {
                _cachedDiagnostics = Array.Empty<SherpaONNXModule.ModuleDiagnostics>();
            }

            _cachedTotalModules = _cachedDiagnostics.Count;
            _cachedActiveModules = 0;
            _cachedPendingModules = 0;
            _cachedErrorModules = 0;
            _cachedTotalTasks = 0;
            _cachedCompletedTasks = 0;
            _cachedLastDuration = 0f;

            double totalDurationWeighted = 0;
            int totalCompleted = 0;
            float maxLastDuration = 0f;

            for (int i = 0; i < _cachedDiagnostics.Count; i++)
            {
                var d = _cachedDiagnostics[i];

                if (d.Initialized && !d.Disposed)
                {
                    _cachedActiveModules++;
                }

                if (d.InitializationStarted && !d.Initialized && !d.Disposed)
                {
                    _cachedPendingModules++;
                }

                if (d.InitializationException != null)
                {
                    _cachedErrorModules++;
                }

                _cachedTotalTasks += d.ActiveTasks;
                _cachedCompletedTasks += d.RunnerMetrics.Completed;
                maxLastDuration = Mathf.Max(maxLastDuration, (float)d.RunnerMetrics.LastDurationMs);

                if (d.RunnerMetrics.Completed > 0 && d.RunnerMetrics.AverageDurationMs > 0)
                {
                    totalDurationWeighted += d.RunnerMetrics.AverageDurationMs * d.RunnerMetrics.Completed;
                    totalCompleted += d.RunnerMetrics.Completed;
                }
            }

            _cachedAvgDuration = totalCompleted > 0 ? (float)(totalDurationWeighted / totalCompleted) : 0f;
            _cachedLastDuration = maxLastDuration;
            _lastDiagnosticsUpdateUtc = DateTime.UtcNow;

            BuildModuleViewCache();
            BuildIssueCache();
        }

        private void BuildModuleViewCache()
        {
            var count = _cachedDiagnostics.Count;
            EnsureModuleViewCapacity(count);
            _moduleViewCount = count;

            for (int i = 0; i < count; i++)
            {
                ref var view = ref _moduleViews[i];
                var d = _cachedDiagnostics[i];

                var status = GetModuleStatus(d);
                view.Status = status;
                view.StatusColor = GetStatusColor(status);
                view.StatusLabel.text = GetStatusLabel(status);

                view.ModelId.text = d.ModelId ?? Tr(SherpaONNXL10n.Profiler.LabelUnknown, "Unknown");
                view.ModuleType.text = d.ModuleType.ToString();
                view.ActiveTasks.text = d.ActiveTasks.ToString();
                view.TotalStarted.text = d.RunnerMetrics.TotalStarted.ToString();
                view.Completed.text = d.RunnerMetrics.Completed.ToString();
                view.AvgDuration.text = FormatDuration(d.RunnerMetrics.AverageDurationMs);
                view.LastDuration.text = FormatDuration(d.RunnerMetrics.LastDurationMs);
                view.LastStatusUtc = d.LastStatusUtc;
                view.Disposed = d.Disposed;
                view.HasError = d.InitializationException != null;
                view.ErrorMessage.text = view.HasError && d.InitializationException != null ? d.InitializationException.Message : string.Empty;
            }
        }

        private void BuildIssueCache()
        {
            EnsureIssueCapacity(_issueCache.Length);
            _issueCount = 0;

            if (_cachedDiagnostics != null)
            {
                for (int i = 0; i < _cachedDiagnostics.Count && _issueCount < _issueCache.Length; i++)
                {
                    var d = _cachedDiagnostics[i];
                    if (d.InitializationException == null)
                    {
                        continue;
                    }

                    ref var issue = ref _issueCache[_issueCount++];
                    issue.Title.text = $"{d.ModelId ?? "Unknown"} ({d.ModuleType})";
                    issue.Message.text = d.InitializationException.Message ?? Tr(SherpaONNXL10n.Profiler.IssuesUnknown, "Unknown error");
                    issue.Time.text = d.LastStatusUtc.ToLocalTime().ToString("HH:mm:ss", CultureInfo.InvariantCulture);
                    issue.AccentColor = Styles.AccentRed;
                }
            }

            var failures = ModelFileResolver.GetRecentFailures();
            if (failures != null)
            {
                for (int i = 0; i < failures.Count && _issueCount < _issueCache.Length; i++)
                {
                    var failure = failures[i];
                    ref var issue = ref _issueCache[_issueCount++];
                    issue.Title.text = Tr(SherpaONNXL10n.Profiler.IssueModelFile, "Model File Issue");
                    issue.Message.text = failure.Message ?? string.Empty;
                    issue.Time.text = failure.Timestamp.ToLocalTime().ToString("HH:mm:ss", CultureInfo.InvariantCulture);
                    issue.AccentColor = Styles.AccentYellow;
                }
            }
        }

        private void RefreshFilteredLogs()
        {
            if (!_logsNeedRefresh)
            {
                return;
            }

            _filteredLogCount = 0;
            var hasSearch = !string.IsNullOrEmpty(_logSearchQuery);

            for (int i = 0; i < _logCount; i++)
            {
                var bufferIndex = (_logStartIndex + i) % MaxLogEntries;
                ref var cache = ref _logRenderCache[bufferIndex];
                var entry = cache.Entry;

                if (entry.Level > _minLogLevel)
                {
                    continue;
                }

                if (hasSearch)
                {
                    if (!ContainsIgnoreCase(cache.Message.text, _logSearchQuery) &&
                        !ContainsIgnoreCase(cache.Category.text, _logSearchQuery))
                    {
                        continue;
                    }
                }

                _filteredLogIndices[_filteredLogCount++] = bufferIndex;
            }

            _logsNeedRefresh = false;
        }

        private void LoadInitialLogs()
        {
            ClearLogs();

            try
            {
                var recent = SherpaLog.GetRecentEntries(MaxLogEntries);
                if (recent != null)
                {
                    for (int i = 0; i < recent.Count; i++)
                    {
                        AppendLog(recent[i]);
                    }
                }
            }
            catch
            {
                // ignore load errors
            }

            _logsNeedRefresh = true;
        }

        private void OnLogCaptured(SherpaLogEntry entry)
        {
            _pendingLogs.Enqueue(entry);
        }

        private void ProcessPendingLogs()
        {
            if (_isPaused)
            {
                return;
            }

            var hasChanges = false;

            while (_pendingLogs.TryDequeue(out var entry))
            {
                AppendLog(entry);
                hasChanges = true;
            }

            if (hasChanges)
            {
                _logsNeedRefresh = true;
            }
        }

        private void AppendLog(in SherpaLogEntry entry)
        {
            var insertIndex = (_logStartIndex + _logCount) % MaxLogEntries;
            if (_logCount == MaxLogEntries)
            {
                _logStartIndex = (_logStartIndex + 1) % MaxLogEntries;
            }
            else
            {
                _logCount++;
            }

            _logEntries[insertIndex] = entry;
            PrepareLogRenderCache(insertIndex, entry);
            AddRecentLogTimestamp(entry.TimestampUtc);
        }

        private void PrepareLogRenderCache(int index, in SherpaLogEntry entry)
        {
            ref var cache = ref _logRenderCache[index];
            cache.Entry = entry;

            cache.Time.text = entry.TimestampUtc.ToLocalTime().ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture);
            cache.LevelLabel.text = GetLogLevelShortLabel(entry.Level);
            cache.Category.text = string.IsNullOrEmpty(entry.Category) ? string.Empty : $"[{entry.Category}]";
            cache.Message.text = string.IsNullOrEmpty(entry.FormattedMessage) ? entry.Message ?? string.Empty : entry.FormattedMessage;
            cache.Thread.text = $"T{entry.ThreadId}";
            cache.Stack.text = entry.HasStackTrace && !string.IsNullOrEmpty(entry.StackTrace) ? entry.StackTrace : string.Empty;
        }

        private void AddRecentLogTimestamp(DateTime timestampUtc)
        {
            var insertIndex = (_recentLogStartIndex + _recentLogCount) % MaxLogEntries;
            if (_recentLogCount == MaxLogEntries)
            {
                _recentLogStartIndex = (_recentLogStartIndex + 1) % MaxLogEntries;
            }
            else
            {
                _recentLogCount++;
            }

            _recentLogTimestamps[insertIndex] = timestampUtc;
        }

        private void UpdateLogHistogram()
        {
            Array.Clear(_logHistogram, 0, _logHistogram.Length);

            var now = DateTime.UtcNow;
            var windowStart = now.AddSeconds(-HistogramWindowSeconds);

            for (int i = 0; i < _logCount; i++)
            {
                var bufferIndex = (_logStartIndex + i) % MaxLogEntries;
                var entry = _logEntries[bufferIndex];
                if (entry.TimestampUtc < windowStart)
                {
                    continue;
                }

                var age = (now - entry.TimestampUtc).TotalSeconds;
                var bucketIndex = (int)((1.0 - age / HistogramWindowSeconds) * (HistogramBuckets - 1));
                bucketIndex = Mathf.Clamp(bucketIndex, 0, HistogramBuckets - 1);
                _logHistogram[bucketIndex]++;
            }
        }

        private void TrimRecentLogQueue()
        {
            var cutoff = DateTime.UtcNow.AddSeconds(-1);
            while (_recentLogCount > 0)
            {
                var current = _recentLogTimestamps[_recentLogStartIndex];
                if (current >= cutoff)
                {
                    break;
                }

                _recentLogStartIndex = (_recentLogStartIndex + 1) % MaxLogEntries;
                _recentLogCount = Mathf.Max(0, _recentLogCount - 1);
            }
        }

        private int GetRecentLogCount()
        {
            TrimRecentLogQueue();
            return _recentLogCount;
        }

        private void UpdatePerformanceGraphSamples()
        {
            if (_performanceGraph == null)
            {
                return;
            }

            _performanceGraph.Complete();
            _performanceGraph.SetVisibleSamples(_requestedVisibleSamples);
            _performanceSamples[(int)PerfTrack.AvgDuration] = _cachedAvgDuration;
            _performanceSamples[(int)PerfTrack.LastDuration] = _cachedLastDuration;
            _performanceSamples[(int)PerfTrack.ActiveTasks] = _cachedTotalTasks;
            _performanceSamples[(int)PerfTrack.LogVolume] = GetRecentLogCount();
            _performanceSamples[(int)PerfTrack.ErrorModules] = _cachedErrorModules;
            for (int i = 0; i < PerfGraphTracks; i++)
            {
                _performanceGraph.SetIncoming(i, _performanceSamples[i]);
            }

            _performanceGraph.Schedule();
            UpdateLogHistogram();
        }

        private void UpdateOverviewStatsCache()
        {
            _overviewStats[0].Title.text = Tr(SherpaONNXL10n.Profiler.StatActiveModules, "Active Modules");
            _overviewStats[0].Value.text = _cachedActiveModules.ToString();
            _overviewStats[0].Accent = Styles.AccentGreen;

            _overviewStats[1].Title.text = Tr(SherpaONNXL10n.Profiler.StatPending, "Pending");
            _overviewStats[1].Value.text = _cachedPendingModules.ToString();
            _overviewStats[1].Accent = Styles.AccentYellow;

            _overviewStats[2].Title.text = Tr(SherpaONNXL10n.Profiler.StatErrors, "Errors");
            _overviewStats[2].Value.text = _cachedErrorModules.ToString();
            _overviewStats[2].Accent = Styles.AccentRed;

            _overviewStats[3].Title.text = Tr(SherpaONNXL10n.Profiler.StatActiveTasks, "Active Tasks");
            _overviewStats[3].Value.text = _cachedTotalTasks.ToString();
            _overviewStats[3].Accent = Styles.AccentBlue;

            _overviewStats[4].Title.text = Tr(SherpaONNXL10n.Profiler.StatAvgDuration, "Avg Duration");
            _overviewStats[4].Value.text = FormatDuration(_cachedAvgDuration);
            _overviewStats[4].Accent = Styles.AccentPurple;
        }

        private void UpdateStatusBarContent()
        {
            _sharedStringBuilder.Clear();
            _sharedStringBuilder.Append(Tr(SherpaONNXL10n.Profiler.StatusModules, "Modules"));
            _sharedStringBuilder.Append(": ");
            _sharedStringBuilder.Append(_cachedActiveModules);
            _sharedStringBuilder.Append('/');
            _sharedStringBuilder.Append(_cachedTotalModules);
            _modulesStatusContent.text = _sharedStringBuilder.ToString();

            _sharedStringBuilder.Clear();
            _sharedStringBuilder.Append(_cachedErrorModules);
            _sharedStringBuilder.Append(' ');
            _sharedStringBuilder.Append(Tr(SherpaONNXL10n.Profiler.StatErrors, "Errors"));
            _errorStatusContent.text = _sharedStringBuilder.ToString();

            _sharedStringBuilder.Clear();
            _sharedStringBuilder.Append("Logs: ");
            _sharedStringBuilder.Append(_logCount);
            _sharedStringBuilder.Append('/');
            _sharedStringBuilder.Append(MaxLogEntries);
            _logCountContent.text = _sharedStringBuilder.ToString();
        }

        private void ComputeLogLevelCounts()
        {
            Array.Clear(_logLevelCounts, 0, _logLevelCounts.Length);

            for (int i = 0; i < _filteredLogCount; i++)
            {
                var entry = _logEntries[_filteredLogIndices[i]];
                var index = entry.Level switch
                {
                    SherpaLogLevel.Error => 0,
                    SherpaLogLevel.Warning => 1,
                    SherpaLogLevel.Info => 2,
                    SherpaLogLevel.Verbose => 3,
                    SherpaLogLevel.Trace => 4,
                    _ => -1
                };

                if (index >= 0)
                {
                    _logLevelCounts[index]++;
                }
            }
        }

        private void ClearLogs()
        {
            _logStartIndex = 0;
            _logCount = 0;
            _filteredLogCount = 0;
            _recentLogStartIndex = 0;
            _recentLogCount = 0;
            _logsNeedRefresh = true;
        }

        #endregion

        #region Clipboard Operations

        private void CopyAllDiagnosticsToClipboard()
        {
            if (_cachedDiagnostics == null || _cachedDiagnostics.Count == 0)
            {
                var msg = Tr(SherpaONNXL10n.Profiler.ToastNoModules, "No active modules.");
                GUIUtility.systemCopyBuffer = msg;
                ShowNotification(new GUIContent(msg));
                return;
            }

            _sharedStringBuilder.Clear();
            _sharedStringBuilder.AppendLine($"SherpaONNX Diagnostics - {DateTime.Now:G}");
            _sharedStringBuilder.AppendLine(new string('=', 50));

            for (int i = 0; i < _cachedDiagnostics.Count; i++)
            {
                var d = _cachedDiagnostics[i];
                _sharedStringBuilder.AppendLine();
                _sharedStringBuilder.AppendLine($"Module: {d.ModelId ?? "Unknown"}");
                _sharedStringBuilder.AppendLine($"  Type: {d.ModuleType}");
                _sharedStringBuilder.AppendLine($"  Status: {GetStatusLabel(GetModuleStatus(d))}");
                _sharedStringBuilder.AppendLine($"  Active Tasks: {d.ActiveTasks}");
                _sharedStringBuilder.AppendLine($"  Completed: {d.RunnerMetrics.Completed}");
                _sharedStringBuilder.AppendLine($"  Avg Duration: {d.RunnerMetrics.AverageDurationMs:F2}ms");

                if (d.InitializationException != null)
                {
                    _sharedStringBuilder.AppendLine($"  Error: {d.InitializationException.Message}");
                }
            }

            GUIUtility.systemCopyBuffer = _sharedStringBuilder.ToString();
            ShowNotification(new GUIContent(Tr(SherpaONNXL10n.Profiler.ToastCopiedDiagnostics, "Copied")));
        }

        private void CopyLogsToClipboard()
        {
            RefreshFilteredLogs();

            if (_filteredLogCount == 0)
            {
                ShowNotification(new GUIContent(Tr(SherpaONNXL10n.Profiler.ToastNoLogs, "No logs to copy")));
                return;
            }

            _sharedStringBuilder.Clear();
            _sharedStringBuilder.AppendLine($"SherpaONNX Logs - {DateTime.Now:G}");
            _sharedStringBuilder.AppendLine(new string('=', 50));

            for (int i = 0; i < _filteredLogCount; i++)
            {
                var bufferIndex = _filteredLogIndices[i];
                ref var cache = ref _logRenderCache[bufferIndex];
                var category = cache.Category.text ?? string.Empty;
                var message = cache.Message.text ?? string.Empty;

                _sharedStringBuilder.Append('[');
                _sharedStringBuilder.Append(cache.Time.text);
                _sharedStringBuilder.Append("][");
                _sharedStringBuilder.Append(cache.Entry.Level);
                _sharedStringBuilder.Append(']');
                _sharedStringBuilder.Append(category);
                _sharedStringBuilder.Append(' ');
                _sharedStringBuilder.AppendLine(message);

                if (_showStackTraces && cache.Entry.HasStackTrace && !string.IsNullOrEmpty(cache.Stack.text))
                {
                    _sharedStringBuilder.AppendLine(cache.Stack.text);
                }
            }

            GUIUtility.systemCopyBuffer = _sharedStringBuilder.ToString();

            var toast = string.Format(Tr(SherpaONNXL10n.Profiler.ToastCopiedLogs, "Copied {0} entries"), _filteredLogCount);
            ShowNotification(new GUIContent(toast));
        }

        #endregion

        #region Utility Methods

        private static string Tr(string key, string fallback)
        {
            return SherpaONNXLocalization.Tr(key, fallback);
        }

        private void DrawEmptyState(string title, string message)
        {
            using (new EditorGUILayout.VerticalScope(Styles.CardBox, GUILayout.MinHeight(80)))
            {
                GUILayout.FlexibleSpace();

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();

                    using (new EditorGUILayout.VerticalScope())
                    {
                        GUILayout.Label(title, Styles.CenteredLabel);
                        GUILayout.Label(message, Styles.CenteredMiniLabel);
                    }

                    GUILayout.FlexibleSpace();
                }

                GUILayout.FlexibleSpace();
            }
        }

        private static string FormatDuration(double ms)
        {
            return ms.ToString("F1", CultureInfo.InvariantCulture);
        }

        private void UpdateTimeLabelCache()
        {
            var now = DateTime.Now;
            if (now.Second == _cachedTimeLabelSecond)
            {
                return;
            }

            _cachedTimeLabelSecond = now.Second;
            _cachedTimeLabel = now.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
        }

        private void CacheModulesHeader(int moduleCount)
        {
            if (_cachedModulesHeaderCount == moduleCount && !string.IsNullOrEmpty(_cachedModulesHeader))
            {
                return;
            }

            _cachedModulesHeaderCount = moduleCount;
            _sharedStringBuilder.Clear();
            _sharedStringBuilder.Append(Tr(SherpaONNXL10n.Profiler.ModulesHeader, "Active Modules"));
            _sharedStringBuilder.Append(" (");
            _sharedStringBuilder.Append(moduleCount);
            _sharedStringBuilder.Append(')');
            _cachedModulesHeader = _sharedStringBuilder.ToString();
        }

        private void UpdateCachedLocalizedStrings()
        {
            _loggingOnText = Tr(SherpaONNXL10n.Profiler.StatusLoggingOn, "Logging: ON");
            _loggingOffText = Tr(SherpaONNXL10n.Profiler.StatusLoggingOff, "Logging: OFF");
            _cachedModulesHeaderCount = -1;
            _cachedModulesHeader = string.Empty;
        }

        private static bool IsRepaintEvent()
        {
            var evt = Event.current;
            return evt != null && evt.type == EventType.Repaint;
        }

        private static ModuleStatus GetModuleStatus(SherpaONNXModule.ModuleDiagnostics d)
        {
            if (d.Disposed)
            {
                return ModuleStatus.Disposed;
            }

            if (d.InitializationException != null)
            {
                return ModuleStatus.Error;
            }

            if (d.Initialized)
            {
                return ModuleStatus.Ready;
            }

            if (d.InitializationStarted)
            {
                return ModuleStatus.Initializing;
            }

            return ModuleStatus.Pending;
        }

        private static Color GetStatusColor(ModuleStatus status)
        {
            return status switch
            {
                ModuleStatus.Ready => Styles.AccentGreen,
                ModuleStatus.Initializing => Styles.AccentYellow,
                ModuleStatus.Error => Styles.AccentRed,
                ModuleStatus.Disposed => Styles.TextMuted,
                _ => Styles.AccentBlue
            };
        }

        private string GetStatusLabel(ModuleStatus status)
        {
            return status switch
            {
                ModuleStatus.Ready => Tr(SherpaONNXL10n.Profiler.StatusReady, "Ready"),
                ModuleStatus.Initializing => Tr(SherpaONNXL10n.Profiler.StatusInitializing, "Init..."),
                ModuleStatus.Error => Tr(SherpaONNXL10n.Profiler.StatusError, "Error"),
                ModuleStatus.Disposed => Tr(SherpaONNXL10n.Profiler.StatusDisposed, "Disposed"),
                _ => Tr(SherpaONNXL10n.Profiler.StatusPending, "Pending")
            };
        }

        private static string GetLogLevelShortLabel(SherpaLogLevel level)
        {
            return level switch
            {
                SherpaLogLevel.Error => "ERR",
                SherpaLogLevel.Warning => "WRN",
                SherpaLogLevel.Info => "INF",
                SherpaLogLevel.Verbose => "VRB",
                SherpaLogLevel.Trace => "TRC",
                _ => "???"
            };
        }

        private static bool ContainsIgnoreCase(string source, string query)
        {
            if (string.IsNullOrEmpty(query))
            {
                return true;
            }

            if (string.IsNullOrEmpty(source))
            {
                return false;
            }

            return source.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void EnsureModuleViewCapacity(int required)
        {
            if (_moduleViews.Length >= required)
            {
                for (int i = 0; i < _moduleViews.Length; i++)
                {
                    if (_moduleViews[i].StatusLabel == null)
                    {
                        _moduleViews[i].StatusLabel = new GUIContent();
                        _moduleViews[i].ModelId = new GUIContent();
                        _moduleViews[i].ModuleType = new GUIContent();
                        _moduleViews[i].ActiveTasks = new GUIContent();
                        _moduleViews[i].TotalStarted = new GUIContent();
                        _moduleViews[i].Completed = new GUIContent();
                        _moduleViews[i].AvgDuration = new GUIContent();
                        _moduleViews[i].LastDuration = new GUIContent();
                        _moduleViews[i].ErrorMessage = new GUIContent();
                    }
                }

                return;
            }

            var newSize = Mathf.Max(required, _moduleViews.Length * 2);
            Array.Resize(ref _moduleViews, newSize);
            for (int i = 0; i < _moduleViews.Length; i++)
            {
                if (_moduleViews[i].StatusLabel == null)
                {
                    _moduleViews[i].StatusLabel = new GUIContent();
                    _moduleViews[i].ModelId = new GUIContent();
                    _moduleViews[i].ModuleType = new GUIContent();
                    _moduleViews[i].ActiveTasks = new GUIContent();
                    _moduleViews[i].TotalStarted = new GUIContent();
                    _moduleViews[i].Completed = new GUIContent();
                    _moduleViews[i].AvgDuration = new GUIContent();
                    _moduleViews[i].LastDuration = new GUIContent();
                    _moduleViews[i].ErrorMessage = new GUIContent();
                }
            }
        }

        private void EnsureIssueCapacity(int required)
        {
            if (_issueCache.Length >= required)
            {
                for (int i = 0; i < _issueCache.Length; i++)
                {
                    if (_issueCache[i].Title == null)
                    {
                        _issueCache[i].Title = new GUIContent();
                        _issueCache[i].Message = new GUIContent();
                        _issueCache[i].Time = new GUIContent();
                    }
                }

                return;
            }

            var newSize = Mathf.Max(required, _issueCache.Length * 2);
            Array.Resize(ref _issueCache, newSize);
            for (int i = 0; i < _issueCache.Length; i++)
            {
                if (_issueCache[i].Title == null)
                {
                    _issueCache[i].Title = new GUIContent();
                    _issueCache[i].Message = new GUIContent();
                    _issueCache[i].Time = new GUIContent();
                }
            }
        }

        #endregion
    }
}

#endif
