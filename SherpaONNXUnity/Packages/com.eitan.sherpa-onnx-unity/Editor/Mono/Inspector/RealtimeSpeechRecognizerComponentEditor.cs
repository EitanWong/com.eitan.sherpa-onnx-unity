// Editor: Packages/com.eitan.sherpa-onnx-unity/Editor/Mono/Inspector/RealtimeSpeechRecognizerComponentEditor.cs

namespace Eitan.Sherpa.Onnx.Unity.Editor.Mono.Inspector
{
    using System;
    using Eitan.Sherpa.Onnx.Unity.Mono.Components;
    using Eitan.Sherpa.Onnx.Unity.Mono.Inputs;
    using Eitan.SherpaONNXUnity.Editor.Localization;
    using Eitan.SherpaONNXUnity.Runtime;
    using Eitan.SherpaONNXUnity.Runtime.Utilities;
    using UnityEditor;
    using UnityEngine;

    [CustomEditor(typeof(RealtimeSpeechRecognizerComponent), true)]
    public sealed class RealtimeSpeechRecognizerComponentEditor : Editor
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
        private SerializedProperty recognitionLanguageProp;

        private SherpaModelSelectorUI modelSelector;

        private readonly struct LanguageOption
        {
            public LanguageOption(string code, string label)
            {
                Code = code;
                Label = label;
            }

            public string Code { get; }
            public string Label { get; }
        }

        private static readonly LanguageOption[] DefaultLanguageOptions =
        {
            new LanguageOption(string.Empty, "Model Default")
        };

        private static readonly LanguageOption[] CohereLanguageOptions =
        {
            new LanguageOption("en", "English (en)"),
            new LanguageOption("fr", "French (fr)"),
            new LanguageOption("de", "German (de)"),
            new LanguageOption("it", "Italian (it)"),
            new LanguageOption("es", "Spanish (es)"),
            new LanguageOption("pt", "Portuguese (pt)"),
            new LanguageOption("el", "Greek (el)"),
            new LanguageOption("nl", "Dutch (nl)"),
            new LanguageOption("pl", "Polish (pl)"),
            new LanguageOption("zh", "Chinese, Mandarin (zh)"),
            new LanguageOption("ja", "Japanese (ja)"),
            new LanguageOption("ko", "Korean (ko)"),
            new LanguageOption("vi", "Vietnamese (vi)"),
            new LanguageOption("ar", "Arabic (ar)")
        };

        private static readonly LanguageOption[] SenseVoiceLanguageOptions =
        {
            new LanguageOption("auto", "Auto (auto)"),
            new LanguageOption("zh", "Chinese, Mandarin (zh)"),
            new LanguageOption("en", "English (en)"),
            new LanguageOption("yue", "Chinese, Cantonese (yue)"),
            new LanguageOption("ja", "Japanese (ja)"),
            new LanguageOption("ko", "Korean (ko)"),
            new LanguageOption("nospeech", "No Speech (nospeech)")
        };

        private static readonly LanguageOption[] FunAsrNanoLanguageOptions =
        {
            new LanguageOption(string.Empty, "Model Default"),
            new LanguageOption("zh", "Chinese (zh)"),
            new LanguageOption("en", "English (en)"),
            new LanguageOption("ja", "Japanese (ja)")
        };

        private static readonly LanguageOption[] CanaryLanguageOptions =
        {
            new LanguageOption(string.Empty, "Model Default"),
            new LanguageOption("en", "English (en)"),
            new LanguageOption("de", "German (de)"),
            new LanguageOption("fr", "French (fr)"),
            new LanguageOption("es", "Spanish (es)")
        };

