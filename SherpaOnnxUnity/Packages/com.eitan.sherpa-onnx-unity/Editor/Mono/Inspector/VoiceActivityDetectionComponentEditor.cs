// Editor: Packages/com.eitan.sherpa-onnx-unity/Editor/Mono/Inspector/VoiceActivityDetectionComponentEditor.cs

namespace Eitan.Sherpa.Onnx.Unity.Editor.Mono.Inspector
{
    using Eitan.Sherpa.Onnx.Unity.Mono.Components;
    using Eitan.Sherpa.Onnx.Unity.Mono.Inputs;
    using Eitan.SherpaOnnxUnity.Editor.Localization;
    using Eitan.SherpaOnnxUnity.Runtime;
    using UnityEditor;
    using UnityEngine;

    [CustomEditor(typeof(VoiceActivityDetectionComponent))]
    public sealed class VoiceActivityDetectionComponentEditor : Editor
    {
        private SerializedProperty modelIdProp;
        private SerializedProperty sampleRateProp;
        private SerializedProperty loadOnAwakeProp;
        private SerializedProperty disposeOnDestroyProp;
        private SerializedProperty logFeedbackProp;

        private SerializedProperty audioInputProp;
        private SerializedProperty autoBindInputProp;
        private SerializedProperty thresholdProp;
        private SerializedProperty minSilenceProp;
        private SerializedProperty minSpeechProp;
        private SerializedProperty maxSpeechProp;
        private SerializedProperty leadingPaddingProp;
        private SerializedProperty onSegmentProp;
        private SerializedProperty onSpeakingStateProp;

        private SherpaModelSelectorUI modelSelector;
        private VoiceActivityDetectionComponent runtimeComponent;

        private static class Styles
        {
            public static readonly GUIStyle Section =
                new GUIStyle(EditorStyles.helpBox) { padding = new RectOffset(12, 12, 10, 12) };

            public static readonly GUIStyle Header = new GUIStyle(EditorStyles.boldLabel);
        }

