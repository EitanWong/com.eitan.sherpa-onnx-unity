namespace Eitan.Sherpa.Onnx.Unity.Editor.Mono.Icons
{
    using System;
    using System.Collections.Generic;
    using Eitan.Sherpa.Onnx.Unity.Mono.Components;
    using Eitan.Sherpa.Onnx.Unity.Mono.Inputs;
    using Eitan.SherpaONNXUnity.Runtime;
    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// Procedural icon factory for SherpaONNXUnity editor UI.
    /// Icons are generated once per id/size/theme and kept in memory for reuse.
    /// </summary>
    public static class SherpaMonoIconProvider
    {
        private const int DefaultIconSize = 32;
        private const int LargeIconSize = 128;
        private const string TextureNamePrefix = "SherpaONNXUnity/";

        private static readonly Dictionary<CacheKey, Texture2D> IconCache = new Dictionary<CacheKey, Texture2D>();
        private static readonly Dictionary<Type, SherpaMonoIconId> TypeIcons = new Dictionary<Type, SherpaMonoIconId>
        {
            { typeof(SherpaAudioInputSource), SherpaMonoIconId.Sherpa },
            { typeof(SherpaModuleComponent<>), SherpaMonoIconId.Sherpa },
            { typeof(SherpaAudioStreamingComponent<>), SherpaMonoIconId.Sherpa },
            { typeof(SherpaMicrophoneInput), SherpaMonoIconId.MicrophoneInput },
            { typeof(RealtimeSpeechRecognizerComponent), SherpaMonoIconId.RealtimeSpeechRecognizer },
            { typeof(OfflineSpeechRecognizerComponent), SherpaMonoIconId.OfflineSpeechRecognizer },
            { typeof(SpeechSynthesizerComponent), SherpaMonoIconId.SpeechSynthesizer },
            { typeof(ZeroShotSpeechSynthesisComponent), SherpaMonoIconId.ZeroShotSpeechSynthesizer },
            { typeof(SpeechEnhancementComponent), SherpaMonoIconId.SpeechEnhancement },
            { typeof(SourceSeparationComponent), SherpaMonoIconId.SourceSeparation },
            { typeof(KeywordSpottingComponent), SherpaMonoIconId.KeywordSpotting },
            { typeof(SpokenLanguageIdentificationComponent), SherpaMonoIconId.SpokenLanguageIdentification },
            { typeof(SpeakerDiarizationComponent), SherpaMonoIconId.SpeakerDiarization },
            { typeof(PunctuationComponent), SherpaMonoIconId.Punctuation },
            { typeof(AudioTaggingComponent), SherpaMonoIconId.AudioTagging },
            { typeof(VoiceActivityDetectionComponent), SherpaMonoIconId.VoiceActivityDetection },
            { typeof(SherpaONNXRuntimeSettings), SherpaMonoIconId.RuntimeSettings },
            { typeof(SherpaONNXCustomModelSettings), SherpaMonoIconId.CustomModels }
        };

        public static Texture2D GetIcon(SherpaMonoIconId iconId)
        {
            return GetIcon(iconId, DefaultIconSize);
        }

        public static Texture2D GetIcon(SherpaMonoIconId iconId, int size)
        {
            var normalizedSize = Mathf.Clamp(size, 16, 128);
            var key = new CacheKey(iconId, normalizedSize, EditorGUIUtility.isProSkin);

            if (!IconCache.TryGetValue(key, out var icon) || icon == null)
            {
                icon = SherpaMonoIconPainter.Paint(iconId, normalizedSize, EditorGUIUtility.isProSkin);
                icon.name = TextureNamePrefix + iconId + "/" + normalizedSize;
                icon.hideFlags = HideFlags.HideAndDontSave;
                IconCache[key] = icon;
            }

            return icon;
        }

        public static Texture2D GetLargeIcon(SherpaMonoIconId iconId)
        {
            return GetIcon(iconId, LargeIconSize);
        }

        public static bool TryGetIconForType(Type componentType, out Texture2D icon)
        {
            icon = null;
            if (!TryGetIconIdForType(componentType, out var iconId))
            {
                return false;
            }

            icon = GetIcon(iconId);
            return true;
        }

        public static bool TryGetIconIdForType(Type componentType, out SherpaMonoIconId iconId)
        {
            iconId = SherpaMonoIconId.Sherpa;
            if (componentType == null)
            {
                return false;
            }

            if (TypeIcons.TryGetValue(componentType, out iconId))
            {
                return true;
            }

            return componentType.IsGenericType && TypeIcons.TryGetValue(componentType.GetGenericTypeDefinition(), out iconId);
        }

        public static GUIContent GetContent(SherpaMonoIconId iconId, string text = null, string tooltip = null)
        {
            return new GUIContent(text ?? string.Empty, GetIcon(iconId), tooltip);
        }

        internal static IReadOnlyDictionary<Type, SherpaMonoIconId> ComponentIconMap => TypeIcons;

        private readonly struct CacheKey : IEquatable<CacheKey>
        {
            private readonly SherpaMonoIconId iconId;
            private readonly int size;
            private readonly bool proSkin;

            public CacheKey(SherpaMonoIconId iconId, int size, bool proSkin)
            {
                this.iconId = iconId;
                this.size = size;
                this.proSkin = proSkin;
            }

            public bool Equals(CacheKey other)
            {
                return iconId == other.iconId && size == other.size && proSkin == other.proSkin;
            }

            public override bool Equals(object obj)
            {
                return obj is CacheKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hashCode = (int)iconId;
                    hashCode = (hashCode * 397) ^ size;
                    hashCode = (hashCode * 397) ^ proSkin.GetHashCode();
                    return hashCode;
                }
            }
        }
    }
}
