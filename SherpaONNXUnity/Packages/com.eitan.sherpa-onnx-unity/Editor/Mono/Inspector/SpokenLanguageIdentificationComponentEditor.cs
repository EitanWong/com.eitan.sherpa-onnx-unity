// Editor: Packages/com.eitan.sherpa-onnx-unity/Editor/Mono/Inspector/SpokenLanguageIdentificationComponentEditor.cs

namespace Eitan.Sherpa.Onnx.Unity.Editor.Mono.Inspector
{
    using Eitan.Sherpa.Onnx.Unity.Mono.Components;
    using Eitan.SherpaONNXUnity.Editor.Localization;
    using Eitan.SherpaONNXUnity.Runtime;
    using UnityEditor;
    using UnityEngine;

    [CustomEditor(typeof(SpokenLanguageIdentificationComponent))]
    public sealed class SpokenLanguageIdentificationComponentEditor : Editor
    {
        private SerializedProperty modelIdProp;
        private SerializedProperty sampleRateProp;
        private SerializedProperty loadOnAwakeProp;
        private SerializedProperty disposeOnDestroyProp;
        private SerializedProperty logFeedbackProp;
        private SerializedProperty clipProp;
        private SerializedProperty identifyOnStartProp;
        private SerializedProperty onIdentifiedProp;
        private SerializedProperty onFailedProp;

        private SpokenLanguageIdentificationComponent runtimeComponent;
        private SherpaModelSelectorUI modelSelector;

        private static class Styles
        {
            public static readonly GUIStyle Section =
                new GUIStyle(EditorStyles.helpBox) { padding = new RectOffset(12, 12, 10, 12) };

            public static readonly GUIStyle Header = new GUIStyle(EditorStyles.boldLabel);
        }

        private void OnEnable()
        {
            runtimeComponent = (SpokenLanguageIdentificationComponent)target;

            modelIdProp = serializedObject.FindProperty("modelId");
            sampleRateProp = serializedObject.FindProperty("sampleRate");
            loadOnAwakeProp = serializedObject.FindProperty("loadOnAwake");
            disposeOnDestroyProp = serializedObject.FindProperty("disposeOnDestroy");
            logFeedbackProp = serializedObject.FindProperty("logFeedbackToConsole");
            clipProp = serializedObject.FindProperty("clip");
            identifyOnStartProp = serializedObject.FindProperty("identifyOnStart");
            onIdentifiedProp = serializedObject.FindProperty("onLanguageIdentified");
            onFailedProp = serializedObject.FindProperty("onIdentificationFailed");

            modelSelector = new SherpaModelSelectorUI(SherpaONNXModuleType.SpokenLanguageIdentification, Repaint);
            modelSelector.Refresh();
        }

        private void OnDisable()
        {
            modelSelector?.Dispose();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawModelSection();
            EditorGUILayout.Space();
            DrawClipSection();
            EditorGUILayout.Space();
            DrawEventsSection();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawModelSection()
        {
            using (new EditorGUILayout.VerticalScope(Styles.Section))
            {
                EditorGUILayout.LabelField(SherpaInspectorContent.Text(SherpaONNXL10n.Inspectors.Common.SectionModelSettings, "Model Settings"), Styles.Header);
                modelSelector?.DrawModelField(modelIdProp, SherpaInspectorContent.Label(SherpaONNXL10n.Inspectors.Common.FieldModelId, "Model ID"));
                EditorGUILayout.PropertyField(sampleRateProp, SherpaInspectorContent.Label(SherpaONNXL10n.Inspectors.Common.FieldSampleRate, "Sample Rate (Hz)"));
                EditorGUILayout.PropertyField(loadOnAwakeProp, SherpaInspectorContent.Label(SherpaONNXL10n.Inspectors.Common.FieldLoadOnAwake, "Load On Awake"));
                EditorGUILayout.PropertyField(disposeOnDestroyProp, SherpaInspectorContent.Label(SherpaONNXL10n.Inspectors.Common.FieldDisposeOnDestroy, "Dispose On Destroy"));
                EditorGUILayout.PropertyField(logFeedbackProp, SherpaInspectorContent.Label(SherpaONNXL10n.Inspectors.Common.FieldLogFeedback, "Log Feedback"));
            }
        }

        private void DrawClipSection()
        {
            using (new EditorGUILayout.VerticalScope(Styles.Section))
            {
                EditorGUILayout.LabelField(SherpaInspectorContent.Text(SherpaONNXL10n.Inspectors.SpokenLanguageIdentification.SectionClip, "Clip Input"), Styles.Header);
                EditorGUILayout.PropertyField(clipProp, SherpaInspectorContent.Label(SherpaONNXL10n.Inspectors.SpokenLanguageIdentification.FieldClip, "Audio Clip"));
                EditorGUILayout.PropertyField(identifyOnStartProp, SherpaInspectorContent.Label(SherpaONNXL10n.Inspectors.SpokenLanguageIdentification.FieldIdentifyOnStart, "Identify On Start"));

                using (new EditorGUI.DisabledScope(!Application.isPlaying || clipProp.objectReferenceValue == null))
                {
                    if (GUILayout.Button(SherpaInspectorContent.Text(SherpaONNXL10n.Inspectors.SpokenLanguageIdentification.ButtonIdentify, "Identify Clip")))
                    {
                        runtimeComponent.IdentifyAssignedClip();
                    }
                }

                if (!Application.isPlaying)
                {
                    EditorGUILayout.HelpBox(
                        SherpaInspectorContent.Text(SherpaONNXL10n.Inspectors.Common.HelpPlaymodeRequired, "Enter Play Mode to run this preview directly from the inspector."),
                        MessageType.Info);
                }
            }
        }

        private void DrawEventsSection()
        {
            using (new EditorGUILayout.VerticalScope(Styles.Section))
            {
                EditorGUILayout.LabelField(SherpaInspectorContent.Text(SherpaONNXL10n.Inspectors.Common.SectionEvents, "Events"), Styles.Header);
                EditorGUILayout.PropertyField(onIdentifiedProp, SherpaInspectorContent.Label(SherpaONNXL10n.Inspectors.SpokenLanguageIdentification.EventIdentified, "On Language Identified"));
                EditorGUILayout.PropertyField(onFailedProp, SherpaInspectorContent.Label(SherpaONNXL10n.Inspectors.SpokenLanguageIdentification.EventFailed, "On Identification Failed"));
            }
        }
    }
}
