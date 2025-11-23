// Editor: Packages/com.eitan.sherpa-onnx-unity/Editor/Mono/Inspector/PunctuationComponentEditor.cs

namespace Eitan.Sherpa.Onnx.Unity.Editor.Mono.Inspector
{
    using Eitan.Sherpa.Onnx.Unity.Mono.Components;
    using Eitan.SherpaONNXUnity.Editor.Localization;
    using Eitan.SherpaONNXUnity.Runtime;
    using UnityEditor;
    using UnityEngine;

    [CustomEditor(typeof(PunctuationComponent))]
    public sealed class PunctuationComponentEditor : Editor
    {
        private SerializedProperty modelIdProp;
        private SerializedProperty sampleRateProp;
        private SerializedProperty loadOnAwakeProp;
        private SerializedProperty disposeOnDestroyProp;
        private SerializedProperty logFeedbackProp;
        private SerializedProperty previewTextProp;
        private SerializedProperty onReadyProp;
        private SerializedProperty onFailedProp;

        private PunctuationComponent runtimeComponent;
        private SherpaModelSelectorUI modelSelector;

        private static class Styles
        {
            public static readonly GUIStyle Section =
                new GUIStyle(EditorStyles.helpBox) { padding = new RectOffset(12, 12, 10, 12) };

            public static readonly GUIStyle Header = new GUIStyle(EditorStyles.boldLabel);
        }

        private void OnEnable()
        {
            runtimeComponent = (PunctuationComponent)target;

            modelIdProp = serializedObject.FindProperty("modelId");
            sampleRateProp = serializedObject.FindProperty("sampleRate");
            loadOnAwakeProp = serializedObject.FindProperty("loadOnAwake");
            disposeOnDestroyProp = serializedObject.FindProperty("disposeOnDestroy");
            logFeedbackProp = serializedObject.FindProperty("logFeedbackToConsole");
            previewTextProp = serializedObject.FindProperty("previewText");
            onReadyProp = serializedObject.FindProperty("onPunctuationReady");
            onFailedProp = serializedObject.FindProperty("onPunctuationFailed");

            modelSelector = new SherpaModelSelectorUI(SherpaONNXModuleType.AddPunctuation, Repaint);
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
            DrawPreviewSection();
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

        private void DrawPreviewSection()
        {
            using (new EditorGUILayout.VerticalScope(Styles.Section))
            {
                EditorGUILayout.LabelField(SherpaInspectorContent.Text(SherpaONNXL10n.Inspectors.Punctuation.SectionPreview, "Preview"), Styles.Header);
                EditorGUILayout.PropertyField(previewTextProp, SherpaInspectorContent.Label(SherpaONNXL10n.Inspectors.Punctuation.FieldInputText, "Input Text"), true);
                using (new EditorGUI.DisabledScope(Application.isPlaying == false))
                {
                    if (GUILayout.Button(SherpaInspectorContent.Text(SherpaONNXL10n.Inspectors.Punctuation.ButtonRun, "Add Punctuation")))
                    {
                        runtimeComponent.RunPreview();
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
                EditorGUILayout.PropertyField(onReadyProp, SherpaInspectorContent.Label(SherpaONNXL10n.Inspectors.Punctuation.EventReady, "On Punctuation Ready"));
                EditorGUILayout.PropertyField(onFailedProp, SherpaInspectorContent.Label(SherpaONNXL10n.Inspectors.Punctuation.EventFailed, "On Punctuation Failed"));
            }
        }
    }
}
