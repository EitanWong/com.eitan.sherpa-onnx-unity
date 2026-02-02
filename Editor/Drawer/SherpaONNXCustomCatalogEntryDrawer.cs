#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Eitan.SherpaONNXUnity.Runtime;
using Eitan.SherpaONNXUnity.Editor.Localization;

namespace Eitan.SherpaONNXUnity.Editor
{
    [CustomPropertyDrawer(typeof(SherpaONNXCustomCatalogEntry))]
    internal sealed class SherpaONNXCustomCatalogEntryDrawer : PropertyDrawer
    {
        private static float LineHeight => EditorGUIUtility.singleLineHeight;
        private static float LineSpacing => EditorGUIUtility.standardVerticalSpacing;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (property == null)
            {
                return LineHeight;
            }

            if (!property.isExpanded)
            {
                return LineHeight;
            }

            var entryType = GetEntryType(property);
            if (entryType == SherpaONNXCustomCatalogEntryType.RemoteManifest)
            {
                var manifestHeight = GetLineHeight(property.FindPropertyRelative("remoteManifestUrl"), includeChildren: false);
                return LineHeight + LineSpacing + manifestHeight;
            }

            var lineHeights = 0f;
            var lineCount = 0;
            lineHeights += AddLineHeight(property, "modelId", false, ref lineCount);
            lineHeights += AddLineHeight(property, "moduleType", false, ref lineCount);
            lineHeights += AddLineHeight(property, "moduleTypeHint", false, ref lineCount);
            lineHeights += AddLineHeight(property, "downloadUrl", false, ref lineCount);
            lineHeights += AddLineHeight(property, "downloadFileHash", false, ref lineCount);
            lineHeights += AddLineHeight(property, "modelTypeHint", false, ref lineCount);

            if (IsSpeechSynthesis(property))
            {
                lineHeights += AddLineHeight(property, "numberOfSpeakers", false, ref lineCount);
                lineHeights += AddLineHeight(property, "sampleRate", false, ref lineCount);
            }

            lineHeights += AddLineHeight(property, "fileBindings", true, ref lineCount);

            if (lineCount == 0)
            {
                return LineHeight;
            }

            var spacingHeight = LineSpacing * (lineCount - 1);
            return LineHeight + LineSpacing + lineHeights + spacingHeight;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property == null)
            {
                return;
            }

            EditorGUI.BeginProperty(position, label, property);

            var line = new Rect(position.x, position.y, position.width, LineHeight);
            var enabledProp = property.FindPropertyRelative("enabled");
            var entryTypeProp = property.FindPropertyRelative("entryType");
            var nameProp = property.FindPropertyRelative("name");
            var isEnabled = enabledProp != null && enabledProp.boolValue;

            if (!isEnabled)
            {
                EditorGUI.DrawRect(position, new Color(0f, 0f, 0f, 0.04f));
            }

            var foldoutRect = new Rect(line.x, line.y, 14f, line.height);
            property.isExpanded = EditorGUI.Foldout(foldoutRect, property.isExpanded, GUIContent.none, true);

            var enabledRect = new Rect(line.x + 16f, line.y, 16f, line.height);
            if (enabledProp != null)
            {
                enabledProp.boolValue = EditorGUI.Toggle(enabledRect, enabledProp.boolValue);
            }

            using (new EditorGUI.DisabledGroupScope(!isEnabled))
            {
                var typeRect = new Rect(line.x + 36f, line.y, 140f, line.height);
                if (entryTypeProp != null)
                {
                    EditorGUI.PropertyField(typeRect, entryTypeProp, new GUIContent(
                        string.Empty,
                        SherpaONNXLocalization.Tr(
                            SherpaONNXL10n.Settings.CustomModelsFieldEntryTypeTooltip,
                            "Choose Model to define a model entry or Remote Manifest to load a JSON manifest.")));
                }

                var nameRect = new Rect(line.x + 180f, line.y, line.width - 180f, line.height);
                if (nameProp != null)
                {
                    EditorGUI.PropertyField(nameRect, nameProp, new GUIContent(
                        string.Empty,
                        SherpaONNXLocalization.Tr(
                            SherpaONNXL10n.Settings.CustomModelsFieldEntryNameTooltip,
                            "Display name for this entry (Settings only).")));
                }
            }