        private static readonly LanguageOption[] WhisperLanguageOptions =
        {
            new LanguageOption(string.Empty, "Model Default"),
            new LanguageOption("en", "English (en)"),
            new LanguageOption("zh", "Chinese (zh)"),
            new LanguageOption("de", "German (de)"),
            new LanguageOption("es", "Spanish (es)"),
            new LanguageOption("ru", "Russian (ru)"),
            new LanguageOption("ko", "Korean (ko)"),
            new LanguageOption("fr", "French (fr)"),
            new LanguageOption("ja", "Japanese (ja)"),
            new LanguageOption("pt", "Portuguese (pt)"),
            new LanguageOption("tr", "Turkish (tr)"),
            new LanguageOption("pl", "Polish (pl)"),
            new LanguageOption("ca", "Catalan (ca)"),
            new LanguageOption("nl", "Dutch (nl)"),
            new LanguageOption("ar", "Arabic (ar)"),
            new LanguageOption("sv", "Swedish (sv)"),
            new LanguageOption("it", "Italian (it)"),
            new LanguageOption("id", "Indonesian (id)"),
            new LanguageOption("hi", "Hindi (hi)"),
            new LanguageOption("fi", "Finnish (fi)"),
            new LanguageOption("vi", "Vietnamese (vi)"),
            new LanguageOption("iw", "Hebrew (iw)"),
            new LanguageOption("uk", "Ukrainian (uk)"),
            new LanguageOption("el", "Greek (el)"),
            new LanguageOption("ms", "Malay (ms)"),
            new LanguageOption("cs", "Czech (cs)"),
            new LanguageOption("ro", "Romanian (ro)"),
            new LanguageOption("da", "Danish (da)"),
            new LanguageOption("hu", "Hungarian (hu)"),
            new LanguageOption("ta", "Tamil (ta)"),
            new LanguageOption("no", "Norwegian (no)"),
            new LanguageOption("th", "Thai (th)"),
            new LanguageOption("ur", "Urdu (ur)"),
            new LanguageOption("hr", "Croatian (hr)"),
            new LanguageOption("bg", "Bulgarian (bg)"),
            new LanguageOption("lt", "Lithuanian (lt)"),
            new LanguageOption("la", "Latin (la)"),
            new LanguageOption("mi", "Maori (mi)"),
            new LanguageOption("ml", "Malayalam (ml)"),
            new LanguageOption("cy", "Welsh (cy)"),
            new LanguageOption("sk", "Slovak (sk)"),
            new LanguageOption("te", "Telugu (te)"),
            new LanguageOption("fa", "Persian (fa)"),
            new LanguageOption("lv", "Latvian (lv)"),
            new LanguageOption("bn", "Bengali (bn)"),
            new LanguageOption("sr", "Serbian (sr)"),
            new LanguageOption("az", "Azerbaijani (az)"),
            new LanguageOption("sl", "Slovenian (sl)"),
            new LanguageOption("kn", "Kannada (kn)"),
            new LanguageOption("et", "Estonian (et)"),
            new LanguageOption("mk", "Macedonian (mk)"),
            new LanguageOption("br", "Breton (br)"),
            new LanguageOption("eu", "Basque (eu)"),
            new LanguageOption("is", "Icelandic (is)"),
            new LanguageOption("hy", "Armenian (hy)"),
            new LanguageOption("ne", "Nepali (ne)"),
            new LanguageOption("mn", "Mongolian (mn)"),
            new LanguageOption("bs", "Bosnian (bs)"),
            new LanguageOption("kk", "Kazakh (kk)"),
            new LanguageOption("sq", "Albanian (sq)"),
            new LanguageOption("sw", "Swahili (sw)"),
            new LanguageOption("gl", "Galician (gl)"),
            new LanguageOption("mr", "Marathi (mr)"),
            new LanguageOption("pa", "Punjabi (pa)"),
            new LanguageOption("si", "Sinhala (si)"),
            new LanguageOption("km", "Khmer (km)"),
            new LanguageOption("sn", "Shona (sn)"),
            new LanguageOption("yo", "Yoruba (yo)"),
            new LanguageOption("so", "Somali (so)"),
            new LanguageOption("af", "Afrikaans (af)"),
            new LanguageOption("oc", "Occitan (oc)"),
            new LanguageOption("ka", "Georgian (ka)"),
            new LanguageOption("be", "Belarusian (be)"),
            new LanguageOption("tg", "Tajik (tg)"),
            new LanguageOption("sd", "Sindhi (sd)"),
            new LanguageOption("gu", "Gujarati (gu)"),
            new LanguageOption("am", "Amharic (am)"),
            new LanguageOption("yi", "Yiddish (yi)"),
            new LanguageOption("lo", "Lao (lo)"),
            new LanguageOption("uz", "Uzbek (uz)"),
            new LanguageOption("fo", "Faroese (fo)"),
            new LanguageOption("ht", "Haitian Creole (ht)"),
            new LanguageOption("ps", "Pashto (ps)"),
            new LanguageOption("tk", "Turkmen (tk)"),
            new LanguageOption("nn", "Nynorsk (nn)"),
            new LanguageOption("mt", "Maltese (mt)"),
            new LanguageOption("sa", "Sanskrit (sa)"),
            new LanguageOption("lb", "Luxembourgish (lb)"),
            new LanguageOption("my", "Myanmar (my)"),
            new LanguageOption("bo", "Tibetan (bo)"),
            new LanguageOption("tl", "Tagalog (tl)"),
            new LanguageOption("mg", "Malagasy (mg)"),
            new LanguageOption("as", "Assamese (as)"),
            new LanguageOption("tt", "Tatar (tt)"),
            new LanguageOption("haw", "Hawaiian (haw)"),
            new LanguageOption("ln", "Lingala (ln)"),
            new LanguageOption("ha", "Hausa (ha)"),
            new LanguageOption("ba", "Bashkir (ba)"),
            new LanguageOption("jw", "Javanese (jw)"),
            new LanguageOption("su", "Sundanese (su)")
        };

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
            recognitionLanguageProp = serializedObject.FindProperty("recognitionLanguage");

