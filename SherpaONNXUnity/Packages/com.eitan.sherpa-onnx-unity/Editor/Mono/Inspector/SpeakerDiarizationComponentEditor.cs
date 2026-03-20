// Editor: Packages/com.eitan.sherpa-onnx-unity/Editor/Mono/Inspector/SpeakerDiarizationComponentEditor.cs

namespace Eitan.Sherpa.Onnx.Unity.Editor.Mono.Inspector
{
    using Eitan.Sherpa.Onnx.Unity.Mono.Components;
    using Eitan.SherpaONNXUnity.Editor.Localization;
    using Eitan.SherpaONNXUnity.Runtime;
    using UnityEditor;
    using UnityEngine;

    [CustomEditor(typeof(SpeakerDiarizationComponent))]
    public sealed class SpeakerDiarizationComponentEditor : Editor
    {
        private SerializedProperty modelIdProp;
        private SerializedProperty embeddingModelIdProp;
        private SerializedProperty sampleRateProp;
        private SerializedProperty loadOnAwakeProp;
        private SerializedProperty disposeOnDestroyProp;
        private SerializedProperty logFeedbackProp;
        private SerializedProperty startModuleImmediatelyProp;

        private SerializedProperty minDurationOnProp;
        private SerializedProperty minDurationOffProp;
        private SerializedProperty numClustersProp;
        private SerializedProperty clusteringThresholdProp;

        private SerializedProperty clipToDiarizeProp;
        private SerializedProperty diarizeAssignedClipOnReadyProp;

        private SerializedProperty onSegmentsReadyProp;
        private SerializedProperty onDiarizationLogReadyProp;
        private SerializedProperty onDiarizationFailedProp;
        private SerializedProperty onFeedbackProp;
        private SerializedProperty onInitializedProp;
        private SerializedProperty onErrorProp;

        private SherpaModelSelectorUI segmentationModelSelector;
        private SherpaModelSelectorUI embeddingModelSelector;
        private SpeakerDiarizationComponent runtimeComponent;

        private static class Styles
        {
            public static readonly GUIStyle Section =
                new GUIStyle(EditorStyles.helpBox) { padding = new RectOffset(12, 12, 10, 12) };

            public static readonly GUIStyle Header = new GUIStyle(EditorStyles.boldLabel);
        }

        private void OnEnable()
        {
            runtimeComponent = (SpeakerDiarizationComponent)target;

            modelIdProp = serializedObject.FindProperty("modelId");
            embeddingModelIdProp = serializedObject.FindProperty("embeddingModelId");
            sampleRateProp = serializedObject.FindProperty("sampleRate");
            loadOnAwakeProp = serializedObject.FindProperty("loadOnAwake");
            disposeOnDestroyProp = serializedObject.FindProperty("disposeOnDestroy");
            logFeedbackProp = serializedObject.FindProperty("logFeedbackToConsole");
            startModuleImmediatelyProp = serializedObject.FindProperty("startModuleImmediately");

            minDurationOnProp = serializedObject.FindProperty("minDurationOn");
            minDurationOffProp = serializedObject.FindProperty("minDurationOff");
            numClustersProp = serializedObject.FindProperty("numClusters");
            clusteringThresholdProp = serializedObject.FindProperty("clusteringThreshold");

            clipToDiarizeProp = serializedObject.FindProperty("clipToDiarize");
            diarizeAssignedClipOnReadyProp = serializedObject.FindProperty("diarizeAssignedClipOnReady");

            onSegmentsReadyProp = serializedObject.FindProperty("onSegmentsReady");
            onDiarizationLogReadyProp = serializedObject.FindProperty("onDiarizationLogReady");
            onDiarizationFailedProp = serializedObject.FindProperty("onDiarizationFailed");
            onFeedbackProp = serializedObject.FindProperty("onFeedbackMessage");
            onInitializedProp = serializedObject.FindProperty("onInitializationStateChanged");
            onErrorProp = serializedObject.FindProperty("onError");

            segmentationModelSelector = new SherpaModelSelectorUI(SherpaONNXModuleType.SpeakerDiarization, Repaint);
            embeddingModelSelector = new SherpaModelSelectorUI(SherpaONNXModuleType.Embedding, Repaint);
            segmentationModelSelector.Refresh();
            embeddingModelSelector.Refresh();
        }

