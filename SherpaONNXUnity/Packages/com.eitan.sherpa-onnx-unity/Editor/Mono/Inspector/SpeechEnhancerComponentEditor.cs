// Editor: Packages/com.eitan.sherpa-onnx-unity/Editor/Mono/Inspector/SpeechEnhancerComponentEditor.cs

namespace Eitan.Sherpa.Onnx.Unity.Editor.Mono.Inspector
{
    using Eitan.Sherpa.Onnx.Unity.Mono.Components;
    using Eitan.SherpaONNXUnity.Editor.Localization;
    using Eitan.SherpaONNXUnity.Runtime;
    using UnityEditor;
    using UnityEngine;

    [CustomEditor(typeof(SpeechEnhancementComponent), true)]
    public sealed class SpeechEnhancementComponentEditor : Editor
    {
        private SerializedProperty modelIdProp;
        private SerializedProperty sampleRateProp;
        private SerializedProperty loadOnAwakeProp;
        private SerializedProperty disposeOnDestroyProp;
        private SerializedProperty logFeedbackProp;

        private SerializedProperty playbackAudioSourceProp;
        private SerializedProperty autoplayProp;

        private SerializedProperty clipReferenceProp;
        private SerializedProperty enhanceOnEnableProp;
        private SerializedProperty duplicateClipProp;

        private SerializedProperty onClipReadyProp;
        private SerializedProperty onErrorProp;
        private SerializedProperty onInitializedProp;
        private SerializedProperty onFeedbackProp;

        private SherpaModelSelectorUI modelSelector;
        private SpeechEnhancementComponent runtimeComponent;

        private static class Styles
        {
            public static readonly GUIStyle Section =
                new GUIStyle(EditorStyles.helpBox) { padding = new RectOffset(12, 12, 10, 12) };

            public static readonly GUIStyle Header = new GUIStyle(EditorStyles.boldLabel);
        }

        private void OnEnable()
        {
            runtimeComponent = (SpeechEnhancementComponent)target;

            modelIdProp = serializedObject.FindProperty("modelId");
            sampleRateProp = serializedObject.FindProperty("sampleRate");
            loadOnAwakeProp = serializedObject.FindProperty("loadOnAwake");
            disposeOnDestroyProp = serializedObject.FindProperty("disposeOnDestroy");
            logFeedbackProp = serializedObject.FindProperty("logFeedbackToConsole");

            playbackAudioSourceProp = serializedObject.FindProperty("playbackAudioSource");
            autoplayProp = serializedObject.FindProperty("autoplay");

            clipReferenceProp = serializedObject.FindProperty("clipReference");
            enhanceOnEnableProp = serializedObject.FindProperty("enhanceOnEnable");
            duplicateClipProp = serializedObject.FindProperty("duplicateClip");

            onClipReadyProp = serializedObject.FindProperty("onClipReady");
            onErrorProp = serializedObject.FindProperty("onError");
            onInitializedProp = serializedObject.FindProperty("onInitializationStateChanged");
            onFeedbackProp = serializedObject.FindProperty("onFeedbackMessage");

            modelSelector = new SherpaModelSelectorUI(SherpaONNXModuleType.SpeechEnhancement, Repaint);
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
            DrawPlaybackSection();
            EditorGUILayout.Space();
            DrawClipEnhancementSection();
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

        private void DrawPlaybackSection()
        {
            using (new EditorGUILayout.VerticalScope(Styles.Section))
            {
                EditorGUILayout.LabelField("Playback", Styles.Header);
                EditorGUILayout.PropertyField(playbackAudioSourceProp, new GUIContent("Playback Audio Source"));
                EditorGUILayout.PropertyField(autoplayProp, new GUIContent("Autoplay"));
            }
        }

        private void DrawClipEnhancementSection()
        {
            using (new EditorGUILayout.VerticalScope(Styles.Section))
            {
                EditorGUILayout.LabelField(SherpaInspectorContent.Text(SherpaONNXL10n.Inspectors.SpeechEnhancer.SectionEnhancement, "Clip Enhancement"), Styles.Header);
                EditorGUILayout.PropertyField(clipReferenceProp, SherpaInspectorContent.Label(SherpaONNXL10n.Inspectors.SpeechEnhancer.FieldClipReference, "Clip Reference"));
                EditorGUILayout.PropertyField(enhanceOnEnableProp, SherpaInspectorContent.Label(SherpaONNXL10n.Inspectors.SpeechEnhancer.FieldEnhanceOnEnable, "Enhance On Enable"));
                EditorGUILayout.PropertyField(duplicateClipProp, SherpaInspectorContent.Label(SherpaONNXL10n.Inspectors.SpeechEnhancer.FieldDuplicateClip, "Duplicate Clip"));

                EditorGUILayout.Space();
                using (new EditorGUI.DisabledScope(!Application.isPlaying))
                {
                    if (GUILayout.Button(SherpaInspectorContent.Text(SherpaONNXL10n.Inspectors.SpeechEnhancer.ButtonEnhanceNow, "Enhance Now")))
                    {
                        runtimeComponent.EnhanceAssignedClip();
                    }
                }

                if (clipReferenceProp.objectReferenceValue == null && playbackAudioSourceProp.objectReferenceValue == null)
                {
                    EditorGUILayout.HelpBox(
                        SherpaInspectorContent.Text(SherpaONNXL10n.Inspectors.SpeechEnhancer.HelpAssignClip, "Assign a clip reference or a Playback Audio Source with an AudioClip."),
                        MessageType.Info);
                }

                EditorGUILayout.HelpBox(
                    "Microphone capture is handled by SherpaMicrophoneInput. Use it to supply samples or AudioClips to this component.",
                    MessageType.None);
            }
        }

        private void DrawEventsSection()
        {
            using (new EditorGUILayout.VerticalScope(Styles.Section))
            {
                EditorGUILayout.LabelField(SherpaInspectorContent.Text(SherpaONNXL10n.Inspectors.Common.SectionEvents, "Events"), Styles.Header);
                EditorGUILayout.PropertyField(onClipReadyProp, SherpaInspectorContent.Label(SherpaONNXL10n.Inspectors.SpeechEnhancer.EventClipEnhanced, "On Clip Ready"));
                EditorGUILayout.PropertyField(onErrorProp, SherpaInspectorContent.Label(SherpaONNXL10n.Inspectors.SpeechEnhancer.EventEnhancementFailed, "On Error"));

                EditorGUILayout.Space();
                EditorGUILayout.LabelField(SherpaInspectorContent.Text(SherpaONNXL10n.Inspectors.Common.SectionLifecycleEvents, "Lifecycle Events"), Styles.Header);
                EditorGUILayout.PropertyField(onInitializedProp, SherpaInspectorContent.Label(SherpaONNXL10n.Inspectors.Common.EventInitialized, "On Initialization State Changed"));
                EditorGUILayout.PropertyField(onFeedbackProp, SherpaInspectorContent.Label(SherpaONNXL10n.Inspectors.Common.EventFeedback, "On Feedback Message"));
            }
        }
    }
}