            onTranscriptionReadyProp = serializedObject.FindProperty("onTranscriptionReady");
            onFeedbackProp = serializedObject.FindProperty("onFeedbackMessage");
            onInitializedProp = serializedObject.FindProperty("onInitializationStateChanged");

            modelSelector = new SherpaModelSelectorUI(SherpaONNXModuleType.SpeechRecognition, Repaint, IsRealtimeSpeechRecognitionModel);
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
            DrawRecognitionOptionsSection();
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

        private void DrawRecognitionOptionsSection()
        {
            DrawRecognitionOptionsSection(modelIdProp, recognitionLanguageProp, Styles.Section, Styles.Header);
        }

        internal static void DrawRecognitionOptionsSection(
            SerializedProperty modelIdProperty,
            SerializedProperty recognitionLanguageProperty,
            GUIStyle sectionStyle,
            GUIStyle headerStyle)
        {
            if (recognitionLanguageProperty == null)
            {
                return;
            }

            var modelType = ResolveSelectedModelType(modelIdProperty);
            if (!SupportsRecognitionLanguage(modelType))
            {
                return;
            }

            using (new EditorGUILayout.VerticalScope(sectionStyle))
            {
                EditorGUILayout.LabelField("Recognition Options", headerStyle);
                var isCohereTranscribe = modelType == SpeechRecognitionModelType.Offline_CohereTranscribe;
                var languageOptions = GetLanguageOptions(modelType);
                if (isCohereTranscribe && string.IsNullOrWhiteSpace(recognitionLanguageProperty.stringValue))
                {
                    recognitionLanguageProperty.stringValue = "en";
                }

                DrawLanguagePopup(recognitionLanguageProperty, languageOptions);

                if (isCohereTranscribe)
                {
                    EditorGUILayout.HelpBox(
                        "Cohere Transcribe requires an explicit language.",
                        MessageType.Info);
                }
            }
        }

