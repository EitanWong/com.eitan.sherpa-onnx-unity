#if UNITY_EDITOR

namespace Eitan.SherpaONNXUnity.Editor
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using UnityEditor;
    using UnityEngine;
    using Eitan.Sherpa.Onnx.Unity.Editor.Mono.Icons;
    using Eitan.SherpaONNXUnity.Runtime;

    internal static class SherpaONNXCustomModelSettingsUtility
    {
        private const string TypeFilter = "t:" + nameof(SherpaONNXCustomModelSettings);

        internal static SherpaONNXCustomModelSettings LoadOrCreateSettingsAsset()
        {
            var existing = FindExistingAsset();
            if (existing != null)
            {
                return existing;
            }

            EnsureResourcesFolder();
            var settings = ScriptableObject.CreateInstance<SherpaONNXCustomModelSettings>();
            AssetDatabase.CreateAsset(settings, SherpaONNXCustomModelSettings.AssetPath);
            SherpaMonoIconRegistry.ApplyIconForAsset(settings);
            AssetDatabase.SaveAssets();
            return settings;
        }

        private static SherpaONNXCustomModelSettings FindExistingAsset()
        {
            var matches = FindAllResourceAssets();
            if (matches.Count == 0)
            {
                return null;
            }

            if (matches.Count > 1)
            {
                LogDuplicateAssets(matches);
            }

            matches.Sort((a, b) => string.CompareOrdinal(a.Path, b.Path));
            return matches[0].Asset;
        }

        private static List<AssetEntry> FindAllResourceAssets()
        {
            var assets = new List<AssetEntry>();
            var guids = AssetDatabase.FindAssets(TypeFilter);
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!IsInsideResources(path))
                {
                    continue;
                }

                var asset = AssetDatabase.LoadAssetAtPath<SherpaONNXCustomModelSettings>(path);
                if (asset != null)
                {
                    assets.Add(new AssetEntry(asset, path));
                }
            }

            return assets;
        }

        private static bool IsInsideResources(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
            {
                return false;
            }

            return assetPath.IndexOf("/Resources/", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void LogDuplicateAssets(List<AssetEntry> entries)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Multiple {nameof(SherpaONNXCustomModelSettings)} assets were found under Resources. Only one asset is supported. Please delete the duplicates:");
            foreach (var entry in entries)
            {
                sb.Append(" • ").AppendLine(entry.Path);
            }

            SherpaLog.Error(sb.ToString().TrimEnd(), category: "Settings");
        }

        private static void EnsureResourcesFolder()
        {
            var directory = Path.GetDirectoryName(SherpaONNXCustomModelSettings.AssetPath);
            if (string.IsNullOrEmpty(directory) || Directory.Exists(directory))
            {
                return;
            }

            Directory.CreateDirectory(directory);
            AssetDatabase.Refresh();
        }

        private readonly struct AssetEntry
        {
            public AssetEntry(SherpaONNXCustomModelSettings asset, string path)
            {
                Asset = asset;
                Path = path;
            }

            public SherpaONNXCustomModelSettings Asset { get; }
            public string Path { get; }
        }
    }
}

#endif
