// Editor: Packages/com.eitan.sherpa-onnx-unity/Editor/Mono/Inspector/OfflineSpeechRecognizerComponentEditor.cs

namespace Eitan.Sherpa.Onnx.Unity.Editor.Mono.Inspector
{
    using Eitan.Sherpa.Onnx.Unity.Mono.Components;
    using Eitan.SherpaONNXUnity.Editor.Localization;
    using Eitan.SherpaONNXUnity.Runtime;
    using UnityEditor;
    using UnityEngine;

    [CustomEditor(typeof(OfflineSpeechRecognizerComponent))]
    public sealed class OfflineSpeechRecognizerComponentEditor : Editor
    {
        private SerializedProperty modelIdProp;
        private SerializedProperty sampleRateProp;
        private SerializedProperty loadOnAwakeProp;
        private SerializedProperty disposeOnDestroyProp;
        private SerializedProperty logFeedbackProp;

        private SerializedProperty vadSourceProp;
        private SerializedProperty autoBindVadProp;
        private SerializedProperty onTranscriptProp;
        private SerializedProperty onFailedProp;

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

            vadSourceProp = serializedObject.FindProperty("voiceActivitySource");
            autoBindVadProp = serializedObject.FindProperty("autoBindVoiceActivitySource");
            onTranscriptProp = serializedObject.FindProperty("onTranscriptReady");
            onFailedProp = serializedObject.FindProperty("onTranscriptionFailed");

            modelSelector = new SherpaModelSelectorUI(SherpaONNXModuleType.SpeechRecognition, Repaint);
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
            DrawPipelineSection();
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
                EditorGUILayout.HelpBox(
                    SherpaInspectorContent.Text(SherpaONNXL10n.Inspectors.Common.WarningSampleRateRange, "Speech models usually expect 8k–48k sample rates. Verify the selected model supports the configured value."),
                    MessageType.Info);
                EditorGUILayout.PropertyField(loadOnAwakeProp, SherpaInspectorContent.Label(SherpaONNXL10n.Inspectors.Common.FieldLoadOnAwake, "Load On Awake"));
                EditorGUILayout.PropertyField(disposeOnDestroyProp, SherpaInspectorContent.Label(SherpaONNXL10n.Inspectors.Common.FieldDisposeOnDestroy, "Dispose On Destroy"));
                EditorGUILayout.PropertyField(logFeedbackProp, SherpaInspectorContent.Label(SherpaONNXL10n.Inspectors.Common.FieldLogFeedback, "Log Feedback"));
            }
        }

        private void DrawPipelineSection()
        {
            using (new EditorGUILayout.VerticalScope(Styles.Section))
            {
                EditorGUILayout.LabelField(SherpaInspectorContent.Text(SherpaONNXL10n.Inspectors.OfflineAsr.SectionVad, "Voice Activity Detector"), Styles.Header);
                EditorGUILayout.PropertyField(vadSourceProp, SherpaInspectorContent.Label(SherpaONNXL10n.Inspectors.OfflineAsr.FieldVadSource, "Source Component"));
                EditorGUILayout.PropertyField(autoBindVadProp, SherpaInspectorContent.Label(SherpaONNXL10n.Inspectors.Common.FieldAutoBind, "Auto Bind Source"));

                if (vadSourceProp.objectReferenceValue == null)
                {
                    EditorGUILayout.HelpBox(
                        SherpaInspectorContent.Text(SherpaONNXL10n.Inspectors.OfflineAsr.HelpAssignVad, "Assign a VoiceActivityDetectionComponent to feed segments into the offline recognizer."),
                        MessageType.Warning);
                }
                else if (GUILayout.Button(SherpaInspectorContent.Text(SherpaONNXL10n.Inspectors.OfflineAsr.ButtonSelectVad, "Select VAD Component")))
                {
                    Selection.activeObject = vadSourceProp.objectReferenceValue;
                }
            }
        }

        private void DrawEventsSection()
        {
            using (new EditorGUILayout.VerticalScope(Styles.Section))
            {
                EditorGUILayout.LabelField(SherpaInspectorContent.Text(SherpaONNXL10n.Inspectors.Common.SectionEvents, "Events"), Styles.Header);
                EditorGUILayout.PropertyField(onTranscriptProp, SherpaInspectorContent.Label(SherpaONNXL10n.Inspectors.OfflineAsr.EventTranscriptReady, "On Transcript Ready"));
                EditorGUILayout.PropertyField(onFailedProp, SherpaInspectorContent.Label(SherpaONNXL10n.Inspectors.OfflineAsr.EventFailed, "On Transcription Failed"));
            }
        }
    }
}