        private static LanguageOption[] GetLanguageOptions(SpeechRecognitionModelType modelType)
        {
            switch (modelType)
            {
                case SpeechRecognitionModelType.Offline_CohereTranscribe:
                    return CohereLanguageOptions;
                case SpeechRecognitionModelType.SenseVoice:
                    return SenseVoiceLanguageOptions;
                case SpeechRecognitionModelType.Offline_FunAsrNano:
                    return FunAsrNanoLanguageOptions;
                case SpeechRecognitionModelType.Whisper:
                    return WhisperLanguageOptions;
                case SpeechRecognitionModelType.Offline_Canary:
                    return CanaryLanguageOptions;
                default:
                    return DefaultLanguageOptions;
            }
        }

        private static void DrawLanguagePopup(SerializedProperty property, LanguageOption[] options)
        {
            if (property == null || options == null || options.Length == 0)
            {
                return;
            }

            var currentValue = property.stringValue ?? string.Empty;
            var selectedIndex = 0;
            var labels = new string[options.Length];
            for (var i = 0; i < options.Length; i++)
            {
                labels[i] = options[i].Label;
                if (string.Equals(options[i].Code, currentValue, StringComparison.OrdinalIgnoreCase))
                {
                    selectedIndex = i;
                }
            }

            var nextIndex = EditorGUILayout.Popup(new GUIContent("Language"), selectedIndex, labels);
            property.stringValue = options[nextIndex].Code;
        }

        private static SpeechRecognitionModelType ResolveSelectedModelType(SerializedProperty modelIdProperty)
        {
            var modelId = modelIdProperty?.stringValue;
            if (string.IsNullOrWhiteSpace(modelId))
            {
                return SpeechRecognitionModelType.None;
            }

            if (SherpaONNXModelRegistry.Instance.TryGetManifest(out var manifest) && manifest?.models != null)
            {
                for (var i = 0; i < manifest.models.Count; i++)
                {
                    var metadata = manifest.models[i];
                    if (metadata != null && string.Equals(metadata.modelId, modelId, StringComparison.OrdinalIgnoreCase))
                    {
                        return SherpaUtils.Model.ResolveSpeechRecognitionModelType(metadata, out _);
                    }
                }
            }

            return SherpaUtils.Model.GetSpeechRecognitionModelType(modelId);
        }

        private static bool SupportsRecognitionLanguage(SpeechRecognitionModelType modelType)
        {
            switch (modelType)
            {
                case SpeechRecognitionModelType.Offline_FunAsrNano:
                case SpeechRecognitionModelType.Offline_CohereTranscribe:
                case SpeechRecognitionModelType.Whisper:
                case SpeechRecognitionModelType.SenseVoice:
                case SpeechRecognitionModelType.Offline_Canary:
                    return true;
                default:
                    return false;
            }
        }

        internal static bool IsRealtimeSpeechRecognitionModel(SherpaONNXModelMetadata metadata)
        {
            if (metadata == null)
            {
                return false;
            }

            var modelType = SherpaUtils.Model.ResolveSpeechRecognitionModelType(metadata, out var isOnline);
            return isOnline || IsRealtimeSpeechRecognitionModel(modelType);
        }

        internal static bool IsOfflineSpeechRecognitionModel(SherpaONNXModelMetadata metadata)
        {
            if (metadata == null)
            {
                return false;
            }

            var modelType = SherpaUtils.Model.ResolveSpeechRecognitionModelType(metadata, out var isOnline);
            return !isOnline && modelType != SpeechRecognitionModelType.None;
        }

        private static bool IsRealtimeSpeechRecognitionModel(SpeechRecognitionModelType modelType)
        {
            switch (modelType)
            {
                case SpeechRecognitionModelType.Online_Transducer:
                case SpeechRecognitionModelType.Online_Ctc:
                case SpeechRecognitionModelType.Online_Paraformer:
                case SpeechRecognitionModelType.Online_Zipformer2Ctc:
                case SpeechRecognitionModelType.Online_Nemo_Ctc:
                case SpeechRecognitionModelType.Online_Tone_Ctc:
                    return true;
                default:
                    return false;
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
