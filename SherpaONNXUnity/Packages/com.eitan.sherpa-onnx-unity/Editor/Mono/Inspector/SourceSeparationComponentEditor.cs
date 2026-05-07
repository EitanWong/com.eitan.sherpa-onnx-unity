// Editor: Packages/com.eitan.sherpa-onnx-unity/Editor/Mono/Inspector/SourceSeparationComponentEditor.cs

namespace Eitan.Sherpa.Onnx.Unity.Editor.Mono.Inspector
{
    using Eitan.Sherpa.Onnx.Unity.Mono.Components;
    using Eitan.SherpaONNXUnity.Editor.Localization;
    using Eitan.SherpaONNXUnity.Runtime;
    using UnityEditor;
    using UnityEngine;

    [CustomEditor(typeof(SourceSeparationComponent))]
    public sealed class SourceSeparationComponentEditor : Editor
    {
        private SerializedProperty modelIdProp;
        private SerializedProperty sampleRateProp;
        private SerializedProperty loadOnAwakeProp;
        private SerializedProperty disposeOnDestroyProp;
        private SerializedProperty logFeedbackProp;

        private SerializedProperty playbackAudioSourcesProp;
        private SerializedProperty autoplayProp;

        private SerializedProperty clipReferenceProp;
        private SerializedProperty separateOnEnableProp;
        private SerializedProperty enableOutputProcessingProp;
        private SerializedProperty outputFadeInMillisecondsProp;
        private SerializedProperty outputFadeOutMillisecondsProp;
        private SerializedProperty removeOutputDcOffsetProp;
        private SerializedProperty clampOutputToUnitRangeProp;
        private SerializedProperty outputFadeCurveProp;

        private SerializedProperty onSeparationReadyProp;
        private SerializedProperty onErrorProp;
        private SerializedProperty onInitializedProp;
        private SerializedProperty onFeedbackProp;

        private SherpaModelSelectorUI modelSelector;
        private SourceSeparationComponent runtimeComponent;

        private static class Styles
        {
            public static readonly GUIStyle Section =
                new GUIStyle(EditorStyles.helpBox) { padding = new RectOffset(12, 12, 10, 12) };

            public static readonly GUIStyle Header = new GUIStyle(EditorStyles.boldLabel);
        }

        private void OnEnable()
        {
            runtimeComponent = (SourceSeparationComponent)target;

            modelIdProp = serializedObject.FindProperty("modelId");
            sampleRateProp = serializedObject.FindProperty("sampleRate");
            loadOnAwakeProp = serializedObject.FindProperty("loadOnAwake");
            disposeOnDestroyProp = serializedObject.FindProperty("disposeOnDestroy");
            logFeedbackProp = serializedObject.FindProperty("logFeedbackToConsole");

            playbackAudioSourcesProp = serializedObject.FindProperty("playbackAudioSources");
            autoplayProp = serializedObject.FindProperty("autoplay");

            clipReferenceProp = serializedObject.FindProperty("clipReference");
            separateOnEnableProp = serializedObject.FindProperty("separateOnEnable");
            enableOutputProcessingProp = serializedObject.FindProperty("enableOutputProcessing");
            outputFadeInMillisecondsProp = serializedObject.FindProperty("outputFadeInMilliseconds");
            outputFadeOutMillisecondsProp = serializedObject.FindProperty("outputFadeOutMilliseconds");
            removeOutputDcOffsetProp = serializedObject.FindProperty("removeOutputDcOffset");
            clampOutputToUnitRangeProp = serializedObject.FindProperty("clampOutputToUnitRange");
            outputFadeCurveProp = serializedObject.FindProperty("outputFadeCurve");

            onSeparationReadyProp = serializedObject.FindProperty("onSeparationReady");
            onErrorProp = serializedObject.FindProperty("onError");
            onInitializedProp = serializedObject.FindProperty("onInitializationStateChanged");
            onFeedbackProp = serializedObject.FindProperty("onFeedbackMessage");

            modelSelector = new SherpaModelSelectorUI(SherpaONNXModuleType.SourceSeparation, Repaint);
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
            DrawSeparationSection();
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
                EditorGUILayout.LabelField(
                    SherpaInspectorContent.Text(SherpaONNXL10n.Inspectors.SourceSeparation.SectionPlayback, "Playback"),
                    Styles.Header);
                EditorGUILayout.PropertyField(
                    playbackAudioSourcesProp,
                    SherpaInspectorContent.Label(SherpaONNXL10n.Inspectors.SourceSeparation.FieldPlaybackAudioSources, "Playback Audio Sources"),
                    true);
                EditorGUILayout.PropertyField(
                    autoplayProp,
                    SherpaInspectorContent.Label(SherpaONNXL10n.Inspectors.SourceSeparation.FieldAutoplay, "Autoplay"));
            }
        }

