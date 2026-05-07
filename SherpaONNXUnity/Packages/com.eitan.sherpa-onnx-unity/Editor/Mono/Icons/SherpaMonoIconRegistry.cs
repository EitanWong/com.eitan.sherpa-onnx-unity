namespace Eitan.Sherpa.Onnx.Unity.Editor.Mono.Icons
{
    using System;
    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// Binds procedural SherpaONNXUnity icons to supported editor objects when the editor domain loads.
    /// </summary>
    [InitializeOnLoad]
    internal static class SherpaMonoIconRegistry
    {
        static SherpaMonoIconRegistry()
        {
            EditorApplication.delayCall += ApplyRegisteredIcons;
        }

        internal static void ApplyRegisteredIcons()
        {
            foreach (var pair in SherpaMonoIconProvider.ComponentIconMap)
            {
                ApplyScriptIcon(pair.Key, pair.Value);
                ApplyAssetIcons(pair.Key, pair.Value);
            }
        }

        internal static void ApplyIconForAsset(UnityEngine.Object asset)
        {
            if (asset == null || !SherpaMonoIconProvider.TryGetIconIdForType(asset.GetType(), out var iconId))
            {
                return;
            }

            ApplyObjectIcon(asset, iconId);
        }

        internal static void ApplyIconForType(Type type)
        {
            if (type == null || !SherpaMonoIconProvider.TryGetIconIdForType(type, out var iconId))
            {
                return;
            }

            ApplyScriptIcon(type, iconId);
            ApplyAssetIcons(type, iconId);
        }

        private static void ApplyScriptIcon(Type type, SherpaMonoIconId iconId)
        {
            var script = FindMonoScript(type);
            if (script == null)
            {
                return;
            }

            var icon = SherpaMonoIconProvider.GetLargeIcon(iconId);
            EditorGUIUtility.SetIconForObject(script, icon);
            EditorUtility.SetDirty(script);
        }

        private static void ApplyAssetIcons(Type type, SherpaMonoIconId iconId)
        {
            if (!typeof(ScriptableObject).IsAssignableFrom(type))
            {
                return;
            }

            var guids = AssetDatabase.FindAssets("t:" + type.Name);
            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var asset = AssetDatabase.LoadAssetAtPath(path, type);
                if (asset != null)
                {
                    ApplyObjectIcon(asset, iconId);
                }
            }
        }

        private static void ApplyObjectIcon(UnityEngine.Object target, SherpaMonoIconId iconId)
        {
            EditorGUIUtility.SetIconForObject(target, SherpaMonoIconProvider.GetLargeIcon(iconId));
            EditorUtility.SetDirty(target);
        }

        private static MonoScript FindMonoScript(Type componentType)
        {
            var scriptName = componentType.Name;
            var genericMarker = scriptName.IndexOf('`');
            if (genericMarker >= 0)
            {
                scriptName = scriptName.Substring(0, genericMarker);
            }

            var guids = AssetDatabase.FindAssets(scriptName + " t:MonoScript");
            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);

                if (script != null && script.GetClass() == componentType)
                {
                    return script;
                }
            }

            return null;
        }
    }
}
