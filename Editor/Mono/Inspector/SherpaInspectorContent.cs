// Editor helper: Packages/com.eitan.sherpa-onnx-unity/Editor/Mono/Inspector/SherpaInspectorContent.cs

namespace Eitan.Sherpa.Onnx.Unity.Editor.Mono.Inspector
{
    using Eitan.SherpaONNXUnity.Editor.Localization;
    using UnityEngine;

    internal static class SherpaInspectorContent
    {
        public static GUIContent Label(string key, string fallback, string tooltipKey = null, string tooltipFallback = null)
        {
            var label = SherpaONNXLocalization.Tr(key, fallback);
            if (string.IsNullOrEmpty(tooltipKey))
            {
                return new GUIContent(label);
            }

            var tooltip = SherpaONNXLocalization.Tr(tooltipKey, tooltipFallback);
            return new GUIContent(label, tooltip);
        }

        public static string Text(string key, string fallback)
        {
            return SherpaONNXLocalization.Tr(key, fallback);
        }
    }
}