        private void OnEnable()
        {
            runtimeComponent = (VoiceActivityDetectionComponent)target;

            modelIdProp = serializedObject.FindProperty("modelId");
            sampleRateProp = serializedObject.FindProperty("sampleRate");
            loadOnAwakeProp = serializedObject.FindProperty("loadOnAwake");
            disposeOnDestroyProp = serializedObject.FindProperty("disposeOnDestroy");
            logFeedbackProp = serializedObject.FindProperty("logFeedbackToConsole");

            audioInputProp = serializedObject.FindProperty("audioInput");
            autoBindInputProp = serializedObject.FindProperty("autoBindInput");
            thresholdProp = serializedObject.FindProperty("threshold");
            minSilenceProp = serializedObject.FindProperty("minSilenceDuration");
            minSpeechProp = serializedObject.FindProperty("minSpeechDuration");
            maxSpeechProp = serializedObject.FindProperty("maxSpeechDuration");
            leadingPaddingProp = serializedObject.FindProperty("leadingPaddingDuration");
            onSegmentProp = serializedObject.FindProperty("onSpeechSegment");
            onSpeakingStateProp = serializedObject.FindProperty("onSpeakingStateChanged");

            modelSelector = new SherpaModelSelectorUI(SherpaOnnxModuleType.VoiceActivityDetection, Repaint);
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
            DrawDetectorSection();
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

        private void DrawInputSection()
        {
            using (new EditorGUILayout.VerticalScope(Styles.Section))
            {
                EditorGUILayout.LabelField(SherpaInspectorContent.Text(SherpaOnnxL10n.Inspectors.Common.SectionAudioInput, "Audio Input"), Styles.Header);
                EditorGUILayout.PropertyField(audioInputProp, SherpaInspectorContent.Label(SherpaOnnxL10n.Inspectors.Common.FieldInputSource, "Source"));
                EditorGUILayout.PropertyField(autoBindInputProp, SherpaInspectorContent.Label(SherpaOnnxL10n.Inspectors.Common.FieldAutoBind, "Auto Bind Source"));

                var audioSource = audioInputProp.objectReferenceValue as Object;
                if (audioSource == null)
                {
                    EditorGUILayout.HelpBox(
                        SherpaInspectorContent.Text(SherpaOnnxL10n.Inspectors.Common.HelpAssignInput, "Assign a SherpaAudioInputSource (e.g., SherpaMicrophoneInput) to stream audio automatically."),
                        MessageType.Info);
                }
                else if (GUILayout.Button(SherpaInspectorContent.Text(SherpaOnnxL10n.Inspectors.Common.ButtonSelectInput, "Select Audio Input")))
                {
                    Selection.activeObject = audioSource;
                }

                if (audioSource is SherpaAudioInputSource sherpaInput)
                {
                    var componentSampleRate = sampleRateProp.intValue;
                    var inputSampleRate = sherpaInput.OutputSampleRate;
                    if (componentSampleRate > 0 && inputSampleRate > 0 && componentSampleRate != inputSampleRate)
                    {
                        var warning = string.Format(
                            SherpaInspectorContent.Text(
                                SherpaOnnxL10n.Inspectors.VoiceActivityDetection.WarningSampleRateMismatch,
                                "Sample rate mismatch. Input={0} Hz Component={1} Hz. Consider aligning values to avoid drift."),
                            inputSampleRate,
                            componentSampleRate);
                        EditorGUILayout.HelpBox(warning, MessageType.Warning);
                    }
                }

                if (Application.isPlaying && GUILayout.Button(SherpaInspectorContent.Text(SherpaOnnxL10n.Inspectors.VoiceActivityDetection.ButtonFlush, "Flush Pending Segments")))
                {
                    _ = runtimeComponent.FlushAsync();
                }
            }
        }

        private void DrawDetectorSection()
        {
            using (new EditorGUILayout.VerticalScope(Styles.Section))
            {
                EditorGUILayout.LabelField(SherpaInspectorContent.Text(SherpaOnnxL10n.Inspectors.VoiceActivityDetection.SectionDetector, "Detector Parameters"), Styles.Header);
                EditorGUILayout.Slider(thresholdProp, 0f, 1f, SherpaInspectorContent.Label(SherpaOnnxL10n.Inspectors.VoiceActivityDetection.FieldThreshold, "Threshold"));
                EditorGUILayout.PropertyField(minSilenceProp, SherpaInspectorContent.Label(SherpaOnnxL10n.Inspectors.VoiceActivityDetection.FieldMinSilence, "Min Silence (s)"));
                EditorGUILayout.PropertyField(minSpeechProp, SherpaInspectorContent.Label(SherpaOnnxL10n.Inspectors.VoiceActivityDetection.FieldMinSpeech, "Min Speech (s)"));
                EditorGUILayout.PropertyField(maxSpeechProp, SherpaInspectorContent.Label(SherpaOnnxL10n.Inspectors.VoiceActivityDetection.FieldMaxSpeech, "Max Speech (s)"));
                EditorGUILayout.PropertyField(leadingPaddingProp, SherpaInspectorContent.Label(SherpaOnnxL10n.Inspectors.VoiceActivityDetection.FieldLeadingPadding, "Leading Padding (s)"));
            }
        }

        private void DrawEventsSection()
        {
            using (new EditorGUILayout.VerticalScope(Styles.Section))
            {
                EditorGUILayout.LabelField(SherpaInspectorContent.Text(SherpaOnnxL10n.Inspectors.Common.SectionEvents, "Events"), Styles.Header);
                EditorGUILayout.PropertyField(onSegmentProp, SherpaInspectorContent.Label(SherpaOnnxL10n.Inspectors.VoiceActivityDetection.EventSegment, "On Speech Segment"));
                EditorGUILayout.PropertyField(onSpeakingStateProp, SherpaInspectorContent.Label(SherpaOnnxL10n.Inspectors.VoiceActivityDetection.EventSpeaking, "On Speaking State Changed"));
            }
        }
    }
}
