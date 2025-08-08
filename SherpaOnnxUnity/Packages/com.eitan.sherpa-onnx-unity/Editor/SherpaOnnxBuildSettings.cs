

using System;
using System.IO;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// ProjectSettings-backed settings for Sherpa ONNX build behavior.
/// Saved as JSON in ProjectSettings/SherpaOnnxSettings.json
/// </summary>
internal sealed class SherpaOnnxBuildSettings
{
    [Serializable]
    private class Data
    {
        // Default = false → 桌面端默认忽略 StreamingAssets/sherpa-onnx
        public bool includeModelsInDesktopBuild = false;
        public int version = 1;
    }

    private const string kSettingsPath = "ProjectSettings/SherpaOnnxSettings.json";
    private static SherpaOnnxBuildSettings _instance;
    private Data _data;

    public static SherpaOnnxBuildSettings Instance => _instance ??= Load();

    public bool IncludeModelsInDesktopBuild
    {
        get => _data.includeModelsInDesktopBuild;
        set { if (_data.includeModelsInDesktopBuild != value) { _data.includeModelsInDesktopBuild = value; Save(); } }
    }

    private static SherpaOnnxBuildSettings Load()
    {
        var inst = new SherpaOnnxBuildSettings { _data = new Data() };
        try
        {
            if (File.Exists(kSettingsPath))
            {
                var json = File.ReadAllText(kSettingsPath);
                EditorJsonUtility.FromJsonOverwrite(json, inst._data);
            }
        }
        catch { /* ignore malformed or IO errors */ }
        return inst;
    }

    public void Save()
    {
        try
        {
            var json = EditorJsonUtility.ToJson(_data, true);
            File.WriteAllText(kSettingsPath, json);
            AssetDatabase.Refresh();
        }
        catch { /* ignore */ }
    }
}

/// <summary>
/// Project Settings UI: Edit ▸ Project Settings ▸ SHERPA ONNX
/// </summary>
internal sealed class SherpaOnnxSettingsProvider : SettingsProvider
{
    private const string kPath = "Project/Sherpa Onnx";

    public SherpaOnnxSettingsProvider() : base(kPath, SettingsScope.Project) { }

    [SettingsProvider]
    public static SettingsProvider Create() => new SherpaOnnxSettingsProvider();

    public override void OnActivate(string searchContext, VisualElement rootElement)
    {
        var settings = SherpaOnnxBuildSettings.Instance;

        var title = new Label("Sherpa Onnx Build Settings");
        title.style.unityFontStyleAndWeight = FontStyle.Bold;
        title.style.marginTop = 6; title.style.marginBottom = 6;
        rootElement.Add(title);

        var toggle = new Toggle("Include downloaded models in desktop builds (Windows/macOS/Linux)")
        {
            tooltip = "If enabled, Assets/StreamingAssets/sherpa-onnx will be included when building Standalone (desktop) targets. Default: OFF.",
            value = settings.IncludeModelsInDesktopBuild
        };
        toggle.RegisterValueChangedCallback(evt => settings.IncludeModelsInDesktopBuild = evt.newValue);
        rootElement.Add(toggle);

        var help = new HelpBox(
            "OFF (default): desktop builds ignore StreamingAssets/sherpa-onnx (smaller/faster).\n" +
            "ON: desktop builds include that folder.\n" +
            "Mobile/WebGL/consoles remain ignored because StreamingAssets is read-only at runtime.",
            HelpBoxMessageType.Info);
        help.style.marginTop = 6; rootElement.Add(help);
    }

    public override void OnGUI(string searchContext)
    {
        // IMGUI fallback
        var settings = SherpaOnnxBuildSettings.Instance;
        EditorGUI.BeginChangeCheck();
        var newVal = EditorGUILayout.ToggleLeft(
            new GUIContent("Include downloaded models in desktop builds (Windows/macOS/Linux)"),
            settings.IncludeModelsInDesktopBuild);
        if (EditorGUI.EndChangeCheck())
        {
            settings.IncludeModelsInDesktopBuild = newVal;
        }


        EditorGUILayout.HelpBox(
            "OFF (default): desktop builds ignore StreamingAssets/sherpa-onnx.\nON: include that folder.",
            MessageType.Info);
    }
}