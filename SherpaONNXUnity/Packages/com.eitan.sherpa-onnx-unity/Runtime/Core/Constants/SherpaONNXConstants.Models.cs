namespace Eitan.SherpaONNXUnity.Runtime.Constants
{
    public partial class SherpaONNXConstants
    {
        public class Models
        {
            public static readonly SherpaONNXModelMetadata[] ASR_MODELS_METADATA_TABLES = new[]
            {
                //TODO: 补全所有的hash信息
                // online models
                new SherpaONNXModelMetadata
                {
                    modelId = "sherpa-onnx-streaming-zipformer-zh-xlarge-int8-2025-06-30",
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "sherpa-onnx-streaming-zipformer-zh-xlarge-fp16-2025-06-30",
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "sherpa-onnx-streaming-zipformer-zh-int8-2025-06-30",
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "sherpa-onnx-streaming-zipformer-korean-2024-06-16",
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "sherpa-onnx-streaming-zipformer-multi-zh-hans-2023-12-12",
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "icefall-asr-zipformer-streaming-wenetspeech-20230615",
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "sherpa-onnx-streaming-zipformer-en-2023-06-26",
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "sherpa-onnx-streaming-zipformer-en-2023-06-21",
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "sherpa-onnx-streaming-zipformer-en-2023-02-21",
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "sherpa-onnx-streaming-zipformer-bilingual-zh-en-2023-02-20",
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "sherpa-onnx-streaming-zipformer-fr-2023-04-14",
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "sherpa-onnx-streaming-zipformer-small-bilingual-zh-en-2023-02-16",
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "sherpa-onnx-streaming-zipformer-zh-14M-2023-02-23",
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "sherpa-onnx-streaming-zipformer-en-20M-2023-02-17",
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "sherpa-onnx-streaming-zipformer-small-ctc-zh-int8-2025-04-01",
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "sherpa-onnx-streaming-zipformer-small-ctc-zh-2025-04-01",
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "sherpa-onnx-streaming-zipformer-ctc-multi-zh-hans-2023-12-13",
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "sherpa-onnx-streaming-paraformer-bilingual-zh-en",
                    downloadFileHash =
                        "5462a1fce42693deae572af1e8c4687124b12aa85fe61ff4d3168bb5280e205f",
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "sherpa-onnx-streaming-paraformer-trilingual-zh-cantonese-en",
                },
                //offline models
                new SherpaONNXModelMetadata
                {
                    modelId = "sherpa-onnx-zipformer-ctc-zh-int8-2025-07-03",
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "sherpa-onnx-zipformer-ctc-zh-fp16-2025-07-03",
                },
                new SherpaONNXModelMetadata { modelId = "sherpa-onnx-zipformer-ctc-zh-2025-07-03" },
                new SherpaONNXModelMetadata { modelId = "sherpa-onnx-zipformer-vi-2025-04-20" },
                new SherpaONNXModelMetadata
                {
                    modelId = "sherpa-onnx-zipformer-vi-int8-2025-04-20",
                },
                new SherpaONNXModelMetadata { modelId = "sherpa-onnx-zipformer-zh-en-2023-11-22" },
                new SherpaONNXModelMetadata { modelId = "sherpa-onnx-zipformer-ru-2024-09-18" },
                new SherpaONNXModelMetadata
                {
                    modelId = "sherpa-onnx-small-zipformer-ru-2024-09-18",
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "sherpa-onnx-zipformer-ja-reazonspeech-2024-08-01",
                },
                new SherpaONNXModelMetadata { modelId = "sherpa-onnx-zipformer-korean-2024-06-24" },
                new SherpaONNXModelMetadata { modelId = "sherpa-onnx-zipformer-thai-2024-06-20" },
                new SherpaONNXModelMetadata
                {
                    modelId = "sherpa-onnx-zipformer-cantonese-2024-03-13",
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "sherpa-onnx-zipformer-gigaspeech-2023-12-12",
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "sherpa-onnx-zipformer-multi-zh-hans-2023-9-2",
                },
                new SherpaONNXModelMetadata
                {
                    modelId =
                        "icefall-asr-cv-corpus-13.0-2023-03-09-en-pruned-transducer-stateless7-2023-04-17",
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "icefall-asr-zipformer-wenetspeech-20230615",
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "sherpa-onnx-zipformer-large-en-2023-06-26",
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "sherpa-onnx-zipformer-small-en-2023-06-26",
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "icefall-asr-multidataset-pruned_transducer_stateless7-2023-05-04",
                },
                new SherpaONNXModelMetadata { modelId = "sherpa-onnx-zipformer-en-2023-06-26" },
                new SherpaONNXModelMetadata { modelId = "sherpa-onnx-zipformer-en-2023-04-01" },
                new SherpaONNXModelMetadata { modelId = "sherpa-onnx-zipformer-en-2023-03-30" },
                new SherpaONNXModelMetadata
                {
                    modelId = "sherpa-onnx-conformer-zh-stateless2-2023-05-23",
                },
                new SherpaONNXModelMetadata { modelId = "sherpa-onnx-conformer-zh-2023-05-23" },
                new SherpaONNXModelMetadata { modelId = "sherpa-onnx-conformer-en-2023-03-18" },
                new SherpaONNXModelMetadata
                {
                    modelId = "sherpa-onnx-nemo-parakeet-tdt-0.6b-v2-int8",
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "sherpa-onnx-nemo-transducer-giga-am-v2-russian-2025-04-19",
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "sherpa-onnx-nemo-transducer-giga-am-russian-2024-10-24",
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "sherpa-onnx-paraformer-trilingual-zh-cantonese-en",
                },
                new SherpaONNXModelMetadata { modelId = "sherpa-onnx-paraformer-en-2024-03-09" },
                new SherpaONNXModelMetadata
                {
                    modelId = "sherpa-onnx-paraformer-zh-small-2024-03-09",
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "sherpa-onnx-paraformer-zh-2024-03-09",
                    downloadFileHash =
                        "8c6724d0a86bd867217d353db1eaa11f2f143bca446a1f2752e8c551a6f2bde0",
                },
                new SherpaONNXModelMetadata { modelId = "sherpa-onnx-paraformer-zh-2023-03-28" },
                new SherpaONNXModelMetadata { modelId = "sherpa-onnx-paraformer-zh-2023-09-14" },
                new SherpaONNXModelMetadata
                {
                    modelId = "sherpa-onnx-paraformer-zh-int8-2025-10-07",
                    downloadFileHash =
                        "a071ee5419e14adb34d7f970ab98105a45e6608018b168f023ca2e4810744abe",
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "sherpa-onnx-paraformer-zh-2025-10-07",
                    downloadFileHash =
                        "934e70708301ad31cb15d6a31ff843059c1b22f494f2492249febd94049586ef",
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "sherpa-onnx-nemo-parakeet_tdt_ctc_110m-en-36000-int8",
                },
                new SherpaONNXModelMetadata { modelId = "sherpa-onnx-nemo-ctc-en-citrinet-512" },
                new SherpaONNXModelMetadata { modelId = "sherpa-onnx-nemo-ctc-en-conformer-small" },
                new SherpaONNXModelMetadata
                {
                    modelId = "sherpa-onnx-nemo-ctc-en-conformer-medium",
                },
                new SherpaONNXModelMetadata { modelId = "sherpa-onnx-nemo-ctc-en-conformer-large" },
                new SherpaONNXModelMetadata
                {
                    modelId = "sherpa-onnx-nemo-ctc-giga-am-v2-russian-2025-04-19",
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "sherpa-onnx-nemo-ctc-giga-am-russian-2024-10-24",
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "sherpa-onnx-nemo-parakeet-tdt_ctc-0.6b-ja-35000-int8",
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "sherpa-onnx-nemo-canary-180m-flash-en-es-de-fr-int8",
                },
                new SherpaONNXModelMetadata { modelId = "sherpa-onnx-tdnn-yesno" },
                new SherpaONNXModelMetadata
                {
                    modelId = "sherpa-onnx-telespeech-ctc-int8-zh-2024-06-04",
                },
                new SherpaONNXModelMetadata { modelId = "sherpa-onnx-whisper-tiny.en" },
                new SherpaONNXModelMetadata { modelId = "sherpa-onnx-whisper-small.en" },
                new SherpaONNXModelMetadata { modelId = "sherpa-onnx-whisper-medium.en" },
                new SherpaONNXModelMetadata { modelId = "sherpa-onnx-whisper-distil-small.en" },
                new SherpaONNXModelMetadata { modelId = "sherpa-onnx-whisper-tiny" },
                new SherpaONNXModelMetadata { modelId = "sherpa-onnx-whisper-base" },
                new SherpaONNXModelMetadata { modelId = "sherpa-onnx-whisper-small" },
                new SherpaONNXModelMetadata { modelId = "sherpa-onnx-whisper-medium" },
                new SherpaONNXModelMetadata { modelId = "sherpa-onnx-moonshine-tiny-en-int8" },
                new SherpaONNXModelMetadata { modelId = "sherpa-onnx-moonshine-base-en-int8" },
                new SherpaONNXModelMetadata
                {
                    modelId = "sherpa-onnx-sense-voice-zh-en-ja-ko-yue-2024-07-17",
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "sherpa-onnx-sense-voice-zh-en-ja-ko-yue-int8-2025-09-09",
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "sherpa-onnx-fire-red-asr-large-zh_en-2025-02-16",
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "sherpa-onnx-dolphin-base-ctc-multi-lang-int8-2025-04-02",
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "sherpa-onnx-dolphin-base-ctc-multi-lang-2025-04-02",
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "sherpa-onnx-dolphin-small-ctc-multi-lang-int8-2025-04-02",
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "sherpa-onnx-dolphin-small-ctc-multi-lang-2025-04-02",
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "sherpa-onnx-omnilingual-asr-1600-languages-300M-ctc-int8-2025-11-12",
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "sherpa-onnx-omnilingual-asr-1600-languages-300M-ctc-2025-11-12",
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "sherpa-onnx-omnilingual-asr-1600-languages-1B-ctc-int8-2025-11-12",
                },

            };

            public static readonly SherpaONNXModelMetadata[] VAD_MODELS_METADATA_TABLES = new[]
            {
                new SherpaONNXModelMetadata
                {
                    modelId = "silero-vad",
                    downloadUrl =
                        "https://github.com/k2-fsa/sherpa-onnx/releases/download/asr-models/silero_vad.onnx",
                    downloadFileHash =
                        "9e2449e1087496d8d4caba907f23e0bd3f78d91fa552479bb9c23ac09cbb1fd6",
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "silero-vad-int8",
                    downloadUrl =
                        "https://github.com/k2-fsa/sherpa-onnx/releases/download/asr-models/silero_vad.int8.onnx",
                    downloadFileHash =
                        "c36d490aff5ab924ca6c7aeec4d8f6bd3d22db6fa17611b9c5b17eae58ac3a20",
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "silero-vad-v4",
                    downloadUrl =
                        "https://raw.githubusercontent.com/snakers4/silero-vad/refs/tags/v4.0/files/silero_vad.onnx",
                    downloadFileHash =
                        "a35ebf52fd3ce5f1469b2a36158dba761bc47b973ea3382b3186ca15b1f5af28",
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "silero-vad-v5",
                    downloadUrl =
                        "https://github.com/snakers4/silero-vad/raw/refs/tags/v5.0/files/silero_vad.onnx",
                    downloadFileHash =
                        "6b99cbfd39246b6706f98ec13c7c50c6b299181f2474fa05cbc8046acc274396",
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "silero-vad-latest",
                    downloadUrl =
                        "https://github.com/snakers4/silero-vad/raw/refs/heads/master/src/silero_vad/data/silero_vad.onnx",
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "ten-vad",
                    downloadUrl =
                        "https://github.com/k2-fsa/sherpa-onnx/releases/download/asr-models/ten-vad.onnx",
                    downloadFileHash =
                        "718cb7eef47e3cf5ddbe7e967a7503f46b8b469c0706872f494dfa921b486206",
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "ten-vad-int8",
                    downloadUrl =
                        "https://github.com/k2-fsa/sherpa-onnx/releases/download/asr-models/ten-vad.int8.onnx",
                    downloadFileHash =
                        "880c072f188efa169ea028b2159d1b3a438e153d080b87eac31b74ecad511e61",
                },
            };

            //https://github.com/k2-fsa/sherpa-onnx/releases/tag/tts-models
            public static readonly SherpaONNXModelMetadata[] TTS_MODELS_METADATA_TABLES = new[]
            {
                //vits_model
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-melo-tts-zh_en",
                    downloadFileHash =
                        "e58351ed7149f290a54534538badd4077cdbe6fddc964b24d0bee870415d1514",
                    sampleRate = 44100,
                },
                #region  Arabic
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-ar_JO-SA_dii-high",
                    numberOfSpeakers = 1,
                    sampleRate = 22050,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-ar_JO-SA_miro-high",
                    numberOfSpeakers = 1,
                    sampleRate = 22050,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-ar_JO-SA_miro_V2-high",
                    numberOfSpeakers = 1,
                    sampleRate = 22050,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-ar_JO-kareem-low",
                    numberOfSpeakers = 1,
                    sampleRate = 16000,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-ar_JO-kareem-medium",
                    numberOfSpeakers = 1,
                    sampleRate = 22050,
                },
                #endregion

                #region Catalan
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-ca_ES-upc_ona-medium",
                    numberOfSpeakers = 1,
                    sampleRate = 22050,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-ca_ES-upc_ona-x_low",
                    numberOfSpeakers = 1,
                    sampleRate = 16000,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-ca_ES-upc_pau-x_low",
                    numberOfSpeakers = 1,
                    sampleRate = 16000,
                },
                #endregion

                #region Chinese
                new SherpaONNXModelMetadata
                {
                    modelId = "matcha-icefall-zh-baker",
                    downloadFileHash =
                        "d9b417a8f52d481a4c9abd540e6f38b18ded6730f67cbffb7f133e196830e09e",
                    numberOfSpeakers = 1,
                    sampleRate = 22050,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "sherpa-onnx-vits-zh-ll",
                    numberOfSpeakers = 5,
                    sampleRate = 16000,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-zh-hf-fanchen-C",
                    numberOfSpeakers = 187,
                    sampleRate = 16000,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-zh-hf-fanchen-wnj",
                    numberOfSpeakers = 1,
                    sampleRate = 16000,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-zh-hf-theresa",
                    numberOfSpeakers = 804,
                    sampleRate = 22050,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-zh-hf-eula",
                    numberOfSpeakers = 804,
                    sampleRate = 22050,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-icefall-zh-aishell3",
                    numberOfSpeakers = 174,
                    sampleRate = 8000,
                },
                #endregion

                #region Czech
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-cs_CZ-jirka-low",
                    numberOfSpeakers = 1,
                    sampleRate = 16000,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-cs_CZ-jirka-medium",
                    numberOfSpeakers = 1,
                    sampleRate = 22050,
                },
                #endregion

                #region Danish
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-da_DK-talesyntese-medium",
                    numberOfSpeakers = 1,
                    sampleRate = 22050,
                },
                #endregion

                #region Dutch
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-nl_BE-nathalie-medium",
                    numberOfSpeakers = 1,
                    sampleRate = 22050,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-nl_BE-nathalie-x_low",
                    numberOfSpeakers = 1,
                    sampleRate = 16000,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-nl_NL-dii-high",
                    numberOfSpeakers = 1,
                    sampleRate = 22050,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-nl_NL-miro-high",
                    numberOfSpeakers = 1,
                    sampleRate = 22050,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-nl_NL-pim-medium",
                    numberOfSpeakers = 1,
                    sampleRate = 22050,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-nl_NL-ronnie-medium",
                    numberOfSpeakers = 1,
                    sampleRate = 22050,
                },
                #endregion
                #region English
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-ljs",
                    numberOfSpeakers = 1,
                    sampleRate = 22050,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-vctk",
                    numberOfSpeakers = 109,
                    sampleRate = 22050,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "matcha-icefall-en_US-ljspeech",
                    numberOfSpeakers = 1,
                    sampleRate = 22050,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "kitten-nano-en-v0_1-fp16",
                    numberOfSpeakers = 8,
                    sampleRate = 24000,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "kitten-nano-en-v0_2-fp16",
                    numberOfSpeakers = 8,
                    sampleRate = 24000,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "kitten-mini-en-v0_1-fp16",
                    numberOfSpeakers = 8,
                    sampleRate = 24000,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "kokoro-en-v0_19",
                    numberOfSpeakers = 11,
                    sampleRate = 24000,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-en_GB-alan-low",
                    numberOfSpeakers = 1,
                    sampleRate = 16000,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-en_GB-alan-medium",
                    numberOfSpeakers = 1,
                    sampleRate = 22050,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-en_GB-alba-medium",
                    numberOfSpeakers = 1,
                    sampleRate = 22050,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-en_GB-aru-medium",
                    numberOfSpeakers = 1,
                    sampleRate = 22050,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-en_GB-cori-high",
                    numberOfSpeakers = 1,
                    sampleRate = 22050,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-en_GB-cori-medium",
                    numberOfSpeakers = 1,
                    sampleRate = 22050,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-en_GB-dii-high",
                    numberOfSpeakers = 1,
                    sampleRate = 22050,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-en_GB-jenny_dioco-medium",
                    numberOfSpeakers = 1,
                    sampleRate = 22050,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-en_GB-miro-high",
                    numberOfSpeakers = 1,
                    sampleRate = 22050,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-en_GB-northern_english_male-medium",
                    numberOfSpeakers = 1,
                    sampleRate = 22050,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-en_GB-semaine-medium",
                    numberOfSpeakers = 4,
                    sampleRate = 22050,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-en_GB-southern_english_female-low",
                    numberOfSpeakers = 1,
                    sampleRate = 16000,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-en_GB-southern_english_female-medium",
                    numberOfSpeakers = 6,
                    sampleRate = 22050,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-en_GB-southern_english_male-medium",
                    numberOfSpeakers = 8,
                    sampleRate = 22050,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-en_GB-vctk-medium",
                    numberOfSpeakers = 109,
                    sampleRate = 22050,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-en_US-amy-low",
                    numberOfSpeakers = 1,
                    sampleRate = 22050,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-en_US-arctic-medium",
                    numberOfSpeakers = 18,
                    sampleRate = 22050,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-en_US-bryce-medium",
                    numberOfSpeakers = 1,
                    sampleRate = 22050,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-en_US-danny-low",
                    numberOfSpeakers = 1,
                    sampleRate = 16000,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-en_US-glados",
                    numberOfSpeakers = 1,
                    sampleRate = 22050,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-en_US-glados-high",
                    numberOfSpeakers = 1,
                    sampleRate = 22050,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-en_US-hfc_female-medium",
                    numberOfSpeakers = 1,
                    sampleRate = 22050,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-en_US-hfc_male-medium",
                    numberOfSpeakers = 1,
                    sampleRate = 22050,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-en_US-joe-medium",
                    numberOfSpeakers = 1,
                    sampleRate = 22050,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-en_US-john-medium",
                    numberOfSpeakers = 1,
                    sampleRate = 22050,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-en_US-kathleen-low",
                    numberOfSpeakers = 1,
                    sampleRate = 16000,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-en_US-kristin-medium",
                    numberOfSpeakers = 1,
                    sampleRate = 22050,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-en_US-kusal-medium",
                    numberOfSpeakers = 1,
                    sampleRate = 22050,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-en_US-l2arctic-medium",
                    numberOfSpeakers = 24,
                    sampleRate = 22050,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-en_US-lessac-high",
                    numberOfSpeakers = 1,
                    sampleRate = 22050,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-en_US-lessac-low",
                    numberOfSpeakers = 1,
                    sampleRate = 16000,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-en_US-lessac-medium",
                    numberOfSpeakers = 1,
                    sampleRate = 22050,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-en_US-libritts-high",
                    numberOfSpeakers = 904,
                    sampleRate = 22050,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-en_US-libritts_r-medium",
                    numberOfSpeakers = 904,
                    sampleRate = 22050,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-en_US-ljspeech-high",
                    numberOfSpeakers = 1,
                    sampleRate = 22050,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-en_US-ljspeech-medium",
                    numberOfSpeakers = 1,
                    sampleRate = 22050,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-en_US-miro-high",
                    numberOfSpeakers = 1,
                    sampleRate = 22050,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-en_US-norman-medium",
                    numberOfSpeakers = 1,
                    sampleRate = 22050,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-en_US-reza_ibrahim-medium",
                    numberOfSpeakers = 1,
                    sampleRate = 22050,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-en_US-ryan-high",
                    numberOfSpeakers = 1,
                    sampleRate = 22050,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-en_US-ryan-low",
                    numberOfSpeakers = 1,
                    sampleRate = 16000,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-en_US-ryan-medium",
                    numberOfSpeakers = 1,
                    sampleRate = 22050,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-en_US-sam-medium",
                    numberOfSpeakers = 1,
                    sampleRate = 22050,
                },
                #endregion

                #region Finnish
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-fi_FI-harri-low",
                    numberOfSpeakers = 1,
                    sampleRate = 16000,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-fi_FI-harri-medium",
                    numberOfSpeakers = 1,
                    sampleRate = 22050,
                },
                #endregion

                #region French
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-fr_FR-gilles-low",
                    numberOfSpeakers = 1,
                    sampleRate = 16000,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-fr_FR-miro-high",
                    numberOfSpeakers = 1,
                    sampleRate = 22050,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-fr_FR-siwis-low",
                    numberOfSpeakers = 1,
                    sampleRate = 16000,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-fr_FR-siwis-medium",
                    numberOfSpeakers = 1,
                    sampleRate = 22050,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-fr_FR-tjiho-model1",
                    numberOfSpeakers = 1,
                    sampleRate = 44100,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-fr_FR-tjiho-model2",
                    numberOfSpeakers = 1,
                    sampleRate = 44100,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-fr_FR-tjiho-model3",
                    numberOfSpeakers = 1,
                    sampleRate = 44100,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-fr_FR-tom-medium",
                    numberOfSpeakers = 1,
                    sampleRate = 44100,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-fr_FR-upmc-medium",
                    numberOfSpeakers = 2,
                    sampleRate = 22050,
                },
                #endregion

                #region Georgian
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-ka_GE-natia-medium",
                    numberOfSpeakers = 1,
                    sampleRate = 22050,
                },
                #endregion

                #region German
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-de_DE-dii-high",
                    numberOfSpeakers = 1,
                    sampleRate = 22050,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-de_DE-eva_k-x_low",
                    numberOfSpeakers = 1,
                    sampleRate = 16000,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-de_DE-glados-high",
                    numberOfSpeakers = 1,
                    sampleRate = 22050,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-de_DE-glados-low",
                    numberOfSpeakers = 1,
                    sampleRate = 16000,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-de_DE-glados-medium",
                    numberOfSpeakers = 1,
                    sampleRate = 22050,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-de_DE-glados_turret-high",
                    numberOfSpeakers = 1,
                    sampleRate = 22050,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-de_DE-glados_turret-low",
                    numberOfSpeakers = 1,
                    sampleRate = 16000,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-de_DE-glados_turret-medium",
                    numberOfSpeakers = 1,
                    sampleRate = 22050,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-de_DE-karlsson-low",
                    numberOfSpeakers = 1,
                    sampleRate = 16000,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-de_DE-kerstin-low",
                    numberOfSpeakers = 1,
                    sampleRate = 16000,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-de_DE-miro-high",
                    numberOfSpeakers = 1,
                    sampleRate = 22050,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-de_DE-pavoque-low",
                    numberOfSpeakers = 1,
                    sampleRate = 16000,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-de_DE-ramona-low",
                    numberOfSpeakers = 1,
                    sampleRate = 16000,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-de_DE-thorsten-high",
                    numberOfSpeakers = 1,
                    sampleRate = 22050,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-de_DE-thorsten-low",
                    numberOfSpeakers = 1,
                    sampleRate = 16000,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-de_DE-thorsten-medium",
                    numberOfSpeakers = 1,
                    sampleRate = 22050,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-de_DE-thorsten_emotional-medium",
                    numberOfSpeakers = 8,
                    sampleRate = 22050,
                },
                #endregion

                #region Greek
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-el_GR-rapunzelina-low",
                    numberOfSpeakers = 1,
                    sampleRate = 16000,
                },
                #endregion

                #region Hindi
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-hi_IN-pratham-medium",
                    numberOfSpeakers = 1,
                    sampleRate = 22050,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-hi_IN-priyamvada-medium",
                    numberOfSpeakers = 1,
                    sampleRate = 22050,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-hi_IN-rohan-medium",
                    numberOfSpeakers = 1,
                    sampleRate = 22050,
                },
                #endregion

                #region Hungarian
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-hu_HU-anna-medium",
                    numberOfSpeakers = 1,
                    sampleRate = 22050,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-hu_HU-berta-medium",
                    numberOfSpeakers = 1,
                    sampleRate = 22050,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-hu_HU-imre-medium",
                    numberOfSpeakers = 1,
                    sampleRate = 22050,
                },
                #endregion

                #region icelandic
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-is_IS-bui-medium",
                    numberOfSpeakers = 1,
                    sampleRate = 22050,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-is_IS-salka-medium",
                    numberOfSpeakers = 1,
                    sampleRate = 22050,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-is_IS-steinn-medium",
                    numberOfSpeakers = 1,
                    sampleRate = 22050,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-is_IS-ugla-medium",
                    numberOfSpeakers = 1,
                    sampleRate = 22050,
                },
                #endregion

                #region Indonesian
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-id_ID-news_tts-medium",
                    numberOfSpeakers = 1,
                    sampleRate = 22050,
                },
                #endregion

                #region Italian
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-it_IT-dii-high",
                    numberOfSpeakers = 1,
                    sampleRate = 22050,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-it_IT-miro-high",
                    numberOfSpeakers = 1,
                    sampleRate = 22050,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-it_IT-paola-medium",
                    numberOfSpeakers = 1,
                    sampleRate = 22050,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-it_IT-riccardo-x_low",
                    numberOfSpeakers = 1,
                    sampleRate = 16000,
                },
                #endregion

                #region Kazakh
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-kk_KZ-iseke-x_low",
                    numberOfSpeakers = 1,
                    sampleRate = 16000,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-kk_KZ-issai-high",
                    numberOfSpeakers = 6,
                    sampleRate = 22050,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-kk_KZ-raya-x_low",
                    numberOfSpeakers = 1,
                    sampleRate = 16000,
                },
                #endregion

                #region Latvian
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-lv_LV-aivars-medium",
                    numberOfSpeakers = 1,
                    sampleRate = 22050,
                },
                #endregion

                #region Luxembourgish
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-lb_LU-marylux-medium",
                    numberOfSpeakers = 1,
                    sampleRate = 22050,
                },
                #endregion

                #region Malayalam
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-ml_IN-arjun-medium",
                    numberOfSpeakers = 1,
                    sampleRate = 22050,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-ml_IN-meera-medium",
                    numberOfSpeakers = 1,
                    sampleRate = 22050,
                },
                #endregion

                #region Nepali
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-ne_NP-chitwan-medium",
                    numberOfSpeakers = 1,
                    sampleRate = 22050,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-ne_NP-google-medium",
                    numberOfSpeakers = 18,
                    sampleRate = 22050,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-ne_NP-google-x_low",
                    numberOfSpeakers = 18,
                    sampleRate = 16000,
                },
                #endregion

                #region Norwegian
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-no_NO-talesyntese-medium",
                    numberOfSpeakers = 1,
                    sampleRate = 22050,
                },
                #endregion

                #region Persian
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-fa_IR-amir-medium",
                    numberOfSpeakers = 1,
                    sampleRate = 22050,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-fa_IR-ganji-medium",
                    numberOfSpeakers = 1,
                    sampleRate = 22050,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-fa_IR-ganji_adabi-medium",
                    numberOfSpeakers = 1,
                    sampleRate = 22050,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-fa_IR-gyro-medium",
                    numberOfSpeakers = 1,
                    sampleRate = 22050,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-fa_IR-reza_ibrahim-medium",
                    numberOfSpeakers = 1,
                    sampleRate = 22050,
                },
                #endregion

                #region Polish
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-pl_PL-darkman-medium",
                    numberOfSpeakers = 1,
                    sampleRate = 22050,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-pl_PL-gosia-medium",
                    numberOfSpeakers = 1,
                    sampleRate = 22050,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-pl_PL-jarvis_wg_glos-medium",
                    numberOfSpeakers = 1,
                    sampleRate = 22050,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-pl_PL-justyna_wg_glos-medium",
                    numberOfSpeakers = 1,
                    sampleRate = 22050,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-pl_PL-mc_speech-medium",
                    numberOfSpeakers = 1,
                    sampleRate = 22050,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-pl_PL-meski_wg_glos-medium",
                    numberOfSpeakers = 1,
                    sampleRate = 22050,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-pl_PL-zenski_wg_glos-medium",
                    numberOfSpeakers = 1,
                    sampleRate = 22050,
                },
                #endregion

                #region Portuguese
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-pt_BR-cadu-medium",
                    numberOfSpeakers = 1,
                    sampleRate = 22050,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-pt_BR-dii-high",
                    numberOfSpeakers = 1,
                    sampleRate = 22050,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-pt_BR-edresson-low",
                    numberOfSpeakers = 1,
                    sampleRate = 16000,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-pt_BR-faber-medium",
                    numberOfSpeakers = 1,
                    sampleRate = 22050,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-pt_BR-jeff-medium",
                    numberOfSpeakers = 1,
                    sampleRate = 22050,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-pt_BR-miro-high",
                    numberOfSpeakers = 1,
                    sampleRate = 22050,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-pt_PT-dii-high",
                    numberOfSpeakers = 1,
                    sampleRate = 22050,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-pt_PT-miro-high",
                    numberOfSpeakers = 1,
                    sampleRate = 22050,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-pt_PT-tugao-medium",
                    numberOfSpeakers = 1,
                    sampleRate = 22050,
                },
                #endregion

                #region Romanian
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-ro_RO-mihai-medium",
                    numberOfSpeakers = 1,
                    sampleRate = 22050,
                },
                #endregion

                #region Russian
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-ru_RU-denis-medium",
                    numberOfSpeakers = 1,
                    sampleRate = 22050,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-ru_RU-dmitri-medium",
                    numberOfSpeakers = 1,
                    sampleRate = 22050,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-ru_RU-irina-medium",
                    numberOfSpeakers = 1,
                    sampleRate = 22050,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-ru_RU-ruslan-medium",
                    numberOfSpeakers = 1,
                    sampleRate = 22050,
                },
                #endregion

                #region Serbian
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-sr_RS-serbski_institut-medium",
                    numberOfSpeakers = 2,
                    sampleRate = 22050,
                },
                #endregion

                #region Slovak
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-sk_SK-lili-medium",
                    numberOfSpeakers = 1,
                    sampleRate = 22050,
                },
                #endregion

                #region Slovenian
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-sl_SI-artur-medium",
                    numberOfSpeakers = 1,
                    sampleRate = 22050,
                },
                #endregion

                #region Spanish
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-es_AR-daniela-high",
                    numberOfSpeakers = 1,
                    sampleRate = 22050,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-es_ES-carlfm-x_low",
                    numberOfSpeakers = 1,
                    sampleRate = 16000,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-es_ES-davefx-medium",
                    numberOfSpeakers = 1,
                    sampleRate = 22050,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-es_ES-glados-medium",
                    numberOfSpeakers = 1,
                    sampleRate = 22050,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-es_ES-miro-high",
                    numberOfSpeakers = 1,
                    sampleRate = 22050,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-es_ES-sharvard-medium",
                    numberOfSpeakers = 2,
                    sampleRate = 22050,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-es_MX-ald-medium",
                    numberOfSpeakers = 1,
                    sampleRate = 22050,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-es_MX-claude-high",
                    numberOfSpeakers = 1,
                    sampleRate = 22050,
                },
                #endregion

                #region Swahili
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-sw_CD-lanfrica-medium",
                    numberOfSpeakers = 1,
                    sampleRate = 22050,
                },
                #endregion

                #region Swedish
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-sv_SE-lisa-medium",
                    numberOfSpeakers = 1,
                    sampleRate = 22050,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-sv_SE-nst-medium",
                    numberOfSpeakers = 1,
                    sampleRate = 22050,
                },
                #endregion

                #region Turkish
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-tr_TR-dfki-medium",
                    numberOfSpeakers = 1,
                    sampleRate = 22050,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-tr_TR-fahrettin-medium",
                    numberOfSpeakers = 1,
                    sampleRate = 22050,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-tr_TR-fettah-medium",
                    numberOfSpeakers = 1,
                    sampleRate = 22050,
                },
                #endregion

                #region Ukrainlan
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-uk_UA-lada-x_low",
                    numberOfSpeakers = 1,
                    sampleRate = 16000,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-uk_UA-ukrainian_tts-medium",
                    numberOfSpeakers = 3,
                    sampleRate = 22050,
                },
                #endregion

                #region Vietnamese
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-vi_VN-25hours_single-low",
                    numberOfSpeakers = 1,
                    sampleRate = 16000,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-vi_VN-vais1000-medium",
                    numberOfSpeakers = 1,
                    sampleRate = 22050,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-vi_VN-vivos-x_low",
                    numberOfSpeakers = 65,
                    sampleRate = 16000,
                },
                #endregion
                #region Weish
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-cy_GB-bu_tts-medium",
                    numberOfSpeakers = 7,
                    sampleRate = 22050,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "vits-piper-cy_GB-gwryw_gogleddol-medium",
                    numberOfSpeakers = 1,
                    sampleRate = 22050,
                },
                #endregion

                //matcha
                new SherpaONNXModelMetadata
                {
                    modelId = "vocos-22khz-univ",
                    downloadUrl =
                        "https://github.com/k2-fsa/sherpa-onnx/releases/download/vocoder-models/vocos-22khz-univ.onnx",
                    downloadFileHash =
                        "0574a135aa1db2de6e181050db2ec528496cacd4a4701fc5d7faf9f9804c0081",
                    sampleRate = 22050,
                },
                #region Chinese+English
                //kokoro
                new SherpaONNXModelMetadata
                {
                    modelId = "kokoro-multi-lang-v1_1",
                    downloadFileHash =
                        "a3f4c73d043860e3fd2e5b06f36795eb81de0fc8e8de6df703245edddd87dbad",
                    numberOfSpeakers = 103,
                    sampleRate = 24000,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "kokoro-int8-multi-lang-v1_1",
                    downloadFileHash =
                        "a1e94694776049035c4f2c6529f003aaece993c76aae9a78995831c3c4dcafc6",
                    numberOfSpeakers = 103,
                    sampleRate = 24000,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "kokoro-multi-lang-v1_0",
                    numberOfSpeakers = 53,
                    sampleRate = 24000,
                },
                //zip-voice
                new SherpaONNXModelMetadata
                {
                    modelId = "sherpa-onnx-zipvoice-distill-zh-en-emilia",
                    downloadFileHash =
                        "9bcb4076c78d9f31778b54189831d999dcca148f640b3f69a1b3c5975bc63599",
                    sampleRate = 24000,
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "sherpa-onnx-zipvoice-zh-en-emilia",
                    downloadFileHash =
                        "098feeb0566875751eb8776b87060b9889b32b17d3649504e4514ce2b6a85100",
                    sampleRate = 24000,
                },
                #endregion
            };

            //https://github.com/k2-fsa/sherpa-onnx/releases/tag/kws-models
            public static readonly SherpaONNXModelMetadata[] KWS_MODELS_METADATA_TABLES = new[]
            {
                //for chinese
                new SherpaONNXModelMetadata
                {
                    modelId = "sherpa-onnx-kws-zipformer-wenetspeech-3.3M-2024-01-01",
                    downloadFileHash =
                        "b2f7c89690dc8ce4c6ed6afeab7cd800c36ad1421fb6b6302b4a4b194cf7f35f",
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "sherpa-onnx-kws-zipformer-wenetspeech-3.3M-2024-01-01-mobile",
                    downloadFileHash =
                        "b812a043aef628a6915f89cb9a94e55f8e87e89ff904b516f822d7e0a3e6de2b",
                },
                //for english
                new SherpaONNXModelMetadata
                {
                    modelId = "sherpa-onnx-kws-zipformer-gigaspeech-3.3M-2024-01-01",
                    downloadFileHash =
                        "f170013b4716e41b62b9bfd809687c207cef798ef9bc6534d524e17af9b6561a",
                },
                new SherpaONNXModelMetadata
                {
                    modelId = "sherpa-onnx-kws-zipformer-gigaspeech-3.3M-2024-01-01-mobile",
                    downloadFileHash =
                        "2e6ac2577310bfa2f4b6b5fab0478b868c9d0b2cb2c51b3e13b50581b588864d",
                },
            };

            public static readonly SherpaONNXModelMetadata[] SPEECH_ENHANCEMENT_MODELS_METADATA_TABLES =
                new[]
                {
                    // GTCRN speech enhancement models
                    new SherpaONNXModelMetadata
                    {
                        modelId = "gtcrn-simple",
                        downloadUrl =
                            "https://github.com/k2-fsa/sherpa-onnx/releases/download/speech-enhancement-models/gtcrn_simple.onnx",
                    },
                };

            public static readonly SherpaONNXModelMetadata[] SPOKEN_LANGUAGEIDENTIFICATION_MODELS_METADATA_TABLES =
                new[]
                {
                    new SherpaONNXModelMetadata
                    {
                        modelId = "sherpa-onnx-whisper-tiny",
                        downloadFileHash =
                            "c46116994e539aa165266d96b325252728429c12535eb9d8b6a2b10f129e66b1",
                    },
                    new SherpaONNXModelMetadata
                    {
                        modelId = "sherpa-onnx-whisper-base",
                        downloadFileHash =
                            "911b2083efd7c0dca2ac3b358b75222660dc09fb716d64fbfc417ba6c99ff3de",
                    },
                    new SherpaONNXModelMetadata { modelId = "sherpa-onnx-whisper-small" },
                    new SherpaONNXModelMetadata { modelId = "sherpa-onnx-whisper-medium" },
                };

            public static readonly SherpaONNXModelMetadata[] PUNCTUATION_MODELS_METADATA_TABLES =
                new[]
                {
                    // new SherpaONNXModelMetadata { modelId ="sherpa-onnx-online-punct-en-2024-08-06", }, // not supported
                    new SherpaONNXModelMetadata
                    {
                        modelId = "sherpa-onnx-punct-ct-transformer-zh-en-vocab272727-2024-04-12",
                    },
                    new SherpaONNXModelMetadata
                    {
                        modelId =
                            "sherpa-onnx-punct-ct-transformer-zh-en-vocab272727-2024-04-12-int8",
                    },
                };

            public static readonly SherpaONNXModelMetadata[] AUDIO_TAGGING_MODELS_METADATA_TABLES =
                new[]
                {
                    new SherpaONNXModelMetadata
                    {
                        modelId = "sherpa-onnx-ced-base-audio-tagging-2024-04-19",
                        downloadFileHash =
                            "8d961778eff7ac71bde8ef99a27c979ec86d16bc226b20c138f583a2b66735ff",
                    },
                    new SherpaONNXModelMetadata
                    {
                        modelId = "sherpa-onnx-ced-mini-audio-tagging-2024-04-19",
                        downloadFileHash =
                            "bec8bfa0af2c20ec3a9e7c6dd6c92d0fb6e96d6b25370ebe7a48e7283a60a02c",
                    },
                    new SherpaONNXModelMetadata
                    {
                        modelId = "sherpa-onnx-ced-small-audio-tagging-2024-04-19",
                        downloadFileHash =
                            "a3c9ef3e7f8dcf2720b148bb3c32a882a896a4abb00450a6c091772c54d11ef5",
                    },
                    new SherpaONNXModelMetadata
                    {
                        modelId = "sherpa-onnx-ced-tiny-audio-tagging-2024-04-19",
                        downloadFileHash =
                            "84baf315b57d61aa69480c4fee878dab54cbc7be3e877db334e65d8b087e23c3",
                    },
                    new SherpaONNXModelMetadata
                    {
                        modelId = "sherpa-onnx-zipformer-audio-tagging-2024-04-09",
                        downloadFileHash =
                            "96e10d903fa3dec602d6585fcd41f10e75143acdb1ec3fb1b25a7781180e6350",
                    },
                    new SherpaONNXModelMetadata
                    {
                        modelId = "sherpa-onnx-zipformer-small-audio-tagging-2024-04-15",
                        downloadFileHash =
                            "07e2fafcdcbc461f2816188d9b0bbafced12584030cf67d5652e549ef256a2c6",
                    },
                };

            public static readonly SherpaONNXModelMetadata[] SPEAKER_IDENTIFICATION_MODELS_METADATA_TABLES =
                new[]
                {
                    new SherpaONNXModelMetadata
                    {
                        modelId = "3dspeaker_speech_campplus_sv_en_voxceleb_16k.onnx",
                        downloadFileHash =
                            "357a834f702b80161e5b981182c038e18553c1f2ca752ed6cec2052365d4129b",
                    },
                    new SherpaONNXModelMetadata
                    {
                        modelId = "3dspeaker_speech_campplus_sv_zh-cn_16k-common.onnx",
                        downloadFileHash =
                            "f682b514c05d947ee3fa91cd6ec6c5c7543479a128373fa29b1faedccd21fd11",
                    },
                    new SherpaONNXModelMetadata
                    {
                        modelId = "3dspeaker_speech_campplus_sv_zh_en_16k-common_advanced.onnx",
                        downloadFileHash =
                            "aa3cfc16963a10586a9393f5035d6d6b57e98d358b347f80c2a30bf4f00ceba2",
                    },
                    new SherpaONNXModelMetadata
                    {
                        modelId = "3dspeaker_speech_eres2netv2_sv_zh-cn_16k-common.onnx",
                        downloadFileHash =
                            "bf1a75b9930474cf3389ef415e6e5d38ca96fea4a3a00f7e301d080a58ee2239",
                    },
                    new SherpaONNXModelMetadata
                    {
                        modelId = "3dspeaker_speech_eres2net_base_200k_sv_zh-cn_16k-common.onnx",
                        downloadFileHash =
                            "e2d2048292e055f7b61cdec3db010503f35369b245bf0b3bbad021c9a91e4053",
                    },
                    new SherpaONNXModelMetadata
                    {
                        modelId = "3dspeaker_speech_eres2net_base_sv_zh-cn_3dspeaker_16k.onnx",
                        downloadFileHash =
                            "1a331345f04805badbb495c775a6ddffcdd1a732567d5ec8b3d5749e3c7a5e4b",
                    },
                    new SherpaONNXModelMetadata
                    {
                        modelId = "3dspeaker_speech_eres2net_large_sv_zh-cn_3dspeaker_16k.onnx",
                        downloadFileHash =
                            "19547e85b6c14ec44b8add4e7cb9ce353c7e995d4f1c9ffd408176ac3a2d6895",
                    },
                    new SherpaONNXModelMetadata
                    {
                        modelId = "3dspeaker_speech_eres2net_sv_en_voxceleb_16k.onnx",
                        downloadFileHash =
                            "c59158379255ad66e161679cca6af8d52d51e389e3224ab7d7a7baae295c2db5",
                    },
                    new SherpaONNXModelMetadata
                    {
                        modelId = "3dspeaker_speech_eres2net_sv_zh-cn_16k-common.onnx",
                        downloadFileHash =
                            "2b9c4219b25326473524f006f1a09050ac28ccaf58c1f7dbc53e7631fa2fb1df",
                    },
                    new SherpaONNXModelMetadata
                    {
                        modelId = "nemo_en_speakerverification_speakernet.onnx",
                        downloadFileHash =
                            "d204dc8aac0014b8543f05fc8e310510c7022bc65b6452c203ec205ef7a66b23",
                    },
                    new SherpaONNXModelMetadata
                    {
                        modelId = "nemo_en_titanet_large.onnx",
                        downloadFileHash =
                            "d51abcf31717ef28162f26acb9d44dd4127c3d44c9b8624f699f3425daca8e77",
                    },
                    new SherpaONNXModelMetadata
                    {
                        modelId = "nemo_en_titanet_small.onnx",
                        downloadFileHash =
                            "ad4a1802485d8b34c722d2a9d04249662f2ece5d28a7a039063ca22f515a789e",
                    },
                    new SherpaONNXModelMetadata
                    {
                        modelId = "wespeaker_en_voxceleb_CAM++.onnx",
                        downloadFileHash =
                            "c46fad10b5f81e1aa4a60c162714208577093655076c5450f8c469e522ec54ef",
                    },
                    new SherpaONNXModelMetadata
                    {
                        modelId = "wespeaker_en_voxceleb_CAM++_LM.onnx",
                        downloadFileHash =
                            "e197af7e9d473030cf486b3124149a19bf37014d0e4485e4c70c483b0ec10cb2",
                    },
                    new SherpaONNXModelMetadata
                    {
                        modelId = "wespeaker_en_voxceleb_resnet152_LM.onnx",
                        downloadFileHash =
                            "3e1c2c7a02097bdddc27c1cf7382237399abdc3b5b682e59455830673c6686e7",
                    },
                    new SherpaONNXModelMetadata
                    {
                        modelId = "wespeaker_en_voxceleb_resnet221_LM.onnx",
                        downloadFileHash =
                            "182f4ae144d70dfeb78064f6d507f8aada35c732a782631152a2626a4f20a60a",
                    },
                    new SherpaONNXModelMetadata
                    {
                        modelId = "wespeaker_en_voxceleb_resnet293_LM.onnx",
                        downloadFileHash =
                            "f65dbc820e534eef64ae12d1e289e20244d60e60f7f00d7b092092b1c458be2e",
                    },
                    new SherpaONNXModelMetadata
                    {
                        modelId = "wespeaker_en_voxceleb_resnet34.onnx",
                        downloadFileHash =
                            "5ef208a9da1453335308a6b6f4e6dfbd7e183a38b604de0a57664f45d257fe94",
                    },
                    new SherpaONNXModelMetadata
                    {
                        modelId = "wespeaker_en_voxceleb_resnet34_LM.onnx",
                        downloadFileHash =
                            "e9848563da86f263117134dfd7ad63c92355b37de492b55e325400c9d9c39012",
                    },
                    new SherpaONNXModelMetadata
                    {
                        modelId = "wespeaker_zh_cnceleb_resnet34.onnx",
                        downloadFileHash =
                            "f86cd6c509f331f0e20b07bd48d1b2eb7de54202643401c4e84695ac861a0e5a",
                    },
                    new SherpaONNXModelMetadata
                    {
                        modelId = "wespeaker_zh_cnceleb_resnet34_LM.onnx",
                        downloadFileHash =
                            "87d1d5068397f3792c730570b53d66cd8be1da7ea22dd04f5b6706d96a3cd168",
                    },
                };
            public static readonly SherpaONNXModelMetadata[] SPEAKER_DIARIZATION_MODELS_METADATA_TABLES =
                new[]
                {
                    new SherpaONNXModelMetadata
                    {
                        modelId = "sherpa-onnx-reverb-diarization-v1",
                        downloadFileHash =
                            "615761e980be1688da0ef81618c056134d63aa55ea0a5f1494c47393b9398eab",
                    },
                    new SherpaONNXModelMetadata
                    {
                        modelId = "sherpa-onnx-reverb-diarization-v2",
                        downloadFileHash =
                            "2ca21f73eac8adb698fe538eaf4a25f69f614131c876772a8d26213cf648c851",
                    },
                    new SherpaONNXModelMetadata
                    {
                        modelId = "sherpa-onnx-pyannote-segmentation-3-0",
                        downloadFileHash =
                            "24615ee884c897d9d2ba09bb4d30da6bb1b15e685065962db5b02e76e4996488",
                    },
                };

            public static readonly SherpaONNXModelMetadata[] SOURCE_SEPARATION_MODELS_METADATA_TABLES =
                new[]
                {
                    new SherpaONNXModelMetadata
                    {
                        modelId = "sherpa-onnx-spleeter-2stems-fp16",
                        downloadFileHash = "c6c5c4307673bc6813ddf58d4efdff57c26d2dfc3f25b05c7a32db453d70aca6",
                    },
                    new SherpaONNXModelMetadata
                    {
                        modelId = "sherpa-onnx-spleeter-2stems-int8",
                        downloadFileHash = "a04f196c07454657e2a6bd705f932e1cc0f7c249d469c4695caded46f8599c96",
                    },
                    new SherpaONNXModelMetadata
                    {
                        modelId = "sherpa-onnx-spleeter-2stems",
                        downloadFileHash = "69b43d35b4cfed03b14c3da6bc64b1cd9e2f75a993f064d953e83d98747828c7",
                    },
                };



        }
    }
}

