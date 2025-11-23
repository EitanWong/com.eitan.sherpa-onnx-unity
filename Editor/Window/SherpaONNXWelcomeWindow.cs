#if UNITY_EDITOR

namespace Eitan.SherpaONNXUnity.Editor
{
    using System;
    using System.Reflection;
    using Eitan.SherpaONNXUnity.Editor.Localization;
    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// Lightweight welcome page shown on editor launch to guide new users.
    /// </summary>
    internal sealed class SherpaONNXWelcomeWindow : EditorWindow
    {
        private const string PackageName = "com.eitan.sherpa-onnx-unity";
        private const string GithubUrl = "https://github.com/EitanWong/com.eitan.sherpa-onnx-unity";
        private const string SettingsPath = "Project/SherpaONNX";
        private const string ShowOnStartupPrefKey = "Eitan.SherpaONNXUnity.Welcome.ShowOnStartup";
        private const string SessionShownKey = "Eitan.SherpaONNXUnity.Welcome.SessionShown";

        private static readonly Vector2 MinWindowSize = new Vector2(540f, 600f);

        private Vector2 _scroll;
        private bool _dontShowAgain;

        [InitializeOnLoadMethod]
        private static void AutoShowOnLoad()
        {
            if (Application.isBatchMode)
            {
                return;
            }

            if (!EditorPrefs.GetBool(ShowOnStartupPrefKey, true))
            {
                return;
            }

            if (SessionState.GetBool(SessionShownKey, false))
            {
                return;
            }

            SessionState.SetBool(SessionShownKey, true);
            EditorApplication.delayCall += AutoShowHandler;
        }

        private static void AutoShowHandler()
        {
            EditorApplication.delayCall -= AutoShowHandler;
            OpenWindow(false);
        }

        [MenuItem("Help/SherpaONNX/Welcome")]
        private static void MenuOpen()
        {
            OpenWindow(true);
        }

        private static void OpenWindow(bool focus)
        {
            var previousWindow = EditorWindow.focusedWindow;
            var window = GetWindow<SherpaONNXWelcomeWindow>(utility: false, title: GetWindowTitle(), focus: focus);
            window.minSize = MinWindowSize;
            if (EditorPrefs.HasKey(ShowOnStartupPrefKey))
            {
                window._dontShowAgain = !EditorPrefs.GetBool(ShowOnStartupPrefKey, true);
            }
            else
            {
                window._dontShowAgain = true;
                EditorPrefs.SetBool(ShowOnStartupPrefKey, false);
            }
            window.Show();

            if (!focus && previousWindow != null)
            {
                EditorApplication.delayCall += () =>
                {
                    if (previousWindow != null)
                    {
                        previousWindow.Focus();
                    }
                };
            }
        }

        private void OnEnable()
        {
            titleContent = new GUIContent(GetWindowTitle());
            minSize = MinWindowSize;
            if (EditorPrefs.HasKey(ShowOnStartupPrefKey))
            {
                _dontShowAgain = !EditorPrefs.GetBool(ShowOnStartupPrefKey, true);
            }
            else
            {
                _dontShowAgain = true;
                EditorPrefs.SetBool(ShowOnStartupPrefKey, false);
            }
            SherpaONNXLocalization.LanguageChanged += Repaint;
        }

        private void OnDisable()
        {
            SaveShowPreference();
            SherpaONNXLocalization.LanguageChanged -= Repaint;
        }

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUILayout.BeginVertical(Styles.Root);
            DrawHero();
            GUILayout.Space(12f);
            DrawPrimaryActions();
            GUILayout.Space(14f);
            DrawSections();
            GUILayout.FlexibleSpace();
            DrawFooter();
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndScrollView();
        }

        private void DrawHero()
        {
            var heroRect = GUILayoutUtility.GetRect(
                GUIContent.none,
                GUIStyle.none,
                GUILayout.ExpandWidth(true),
                GUILayout.Height(140f));
            var background = EditorGUIUtility.isProSkin
                ? new Color(0.14f, 0.19f, 0.23f)
                : new Color(0.87f, 0.92f, 0.97f);
            EditorGUI.DrawRect(heroRect, background);

            var padded = heroRect;
            padded.xMin += 16f;
            padded.xMax -= 16f;
            padded.yMin += 12f;
            padded.yMax -= 12f;

            EditorGUI.LabelField(
                new Rect(padded.x, padded.y, padded.width, 28f),
                L(SherpaONNXL10n.Welcome.HeroTitle, "SherpaONNX for Unity"),
                Styles.HeroTitle);

            var subtitleRect = new Rect(padded.x, padded.y + 34f, padded.width, padded.height - 34f);
            EditorGUI.LabelField(
                subtitleRect,
                L(
                    SherpaONNXL10n.Welcome.HeroSubtitle,
                    "Speech AI toolkit for recognition, VAD, KWS, tagging, and synthesis."),
                Styles.HeroSubtitle);
        }

