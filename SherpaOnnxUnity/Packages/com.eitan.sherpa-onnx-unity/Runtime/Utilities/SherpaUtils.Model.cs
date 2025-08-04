using System.Linq;
namespace Eitan.SherpaOnnxUnity.Runtime.Utilities
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
            private static readonly string[] tdnn_keywords = { "tdnn" };
            private static readonly string[] paraformer_keywords = { "paraformer" };
            private static readonly string[] whisper_keywords = { "whisper" };
            private static readonly string[] moonshine_keywords = { "moonshine" };
            private static readonly string[] sensevoice_keywords = { "sense-voice" };
            private static readonly string[] fireredasr_keywords = { "fire-red-asr" };
            private static readonly string[] dolphin_keywords = { "dolphin" };
            private static readonly string[] telespeech_keywords = { "telespeech" };

            // Special model keywords that take precedence
            #endregion

            #region VoiceActivityDetectionModelKeywords
            private static readonly string[] silero_keywords = { "silero" };
            private static readonly string[] ten_keywords = { "ten" };
            #endregion

            #region SpeechSynthesisModelKeywords
            
            private static readonly string[] vits_keywords = { "vits" };
            private static readonly string[] matcha_keywords = { "matcha","vocos" };
            private static readonly string[] kokoro_keywords = { "kokoro" };
            #endregion

            #endregion
            #region Methods

            public static SherpaOnnxModuleType GetModuleTypeByModelId(string modelID)
            {
                if (GetSpeechRecognitionModelType(modelID) != SpeechRecognitionModelType.None)
                { return SherpaOnnxModuleType.SpeechRecognition; }
                else if (GetVoiceActivityDetectionModelType(modelID) != VoiceActivityDetectionModelType.None)
                { return SherpaOnnxModuleType.VoiceActivityDetection; }
                else if (GetSpeechSynthesisModelType(modelID) != SpeechSynthesisModelType.None)
                {return SherpaOnnxModuleType.SpeechSynthesis;}
                return SherpaOnnxModuleType.Undefined;
            }

            public static SpeechRecognitionModelType GetSpeechRecognitionModelType(string modelID)
            {
                if (string.IsNullOrEmpty(modelID))
                { return SpeechRecognitionModelType.None; }

                string lowerModelID = modelID.ToLower();

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
                else if (ContainsAnyKeyword(lowerModelID, telespeech_keywords))
                { return SpeechRecognitionModelType.Offline_ZipformerCtc; }
                else if (ContainsAnyKeyword(lowerModelID, nemo_ctc_keywords))
                { return SpeechRecognitionModelType.Offline_Nemo_Ctc; }
                else if (ContainsAnyKeyword(lowerModelID, tdnn_keywords))
                { return SpeechRecognitionModelType.Tdnn; }


                // Check if it's an online model
                bool isOnline = ContainsAnyKeyword(lowerModelID, online_streaming_keywords);

                // Determine architecture type

                if (ContainsAnyKeyword(lowerModelID, ctc_keywords))
                {
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

            public static VoiceActivityDetectionModelType GetVoiceActivityDetectionModelType(string modelID)
            {
                if (string.IsNullOrEmpty(modelID))
                { return VoiceActivityDetectionModelType.None; }

                string lowerModelID = modelID.ToLower();

                // Check for special models first (they have unique identification)
                if (ContainsAnyKeyword(lowerModelID, silero_keywords))
                { return VoiceActivityDetectionModelType.SileroVad; }
                else if (ContainsAnyKeyword(lowerModelID, ten_keywords))
                { return VoiceActivityDetectionModelType.TenVad; }
                return VoiceActivityDetectionModelType.None;
            }

            public static SpeechSynthesisModelType GetSpeechSynthesisModelType(string modelID)
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

                return SpeechSynthesisModelType.None;
                
            }
            public static bool IsOnlineModel(string modelID)
            {
                if (string.IsNullOrEmpty(modelID))
                { return false; }

                SpeechRecognitionModelType type = GetSpeechRecognitionModelType(modelID);
                switch (type)
                {
                    case SpeechRecognitionModelType.Online_Transducer:
                    case SpeechRecognitionModelType.Online_Paraformer:
                    case SpeechRecognitionModelType.Online_Ctc:
                        return true;
                    default:
                        return false;
                }
            }

            /// <summary>
            /// Helper method to check if a model ID contains any of the specified keywords
            /// </summary>
            private static bool ContainsAnyKeyword(string modelID, string[] keywords)
            {
                return keywords.Any(keyword => modelID.Contains(keyword));
            }

            /// <summary>
            /// Get all supported model architecture types that can be detected
            /// </summary>
            public static string[] GetSupportedModelTypes()
            {
                return new string[]
                {
            "Online Transducer (streaming + zipformer/conformer/transducer)",
            "Online CTC (streaming + ctc)",
            "Online Paraformer (streaming + paraformer)",
            "Offline Transducer (zipformer/conformer/transducer)",
            "Offline CTC (ctc)",
            "Offline Paraformer (paraformer)",
            "Whisper (whisper)",
            "Moonshine (moonshine)",
            "SenseVoice (sense-voice)",
            "FireRedAsr (fire-red-asr)",
            "Dolphin (dolphin)",
            "TeleSpeech (telespeech)"
                };
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
                        return transducer_keywords;
                    case SpeechRecognitionModelType.Online_Ctc:
                        return ctc_keywords;
                    case SpeechRecognitionModelType.Offline_Nemo_Ctc:
                        return nemo_ctc_keywords;
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
                    default:
                        return new string[0];
                }
            }
            #endregion
        }

    }
}