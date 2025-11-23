// Editor: Packages/com.eitan.sherpa-onnx-unity/Editor/Mono/Inspector/AudioTaggingComponentEditor.cs

namespace Eitan.Sherpa.Onnx.Unity.Editor.Mono.Inspector
{
    using Eitan.Sherpa.Onnx.Unity.Mono.Components;
    using Eitan.SherpaONNXUnity.Editor.Localization;
    using Eitan.SherpaONNXUnity.Runtime;
    using UnityEditor;
    using UnityEngine;

    [CustomEditor(typeof(AudioTaggingComponent))]
    public sealed class AudioTaggingComponentEditor : Editor
    {
        private SerializedProperty modelIdProp;
        private SerializedProperty sampleRateProp;
        private SerializedProperty loadOnAwakeProp;
        private SerializedProperty disposeOnDestroyProp;
        private SerializedProperty logFeedbackProp;

        private SerializedProperty audioInputProp;
        private SerializedProperty autoBindInputProp;
        private SerializedProperty autoStartCaptureProp;

        private SerializedProperty clipProp;
        private SerializedProperty tagClipOnStartProp;
        private SerializedProperty topKProp;
        private SerializedProperty warnMismatchProp;

        private SerializedProperty onTagsReadyProp;
        private SerializedProperty onFailedProp;
        private SerializedProperty onFeedbackProp;
        private SerializedProperty onInitializedProp;

        private SherpaModelSelectorUI modelSelector;
        private AudioTaggingComponent runtimeComponent;

        private static class Styles
        {
            public static readonly GUIStyle Section =
                new GUIStyle(EditorStyles.helpBox) { padding = new RectOffset(12, 12, 10, 12) };

            public static readonly GUIStyle Header = new GUIStyle(EditorStyles.boldLabel);
        }

        private void OnEnable()
        {
            runtimeComponent = (AudioTaggingComponent)target;

            modelIdProp = serializedObject.FindProperty("modelId");
            sampleRateProp = serializedObject.FindProperty("sampleRate");
            loadOnAwakeProp = serializedObject.FindProperty("loadOnAwake");
            disposeOnDestroyProp = serializedObject.FindProperty("disposeOnDestroy");
            logFeedbackProp = serializedObject.FindProperty("logFeedbackToConsole");

            audioInputProp = serializedObject.FindProperty("audioInput");
            autoBindInputProp = serializedObject.FindProperty("autoBindInput");
            autoStartCaptureProp = serializedObject.FindProperty("startCaptureWhenReady");

            clipProp = serializedObject.FindProperty("clipToTag");
            tagClipOnStartProp = serializedObject.FindProperty("tagClipOnStart");
            topKProp = serializedObject.FindProperty("topK");
            warnMismatchProp = serializedObject.FindProperty("warnOnSampleRateMismatch");

            onTagsReadyProp = serializedObject.FindProperty("onTagsReady");
            onFailedProp = serializedObject.FindProperty("onTaggingFailed");
            onFeedbackProp = serializedObject.FindProperty("onFeedbackMessage");
            onInitializedProp = serializedObject.FindProperty("onInitializationStateChanged");

            modelSelector = new SherpaModelSelectorUI(SherpaONNXModuleType.AudioTagging, Repaint);
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
            DrawTaggingSection();
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
            }
        }

        private void DrawTaggingSection()
        {
            using (new EditorGUILayout.VerticalScope(Styles.Section))
            {
                EditorGUILayout.LabelField(SherpaInspectorContent.Text(SherpaONNXL10n.Inspectors.AudioTagging.SectionTagging, "Tagging"), Styles.Header);
                EditorGUILayout.PropertyField(topKProp, SherpaInspectorContent.Label(SherpaONNXL10n.Inspectors.AudioTagging.FieldTopK, "Top K"));
                EditorGUILayout.PropertyField(warnMismatchProp, SherpaInspectorContent.Label(SherpaONNXL10n.Inspectors.AudioTagging.FieldWarnMismatch, "Warn On Sample Rate Mismatch"));

                EditorGUILayout.PropertyField(clipProp, SherpaInspectorContent.Label(SherpaONNXL10n.Inspectors.AudioTagging.FieldClip, "Offline Clip"));
                EditorGUILayout.PropertyField(tagClipOnStartProp, SherpaInspectorContent.Label(SherpaONNXL10n.Inspectors.AudioTagging.FieldTagClipOnStart, "Tag Clip On Start"));

                using (new EditorGUI.DisabledScope(!Application.isPlaying || clipProp.objectReferenceValue == null))
                {
                    if (GUILayout.Button(SherpaInspectorContent.Text(SherpaONNXL10n.Inspectors.AudioTagging.ButtonTagClip, "Tag Assigned Clip")))
                    {
                        runtimeComponent.TagAssignedClipAsync();
                    }
                }
            }
        }

        private void DrawEventsSection()
        {
            using (new EditorGUILayout.VerticalScope(Styles.Section))
            {
                EditorGUILayout.LabelField(SherpaInspectorContent.Text(SherpaONNXL10n.Inspectors.Common.SectionEvents, "Events"), Styles.Header);
                EditorGUILayout.PropertyField(onTagsReadyProp, SherpaInspectorContent.Label(SherpaONNXL10n.Inspectors.AudioTagging.EventTagsReady, "On Tags Ready"));
                EditorGUILayout.PropertyField(onFailedProp, SherpaInspectorContent.Label(SherpaONNXL10n.Inspectors.AudioTagging.EventTaggingFailed, "On Tagging Failed"));

                EditorGUILayout.Space();
                EditorGUILayout.LabelField(SherpaInspectorContent.Text(SherpaONNXL10n.Inspectors.Common.SectionLifecycleEvents, "Lifecycle Events"), Styles.Header);
                EditorGUILayout.PropertyField(onInitializedProp, SherpaInspectorContent.Label(SherpaONNXL10n.Inspectors.Common.EventInitialized, "On Initialization State Changed"));
                EditorGUILayout.PropertyField(onFeedbackProp, SherpaInspectorContent.Label(SherpaONNXL10n.Inspectors.Common.EventFeedback, "On Feedback Message"));
            }
        }
    }
}
