// Editor: Packages/com.eitan.sherpa-onnx-unity/Editor/Mono/Inspector/KeywordSpottingComponentEditor.cs

namespace Eitan.Sherpa.Onnx.Unity.Editor.Mono.Inspector
{
    using Eitan.Sherpa.Onnx.Unity.Mono.Components;
    using Eitan.SherpaONNXUnity.Editor.Localization;
    using Eitan.SherpaONNXUnity.Runtime;
    using UnityEditor;
    using UnityEngine;

    [CustomEditor(typeof(KeywordSpottingComponent))]
    public sealed class KeywordSpottingComponentEditor : Editor
    {
        private SerializedProperty modelIdProp;
        private SerializedProperty sampleRateProp;
        private SerializedProperty loadOnAwakeProp;
        private SerializedProperty disposeOnDestroyProp;
        private SerializedProperty logFeedbackProp;

        private SerializedProperty audioInputProp;
        private SerializedProperty autoBindInputProp;
        private SerializedProperty autoStartCaptureProp;
        private SerializedProperty keywordsScoreProp;
        private SerializedProperty keywordsThresholdProp;
        private SerializedProperty customKeywordsProp;
        private SerializedProperty onKeywordDetectedProp;

        private SherpaModelSelectorUI modelSelector;

        private static class Styles
        {
            public static readonly GUIStyle Section =
                new GUIStyle(EditorStyles.helpBox) { padding = new RectOffset(12, 12, 10, 12) };

            public static readonly GUIStyle Header = new GUIStyle(EditorStyles.boldLabel);
        }

        private void OnEnable()
        {
            modelIdProp = serializedObject.FindProperty("modelId");
            sampleRateProp = serializedObject.FindProperty("sampleRate");
            loadOnAwakeProp = serializedObject.FindProperty("loadOnAwake");
            disposeOnDestroyProp = serializedObject.FindProperty("disposeOnDestroy");
            logFeedbackProp = serializedObject.FindProperty("logFeedbackToConsole");

            audioInputProp = serializedObject.FindProperty("audioInput");
            autoBindInputProp = serializedObject.FindProperty("autoBindInput");
            autoStartCaptureProp = serializedObject.FindProperty("startCaptureWhenReady");
            keywordsScoreProp = serializedObject.FindProperty("keywordsScore");
            keywordsThresholdProp = serializedObject.FindProperty("keywordsThreshold");
            customKeywordsProp = serializedObject.FindProperty("customKeywords");
            onKeywordDetectedProp = serializedObject.FindProperty("onKeywordDetected");

            modelSelector = new SherpaModelSelectorUI(SherpaONNXModuleType.KeywordSpotting, Repaint);
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
            DrawInputSection();
            EditorGUILayout.Space();
            DrawKeywordSection();
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

        private void DrawInputSection()
        {
            using (new EditorGUILayout.VerticalScope(Styles.Section))
            {
                EditorGUILayout.LabelField(SherpaInspectorContent.Text(SherpaONNXL10n.Inspectors.Common.SectionAudioInput, "Audio Input"), Styles.Header);
                EditorGUILayout.PropertyField(audioInputProp, SherpaInspectorContent.Label(SherpaONNXL10n.Inspectors.Common.FieldInputSource, "Source"));
                EditorGUILayout.PropertyField(autoBindInputProp, SherpaInspectorContent.Label(SherpaONNXL10n.Inspectors.Common.FieldAutoBind, "Auto Bind Source"));
                EditorGUILayout.PropertyField(autoStartCaptureProp, SherpaInspectorContent.Label(null, "Start Capture When Ready"));

                if (audioInputProp.objectReferenceValue == null)
                {
                    EditorGUILayout.HelpBox(
                        SherpaInspectorContent.Text(SherpaONNXL10n.Inspectors.Common.HelpAssignInput, "Assign a SherpaAudioInputSource (e.g., SherpaMicrophoneInput) to stream audio automatically."),
                        MessageType.Info);
                }
                else if (GUILayout.Button(SherpaInspectorContent.Text(SherpaONNXL10n.Inspectors.Common.ButtonSelectInput, "Select Audio Input")))
                {
                    Selection.activeObject = audioInputProp.objectReferenceValue;
                }
            }
        }

        private void DrawKeywordSection()
        {
            using (new EditorGUILayout.VerticalScope(Styles.Section))
            {
                EditorGUILayout.LabelField(SherpaInspectorContent.Text(SherpaONNXL10n.Inspectors.KeywordSpotting.SectionKeywords, "Keyword Settings"), Styles.Header);
                EditorGUILayout.PropertyField(keywordsScoreProp, SherpaInspectorContent.Label(SherpaONNXL10n.Inspectors.KeywordSpotting.FieldScore, "Keywords Score"));
                EditorGUILayout.Slider(keywordsThresholdProp, 0f, 1f, SherpaInspectorContent.Label(SherpaONNXL10n.Inspectors.KeywordSpotting.FieldThreshold, "Trigger Threshold"));
                EditorGUILayout.PropertyField(customKeywordsProp, SherpaInspectorContent.Label(SherpaONNXL10n.Inspectors.KeywordSpotting.FieldCustomKeywords, "Custom Keywords"), true);
            }
        }

        private void DrawEventsSection()
        {
            using (new EditorGUILayout.VerticalScope(Styles.Section))
            {
                EditorGUILayout.LabelField(SherpaInspectorContent.Text(SherpaONNXL10n.Inspectors.Common.SectionEvents, "Events"), Styles.Header);
                EditorGUILayout.PropertyField(onKeywordDetectedProp, SherpaInspectorContent.Label(SherpaONNXL10n.Inspectors.KeywordSpotting.EventDetected, "On Keyword Detected"));
            }
        }
    }
}
