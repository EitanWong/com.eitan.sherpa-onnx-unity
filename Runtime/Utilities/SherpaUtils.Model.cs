
using System;
using System.Linq;
using System.Runtime.CompilerServices;


namespace Eitan.SherpaONNXUnity.Runtime.Utilities
{
    public partial class SherpaUtils
    {
        public class Model
        {
            #region Model Type Keywords

            #region SpeechRecognitionModelKeywords
            // Online model keywords
            private static readonly string[] online_streaming_keywords = { "streaming" };

            // Model architecture keywords
            private static readonly string[] transducer_keywords = { "zipformer", "conformer", "transducer" };
            private static readonly string[] ctc_keywords = { "ctc" };
            private static readonly string[] nemo_ctc_keywords = { "nemo-ctc" };
            // NVIDIA NeMo Parakeet/Canary variants
            private static readonly string[] nemo_parakeet_tdt_ctc_keywords = { "parakeet_tdt_ctc", "parakeet-tdt-ctc", "tdt_ctc" };
            private static readonly string[] nemo_parakeet_tdt_keywords = { "nemo-parakeet-tdt", "parakeet-tdt", "parakeet_tdt" };
            private static readonly string[] nemo_canary_keywords = { "nemo-canary", "canary" };
            private static readonly string[] tdnn_keywords = { "tdnn" };
            private static readonly string[] paraformer_keywords = { "paraformer" };
            private static readonly string[] whisper_keywords = { "whisper" };
            private static readonly string[] moonshine_keywords = { "moonshine" };
            private static readonly string[] sensevoice_keywords = { "sense-voice" };
            private static readonly string[] fireredasr_keywords = { "fire-red-asr" };
            private static readonly string[] dolphin_keywords = { "dolphin" };
            private static readonly string[] telespeech_keywords = { "telespeech" };

            private static readonly string[] omnilingual_keywords = { "omnilingual" };

            // Special model keywords that take precedence
            #endregion

            #region VoiceActivityDetectionModelKeywords
            private static readonly string[] silero_keywords = { "silero" };
            private static readonly string[] ten_keywords = { "ten" };
            #endregion

            #region SpeechSynthesisModelKeywords

            private static readonly string[] vits_keywords = { "vits" };
            private static readonly string[] matcha_keywords = { "matcha", "vocos" };
            private static readonly string[] kokoro_keywords = { "kokoro" };

            private static readonly string[] kitten_keywords = { "kitten" };

            private static readonly string[] zipvoice_keywords = { "zipvoice" };
            #endregion

            #region KeywordSpottingModelKeywords
            private static readonly string[] kws_keywords = { "kws", "keyword" };
            #endregion

            #region SpeechEnhancementModelKeyewords
            private static readonly string[] speechEnhancement_keywords = { "gtcrn" };

            #endregion

            #region SpokenLanguageIdentification
            // Use specific LID markers to avoid colliding with ASR Whisper models
            // the sli use whisper model
            private static readonly string[] spoken_language_id_keywords = { "whisper", "langid", "language-id", "spoken-language-identification", "lid" };
            #endregion

            #region Punctuation
            private static readonly string[] punctuation_keywords = { "punct" };
            #endregion

            #region AudioTagging
            private static readonly string[] audio_tagging_ced_keywords = { "tagging", "ced" };
            private static readonly string[] audio_tagging_zipformer_keywords = { "tagging", "zipformer" };
            #endregion
            #endregion


            #region Methods

