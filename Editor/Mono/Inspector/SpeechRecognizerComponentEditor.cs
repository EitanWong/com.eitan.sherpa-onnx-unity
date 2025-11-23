// Editor: Packages/com.eitan.sherpa-onnx-unity/Editor/Mono/Inspector/SpeechRecognizerComponentEditor.cs

namespace Eitan.Sherpa.Onnx.Unity.Editor.Mono.Inspector
{
    using System;
    using Eitan.Sherpa.Onnx.Unity.Mono.Components;
    using Eitan.Sherpa.Onnx.Unity.Mono.Inputs;
    using Eitan.SherpaONNXUnity.Editor.Localization;
    using Eitan.SherpaONNXUnity.Runtime;
    using UnityEditor;
    using UnityEngine;

    [CustomEditor(typeof(SpeechRecognizerComponent))]
    public sealed class SpeechRecognizerComponentEditor : Editor
    {
        private SerializedProperty modelIdProp;
        private SerializedProperty sampleRateProp;
        private SerializedProperty loadOnAwakeProp;
        private SerializedProperty disposeOnDestroyProp;
        private SerializedProperty audioInputProp;
        private SerializedProperty autoBindInputProp;
        private SerializedProperty autoStartCaptureProp;
        private SerializedProperty deduplicateProp;
        private SerializedProperty onTranscriptionReadyProp;
        private SerializedProperty onFeedbackProp;
        private SerializedProperty onInitializedProp;
        private SerializedProperty logFeedbackProp;

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
            deduplicateProp = serializedObject.FindProperty("deduplicateStreamingResults");

            onTranscriptionReadyProp = serializedObject.FindProperty("onTranscriptionReady");
            onFeedbackProp = serializedObject.FindProperty("onFeedbackMessage");
            onInitializedProp = serializedObject.FindProperty("onInitializationStateChanged");

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
            DrawInputSection();
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

                if (sampleRateProp.intValue < 8000 || sampleRateProp.intValue > 48000)
                {
                    EditorGUILayout.HelpBox(
                        SherpaInspectorContent.Text(SherpaONNXL10n.Inspectors.Common.WarningSampleRateRange, "Speech models usually expect 8k–48k sample rates. Verify the selected model supports the configured value."),
                        MessageType.Warning);
                }

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
                EditorGUILayout.PropertyField(deduplicateProp, SherpaInspectorContent.Label(SherpaONNXL10n.Inspectors.Common.FieldDeduplicate, "Deduplicate Results"));

                var audioInput = audioInputProp.objectReferenceValue as SherpaAudioInputSource;
                if (audioInput == null)
                {
                    EditorGUILayout.HelpBox(
                        SherpaInspectorContent.Text(SherpaONNXL10n.Inspectors.Common.HelpAssignInput, "Assign a SherpaAudioInputSource (e.g., SherpaMicrophoneInput) to stream audio automatically."),
                        MessageType.Info);
                }
                else
                {
                    if (GUILayout.Button(SherpaInspectorContent.Text(SherpaONNXL10n.Inspectors.Common.ButtonSelectInput, "Select Audio Input")))
                    {
                        Selection.activeObject = audioInput;
                    }

                    if (!Application.isPlaying)
                    {
                        EditorGUILayout.HelpBox(
                            SherpaInspectorContent.Text(SherpaONNXL10n.Inspectors.Common.HelpInputLivesOnSource, "Capture settings live on the input component. Configure it there for better reuse across modules."),
                            MessageType.None);
                    }
                }
            }
        }

        private void DrawEventsSection()
        {
            using (new EditorGUILayout.VerticalScope(Styles.Section))
            {
                EditorGUILayout.LabelField(SherpaInspectorContent.Text(SherpaONNXL10n.Inspectors.Common.SectionEvents, "Events"), Styles.Header);
                EditorGUILayout.PropertyField(onTranscriptionReadyProp, SherpaInspectorContent.Label(SherpaONNXL10n.Inspectors.SpeechRecognizer.EventTranscriptionReady, "On Transcription Ready"));

                EditorGUILayout.Space();
                EditorGUILayout.LabelField(SherpaInspectorContent.Text(SherpaONNXL10n.Inspectors.Common.SectionLifecycleEvents, "Lifecycle Events"), Styles.Header);
                EditorGUILayout.PropertyField(onInitializedProp, SherpaInspectorContent.Label(SherpaONNXL10n.Inspectors.Common.EventInitialized, "On Initialization State Changed"));
                EditorGUILayout.PropertyField(onFeedbackProp, SherpaInspectorContent.Label(SherpaONNXL10n.Inspectors.Common.EventFeedback, "On Feedback Message"));
            }
        }

    }
}