        private void DrawPrimaryActions()
        {
            EditorGUILayout.BeginHorizontal();
            DrawPrimaryButton(
                L(SherpaONNXL10n.Welcome.ButtonOpenSamples, "Open Samples"),
                OpenPackageSamples);
            DrawPrimaryButton(
                L(SherpaONNXL10n.Welcome.ButtonOpenModels, "Model Manager"),
                OpenModelManager);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            DrawPrimaryButton(
                L(SherpaONNXL10n.Welcome.ButtonOpenSettings, "Open Settings"),
                OpenSettings);
            DrawPrimaryButton(
                L(SherpaONNXL10n.Welcome.ButtonOpenGithub, "View on GitHub"),
                () => Application.OpenURL(GithubUrl));
            EditorGUILayout.EndHorizontal();
        }

        private void DrawPrimaryButton(string label, Action onClick)
        {
            if (GUILayout.Button(label, Styles.PrimaryButton, GUILayout.Height(34f)))
            {
                onClick?.Invoke();
            }
        }

        private void DrawSections()
        {
            DrawSection(
                L(SherpaONNXL10n.Welcome.SectionGetStarted, "Start With Samples"),
                L(
                    SherpaONNXL10n.Welcome.BodyGetStarted,
                    "Open Package Manager → com.eitan.sherpa-onnx-unity → Samples to import ready-to-run scenes and scripts."),
                L(SherpaONNXL10n.Welcome.LabelPackageManager, "Package Manager → com.eitan.sherpa-onnx-unity"),
                L(SherpaONNXL10n.Welcome.ButtonOpenSamples, "Open Samples"),
                OpenPackageSamples);

            DrawSection(
                L(SherpaONNXL10n.Welcome.SectionModels, "Manage Models"),
                L(
                    SherpaONNXL10n.Welcome.BodyModels,
                    "Use the Model Manager to browse, download, verify, and manage all supported SherpaONNX models."),
                L(SherpaONNXL10n.Welcome.LabelModelsWindow, "Window → SherpaONNX → Model Manager"),
                L(SherpaONNXL10n.Welcome.ButtonOpenModels, "Model Manager"),
                OpenModelManager);

            DrawSection(
                L(SherpaONNXL10n.Welcome.SectionSettings, "Project Settings"),
                L(
                    SherpaONNXL10n.Welcome.BodySettings,
                    "Adjust runtime defaults, download behavior, and editor language under Project Settings → SherpaONNX."),
                L(SherpaONNXL10n.Welcome.LabelSettingsPath, "Project Settings → SherpaONNX"),
                L(SherpaONNXL10n.Welcome.ButtonOpenSettings, "Open Settings"),
                OpenSettings);

            DrawSection(
                L(SherpaONNXL10n.Welcome.SectionResources, "Resources"),
                L(
                    SherpaONNXL10n.Welcome.BodyResources,
                    "Visit the GitHub repository for source, docs, and issue tracking."),
                GithubUrl,
                L(SherpaONNXL10n.Welcome.ButtonOpenGithub, "View on GitHub"),
                () => Application.OpenURL(GithubUrl));
        }

        private void DrawSection(string title, string body, string note, string buttonLabel, Action onClick)
        {
            EditorGUILayout.BeginVertical(Styles.SectionCard);
            EditorGUILayout.LabelField(title, Styles.SectionTitle);
            GUILayout.Space(2f);
            EditorGUILayout.LabelField(body, Styles.Body);
            if (!string.IsNullOrEmpty(note))
            {
                GUILayout.Space(2f);
                EditorGUILayout.LabelField(note, Styles.Note);
            }
            GUILayout.Space(8f);
            if (GUILayout.Button(buttonLabel, Styles.CardButton))
            {
                onClick?.Invoke();
            }
            EditorGUILayout.EndVertical();
            GUILayout.Space(8f);
        }