            internal static SherpaONNXModuleType GetModuleTypeByModelId(string modelID)
            {
                if (string.IsNullOrEmpty(modelID))
                { return SherpaONNXModuleType.Undefined; }

                // Order matters: avoid collisions (e.g., Whisper ASR vs LID)
                if (IsKeywordSpottingModel(modelID))
                { return SherpaONNXModuleType.KeywordSpotting; }

                // Order matters: avoid collisions (e.g., Zipformer ASR vs ATG)
                var atgType = GetAudioTaggingModelType(modelID);
                if (atgType != AudioTaggingModelType.None)
                { return SherpaONNXModuleType.AudioTagging; }

                var asrType = GetSpeechRecognitionModelType(modelID);
                if (asrType != SpeechRecognitionModelType.None)
                { return SherpaONNXModuleType.SpeechRecognition; }

                var ttsType = GetSpeechSynthesisModelType(modelID);
                if (ttsType != SpeechSynthesisModelType.None)
                { return SherpaONNXModuleType.SpeechSynthesis; }

                var vadType = GetVoiceActivityDetectionModelType(modelID);
                if (vadType != VoiceActivityDetectionModelType.None)
                { return SherpaONNXModuleType.VoiceActivityDetection; }

                if (IsSpeechEnhancementModel(modelID))
                { return SherpaONNXModuleType.SpeechEnhancement; }

                var lidType = GetSpokenLanguageIdentificationModelType(modelID);
                if (lidType != SpokenLanguageIdentificationModelType.None)
                { return SherpaONNXModuleType.SpokenLanguageIdentification; }


                if (IsPunctuationModel(modelID))
                { return SherpaONNXModuleType.AddPunctuation; }

                return SherpaONNXModuleType.Undefined;
            }

            internal static SpeechRecognitionModelType GetSpeechRecognitionModelType(string modelID)
            {
                if (string.IsNullOrEmpty(modelID))
                { return SpeechRecognitionModelType.None; }

                string lowerModelID = modelID.ToLowerInvariant();
                // Determine whether it is an online/streaming model early for family-specific branches
                bool isOnline = ContainsAnyKeyword(lowerModelID, online_streaming_keywords);

                // Check for special models first (they have unique identification)
                if (ContainsAnyKeyword(lowerModelID, whisper_keywords))
                { return SpeechRecognitionModelType.Whisper; }
                else if (ContainsAnyKeyword(lowerModelID, moonshine_keywords))
                { return SpeechRecognitionModelType.Moonshine; }
                else if (ContainsAnyKeyword(lowerModelID, sensevoice_keywords))
                { return SpeechRecognitionModelType.SenseVoice; }
                else if (ContainsAnyKeyword(lowerModelID, fireredasr_keywords))
                { return SpeechRecognitionModelType.FireRedAsr; }
                else if (ContainsAnyKeyword(lowerModelID, dolphin_keywords))
                { return SpeechRecognitionModelType.Dolphin; }
                else if (ContainsAnyKeyword(lowerModelID, telespeech_keywords))
                { return SpeechRecognitionModelType.TeleSpeech; }
                else if (ContainsAnyKeyword(lowerModelID, nemo_ctc_keywords))
                { return SpeechRecognitionModelType.Offline_Nemo_Ctc; }
                else if (ContainsAnyKeyword(lowerModelID, tdnn_keywords))
                { return SpeechRecognitionModelType.Tdnn; }
                else if (ContainsAnyKeyword(lowerModelID, omnilingual_keywords))
                { return SpeechRecognitionModelType.Omnilingual; }


                // NeMo family special cases
                // 1) Parakeet TDT CTC should map to Nemo CTC explicitly to avoid falling into generic CTC
                if (ContainsAnyKeyword(lowerModelID, nemo_parakeet_tdt_ctc_keywords))
                { return SpeechRecognitionModelType.Offline_Nemo_Ctc; }

                // 2) Parakeet TDT (no explicit CTC) -> treat as Transducer family
                if (ContainsAnyKeyword(lowerModelID, nemo_parakeet_tdt_keywords))
                { return isOnline ? SpeechRecognitionModelType.Online_Transducer : SpeechRecognitionModelType.Offline_Transducer; }

                // 3) Canary models (e.g., nemo-canary-180m[-flash]-...) -> treat as Transducer family
                if (ContainsAnyKeyword(lowerModelID, nemo_canary_keywords))
                { return isOnline ? SpeechRecognitionModelType.Online_Transducer : SpeechRecognitionModelType.Offline_Transducer; }

                // Determine architecture type
                if (ContainsAnyKeyword(lowerModelID, ctc_keywords))
                {
                    // Generic CTC models (non-Nemo)
                    return isOnline ? SpeechRecognitionModelType.Online_Ctc : SpeechRecognitionModelType.Offline_ZipformerCtc;
                }
                else if (ContainsAnyKeyword(lowerModelID, transducer_keywords))
                {
                    return isOnline ? SpeechRecognitionModelType.Online_Transducer : SpeechRecognitionModelType.Offline_Transducer;
                }
                else if (ContainsAnyKeyword(lowerModelID, paraformer_keywords))
                {
                    return isOnline ? SpeechRecognitionModelType.Online_Paraformer : SpeechRecognitionModelType.Offline_Paraformer;
                }

                return SpeechRecognitionModelType.None;
            }