        private void DrawSeparationSection()
        {
            using (new EditorGUILayout.VerticalScope(Styles.Section))
            {
                EditorGUILayout.LabelField(
                    SherpaInspectorContent.Text(SherpaONNXL10n.Inspectors.SourceSeparation.SectionSeparation, "Source Separation"),
                    Styles.Header);
                EditorGUILayout.PropertyField(
                    clipReferenceProp,
                    SherpaInspectorContent.Label(SherpaONNXL10n.Inspectors.SourceSeparation.FieldClipReference, "Clip Reference"));
                EditorGUILayout.PropertyField(
                    separateOnEnableProp,
                    SherpaInspectorContent.Label(SherpaONNXL10n.Inspectors.SourceSeparation.FieldSeparateOnEnable, "Separate On Enable"));

                EditorGUILayout.Space();
                EditorGUILayout.LabelField(
                    SherpaInspectorContent.Text(SherpaONNXL10n.Inspectors.SourceSeparation.SectionOutputProcessing, "Output Processing"),
                    EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(
                    enableOutputProcessingProp,
                    SherpaInspectorContent.Label(SherpaONNXL10n.Inspectors.SourceSeparation.FieldEnableProcessing, "Enable Processing"));
                using (new EditorGUI.DisabledScope(!enableOutputProcessingProp.boolValue))
                {
                    EditorGUILayout.PropertyField(
                        outputFadeInMillisecondsProp,
                        SherpaInspectorContent.Label(SherpaONNXL10n.Inspectors.SourceSeparation.FieldFadeInMilliseconds, "Fade In (ms)"));
                    EditorGUILayout.PropertyField(
                        outputFadeOutMillisecondsProp,
                        SherpaInspectorContent.Label(SherpaONNXL10n.Inspectors.SourceSeparation.FieldFadeOutMilliseconds, "Fade Out (ms)"));
                    EditorGUILayout.PropertyField(
                        removeOutputDcOffsetProp,
                        SherpaInspectorContent.Label(SherpaONNXL10n.Inspectors.SourceSeparation.FieldRemoveDcOffset, "Remove DC Offset"));
                    EditorGUILayout.PropertyField(
                        clampOutputToUnitRangeProp,
                        SherpaInspectorContent.Label(SherpaONNXL10n.Inspectors.SourceSeparation.FieldClampToUnitRange, "Clamp To Unit Range"));
                    EditorGUILayout.PropertyField(
                        outputFadeCurveProp,
                        SherpaInspectorContent.Label(SherpaONNXL10n.Inspectors.SourceSeparation.FieldFadeCurve, "Fade Curve"));
                }

                using (new EditorGUI.DisabledScope(!Application.isPlaying))
                {
                    if (GUILayout.Button(SherpaInspectorContent.Text(SherpaONNXL10n.Inspectors.SourceSeparation.ButtonSeparateNow, "Separate Now")))
                    {
                        runtimeComponent.SeparateAssignedClip();
                    }
                }

                if (clipReferenceProp.objectReferenceValue == null && playbackAudioSourcesProp.arraySize == 0)
                {
                    EditorGUILayout.HelpBox(
                        SherpaInspectorContent.Text(
                            SherpaONNXL10n.Inspectors.SourceSeparation.HelpAssignClip,
                            "Assign a clip reference or configure at least one Playback Audio Source with an AudioClip."),
                        MessageType.Info);
                }
            }
        }

        private void DrawEventsSection()
        {
            using (new EditorGUILayout.VerticalScope(Styles.Section))
            {
                EditorGUILayout.LabelField(SherpaInspectorContent.Text(SherpaONNXL10n.Inspectors.Common.SectionEvents, "Events"), Styles.Header);
                EditorGUILayout.PropertyField(
                    onSeparationReadyProp,
                    SherpaInspectorContent.Label(SherpaONNXL10n.Inspectors.SourceSeparation.EventSeparationReady, "On Separation Ready"));
                EditorGUILayout.PropertyField(
                    onErrorProp,
                    SherpaInspectorContent.Label(SherpaONNXL10n.Inspectors.SourceSeparation.EventError, "On Error"));

                EditorGUILayout.Space();
                EditorGUILayout.LabelField(SherpaInspectorContent.Text(SherpaONNXL10n.Inspectors.Common.SectionLifecycleEvents, "Lifecycle Events"), Styles.Header);
                EditorGUILayout.PropertyField(onInitializedProp, SherpaInspectorContent.Label(SherpaONNXL10n.Inspectors.Common.EventInitialized, "On Initialization State Changed"));
                EditorGUILayout.PropertyField(onFeedbackProp, SherpaInspectorContent.Label(SherpaONNXL10n.Inspectors.Common.EventFeedback, "On Feedback Message"));
            }
        }
    }
}
