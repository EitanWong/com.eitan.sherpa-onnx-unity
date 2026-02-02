#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;
using Eitan.SherpaONNXUnity.Runtime;
using Eitan.SherpaONNXUnity.Editor.Localization;

namespace Eitan.SherpaONNXUnity.Editor
{
    [CustomPropertyDrawer(typeof(SherpaONNXModelFileBinding))]
    internal sealed class SherpaONNXModelFileBindingDrawer : PropertyDrawer
    {
        private static float LineHeight => EditorGUIUtility.singleLineHeight;
        private static float LineSpacing => EditorGUIUtility.standardVerticalSpacing;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return (LineHeight * 2f) + LineSpacing;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property == null)
            {
                return;
            }

            EditorGUI.BeginProperty(position, label, property);

            var keyProp = property.FindPropertyRelative("key");
            var pathProp = property.FindPropertyRelative("path");
            var line = new Rect(position.x, position.y, position.width, LineHeight);

            var keyLabel = SherpaONNXLocalization.Tr(
                SherpaONNXL10n.Settings.CustomModelsFieldBindingKeyLabel,
                "Key");
            var keyTooltip = SherpaONNXLocalization.Tr(
                SherpaONNXL10n.Settings.CustomModelsFieldBindingKeyTooltip,
                "Select the role of this file (encoder, decoder, tokens, vocoder, etc.).");
            EditorGUI.PropertyField(line, keyProp, new GUIContent(keyLabel, keyTooltip));

            line.y += LineHeight + LineSpacing;

            var pathLabel = SherpaONNXLocalization.Tr(
                SherpaONNXL10n.Settings.CustomModelsFieldBindingPathLabel,
                "Path");
            var pathTooltip = SherpaONNXLocalization.Tr(
                SherpaONNXL10n.Settings.CustomModelsFieldBindingPathTooltip,
                "Relative path inside the model folder or absolute path on disk.");
            EditorGUI.PropertyField(line, pathProp, new GUIContent(pathLabel, pathTooltip));

            EditorGUI.EndProperty();
        }
    }
}

#endif