            if (!isEnabled)
            {
                var statusText = SherpaONNXLocalization.Tr(
                    SherpaONNXL10n.Settings.CustomModelsDisabledLabel,
                    "Disabled");
                var size = EditorStyles.miniLabel.CalcSize(new GUIContent(statusText));
                var statusRect = new Rect(line.x + line.width - size.x - 4f, line.y, size.x + 4f, line.height);
                EditorGUI.LabelField(statusRect, statusText, EditorStyles.miniLabel);
            }

            if (!property.isExpanded)
            {
                EditorGUI.EndProperty();
                return;
            }

            line.y += LineHeight + LineSpacing;
            if (GetEntryType(property) == SherpaONNXCustomCatalogEntryType.RemoteManifest)
            {
                using (new EditorGUI.DisabledGroupScope(!isEnabled))
                {
                    DrawPropertyLine(ref line,
                        property.FindPropertyRelative("remoteManifestUrl"),
                        SherpaONNXLocalization.Tr(SherpaONNXL10n.Settings.CustomModelsFieldManifestUrl, "Manifest URL"),
                        SherpaONNXLocalization.Tr(
                            SherpaONNXL10n.Settings.CustomModelsFieldManifestUrlTooltip,
                            "HTTP(s) URL to a SherpaONNXModelManifest JSON file."));
                }
                EditorGUI.EndProperty();
                return;
            }

            using (new EditorGUI.DisabledGroupScope(!isEnabled))
            {
                DrawPropertyLine(ref line,
                    property.FindPropertyRelative("modelId"),
                    SherpaONNXLocalization.Tr(SherpaONNXL10n.Settings.CustomModelsFieldModelId, "Model Id"),
                    SherpaONNXLocalization.Tr(
                        SherpaONNXL10n.Settings.CustomModelsFieldModelIdTooltip,
                        "Must match the modelId in the manifest and the model folder name under StreamingAssets/sherpa-onnx."));
                DrawModuleTypeLine(ref line,
                    property.FindPropertyRelative("moduleType"),
                    SherpaONNXLocalization.Tr(SherpaONNXL10n.Settings.CustomModelsFieldModuleType, "Module Type"),
                    SherpaONNXLocalization.Tr(
                        SherpaONNXL10n.Settings.CustomModelsFieldModuleTypeTooltip,
                        "Select the module that will load this model (e.g., SpeechRecognition, SpeechSynthesis)."));
                DrawPropertyLine(ref line,
                    property.FindPropertyRelative("downloadUrl"),
                    SherpaONNXLocalization.Tr(SherpaONNXL10n.Settings.CustomModelsFieldDownloadUrl, "Download URL"),
                    SherpaONNXLocalization.Tr(
                        SherpaONNXL10n.Settings.CustomModelsFieldDownloadUrlTooltip,
                        "Direct URL to the model archive/file used for auto-download."));
                DrawPropertyLine(ref line,
                    property.FindPropertyRelative("downloadFileHash"),
                    SherpaONNXLocalization.Tr(SherpaONNXL10n.Settings.CustomModelsFieldDownloadHash, "Download File Hash"),
                    SherpaONNXLocalization.Tr(
                        SherpaONNXL10n.Settings.CustomModelsFieldDownloadHashTooltip,
                        "SHA256 hash of the download file for integrity verification."));
                DrawModelTypeHintLine(ref line,
                    property.FindPropertyRelative("modelTypeHint"),
                    property.FindPropertyRelative("moduleType"),
                    SherpaONNXLocalization.Tr(SherpaONNXL10n.Settings.CustomModelsFieldModelTypeHint, "Model Type Hint"),
                    SherpaONNXLocalization.Tr(
                        SherpaONNXL10n.Settings.CustomModelsFieldModelTypeHintTooltip,
                        "Optional enum name to force model type detection (e.g., Offline_Transducer, Vits, SileroVad, Whisper)."));

                if (IsSpeechSynthesis(property))
                {
                    DrawPropertyLine(ref line,
                        property.FindPropertyRelative("numberOfSpeakers"),
                        SherpaONNXLocalization.Tr(SherpaONNXL10n.Settings.CustomModelsFieldSpeakers, "Number Of Speakers"),
                        SherpaONNXLocalization.Tr(
                            SherpaONNXL10n.Settings.CustomModelsFieldSpeakersTooltip,
                            "For multi-speaker TTS models, specify the number of available speakers."));
                    DrawPropertyLine(ref line,
                        property.FindPropertyRelative("sampleRate"),
                        SherpaONNXLocalization.Tr(SherpaONNXL10n.Settings.CustomModelsFieldSampleRate, "Sample Rate"),
                        SherpaONNXLocalization.Tr(
                            SherpaONNXL10n.Settings.CustomModelsFieldSampleRateTooltip,
                            "Expected audio sample rate (e.g., 16000 or 24000)."));
                }

                DrawPropertyLine(ref line,
                    property.FindPropertyRelative("fileBindings"),
                    SherpaONNXLocalization.Tr(SherpaONNXL10n.Settings.CustomModelsFieldBindings, "File Bindings"),
                    SherpaONNXLocalization.Tr(
                        SherpaONNXL10n.Settings.CustomModelsFieldBindingsTooltip,
                        "Map SherpaONNXModelFileKey to specific files. Paths are relative to the model folder unless absolute."),
                    includeChildren: true);
            }