            internal static VoiceActivityDetectionModelType GetVoiceActivityDetectionModelType(string modelID)
            {
                if (string.IsNullOrEmpty(modelID))
                { return VoiceActivityDetectionModelType.None; }

                string lowerModelID = modelID.ToLowerInvariant();

                // Check for special models first (they have unique identification)
                if (ContainsAnyKeyword(lowerModelID, silero_keywords))
                { return VoiceActivityDetectionModelType.SileroVad; }
                else if (ContainsAnyKeyword(lowerModelID, ten_keywords))
                { return VoiceActivityDetectionModelType.TenVad; }
                return VoiceActivityDetectionModelType.None;
            }

            internal static SpeechSynthesisModelType GetSpeechSynthesisModelType(string modelID)
            {
                if (string.IsNullOrEmpty(modelID))
                { return SpeechSynthesisModelType.None; }

                string lowerModelID = modelID.ToLower();

                // Check for special models first (they have unique identification)
                if (ContainsAnyKeyword(lowerModelID, vits_keywords))
                { return SpeechSynthesisModelType.Vits; }
                else if (ContainsAnyKeyword(lowerModelID, matcha_keywords))
                { return SpeechSynthesisModelType.Matcha; }
                else if (ContainsAnyKeyword(lowerModelID, kokoro_keywords))
                { return SpeechSynthesisModelType.Kokoro; }
                else if (ContainsAnyKeyword(lowerModelID, kitten_keywords))
                { return SpeechSynthesisModelType.KittenTTS; }
                else if (ContainsAnyKeyword(lowerModelID, zipvoice_keywords))
                { return SpeechSynthesisModelType.ZipVoice; }

                return SpeechSynthesisModelType.None;

            }
            internal static SpokenLanguageIdentificationModelType GetSpokenLanguageIdentificationModelType(string modelID)
            {
                if (string.IsNullOrEmpty(modelID))
                { return SpokenLanguageIdentificationModelType.None; }

                string lowerModelID = modelID.ToLowerInvariant();

                if (ContainsAnyKeyword(lowerModelID, spoken_language_id_keywords))
                { return SpokenLanguageIdentificationModelType.Whisper; }

                return SpokenLanguageIdentificationModelType.None;
            }

            internal static AudioTaggingModelType GetAudioTaggingModelType(string modelID)
            {
                if (string.IsNullOrEmpty(modelID))
                {
                    return AudioTaggingModelType.None;
                }
                string lowerModelID = modelID.ToLowerInvariant();
                if (ContainsAnyKeyword(lowerModelID, audio_tagging_ced_keywords, true))
                {
                    return AudioTaggingModelType.Ced;
                }
                else if (ContainsAnyKeyword(lowerModelID, audio_tagging_zipformer_keywords, true))
                {
                    return AudioTaggingModelType.Zipformer;
                }
                return AudioTaggingModelType.None;

            }

            public static bool IsOnlineModel(string modelID)
            {
                if (string.IsNullOrEmpty(modelID))
                { return false; }
                modelID = modelID.ToLowerInvariant();

                SpeechRecognitionModelType type = GetSpeechRecognitionModelType(modelID);
                switch (type)
                {
                    case SpeechRecognitionModelType.Online_Transducer:
                    case SpeechRecognitionModelType.Online_Paraformer:
                    case SpeechRecognitionModelType.Online_Ctc:
                        return true;
                    case SpeechRecognitionModelType.None:
                        return ContainsAnyKeyword(modelID, online_streaming_keywords); ;
                    default:
                        return false;
                }

            }