        private void OnDisable()
        {
            segmentationModelSelector?.Dispose();
            embeddingModelSelector?.Dispose();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawModelSection();
            EditorGUILayout.Space();
            DrawOptionsSection();
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

                segmentationModelSelector?.DrawModelField(
                    modelIdProp,
                    SherpaInspectorContent.Label(null, "Segmentation Model ID"));

                embeddingModelSelector?.DrawModelField(
                    embeddingModelIdProp,
                    SherpaInspectorContent.Label(null, "Embedding Model ID"));

                EditorGUILayout.PropertyField(sampleRateProp, SherpaInspectorContent.Label(SherpaONNXL10n.Inspectors.Common.FieldSampleRate, "Sample Rate (Hz)"));
                EditorGUILayout.HelpBox(
                    SherpaInspectorContent.Text(null, "Speaker diarization models typically expect mono PCM at the model sample rate. Keep the clip and module sample rate aligned."),
                    MessageType.Info);
                EditorGUILayout.PropertyField(loadOnAwakeProp, SherpaInspectorContent.Label(SherpaONNXL10n.Inspectors.Common.FieldLoadOnAwake, "Load On Awake"));
                EditorGUILayout.PropertyField(startModuleImmediatelyProp, SherpaInspectorContent.Label(null, "Start Module Immediately"));
                EditorGUILayout.PropertyField(disposeOnDestroyProp, SherpaInspectorContent.Label(SherpaONNXL10n.Inspectors.Common.FieldDisposeOnDestroy, "Dispose On Destroy"));
                EditorGUILayout.PropertyField(logFeedbackProp, SherpaInspectorContent.Label(SherpaONNXL10n.Inspectors.Common.FieldLogFeedback, "Log Feedback"));
            }
        }

        private void DrawOptionsSection()
        {
            using (new EditorGUILayout.VerticalScope(Styles.Section))
            {
                EditorGUILayout.LabelField(SherpaInspectorContent.Text(null, "Diarization Options"), Styles.Header);
                EditorGUILayout.PropertyField(minDurationOnProp, SherpaInspectorContent.Label(null, "Min Duration On"));
                EditorGUILayout.PropertyField(minDurationOffProp, SherpaInspectorContent.Label(null, "Min Duration Off"));
                EditorGUILayout.PropertyField(numClustersProp, SherpaInspectorContent.Label(null, "Num Clusters"));
                EditorGUILayout.PropertyField(clusteringThresholdProp, SherpaInspectorContent.Label(null, "Clustering Threshold"));

                EditorGUILayout.HelpBox(
                    SherpaInspectorContent.Text(null, "Set Num Clusters to -1 to let the model infer the speaker count automatically."),
                    MessageType.None);
            }
        }

        private void DrawClipSection()
        {
            using (new EditorGUILayout.VerticalScope(Styles.Section))
            {
                EditorGUILayout.LabelField(SherpaInspectorContent.Text(null, "Clip Input"), Styles.Header);
                EditorGUILayout.PropertyField(clipToDiarizeProp, SherpaInspectorContent.Label(null, "Audio Clip"));
                EditorGUILayout.PropertyField(diarizeAssignedClipOnReadyProp, SherpaInspectorContent.Label(null, "Diarize Assigned Clip On Ready"));

                using (new EditorGUI.DisabledScope(!Application.isPlaying || clipToDiarizeProp.objectReferenceValue == null))
                {
                    if (GUILayout.Button(SherpaInspectorContent.Text(null, "Diarize Assigned Clip")))
                    {
                        runtimeComponent.DiarizeAssignedClipAsync();
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
                EditorGUILayout.PropertyField(onSegmentsReadyProp, SherpaInspectorContent.Label(null, "On Segments Ready"));
                EditorGUILayout.PropertyField(onDiarizationLogReadyProp, SherpaInspectorContent.Label(null, "On Diarization Log Ready"));
                EditorGUILayout.PropertyField(onDiarizationFailedProp, SherpaInspectorContent.Label(null, "On Diarization Failed"));
                EditorGUILayout.PropertyField(onErrorProp, SherpaInspectorContent.Label(null, "On Error"));

                EditorGUILayout.Space();
                EditorGUILayout.LabelField(SherpaInspectorContent.Text(SherpaONNXL10n.Inspectors.Common.SectionLifecycleEvents, "Lifecycle Events"), Styles.Header);
                EditorGUILayout.PropertyField(onInitializedProp, SherpaInspectorContent.Label(SherpaONNXL10n.Inspectors.Common.EventInitialized, "On Initialization State Changed"));
                EditorGUILayout.PropertyField(onFeedbackProp, SherpaInspectorContent.Label(SherpaONNXL10n.Inspectors.Common.EventFeedback, "On Feedback Message"));
            }
        }
    }
}
