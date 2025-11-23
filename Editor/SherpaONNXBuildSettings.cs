#if UNITY_EDITOR
namespace Eitan.SherpaONNXUnity.Editor
{

    using System;
    using System.IO;
    using UnityEditor;


    /// <summary>
    /// ProjectSettings-backed settings for SherpaONNX build behavior.
    /// Saved as JSON in ProjectSettings/SherpaONNXSettings.json
    /// </summary>
    internal sealed class SherpaONNXBuildSettings
    {
        [Serializable]
        private class Data
        {
            // Default = false → 桌面端默认忽略 StreamingAssets/sherpa-onnx
            public bool includeModelsInDesktopBuild = false;
            public int version = 1;
        }

        private const string kSettingsPath = "ProjectSettings/SherpaONNXSettings.json";
        private static SherpaONNXBuildSettings _instance;
        private Data _data;

        public static SherpaONNXBuildSettings Instance => _instance ??= Load();

        public bool IncludeModelsInDesktopBuild
        {
            get => _data.includeModelsInDesktopBuild;
            set { if (_data.includeModelsInDesktopBuild != value) { _data.includeModelsInDesktopBuild = value; Save(); } }
        }

        private static SherpaONNXBuildSettings Load()
        {
            var inst = new SherpaONNXBuildSettings { _data = new Data() };
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
