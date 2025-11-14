// Editor: Packages/com.eitan.sherpa-onnx-unity/Editor/Mono/Inspector/SpeechEnhancerComponentEditor.cs

namespace Eitan.Sherpa.Onnx.Unity.Editor.Mono.Inspector
{
    using Eitan.Sherpa.Onnx.Unity.Mono.Components;
    using Eitan.SherpaOnnxUnity.Editor.Localization;
    using Eitan.SherpaOnnxUnity.Runtime;
    using UnityEditor;
    using UnityEngine;

    [CustomEditor(typeof(SpeechEnhancerComponent))]
    public sealed class SpeechEnhancerComponentEditor : Editor
    {
        private SerializedProperty modelIdProp;
        private SerializedProperty sampleRateProp;
        private SerializedProperty loadOnAwakeProp;
        private SerializedProperty disposeOnDestroyProp;
        private SerializedProperty logFeedbackProp;

        private SerializedProperty audioSourceProp;
        private SerializedProperty clipReferenceProp;
        private SerializedProperty enhanceOnEnableProp;
        private SerializedProperty duplicateClipProp;
        private SerializedProperty onEnhancedProp;
        private SerializedProperty onFailedProp;
        private SerializedProperty onFeedbackProp;
        private SerializedProperty onInitializedProp;

        private SherpaModelSelectorUI modelSelector;
        private SpeechEnhancerComponent runtimeComponent;

        private static class Styles
        {
            public static readonly GUIStyle Section =
                new GUIStyle(EditorStyles.helpBox) { padding = new RectOffset(12, 12, 10, 12) };

            public static readonly GUIStyle Header = new GUIStyle(EditorStyles.boldLabel);
        }