        private void DrawFooter()
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            var label = new GUIContent(L(SherpaONNXL10n.Welcome.ToggleDontShowAgain, "Don't show this welcome screen on startup"));
            var toggleRect = GUILayoutUtility.GetRect(label, Styles.FooterToggle);
            toggleRect.x = Mathf.Max(toggleRect.x, position.width - toggleRect.width - 24f);
            var dontShow = GUI.Toggle(toggleRect, _dontShowAgain, label, Styles.FooterToggle);
            if (dontShow != _dontShowAgain)
            {
                _dontShowAgain = dontShow;
                SaveShowPreference();
            }
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(4f);
        }

        private static void OpenPackageSamples()
        {
            if (TryOpenPackageManagerForPackage(PackageName))
            {
                FocusPackageManagerWindow();
                return;
            }

            EditorUtility.DisplayDialog(
                L(SherpaONNXL10n.Welcome.ButtonOpenSamples, "Open Samples"),
                L(
                    SherpaONNXL10n.Welcome.BodyGetStarted,
                    "Open Package Manager → com.eitan.sherpa-onnx-unity → Samples to import ready-to-run scenes and scripts."),
                "OK");
        }

        private static void OpenModelManager()
        {
            if (!EditorApplication.ExecuteMenuItem("Window/SherpaONNX/Model Manager"))
            {
                var window = GetWindow<SherpaONNXModelsEditorWindow>();
                window.Show();
                window.Focus();
                return;
            }

            FocusModelManagerWindow();
        }

        private static void OpenSettings()
        {
            SettingsService.OpenProjectSettings(SettingsPath);
        }

        private static bool TryOpenPackageManagerForPackage(string packageName)
        {
            try
            {
                var windowType = Type.GetType("UnityEditor.PackageManager.UI.Window, UnityEditor.PackageManagerUIEditor");
                var openMethod = windowType?.GetMethod("Open", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string) }, null);
                if (openMethod != null)
                {
                    openMethod.Invoke(null, new object[] { packageName });
                    return true;
                }
            }
            catch
            {
                // Ignore reflection failures and fall back to generic menu item.
            }

            return EditorApplication.ExecuteMenuItem("Window/Package Manager");
        }

        private static void FocusModelManagerWindow()
        {
            EditorApplication.delayCall += () =>
            {
                var instances = Resources.FindObjectsOfTypeAll<SherpaONNXModelsEditorWindow>();
                if (instances != null && instances.Length > 0)
                {
                    instances[0].Focus();
                }
            };
        }

        private static void FocusPackageManagerWindow()
        {
            EditorApplication.delayCall += () =>
            {
                var windowType =
                    Type.GetType("UnityEditor.PackageManager.UI.PackageManagerWindow, UnityEditor.PackageManagerUIEditor") ??
                    Type.GetType("UnityEditor.PackageManager.UI.PackageManagerWindow, UnityEditor.PackageManagerUIModule") ??
                    Type.GetType("UnityEditor.PackageManager.UI.Window, UnityEditor.PackageManagerUIEditor");

                if (windowType == null)
                {
                    return;
                }

                var windows = Resources.FindObjectsOfTypeAll(windowType);
                if (windows != null && windows.Length > 0 && windows[0] is EditorWindow editorWindow)
                {
                    editorWindow.Focus();
                }
            };
        }

        private void SaveShowPreference()
        {
            EditorPrefs.SetBool(ShowOnStartupPrefKey, !_dontShowAgain);
        }

        private static string GetWindowTitle()
        {
            return L(SherpaONNXL10n.Welcome.WindowTitle, "Welcome to SherpaONNX");
        }

        private static string L(string key, string fallback) => SherpaONNXLocalization.Tr(key, fallback);

        private static class Styles
        {
            internal static readonly GUIStyle HeroTitle;
            internal static readonly GUIStyle HeroSubtitle;
            internal static readonly GUIStyle Root;
            internal static readonly GUIStyle PrimaryButton;
            internal static readonly GUIStyle SectionCard;
            internal static readonly GUIStyle SectionTitle;
            internal static readonly GUIStyle Body;
            internal static readonly GUIStyle Note;
            internal static readonly GUIStyle CardButton;
            internal static readonly GUIStyle FooterToggle;

            static Styles()
            {
                HeroTitle = new GUIStyle(EditorStyles.label)
                {
                    fontSize = 20,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleLeft,
                    wordWrap = true
                };

                HeroSubtitle = new GUIStyle(EditorStyles.wordWrappedLabel)
                {
                    fontSize = 12,
                    wordWrap = true
                };

                Root = new GUIStyle
                {
                    padding = new RectOffset(12, 12, 10, 10)
                };

                PrimaryButton = new GUIStyle(GUI.skin.button)
                {
                    fontSize = 12,
                    margin = new RectOffset(0, 8, 0, 8)
                };

                SectionCard = new GUIStyle("helpbox")
                {
                    padding = new RectOffset(12, 12, 10, 12)
                };

                SectionTitle = new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = 13
                };

                Body = new GUIStyle(EditorStyles.wordWrappedLabel)
                {
                    fontSize = 11
                };

                Note = new GUIStyle(EditorStyles.wordWrappedMiniLabel)
                {
                    fontSize = 10,
                    normal = { textColor = new Color(0.45f, 0.45f, 0.45f) }
                };

                CardButton = new GUIStyle(GUI.skin.button)
                {
                    fixedHeight = 28f
                };

                FooterToggle = new GUIStyle(EditorStyles.toggle);
            }
        }
    }
}

#endif