            internal static bool IsPunctuationModel(string modelID)
            {
                if (string.IsNullOrEmpty(modelID))
                { return false; }

                string lowerModelID = modelID.ToLower();
                return ContainsAnyKeyword(lowerModelID, punctuation_keywords);
            }

            internal static bool IsKeywordSpottingModel(string modelID)
            {
                if (string.IsNullOrEmpty(modelID))
                { return false; }

                string lowerModelID = modelID.ToLower();
                return ContainsAnyKeyword(lowerModelID, kws_keywords);
            }

            internal static bool IsSpeechEnhancementModel(string modelID)
            {
                if (string.IsNullOrEmpty(modelID))
                { return false; }

                string lowerModelID = modelID.ToLower();
                return ContainsAnyKeyword(lowerModelID, speechEnhancement_keywords);
            }

            /// <summary>
            /// Helper method to check if a model ID contains any of the specified keywords as distinct segments
            /// A segment boundary is any non-alphanumeric character (e.g., '-', '_', '.') or string boundaries.
            /// </summary>
            private static bool ContainsAnyKeyword(string modelID, string[] keywords, bool needMatchAll = false)
            {
                if (needMatchAll)
                {
                    return keywords.All(keyword => ContainsSegment(modelID, keyword));
                }
                else
                {
                    return keywords.Any(keyword => ContainsSegment(modelID, keyword));
                }
            }

            /// <summary>
            /// Returns true if `keyword` appears in `text` delimited by non-alphanumeric boundaries.
            /// Example: "zipformer" matches in "sherpa-onnx-zipformer-en" but not inside "foozipformerx".
            /// </summary>
            private static bool ContainsSegment(string text, string keyword)
            {
                if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(keyword))
                {
                    return false;
                }

                int index = text.IndexOf(keyword, StringComparison.Ordinal);
                while (index >= 0)
                {
                    bool leftOk = index == 0 || !char.IsLetterOrDigit(text[index - 1]);
                    int end = index + keyword.Length;
                    bool rightOk = end == text.Length || !char.IsLetterOrDigit(text[end]);
                    if (leftOk && rightOk)
                    {
                        return true;
                    }


                    index = text.IndexOf(keyword, index + 1, StringComparison.Ordinal);
                }
                return false;
            }

            /// <summary>
            /// Get the keywords that identify a specific model type
            /// </summary>
            public static string[] GetModelTypeKeywords(SpeechRecognitionModelType modelType)
            {
                switch (modelType)
                {
                    case SpeechRecognitionModelType.Online_Transducer:
                    case SpeechRecognitionModelType.Offline_Transducer:
                        return transducer_keywords
                            .Concat(nemo_parakeet_tdt_keywords)
                            .Concat(nemo_canary_keywords)
                            .ToArray();
                    case SpeechRecognitionModelType.Online_Ctc:
                        return ctc_keywords;
                    case SpeechRecognitionModelType.Offline_Nemo_Ctc:
                        return nemo_ctc_keywords
                            .Concat(nemo_parakeet_tdt_ctc_keywords)
                            .ToArray();
                    case SpeechRecognitionModelType.Online_Paraformer:
                    case SpeechRecognitionModelType.Offline_Paraformer:
                        return paraformer_keywords;
                    case SpeechRecognitionModelType.Whisper:
                        return whisper_keywords;
                    case SpeechRecognitionModelType.Moonshine:
                        return moonshine_keywords;
                    case SpeechRecognitionModelType.SenseVoice:
                        return sensevoice_keywords;
                    case SpeechRecognitionModelType.FireRedAsr:
                        return fireredasr_keywords;
                    case SpeechRecognitionModelType.Dolphin:
                        return dolphin_keywords;
                    case SpeechRecognitionModelType.TeleSpeech:
                        return telespeech_keywords;
                    case SpeechRecognitionModelType.Omnilingual:
                        return omnilingual_keywords;
                    default:
                        return new string[0];
                }
            }


            #endregion
        }

    }
}
