#if UNITY_EDITOR
namespace Eitan.SherpaOnnxUnity.Editor
{

    using System;
    using System.IO;
    using UnityEditor;


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
}
#endif
