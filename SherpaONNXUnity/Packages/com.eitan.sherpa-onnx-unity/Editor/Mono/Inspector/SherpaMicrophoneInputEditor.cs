// Editor: Packages/com.eitan.sherpa-onnx-unity/Editor/Mono/Inspector/SherpaMicrophoneInputEditor.cs

namespace Eitan.Sherpa.Onnx.Unity.Editor.Mono.Inspector
{
    using Eitan.Sherpa.Onnx.Unity.Mono.Inputs;
    using Eitan.SherpaONNXUnity.Editor.Localization;
    using UnityEditor;
    using UnityEngine;

    [CustomEditor(typeof(SherpaMicrophoneInput))]
    public sealed class SherpaMicrophoneInputEditor : Editor
    {
        private SerializedProperty autoStartProp;
        private SerializedProperty preferredDeviceProp;
        private SerializedProperty sampleRateProp;
        private SerializedProperty chunkDurationProp;
        private SerializedProperty bufferSecondsProp;
        private SerializedProperty downmixProp;
        private SerializedProperty onChunkProp;
        private SerializedProperty onStateChangedProp;

        private SherpaMicrophoneInput runtimeInput;
        private string[] deviceNames = new string[0];
        private double nextDeviceRefreshTime;

        private void OnEnable()
        {
            runtimeInput = (SherpaMicrophoneInput)target;

            autoStartProp = serializedObject.FindProperty("autoStartOnEnable");
            preferredDeviceProp = serializedObject.FindProperty("preferredDevice");
            sampleRateProp = serializedObject.FindProperty("requestedSampleRate");
            chunkDurationProp = serializedObject.FindProperty("chunkDurationSeconds");
            bufferSecondsProp = serializedObject.FindProperty("bufferLengthSeconds");
            downmixProp = serializedObject.FindProperty("downmixToMono");
            onChunkProp = serializedObject.FindProperty("onChunkReady");
            onStateChangedProp = serializedObject.FindProperty("onRecordingStateChanged");

            RefreshDevices();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawCaptureSection();
            EditorGUILayout.Space();
            DrawEventsSection();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawCaptureSection()
        {
            EditorGUILayout.LabelField(SherpaInspectorContent.Text(SherpaONNXL10n.Inspectors.Microphone.SectionCapture, "Capture"), EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(autoStartProp, SherpaInspectorContent.Label(SherpaONNXL10n.Inspectors.Microphone.FieldAutoStart, "Auto Start On Enable"));
            EditorGUILayout.PropertyField(sampleRateProp, SherpaInspectorContent.Label(SherpaONNXL10n.Inspectors.Microphone.FieldSampleRate, "Sample Rate (Hz)"));
            EditorGUILayout.Slider(chunkDurationProp, 0.05f, 0.5f, SherpaInspectorContent.Label(SherpaONNXL10n.Inspectors.Microphone.FieldChunkDuration, "Chunk Duration (s)"));
            EditorGUILayout.IntSlider(bufferSecondsProp, 1, 30, SherpaInspectorContent.Label(SherpaONNXL10n.Inspectors.Microphone.FieldBufferLength, "Buffer Length (s)"));
            EditorGUILayout.PropertyField(downmixProp, SherpaInspectorContent.Label(SherpaONNXL10n.Inspectors.Microphone.FieldDownmix, "Downmix To Mono"));

            DrawDevicePopup();

            if (Application.isPlaying)
            {
                EditorGUILayout.Space();
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(runtimeInput.IsCapturing))
                    {
                        if (GUILayout.Button(SherpaInspectorContent.Text(SherpaONNXL10n.Inspectors.Microphone.ButtonStart, "Start Capture")))
                        {
                            runtimeInput.TryStartCapture();
                        }
                    }

                    using (new EditorGUI.DisabledScope(!runtimeInput.IsCapturing))
                    {
                        if (GUILayout.Button(SherpaInspectorContent.Text(SherpaONNXL10n.Inspectors.Microphone.ButtonStop, "Stop Capture")))
                        {
                            runtimeInput.StopCapture();
                        }
                    }
                }
            }
        }

        private void DrawDevicePopup()
        {
            if (EditorApplication.timeSinceStartup > nextDeviceRefreshTime)
            {
                RefreshDevices();
                nextDeviceRefreshTime = EditorApplication.timeSinceStartup + 2.0;
            }

            if (deviceNames.Length == 0)
            {
                EditorGUILayout.HelpBox(
                    SherpaInspectorContent.Text(SherpaONNXL10n.Inspectors.Microphone.HelpNoDevices, "No microphone devices found."),
                    MessageType.Info);
                return;
            }

            var currentIndex = Mathf.Max(0, System.Array.IndexOf(deviceNames, preferredDeviceProp.stringValue));
            var selection = EditorGUILayout.Popup(
                SherpaInspectorContent.Text(SherpaONNXL10n.Inspectors.Microphone.FieldDevice, "Microphone Device"),
                currentIndex,
                deviceNames);
            preferredDeviceProp.stringValue = deviceNames[Mathf.Clamp(selection, 0, deviceNames.Length - 1)];
        }

        private void DrawEventsSection()
        {
            EditorGUILayout.LabelField(SherpaInspectorContent.Text(SherpaONNXL10n.Inspectors.Common.SectionEvents, "Events"), EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(onChunkProp, SherpaInspectorContent.Label(SherpaONNXL10n.Inspectors.Microphone.EventChunkReady, "On Chunk Ready"));
            EditorGUILayout.PropertyField(onStateChangedProp, SherpaInspectorContent.Label(SherpaONNXL10n.Inspectors.Microphone.EventRecordingState, "On Recording State Changed"));
        }

        private void RefreshDevices()
        {
            var devices = runtimeInput?.GetAvailableDevices();
            if (devices == null || devices.Count == 0)
            {
                deviceNames = new string[0];
                return;
            }

            deviceNames = new string[devices.Count];
            for (int i = 0; i < devices.Count; i++)
            {
                deviceNames[i] = devices[i];
            }
        }
    }
}