            EditorGUI.EndProperty();
        }

        private static void DrawPropertyLine(ref Rect line, SerializedProperty property, string label, string tooltip = null, bool includeChildren = false)
        {
            if (property != null)
            {
                var content = new GUIContent(label, tooltip ?? string.Empty);
                if (includeChildren)
                {
                    var height = EditorGUI.GetPropertyHeight(property, includeChildren: true);
                    var rect = new Rect(line.x, line.y, line.width, height);
                    EditorGUI.PropertyField(rect, property, content, includeChildren: true);
                    line.y += height + LineSpacing;
                    return;
                }

                EditorGUI.PropertyField(line, property, content, false);
            }

            line.y += LineHeight + LineSpacing;
        }

        private static void DrawModuleTypeLine(ref Rect line, SerializedProperty property, string label, string tooltip)
        {
            if (property == null)
            {
                line.y += LineHeight + LineSpacing;
                return;
            }

            var current = (SherpaONNXModuleType)property.enumValueIndex;
            var currentValue = current == SherpaONNXModuleType.Undefined ? string.Empty : current.ToString();
            var options = BuildEnumOptions(typeof(SherpaONNXModuleType), currentValue, excludeUndefined: true, out var values, out var selectedIndex);
            var content = new GUIContent(label, tooltip ?? string.Empty);
            var newIndex = EditorGUI.Popup(line, content, selectedIndex, options);
            if (newIndex != selectedIndex)
            {
                if (string.IsNullOrWhiteSpace(values[newIndex]))
                {
                    property.enumValueIndex = (int)SherpaONNXModuleType.SpeechRecognition;
                }
                else if (Enum.TryParse(values[newIndex], true, out SherpaONNXModuleType parsed))
                {
                    property.enumValueIndex = (int)parsed;
                }
            }

            line.y += LineHeight + LineSpacing;
        }

        private static void DrawModelTypeHintLine(ref Rect line, SerializedProperty modelTypeProp, SerializedProperty moduleTypeProp, string label, string tooltip)
        {
            if (modelTypeProp == null || moduleTypeProp == null)
            {
                line.y += LineHeight + LineSpacing;
                return;
            }

            var moduleType = (SherpaONNXModuleType)moduleTypeProp.enumValueIndex;
            var enumType = GetModelTypeEnumType(moduleType);
            if (enumType == null)
            {
                DrawPropertyLine(ref line, modelTypeProp, label, tooltip);
                return;
            }

            var options = BuildEnumOptions(enumType, modelTypeProp.stringValue, excludeUndefined: true, out var values, out var selectedIndex);
            var content = new GUIContent(label, tooltip ?? string.Empty);
            var newIndex = EditorGUI.Popup(line, content, selectedIndex, options);
            if (newIndex != selectedIndex)
            {
                modelTypeProp.stringValue = values[newIndex];
            }

            line.y += LineHeight + LineSpacing;
        }

        private static Type GetModelTypeEnumType(SherpaONNXModuleType moduleType)
        {
            switch (moduleType)
            {
                case SherpaONNXModuleType.SpeechRecognition:
                    return typeof(SpeechRecognitionModelType);
                case SherpaONNXModuleType.SpeechSynthesis:
                    return typeof(SpeechSynthesisModelType);
                case SherpaONNXModuleType.VoiceActivityDetection:
                    return typeof(VoiceActivityDetectionModelType);
                case SherpaONNXModuleType.SpokenLanguageIdentification:
                    return typeof(SpokenLanguageIdentificationModelType);
                case SherpaONNXModuleType.AudioTagging:
                    return typeof(AudioTaggingModelType);
                default:
                    return null;
            }
        }

        private static GUIContent[] BuildEnumOptions(Type enumType, string currentValue, bool excludeUndefined, out string[] values, out int selectedIndex)
        {
            var options = new List<string>();
            var optionValues = new List<string>();
            var autoLabel = SherpaONNXLocalization.Tr(SherpaONNXL10n.Common.OptionAuto, "Auto");

            options.Add(autoLabel);
            optionValues.Add(string.Empty);

            var names = Enum.GetNames(enumType);
            for (int i = 0; i < names.Length; i++)
            {
                var name = names[i];
                if (excludeUndefined && string.Equals(name, "Undefined", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                if (excludeUndefined && string.Equals(name, "None", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                options.Add(name);
                optionValues.Add(name);
            }

            selectedIndex = 0;
            if (!string.IsNullOrWhiteSpace(currentValue))
            {
                var idx = optionValues.FindIndex(v => string.Equals(v, currentValue.Trim(), StringComparison.OrdinalIgnoreCase));
                if (idx >= 0)
                {
                    selectedIndex = idx;
                }
                else
                {
                    options.Add(currentValue);
                    optionValues.Add(currentValue);
                    selectedIndex = optionValues.Count - 1;
                }
            }

            values = optionValues.ToArray();
            var contents = new GUIContent[options.Count];
            for (int i = 0; i < options.Count; i++)
            {
                contents[i] = new GUIContent(options[i]);
            }
            return contents;
        }

        private static float AddLineHeight(SerializedProperty property, string relativeName, bool includeChildren, ref int lineCount)
        {
            var prop = property.FindPropertyRelative(relativeName);
            if (prop == null)
            {
                return 0f;
            }

            lineCount++;
            return GetLineHeight(prop, includeChildren);
        }

        private static float GetLineHeight(SerializedProperty property, bool includeChildren)
        {
            return property != null
                ? EditorGUI.GetPropertyHeight(property, includeChildren)
                : LineHeight;
        }

        private static SherpaONNXCustomCatalogEntryType GetEntryType(SerializedProperty property)
        {
            var entryTypeProp = property.FindPropertyRelative("entryType");
            return entryTypeProp != null
                ? (SherpaONNXCustomCatalogEntryType)entryTypeProp.enumValueIndex
                : SherpaONNXCustomCatalogEntryType.Model;
        }

        private static bool IsSpeechSynthesis(SerializedProperty property)
        {
            var moduleTypeProp = property.FindPropertyRelative("moduleType");
            if (moduleTypeProp != null && moduleTypeProp.enumValueIndex == (int)SherpaONNXModuleType.SpeechSynthesis)
            {
                return true;
            }

            var hintProp = property.FindPropertyRelative("moduleTypeHint");
            if (hintProp == null || string.IsNullOrWhiteSpace(hintProp.stringValue))
            {
                return false;
            }

            return string.Equals(hintProp.stringValue.Trim(), nameof(SherpaONNXModuleType.SpeechSynthesis), StringComparison.OrdinalIgnoreCase);
        }
    }
}

#endif
