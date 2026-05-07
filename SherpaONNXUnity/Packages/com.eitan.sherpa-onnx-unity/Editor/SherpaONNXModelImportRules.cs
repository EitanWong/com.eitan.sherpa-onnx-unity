#if UNITY_EDITOR

namespace Eitan.SherpaONNXUnity.Editor
{
    using System;
    using System.Collections.Generic;
    using Eitan.SherpaONNXUnity.Runtime;
    using Eitan.SherpaONNXUnity.Runtime.Utilities;

    internal static class SherpaONNXModelImportRules
    {
        private sealed class BindingRequirement
        {
            public BindingRequirement(string warningMessage, params SherpaONNXModelFileKey[][] alternatives)
            {
                WarningMessage = warningMessage ?? string.Empty;
                Alternatives = alternatives ?? Array.Empty<SherpaONNXModelFileKey[]>();
            }

            public string WarningMessage { get; }
            public SherpaONNXModelFileKey[][] Alternatives { get; }

            public bool IsSatisfied(Func<SherpaONNXModelFileKey, bool> hasBinding)
            {
                if (hasBinding == null)
                {
                    return false;
                }

                for (int i = 0; i < Alternatives.Length; i++)
                {
                    var alternative = Alternatives[i];
                    if (alternative == null || alternative.Length == 0)
                    {
                        continue;
                    }

                    var satisfied = true;
                    for (int j = 0; j < alternative.Length; j++)
                    {
                        if (!hasBinding(alternative[j]))
                        {
                            satisfied = false;
                            break;
                        }
                    }

                    if (satisfied)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        private sealed class ImportRule
        {
            public ImportRule(
                SherpaONNXModuleType moduleType,
                string modelTypeHint,
                params BindingRequirement[] requirements)
            {
                ModuleType = moduleType;
                ModelTypeHint = modelTypeHint ?? string.Empty;
                Requirements = requirements ?? Array.Empty<BindingRequirement>();
            }

            public SherpaONNXModuleType ModuleType { get; }
            public string ModelTypeHint { get; }
            public BindingRequirement[] Requirements { get; }
        }

        private static readonly Dictionary<SherpaONNXModuleType, ImportRule> s_DefaultRules =
            new Dictionary<SherpaONNXModuleType, ImportRule>
            {
                {
                    SherpaONNXModuleType.SpeechRecognition,
                    new ImportRule(
                        SherpaONNXModuleType.SpeechRecognition,
                        string.Empty,
                        Require("Model file (.onnx) not detected. Please bind it manually.", SherpaONNXModelFileKey.Model),
                        Require("Tokens file not detected. Please bind tokens.txt manually if required.", SherpaONNXModelFileKey.Tokens))
                },
                {
                    SherpaONNXModuleType.VoiceActivityDetection,
                    new ImportRule(
                        SherpaONNXModuleType.VoiceActivityDetection,
                        string.Empty,
                        RequireAny(
                            "Model file (.onnx) not detected. Please bind it manually.",
                            new[] { SherpaONNXModelFileKey.SileroVad },
                            new[] { SherpaONNXModelFileKey.TenVad },
                            new[] { SherpaONNXModelFileKey.Model }))
                },
                {
                    SherpaONNXModuleType.SpeechSynthesis,
                    new ImportRule(
                        SherpaONNXModuleType.SpeechSynthesis,
                        string.Empty,
                        Require("Model file (.onnx) not detected. Please bind it manually.", SherpaONNXModelFileKey.Model))
                },
                {
                    SherpaONNXModuleType.SpokenLanguageIdentification,
                    new ImportRule(
                        SherpaONNXModuleType.SpokenLanguageIdentification,
                        string.Empty,
                        Require("Whisper encoder not detected. Please bind it manually.", SherpaONNXModelFileKey.Encoder),
                        Require("Whisper decoder not detected. Please bind it manually.", SherpaONNXModelFileKey.Decoder))
                },
                {
                    SherpaONNXModuleType.AudioTagging,
                    new ImportRule(
                        SherpaONNXModuleType.AudioTagging,
                        string.Empty,
                        RequireAny(
                            "Model file (.onnx) not detected. Please bind it manually.",
                            new[] { SherpaONNXModelFileKey.Ced },
                            new[] { SherpaONNXModelFileKey.Zipformer },
                            new[] { SherpaONNXModelFileKey.Model }),
                        Require("Labels file not detected. Please bind it manually.", SherpaONNXModelFileKey.Labels))
                },
            };

        private static readonly Dictionary<string, ImportRule> s_SpecificRules =
            new Dictionary<string, ImportRule>(StringComparer.OrdinalIgnoreCase)
            {
                {
                    nameof(SpeechRecognitionModelType.Offline_Transducer),
                    new ImportRule(
                        SherpaONNXModuleType.SpeechRecognition,
                        nameof(SpeechRecognitionModelType.Offline_Transducer),
                        Require("Transducer encoder not detected. Please bind it manually.", SherpaONNXModelFileKey.Encoder),
                        Require("Transducer decoder not detected. Please bind it manually.", SherpaONNXModelFileKey.Decoder),
                        Require("Transducer joiner not detected. Please bind it manually.", SherpaONNXModelFileKey.Joiner),
                        Require("Tokens file not detected. Please bind tokens.txt manually if required.", SherpaONNXModelFileKey.Tokens))
                },
                {
                    nameof(SpeechRecognitionModelType.Offline_Paraformer),
                    new ImportRule(
                        SherpaONNXModuleType.SpeechRecognition,
                        nameof(SpeechRecognitionModelType.Offline_Paraformer),
                        Require("Paraformer model not detected. Please bind it manually.", SherpaONNXModelFileKey.Model),
                        Require("Tokens file not detected. Please bind tokens.txt manually if required.", SherpaONNXModelFileKey.Tokens))
                },
                {
                    nameof(SpeechRecognitionModelType.Offline_ZipformerCtc),
                    new ImportRule(
                        SherpaONNXModuleType.SpeechRecognition,
                        nameof(SpeechRecognitionModelType.Offline_ZipformerCtc),
                        RequireAny(
                            "Zipformer CTC model not detected. Please bind it manually.",
                            new[] { SherpaONNXModelFileKey.Zipformer },
                            new[] { SherpaONNXModelFileKey.Model }),
                        Require("Tokens file not detected. Please bind tokens.txt manually if required.", SherpaONNXModelFileKey.Tokens))
                },
                {
                    nameof(SpeechRecognitionModelType.Offline_Nemo_Ctc),
                    new ImportRule(
                        SherpaONNXModuleType.SpeechRecognition,
                        nameof(SpeechRecognitionModelType.Offline_Nemo_Ctc),
                        Require("NeMo CTC model not detected. Please bind it manually.", SherpaONNXModelFileKey.Model),
                        Require("Tokens file not detected. Please bind tokens.txt manually if required.", SherpaONNXModelFileKey.Tokens))
                },
                {
                    nameof(SpeechRecognitionModelType.Whisper),
                    new ImportRule(
                        SherpaONNXModuleType.SpeechRecognition,
                        nameof(SpeechRecognitionModelType.Whisper),
                        Require("Whisper encoder not detected. Please bind it manually.", SherpaONNXModelFileKey.Encoder),
                        Require("Whisper decoder not detected. Please bind it manually.", SherpaONNXModelFileKey.Decoder),
                        Require("Tokens file not detected. Please bind tokens.txt manually if required.", SherpaONNXModelFileKey.Tokens))
                },
                {
                    nameof(SpeechRecognitionModelType.Moonshine),
                    new ImportRule(
                        SherpaONNXModuleType.SpeechRecognition,
                        nameof(SpeechRecognitionModelType.Moonshine),
                        Require("Moonshine encoder not detected. Please bind it manually.", SherpaONNXModelFileKey.Encoder),
                        new BindingRequirement(
                            "Moonshine decoder files not detected. Please bind merged_decoder or preprocessor + cached/uncached decoders manually.",
                            new[] { SherpaONNXModelFileKey.Decoder },
                            new[]
                            {
                                SherpaONNXModelFileKey.Preprocessor,
                                SherpaONNXModelFileKey.CachedDecoder,
                                SherpaONNXModelFileKey.UncachedDecoder
                            }),
                        Require("Tokens file not detected. Please bind tokens.txt manually if required.", SherpaONNXModelFileKey.Tokens))
                },
                {
                    nameof(SpeechRecognitionModelType.SenseVoice),
                    new ImportRule(
                        SherpaONNXModuleType.SpeechRecognition,
                        nameof(SpeechRecognitionModelType.SenseVoice),
                        Require("SenseVoice model not detected. Please bind it manually.", SherpaONNXModelFileKey.Model),
                        Require("Tokens file not detected. Please bind tokens.txt manually if required.", SherpaONNXModelFileKey.Tokens))
                },
                {
                    nameof(SpeechRecognitionModelType.FireRedAsr),
                    new ImportRule(
                        SherpaONNXModuleType.SpeechRecognition,
                        nameof(SpeechRecognitionModelType.FireRedAsr),
                        Require("FireRed ASR encoder not detected. Please bind it manually.", SherpaONNXModelFileKey.Encoder),
                        Require("FireRed ASR decoder not detected. Please bind it manually.", SherpaONNXModelFileKey.Decoder),
                        Require("Tokens file not detected. Please bind tokens.txt manually if required.", SherpaONNXModelFileKey.Tokens))
                },
                {
                    nameof(SpeechRecognitionModelType.Offline_FireRedAsrCtc),
                    new ImportRule(
                        SherpaONNXModuleType.SpeechRecognition,
                        nameof(SpeechRecognitionModelType.Offline_FireRedAsrCtc),
                        Require("FireRed ASR CTC model not detected. Please bind it manually.", SherpaONNXModelFileKey.Model),
                        Require("Tokens file not detected. Please bind tokens.txt manually if required.", SherpaONNXModelFileKey.Tokens))
                },
                {
                    nameof(SpeechRecognitionModelType.Offline_Canary),
                    new ImportRule(
                        SherpaONNXModuleType.SpeechRecognition,
                        nameof(SpeechRecognitionModelType.Offline_Canary),
                        Require("Canary encoder not detected. Please bind it manually.", SherpaONNXModelFileKey.Encoder),
                        Require("Canary decoder not detected. Please bind it manually.", SherpaONNXModelFileKey.Decoder),
                        Require("Tokens file not detected. Please bind tokens.txt manually if required.", SherpaONNXModelFileKey.Tokens))
                },
                {
                    nameof(SpeechRecognitionModelType.Omnilingual),
                    new ImportRule(
                        SherpaONNXModuleType.SpeechRecognition,
                        nameof(SpeechRecognitionModelType.Omnilingual),
                        Require("Omnilingual model not detected. Please bind it manually.", SherpaONNXModelFileKey.Model),
                        Require("Tokens file not detected. Please bind tokens.txt manually if required.", SherpaONNXModelFileKey.Tokens))
                },
                {
                    nameof(SpeechRecognitionModelType.Offline_WenetCtc),
                    new ImportRule(
                        SherpaONNXModuleType.SpeechRecognition,
                        nameof(SpeechRecognitionModelType.Offline_WenetCtc),
                        Require("Wenet CTC model not detected. Please bind it manually.", SherpaONNXModelFileKey.Model),
                        Require("Tokens file not detected. Please bind tokens.txt manually if required.", SherpaONNXModelFileKey.Tokens))
                },
                {
                    nameof(SpeechRecognitionModelType.Offline_MedAsrCtc),
                    new ImportRule(
                        SherpaONNXModuleType.SpeechRecognition,
                        nameof(SpeechRecognitionModelType.Offline_MedAsrCtc),
                        Require("Med ASR CTC model not detected. Please bind it manually.", SherpaONNXModelFileKey.Model),
                        Require("Tokens file not detected. Please bind tokens.txt manually if required.", SherpaONNXModelFileKey.Tokens))
                },
                {
                    nameof(SpeechRecognitionModelType.Offline_FunAsrNano),
                    new ImportRule(
                        SherpaONNXModuleType.SpeechRecognition,
                        nameof(SpeechRecognitionModelType.Offline_FunAsrNano),
                        Require("FunASR Nano encoder adaptor not detected. Please bind it manually.", SherpaONNXModelFileKey.EncoderAdaptor),
                        Require("FunASR Nano LLM not detected. Please bind it manually.", SherpaONNXModelFileKey.Llm),
                        Require("FunASR Nano embedding not detected. Please bind it manually.", SherpaONNXModelFileKey.Embedding),
                        Require("FunASR Nano tokenizer directory not detected. Please bind it manually.", SherpaONNXModelFileKey.Tokenizer))
                },
                {
                    nameof(SpeechRecognitionModelType.Offline_Qwen3Asr),
                    new ImportRule(
                        SherpaONNXModuleType.SpeechRecognition,
                        nameof(SpeechRecognitionModelType.Offline_Qwen3Asr),
                        Require("Qwen3 ASR conv_frontend.onnx not detected. Please bind it manually.", SherpaONNXModelFileKey.ConvFrontend),
                        Require("Qwen3 ASR encoder not detected. Please bind it manually.", SherpaONNXModelFileKey.Encoder),
                        Require("Qwen3 ASR decoder not detected. Please bind it manually.", SherpaONNXModelFileKey.Decoder),
                        Require("Qwen3 ASR tokenizer directory not detected. Please bind it manually.", SherpaONNXModelFileKey.Tokenizer))
                },
                {
                   nameof(SpeechRecognitionModelType.Offline_CohereTranscribe),
                   new ImportRule(
                    SherpaONNXModuleType.SpeechRecognition,
                    nameof(SpeechRecognitionModelType.Offline_CohereTranscribe),
                    Require("Cohere Transcribe encoder not detected. Please bind it manually.", SherpaONNXModelFileKey.Encoder),
                    Require("Cohere Transcribe decoder not detected. Please bind it manually.", SherpaONNXModelFileKey.Decoder)
                   )
                }
            };

        public static string InferModelTypeHint(SherpaONNXModuleType moduleType, string modelId)
        {
            if (string.IsNullOrWhiteSpace(modelId))
            {
                return string.Empty;
            }

            switch (moduleType)
            {
                case SherpaONNXModuleType.SpeechRecognition:
                    {
                        var type = SherpaUtils.Model.GetSpeechRecognitionModelType(modelId);
                        return type == SpeechRecognitionModelType.None ? string.Empty : type.ToString();
                    }
                case SherpaONNXModuleType.SpeechSynthesis:
                    {
                        var type = SherpaUtils.Model.GetSpeechSynthesisModelType(modelId);
                        return type == SpeechSynthesisModelType.None ? string.Empty : type.ToString();
                    }
                case SherpaONNXModuleType.VoiceActivityDetection:
                    {
                        var type = SherpaUtils.Model.GetVoiceActivityDetectionModelType(modelId);
                        return type == VoiceActivityDetectionModelType.None ? string.Empty : type.ToString();
                    }
                case SherpaONNXModuleType.SpokenLanguageIdentification:
                    {
                        var type = SherpaUtils.Model.GetSpokenLanguageIdentificationModelType(modelId);
                        return type == SpokenLanguageIdentificationModelType.None ? string.Empty : type.ToString();
                    }
                case SherpaONNXModuleType.AudioTagging:
                    {
                        var type = SherpaUtils.Model.GetAudioTaggingModelType(modelId);
                        return type == AudioTaggingModelType.None ? string.Empty : type.ToString();
                    }
                default:
                    return string.Empty;
            }
        }

        public static void AppendImportWarnings(
            SherpaONNXModuleType moduleType,
            string modelTypeHint,
            Func<SherpaONNXModelFileKey, bool> hasBinding,
            List<string> warnings)
        {
            if (warnings == null || hasBinding == null)
            {
                return;
            }

            ImportRule rule = null;
            if (!string.IsNullOrWhiteSpace(modelTypeHint))
            {
                s_SpecificRules.TryGetValue(modelTypeHint.Trim(), out rule);
            }

            if (rule == null)
            {
                s_DefaultRules.TryGetValue(moduleType, out rule);
            }

            if (rule == null || rule.Requirements == null)
            {
                return;
            }

            for (int i = 0; i < rule.Requirements.Length; i++)
            {
                var requirement = rule.Requirements[i];
                if (requirement == null || requirement.IsSatisfied(hasBinding))
                {
                    continue;
                }

                warnings.Add(requirement.WarningMessage);
            }
        }

        public static SherpaONNXModelFileKey MapBindingKey(string name, string relativePath, bool isDirectory)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return SherpaONNXModelFileKey.None;
            }

            var lowerName = name.ToLowerInvariant();
            var lowerPath = (relativePath ?? string.Empty).ToLowerInvariant();

            if (isDirectory)
            {
                if (lowerName.Contains("dict")) return SherpaONNXModelFileKey.DictDir;
                if (lowerName.Contains("espeak-ng-data") || lowerName == "data") return SherpaONNXModelFileKey.DataDir;
                if (lowerName.Contains("tokenizer") || lowerName.Contains("qwen")) return SherpaONNXModelFileKey.Tokenizer;
                if (lowerName.Contains("voices")) return SherpaONNXModelFileKey.Voices;
                return SherpaONNXModelFileKey.None;
            }

            var ext = System.IO.Path.GetExtension(lowerName);
            if (ext == ".fst") return SherpaONNXModelFileKey.RuleFsts;
            if (ext == ".far") return SherpaONNXModelFileKey.RuleFars;

            if (lowerName.Contains("tokens")) return SherpaONNXModelFileKey.Tokens;
            if (lowerName.Contains("lexicon")) return SherpaONNXModelFileKey.Lexicon;
            if (lowerName.Contains("vocos") || lowerName.Contains("vocoder")) return SherpaONNXModelFileKey.Vocoder;
            if (lowerName.Contains("acoustic") || lowerName.Contains("matcha")) return SherpaONNXModelFileKey.AcousticModel;
            if (lowerName.Contains("fm") || lowerName.Contains("flow")) return SherpaONNXModelFileKey.FlowMatchingModel;
            if (lowerName.Contains("text")) return SherpaONNXModelFileKey.TextModel;
            if (lowerName.Contains("conv_frontend") || lowerPath.Contains("conv_frontend")) return SherpaONNXModelFileKey.ConvFrontend;
            if ((lowerName.Contains("conv") && lowerName.Contains("frontend")) || (lowerPath.Contains("conv") && lowerPath.Contains("frontend"))) return SherpaONNXModelFileKey.ConvFrontend;
            if (lowerName.Contains("preprocess")) return SherpaONNXModelFileKey.Preprocessor;
            if (lowerName.Contains("cached")) return SherpaONNXModelFileKey.CachedDecoder;
            if (lowerName.Contains("uncached")) return SherpaONNXModelFileKey.UncachedDecoder;
            if (lowerName.Contains("embedding")) return SherpaONNXModelFileKey.Embedding;
            if (lowerName.Contains("tokenizer")) return SherpaONNXModelFileKey.Tokenizer;
            if (lowerName.Contains("llm")) return SherpaONNXModelFileKey.Llm;
            if (lowerName.Contains("adaptor") || lowerName.Contains("adapter")) return SherpaONNXModelFileKey.EncoderAdaptor;
            if (lowerName.Contains("labels")) return SherpaONNXModelFileKey.Labels;
            if (lowerName.Contains("keywords")) return SherpaONNXModelFileKey.Keywords;
            if (lowerName.Contains("hotwords")) return SherpaONNXModelFileKey.Hotwords;
            if (lowerName.Contains("pinyin")) return SherpaONNXModelFileKey.Pinyin;
            if (lowerName.Contains("silero")) return SherpaONNXModelFileKey.SileroVad;
            if (lowerName.Contains("ten")) return SherpaONNXModelFileKey.TenVad;
            if (lowerName.Contains("tdnn")) return SherpaONNXModelFileKey.Tdnn;
            if (lowerName.Contains("gtcrn")) return SherpaONNXModelFileKey.Gtcrn;
            if (lowerName.Contains("ced")) return SherpaONNXModelFileKey.Ced;
            if (lowerName.Contains("zipformer")) return SherpaONNXModelFileKey.Zipformer;
            if (lowerName.Contains("encoder") || lowerName.Contains("encode")) return SherpaONNXModelFileKey.Encoder;
            if (lowerName.Contains("decoder")) return SherpaONNXModelFileKey.Decoder;
            if (lowerName.Contains("joiner")) return SherpaONNXModelFileKey.Joiner;

            if (ext == ".onnx") return SherpaONNXModelFileKey.Model;

            return SherpaONNXModelFileKey.None;
        }

        private static BindingRequirement Require(string warningMessage, params SherpaONNXModelFileKey[] keys)
        {
            return new BindingRequirement(warningMessage, keys);
        }

        private static BindingRequirement RequireAny(string warningMessage, params SherpaONNXModelFileKey[][] alternatives)
        {
            return new BindingRequirement(warningMessage, alternatives);
        }
    }
}

#endif
