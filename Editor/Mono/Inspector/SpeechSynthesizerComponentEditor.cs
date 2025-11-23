// Editor: Packages/com.eitan.sherpa-onnx-unity/Editor/Mono/Inspector/SpeechSynthesizerComponentEditor.cs

namespace Eitan.Sherpa.Onnx.Unity.Editor.Mono.Inspector
{
    using Eitan.Sherpa.Onnx.Unity.Mono.Components;
    using Eitan.SherpaONNXUnity.Editor.Localization;
    using Eitan.SherpaONNXUnity.Runtime;
    using UnityEditor;
    using UnityEngine;

    [CustomEditor(typeof(SpeechSynthesizerComponent))]
    public sealed class SpeechSynthesizerComponentEditor : Editor
    {
        private SerializedProperty modelIdProp;
        private SerializedProperty sampleRateProp;
        private SerializedProperty loadOnAwakeProp;
        private SerializedProperty disposeOnDestroyProp;
        private SerializedProperty logFeedbackProp;

        private SerializedProperty audioSourceProp;
        private SerializedProperty autoplayProp;
        private SerializedProperty voiceIdProp;
        private SerializedProperty speechRateProp;
        private SerializedProperty onStartedProp;
        private SerializedProperty onClipProp;
        private SerializedProperty onFailedProp;
        private SerializedProperty onFeedbackProp;
        private SerializedProperty onInitializedProp;

        private string previewText;
        private SpeechSynthesizerComponent runtimeComponent;
        private SherpaModelSelectorUI modelSelector;

        private static class Styles
        {
            public static readonly GUIStyle Section =
                new GUIStyle(EditorStyles.helpBox) { padding = new RectOffset(12, 12, 10, 12) };

            public static readonly GUIStyle Header = new GUIStyle(EditorStyles.boldLabel);
        }

        private void OnEnable()
        {
            runtimeComponent = (SpeechSynthesizerComponent)target;
            modelIdProp = serializedObject.FindProperty("modelId");
            sampleRateProp = serializedObject.FindProperty("sampleRate");
            loadOnAwakeProp = serializedObject.FindProperty("loadOnAwake");
            disposeOnDestroyProp = serializedObject.FindProperty("disposeOnDestroy");
            logFeedbackProp = serializedObject.FindProperty("logFeedbackToConsole");

            audioSourceProp = serializedObject.FindProperty("outputAudioSource");
            autoplayProp = serializedObject.FindProperty("autoplay");
            voiceIdProp = serializedObject.FindProperty("voiceId");
            speechRateProp = serializedObject.FindProperty("speechRate");

            onStartedProp = serializedObject.FindProperty("onSynthesisStarted");
            onClipProp = serializedObject.FindProperty("onClipReady");
            onFailedProp = serializedObject.FindProperty("onSynthesisFailed");

            onFeedbackProp = serializedObject.FindProperty("onFeedbackMessage");
            onInitializedProp = serializedObject.FindProperty("onInitializationStateChanged");

            modelSelector = new SherpaModelSelectorUI(SherpaONNXModuleType.SpeechSynthesis, Repaint);
            modelSelector.Refresh();

            if (string.IsNullOrEmpty(previewText))
            {
                previewText = SherpaInspectorContent.Text(SherpaONNXL10n.Inspectors.SpeechSynthesizer.PreviewDefaultText, "Hello from sherpa-onnx!");
            }
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
            DrawSynthesisSection();
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

                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.PropertyField(sampleRateProp, SherpaInspectorContent.Label(SherpaONNXL10n.Inspectors.Common.FieldSampleRate, "Sample Rate (Hz)"), true);
                EditorGUI.EndDisabledGroup();
                EditorGUILayout.HelpBox(
                    SherpaInspectorContent.Text(SherpaONNXL10n.Inspectors.Common.HelpSampleRateIgnored, "Speech synthesis modules ignore the sample rate field (fixed at -1) and derive the correct value from the model metadata."),
                    MessageType.Info);

                EditorGUILayout.PropertyField(loadOnAwakeProp, SherpaInspectorContent.Label(SherpaONNXL10n.Inspectors.Common.FieldLoadOnAwake, "Load On Awake"));
                EditorGUILayout.PropertyField(disposeOnDestroyProp, SherpaInspectorContent.Label(SherpaONNXL10n.Inspectors.Common.FieldDisposeOnDestroy, "Dispose On Destroy"));
                EditorGUILayout.PropertyField(logFeedbackProp, SherpaInspectorContent.Label(SherpaONNXL10n.Inspectors.Common.FieldLogFeedback, "Log Feedback"));
            }
        }