        private void OnEnable()
        {
            runtimeComponent = (SpeechEnhancerComponent)target;

            modelIdProp = serializedObject.FindProperty("modelId");
            sampleRateProp = serializedObject.FindProperty("sampleRate");
            loadOnAwakeProp = serializedObject.FindProperty("loadOnAwake");
            disposeOnDestroyProp = serializedObject.FindProperty("disposeOnDestroy");
            logFeedbackProp = serializedObject.FindProperty("logFeedbackToConsole");

            audioSourceProp = serializedObject.FindProperty("targetAudioSource");
            clipReferenceProp = serializedObject.FindProperty("clipReference");
            enhanceOnEnableProp = serializedObject.FindProperty("enhanceOnEnable");
            duplicateClipProp = serializedObject.FindProperty("duplicateClip");

            onEnhancedProp = serializedObject.FindProperty("onClipEnhanced");
            onFailedProp = serializedObject.FindProperty("onEnhancementFailed");
            onFeedbackProp = serializedObject.FindProperty("onFeedbackMessage");
            onInitializedProp = serializedObject.FindProperty("onInitializationStateChanged");

            modelSelector = new SherpaModelSelectorUI(SherpaOnnxModuleType.SpeechEnhancement, Repaint);
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
            DrawTargetSection();
            EditorGUILayout.Space();
            DrawEventsSection();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawModelSection()
        {
            using (new EditorGUILayout.VerticalScope(Styles.Section))
            {
                EditorGUILayout.LabelField(SherpaInspectorContent.Text(SherpaOnnxL10n.Inspectors.Common.SectionModelSettings, "Model Settings"), Styles.Header);
                modelSelector?.DrawModelField(modelIdProp, SherpaInspectorContent.Label(SherpaOnnxL10n.Inspectors.Common.FieldModelId, "Model ID"));
                EditorGUILayout.PropertyField(sampleRateProp, SherpaInspectorContent.Label(SherpaOnnxL10n.Inspectors.Common.FieldSampleRate, "Sample Rate (Hz)"));
                EditorGUILayout.PropertyField(loadOnAwakeProp, SherpaInspectorContent.Label(SherpaOnnxL10n.Inspectors.Common.FieldLoadOnAwake, "Load On Awake"));
                EditorGUILayout.PropertyField(disposeOnDestroyProp, SherpaInspectorContent.Label(SherpaOnnxL10n.Inspectors.Common.FieldDisposeOnDestroy, "Dispose On Destroy"));
                EditorGUILayout.PropertyField(logFeedbackProp, SherpaInspectorContent.Label(SherpaOnnxL10n.Inspectors.Common.FieldLogFeedback, "Log Feedback"));
            }
        }

        private void DrawTargetSection()
        {
            using (new EditorGUILayout.VerticalScope(Styles.Section))
            {
                EditorGUILayout.LabelField(SherpaInspectorContent.Text(SherpaOnnxL10n.Inspectors.SpeechEnhancer.SectionEnhancement, "Enhancement"), Styles.Header);
                EditorGUILayout.PropertyField(audioSourceProp, SherpaInspectorContent.Label(SherpaOnnxL10n.Inspectors.SpeechEnhancer.FieldTargetAudioSource, "Target Audio Source"));
                EditorGUILayout.PropertyField(clipReferenceProp, SherpaInspectorContent.Label(SherpaOnnxL10n.Inspectors.SpeechEnhancer.FieldClipReference, "Clip Reference"));
                EditorGUILayout.PropertyField(enhanceOnEnableProp, SherpaInspectorContent.Label(SherpaOnnxL10n.Inspectors.SpeechEnhancer.FieldEnhanceOnEnable, "Enhance On Enable"));
                EditorGUILayout.PropertyField(duplicateClipProp, SherpaInspectorContent.Label(SherpaOnnxL10n.Inspectors.SpeechEnhancer.FieldDuplicateClip, "Duplicate Clip"));

                var clip = clipReferenceProp.objectReferenceValue as AudioClip;
                if (clip == null && audioSourceProp.objectReferenceValue == null)
                {
                    EditorGUILayout.HelpBox(
                        SherpaInspectorContent.Text(SherpaOnnxL10n.Inspectors.SpeechEnhancer.HelpAssignClip, "Assign a clip or an AudioSource to process."),
                        MessageType.Info);
                }

                using (new EditorGUI.DisabledScope(!Application.isPlaying))
                {
                    if (GUILayout.Button(SherpaInspectorContent.Text(SherpaOnnxL10n.Inspectors.SpeechEnhancer.ButtonEnhanceNow, "Enhance Now")))
                    {
                        runtimeComponent.EnhanceAssignedClip();
                    }
                }
            }
        }

        private void DrawEventsSection()
        {
            using (new EditorGUILayout.VerticalScope(Styles.Section))
            {
                EditorGUILayout.LabelField(SherpaInspectorContent.Text(SherpaOnnxL10n.Inspectors.Common.SectionEvents, "Events"), Styles.Header);
                EditorGUILayout.PropertyField(onEnhancedProp, SherpaInspectorContent.Label(SherpaOnnxL10n.Inspectors.SpeechEnhancer.EventClipEnhanced, "On Clip Enhanced"));
                EditorGUILayout.PropertyField(onFailedProp, SherpaInspectorContent.Label(SherpaOnnxL10n.Inspectors.SpeechEnhancer.EventEnhancementFailed, "On Enhancement Failed"));

                EditorGUILayout.Space();
                EditorGUILayout.LabelField(SherpaInspectorContent.Text(SherpaOnnxL10n.Inspectors.Common.SectionLifecycleEvents, "Lifecycle Events"), Styles.Header);
                EditorGUILayout.PropertyField(onInitializedProp, SherpaInspectorContent.Label(SherpaOnnxL10n.Inspectors.Common.EventInitialized, "On Initialization State Changed"));
                EditorGUILayout.PropertyField(onFeedbackProp, SherpaInspectorContent.Label(SherpaOnnxL10n.Inspectors.Common.EventFeedback, "On Feedback Message"));
            }
        }
    }
}
