// Editor: Packages/com.eitan.sherpa-onnx-unity/Editor/Mono/SherpaMonoMenuItems.cs

namespace Eitan.Sherpa.Onnx.Unity.Editor.Mono
{
    using Eitan.Sherpa.Onnx.Unity.Mono.Components;
    using Eitan.Sherpa.Onnx.Unity.Mono.Inputs;
    using UnityEditor;
    using UnityEngine;

    internal static class SherpaMonoMenuItems
    {
        private const string Root = "GameObject/SherpaONNX/";
        private const int MenuPriority = 10;

        [MenuItem(Root + "Audio/Microphone Input", false, MenuPriority)]
        private static void CreateMicrophoneInput(MenuCommand command) =>
            CreateWithComponent<SherpaMicrophoneInput>("Sherpa Microphone Input", command);

        [MenuItem(Root + "Voice/Voice Activity Detector", false, MenuPriority + 1)]
        private static void CreateVoiceActivity(MenuCommand command) =>
            CreateWithComponent<VoiceActivityDetectionComponent>("Voice Activity Detector", command);

        [MenuItem(Root + "Speech Recognition/Streaming Recognizer", false, MenuPriority + 2)]
        private static void CreateSpeechRecognizer(MenuCommand command) =>
            CreateWithComponent<SpeechRecognizerComponent>("Speech Recognizer", command);

        [MenuItem(Root + "Speech Recognition/Offline Recognizer", false, MenuPriority + 3)]
        private static void CreateOfflineSpeechRecognizer(MenuCommand command) =>
            CreateWithComponent<OfflineSpeechRecognizerComponent>("Offline Speech Recognizer", command);

        [MenuItem(Root + "Speech Synthesis/Speech Synthesizer", false, MenuPriority + 4)]
        private static void CreateSpeechSynthesizer(MenuCommand command) =>
            CreateWithComponent<SpeechSynthesizerComponent>("Speech Synthesizer", command);
        [MenuItem(Root + "Speech Synthesis/ZeroShot Speech Synthesizer (Experimental)", false, MenuPriority + 4)]
        private static void CreateZeroShotSpeechSynthesizer(MenuCommand command) =>
            CreateWithComponent<ZeroShotSpeechSynthesisComponent>("ZeroShot Speech Synthesizer (Experimental)", command);

        [MenuItem(Root + "Speech Enhancement/Speech Enhancement Component", false, MenuPriority + 5)]
        private static void CreateSpeechEnhancement(MenuCommand command) =>
            CreateWithComponent<SpeechEnhancementComponent>("Speech Enhancement Component", command);

        [MenuItem(Root + "Keyword Spotting/Keyword Spotter", false, MenuPriority + 6)]
        private static void CreateKeywordSpotter(MenuCommand command) =>
            CreateWithComponent<KeywordSpottingComponent>("Keyword Spotter", command);

        [MenuItem(Root + "Language/Spoken Language Identification", false, MenuPriority + 7)]
        private static void CreateSpokenLanguage(MenuCommand command) =>
            CreateWithComponent<SpokenLanguageIdentificationComponent>("Spoken Language Identification", command);

        [MenuItem(Root + "Text/Punctuation", false, MenuPriority + 8)]
        private static void CreatePunctuation(MenuCommand command) =>
            CreateWithComponent<PunctuationComponent>("Punctuation", command);
        [MenuItem(Root + "Text/AudioTagging", false, MenuPriority + 8)]
        private static void CreateAudioTagging(MenuCommand command) =>
            CreateWithComponent<AudioTaggingComponent>("AudioTagging", command);

        private static void CreateWithComponent<T>(string label, MenuCommand command) where T : Component
        {
            var target = ResolveTarget(label, command);
            Undo.AddComponent<T>(target);
            Selection.activeGameObject = target;
        }

        private static GameObject ResolveTarget(string label, MenuCommand command)
        {
            if (command.context is GameObject ctx)
            {
                Undo.RecordObject(ctx, $"Add {label}");
                return ctx;
            }

            var go = new GameObject(label);
            GameObjectUtility.SetParentAndAlign(go, Selection.activeGameObject);
            Undo.RegisterCreatedObjectUndo(go, $"Create {label}");
            return go;
        }
    }
}