        private void DrawSynthesisSection()
        {
            using (new EditorGUILayout.VerticalScope(Styles.Section))
            {
                EditorGUILayout.LabelField(SherpaInspectorContent.Text(SherpaONNXL10n.Inspectors.SpeechSynthesizer.SectionSynthesis, "Synthesis"), Styles.Header);
                EditorGUILayout.PropertyField(audioSourceProp, SherpaInspectorContent.Label(SherpaONNXL10n.Inspectors.SpeechSynthesizer.FieldOutputAudioSource, "Output AudioSource"));
                EditorGUILayout.PropertyField(autoplayProp, SherpaInspectorContent.Label(SherpaONNXL10n.Inspectors.SpeechSynthesizer.FieldAutoplay, "Autoplay Result"));
                EditorGUILayout.PropertyField(voiceIdProp, SherpaInspectorContent.Label(SherpaONNXL10n.Inspectors.SpeechSynthesizer.FieldVoiceId, "Voice ID"));
                EditorGUILayout.Slider(speechRateProp, 0.5f, 2.5f, SherpaInspectorContent.Label(SherpaONNXL10n.Inspectors.SpeechSynthesizer.FieldSpeechRate, "Speech Rate"));

                EditorGUILayout.Space();
                EditorGUILayout.LabelField(SherpaInspectorContent.Text(SherpaONNXL10n.Inspectors.SpeechSynthesizer.SectionPreview, "Preview"), Styles.Header);
                previewText = EditorGUILayout.TextArea(previewText, GUILayout.MinHeight(40));

                using (new EditorGUI.DisabledScope(!Application.isPlaying))
                {
                    if (GUILayout.Button(SherpaInspectorContent.Text(SherpaONNXL10n.Inspectors.SpeechSynthesizer.ButtonSynthesizePreview, "Synthesize Preview")))
                    {
                        runtimeComponent.SynthesizeText(previewText);
                    }
                }
            }
        }

        private void DrawEventsSection()
        {
            using (new EditorGUILayout.VerticalScope(Styles.Section))
            {
                EditorGUILayout.LabelField(SherpaInspectorContent.Text(SherpaONNXL10n.Inspectors.Common.SectionEvents, "Events"), Styles.Header);
                EditorGUILayout.PropertyField(onStartedProp, SherpaInspectorContent.Label(SherpaONNXL10n.Inspectors.SpeechSynthesizer.EventStarted, "On Synthesis Started"));
                EditorGUILayout.PropertyField(onClipProp, SherpaInspectorContent.Label(SherpaONNXL10n.Inspectors.SpeechSynthesizer.EventClipReady, "On Clip Ready"));
                EditorGUILayout.PropertyField(onFailedProp, SherpaInspectorContent.Label(SherpaONNXL10n.Inspectors.SpeechSynthesizer.EventFailed, "On Synthesis Failed"));

                EditorGUILayout.Space();
                EditorGUILayout.LabelField(SherpaInspectorContent.Text(SherpaONNXL10n.Inspectors.Common.SectionLifecycleEvents, "Lifecycle Events"), Styles.Header);
                EditorGUILayout.PropertyField(onInitializedProp, SherpaInspectorContent.Label(SherpaONNXL10n.Inspectors.Common.EventInitialized, "On Initialization State Changed"));
                EditorGUILayout.PropertyField(onFeedbackProp, SherpaInspectorContent.Label(SherpaONNXL10n.Inspectors.Common.EventFeedback, "On Feedback Message"));
            }
        }
    }
}
