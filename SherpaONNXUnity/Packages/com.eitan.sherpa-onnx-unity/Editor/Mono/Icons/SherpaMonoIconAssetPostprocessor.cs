namespace Eitan.Sherpa.Onnx.Unity.Editor.Mono.Icons
{
    using UnityEditor;
    using Eitan.SherpaONNXUnity.Runtime;

    internal sealed class SherpaMonoIconAssetPostprocessor : AssetPostprocessor
    {
        private const string RuntimeMonoPath = "Packages/com.eitan.sherpa-onnx-unity/Runtime/Mono/";
        private const string RuntimeSettingsPath = "Packages/com.eitan.sherpa-onnx-unity/Runtime/Core/Structs/";

        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (!ContainsIconTarget(importedAssets) && !ContainsIconTarget(movedAssets))
            {
                return;
            }

            EditorApplication.delayCall -= SherpaMonoIconRegistry.ApplyRegisteredIcons;
            EditorApplication.delayCall += SherpaMonoIconRegistry.ApplyRegisteredIcons;
        }

        private static bool ContainsIconTarget(string[] assetPaths)
        {
            if (assetPaths == null)
            {
                return false;
            }

            for (var i = 0; i < assetPaths.Length; i++)
            {
                var path = assetPaths[i];
                if (!string.IsNullOrEmpty(path) && path.StartsWith(RuntimeMonoPath) && path.EndsWith(".cs"))
                {
                    return true;
                }

                if (!string.IsNullOrEmpty(path) &&
                    path.StartsWith(RuntimeSettingsPath) &&
                    (path.EndsWith("SherpaONNXRuntimeSettings.cs") || path.EndsWith("SherpaONNXCustomModelSettings.cs")))
                {
                    return true;
                }

                if (path == SherpaONNXRuntimeSettings.AssetPath || path == SherpaONNXCustomModelSettings.AssetPath)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
