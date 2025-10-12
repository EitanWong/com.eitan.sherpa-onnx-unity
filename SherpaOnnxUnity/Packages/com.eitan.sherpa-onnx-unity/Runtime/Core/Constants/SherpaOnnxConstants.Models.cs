namespace Eitan.SherpaOnnxUnity.Runtime.Constants
{

    public partial class SherpaOnnxConstants
    {

        public class Models
        {
            public static readonly SherpaOnnxModelMetadata[] ASR_MODELS_METADATA_TABLES = new[]
            {
                //TODO: 补全所有的hash信息
                // online models
                new SherpaOnnxModelMetadata { modelId = "sherpa-onnx-streaming-zipformer-zh-xlarge-int8-2025-06-30",modelFileNames = new[]{ "decoder.onnx","encoder.int8.onnx","joiner.int8.onnx","tokens.txt"} ,},
                new SherpaOnnxModelMetadata { modelId = "sherpa-onnx-streaming-zipformer-zh-xlarge-fp16-2025-06-30", modelFileNames = new[]{ "decoder.fp16.onnx","encoder.fp16.onnx","joiner.fp16.onnx","tokens.txt"}, },
                new SherpaOnnxModelMetadata { modelId = "sherpa-onnx-streaming-zipformer-zh-int8-2025-06-30",modelFileNames = new[]{ "decoder.onnx","encoder.int8.onnx","joiner.int8.onnx","tokens.txt"} ,},
                new SherpaOnnxModelMetadata { modelId = "sherpa-onnx-streaming-zipformer-korean-2024-06-16",modelFileNames= new[]{"decoder-epoch-99-avg-1.int8.onnx","decoder-epoch-99-avg-1.onnx","encoder-epoch-99-avg-1.int8.onnx","encoder-epoch-99-avg-1.onnx","joiner-epoch-99-avg-1.int8.onnx","joiner-epoch-99-avg-1.onnx","tokens.txt"} },
                new SherpaOnnxModelMetadata { modelId = "sherpa-onnx-streaming-zipformer-multi-zh-hans-2023-12-12", modelFileNames = new[] { "decoder-epoch-20-avg-1-chunk-16-left-128.int8.onnx", "decoder-epoch-20-avg-1-chunk-16-left-128.onnx", "encoder-epoch-20-avg-1-chunk-16-left-128.int8.onnx", "encoder-epoch-20-avg-1-chunk-16-left-128.onnx", "joiner-epoch-20-avg-1-chunk-16-left-128.int8.onnx", "joiner-epoch-20-avg-1-chunk-16-left-128.onnx", "tokens.txt" } },
                new SherpaOnnxModelMetadata { modelId = "icefall-asr-zipformer-streaming-wenetspeech-20230615", modelFileNames = new[] {"exp/decoder-epoch-12-avg-4-chunk-16-left-128.onnx","exp/decoder-epoch-12-avg-4-chunk-16-left-128.int8.onnx","exp/encoder-epoch-12-avg-4-chunk-16-left-128.onnx","exp/encoder-epoch-12-avg-4-chunk-16-left-128.int8.onnx","exp/joiner-epoch-12-avg-4-chunk-16-left-128.onnx","exp/joiner-epoch-12-avg-4-chunk-16-left-128.int8.onnx","/data/lang_char/tokens.txt"} },
                new SherpaOnnxModelMetadata { modelId = "sherpa-onnx-streaming-zipformer-en-2023-06-26", modelFileNames = new[] {"decoder-epoch-99-avg-1-chunk-16-left-128.onnx","decoder-epoch-99-avg-1-chunk-16-left-128.int8.onnx","encoder-epoch-99-avg-1-chunk-16-left-128.onnx","encoder-epoch-99-avg-1-chunk-16-left-128.int8.onnx","joiner-epoch-99-avg-1-chunk-16-left-128.onnx","joiner-epoch-99-avg-1-chunk-16-left-128.int8.onnx","tokens.txt"}  },
                new SherpaOnnxModelMetadata { modelId = "sherpa-onnx-streaming-zipformer-en-2023-06-21", modelFileNames = new []{"decoder-epoch-99-avg-1.onnx","decoder-epoch-99-avg-1.int8.onnx","encoder-epoch-99-avg-1.onnx","encoder-epoch-99-avg-1.int8.onnx","joiner-epoch-99-avg-1.onnx","joiner-epoch-99-avg-1.int8.onnx","tokens.txt"}},
                new SherpaOnnxModelMetadata { modelId = "sherpa-onnx-streaming-zipformer-en-2023-02-21", modelFileNames = new []{"decoder-epoch-99-avg-1.onnx","decoder-epoch-99-avg-1.int8.onnx","encoder-epoch-99-avg-1.onnx","encoder-epoch-99-avg-1.int8.onnx","joiner-epoch-99-avg-1.onnx","joiner-epoch-99-avg-1.int8.onnx","tokens.txt"}},
                new SherpaOnnxModelMetadata { modelId = "sherpa-onnx-streaming-zipformer-bilingual-zh-en-2023-02-20", modelFileNames= new[]{"decoder-epoch-99-avg-1.onnx","decoder-epoch-99-avg-1.int8.onnx","encoder-epoch-99-avg-1.onnx","encoder-epoch-99-avg-1.int8.onnx","joiner-epoch-99-avg-1.onnx","joiner-epoch-99-avg-1.int8.onnx","tokens.txt"}, modelFileHashes= new []{"2e3b5ec371f8899ee6acd829fd753ba45772df57a91bdf37cde3136354e7db7d", "1a70c593d71e53f023f5f55b0b4cfff5055abb786ee3992e5f63dc2e273cc4fa", "709f0ed53a734b7942f170127e7547b566cb29c4afc5e67719f314c3d63ccb10", "8fa764187a261844f859d7143ebaa563af5d10adfece4c18a8f414c88cba2a9b", "5f2adc585dd1bec6421c8bb8660d2a73fc8b9ceb24491ef51399ba2a2f0fc31b", "1ed689c5ed19dbaa725d9d191bb4822b5f4855a39e1ffd28cbc1f340d25b2ee0", "a8e0e4ec53810e433789b54a5c0134a7eaa2ffca595a6334d54c00da858841d3"} },
                new SherpaOnnxModelMetadata { modelId = "sherpa-onnx-streaming-zipformer-fr-2023-04-14", modelFileNames = new []{"decoder-epoch-29-avg-9-with-averaged-model.onnx","decoder-epoch-29-avg-9-with-averaged-model.int8.onnx","encoder-epoch-29-avg-9-with-averaged-model.onnx","encoder-epoch-29-avg-9-with-averaged-model.int8.onnx","joiner-epoch-29-avg-9-with-averaged-model.onnx","joiner-epoch-29-avg-9-with-averaged-model.int8.onnx","tokens.txt"}},
                new SherpaOnnxModelMetadata { modelId = "sherpa-onnx-streaming-zipformer-small-bilingual-zh-en-2023-02-16", modelFileNames = new []{"decoder-epoch-99-avg-1.onnx","decoder-epoch-99-avg-1.int8.onnx","encoder-epoch-99-avg-1.onnx","encoder-epoch-99-avg-1.int8.onnx","joiner-epoch-99-avg-1.onnx","joiner-epoch-99-avg-1.int8.onnx","tokens.txt"}},
                new SherpaOnnxModelMetadata { modelId = "sherpa-onnx-streaming-zipformer-zh-14M-2023-02-23", modelFileNames = new []{"decoder-epoch-99-avg-1.onnx","decoder-epoch-99-avg-1.int8.onnx","encoder-epoch-99-avg-1.onnx","encoder-epoch-99-avg-1.int8.onnx","joiner-epoch-99-avg-1.onnx","joiner-epoch-99-avg-1.int8.onnx","tokens.txt"}},
                new SherpaOnnxModelMetadata { modelId = "sherpa-onnx-streaming-zipformer-en-20M-2023-02-17", modelFileNames = new []{"decoder-epoch-99-avg-1.onnx","decoder-epoch-99-avg-1.int8.onnx","encoder-epoch-99-avg-1.onnx","encoder-epoch-99-avg-1.int8.onnx","joiner-epoch-99-avg-1.onnx","joiner-epoch-99-avg-1.int8.onnx","tokens.txt"}},
                new SherpaOnnxModelMetadata { modelId = "sherpa-onnx-streaming-zipformer-small-ctc-zh-int8-2025-04-01", modelFileNames = new []{"model.int8.onnx","tokens.txt"} },
                new SherpaOnnxModelMetadata { modelId = "sherpa-onnx-streaming-zipformer-small-ctc-zh-2025-04-01" , modelFileNames = new []{"model.onnx","tokens.txt"} },
                new SherpaOnnxModelMetadata { modelId = "sherpa-onnx-streaming-zipformer-ctc-multi-zh-hans-2023-12-13", modelFileNames = new []{"ctc-epoch-20-avg-1-chunk-16-left-128.onnx","ctc-epoch-20-avg-1-chunk-16-left-128.int8.onnx","tokens.txt"} },
                new SherpaOnnxModelMetadata { modelId = "sherpa-onnx-streaming-paraformer-bilingual-zh-en", modelFileNames = new[] {"decoder.onnx","decoder.int8.onnx",  "encoder.onnx","encoder.int8.onnx", "tokens.txt" }, modelFileHashes = new[] {"e178f5a7dd4efbf5905a797807006d773b12116eb39fed3d16758e68f9f50921", "f3cca9f77bb9d93c8fcbfb63ae617b6b1ee96818df3aa3b151c40658fe38594f", "832c8e8d3f758f4ab0fcfc011eec91154ecd129b7305564a7b461b20064ebcc6", "81a70226a8934e6ed92aa1d4fc486b428b5398e2f2619ed4897b7294cab90e9a", "59aba8873a2ed1e122c25fee421e25f283b63290efbde85c1f01a853d83cb6e6" }, downloadFileHash = "5462a1fce42693deae572af1e8c4687124b12aa85fe61ff4d3168bb5280e205f"  },
                new SherpaOnnxModelMetadata { modelId = "sherpa-onnx-streaming-paraformer-trilingual-zh-cantonese-en", modelFileNames = new[] { "decoder.onnx","decoder.int8.onnx","encoder.onnx","encoder.int8.onnx","tokens.txt" } },
                //offline models
                new SherpaOnnxModelMetadata { modelId = "sherpa-onnx-zipformer-ctc-zh-int8-2025-07-03", modelFileNames = new[] { "model.int8.onnx", "tokens.txt" }, modelFileHashes= new[]{"e291b9c468b651e2697caa09bc684326c3addc6a019e78eb537cfd1a8248ca07","6fed8c6c248516f38e7faa19404b57413e8ce259f1cbc1fa4aebc86eac32fdfd"} },
                new SherpaOnnxModelMetadata { modelId = "sherpa-onnx-zipformer-ctc-zh-fp16-2025-07-03", modelFileNames = new[] { "model.fp16.onnx", "tokens.txt" } },
                new SherpaOnnxModelMetadata { modelId = "sherpa-onnx-zipformer-ctc-zh-2025-07-03", modelFileNames = new[] { "model.onnx", "tokens.txt" } },
                new SherpaOnnxModelMetadata { modelId = "sherpa-onnx-zipformer-vi-2025-04-20", modelFileNames = new[] { "decoder-epoch-12-avg-8.onnx", "encoder-epoch-12-avg-8.onnx", "joiner-epoch-12-avg-8.onnx", "tokens.txt" } },
                new SherpaOnnxModelMetadata { modelId = "sherpa-onnx-zipformer-vi-int8-2025-04-20", modelFileNames = new[] { "decoder-epoch-12-avg-8.onnx", "encoder-epoch-12-avg-8.onnx", "joiner-epoch-12-avg-8.onnx", "tokens.txt" } },
                new SherpaOnnxModelMetadata { modelId = "sherpa-onnx-zipformer-zh-en-2023-11-22", modelFileNames = new[] { "decoder-epoch-34-avg-19.onnx", "encoder-epoch-34-avg-19.onnx", "encoder-epoch-34-avg-19.int8.onnx","joiner-epoch-34-avg-19.onnx","joiner-epoch-34-avg-19.int8.onnx", "tokens.txt" } },
                new SherpaOnnxModelMetadata { modelId = "sherpa-onnx-zipformer-ru-2024-09-18", modelFileNames = new[] { "decoder.onnx","decoder.int8.onnx", "encoder.onnx","encoder.int8.onnx", "joiner.onnx","joiner.int8.onnx", "tokens.txt" } },
                new SherpaOnnxModelMetadata { modelId = "sherpa-onnx-small-zipformer-ru-2024-09-18", modelFileNames = new[] { "decoder.onnx","decoder.int8.onnx", "encoder.onnx","encoder.int8.onnx", "joiner.onnx","joiner.int8.onnx", "tokens.txt" } },
                new SherpaOnnxModelMetadata { modelId = "sherpa-onnx-zipformer-ja-reazonspeech-2024-08-01", modelFileNames = new[] { "decoder-epoch-99-avg-1.onnx", "decoder-epoch-99-avg-1.int8.onnx","encoder-epoch-99-avg-1.onnx","encoder-epoch-99-avg-1.int8.onnx","joiner-epoch-99-avg-1.onnx","joiner-epoch-99-avg-1.int8.onnx", "tokens.txt" } },
                new SherpaOnnxModelMetadata { modelId = "sherpa-onnx-zipformer-korean-2024-06-24", modelFileNames = new[] { "decoder-epoch-99-avg-1.onnx", "decoder-epoch-99-avg-1.int8.onnx","encoder-epoch-99-avg-1.onnx","encoder-epoch-99-avg-1.int8.onnx","joiner-epoch-99-avg-1.onnx","joiner-epoch-99-avg-1.int8.onnx", "tokens.txt" } },
                new SherpaOnnxModelMetadata { modelId = "sherpa-onnx-zipformer-thai-2024-06-20", modelFileNames = new[] { "decoder-epoch-12-avg-5.onnx", "decoder-epoch-12-avg-5.int8.onnx", "encoder-epoch-12-avg-5.onnx","encoder-epoch-12-avg-5.int8.onnx","joiner-epoch-12-avg-5.onnx","joiner-epoch-12-avg-5.int8.onnx", "tokens.txt" } },
                new SherpaOnnxModelMetadata { modelId = "sherpa-onnx-zipformer-cantonese-2024-03-13", modelFileNames = new[] { "decoder-epoch-45-avg-35.onnx", "decoder-epoch-45-avg-35.int8.onnx","encoder-epoch-45-avg-35.onnx","encoder-epoch-45-avg-35.int8.onnx","joiner-epoch-45-avg-35.onnx", "joiner-epoch-45-avg-35.int8.onnx", "tokens.txt" } },
                new SherpaOnnxModelMetadata { modelId = "sherpa-onnx-zipformer-gigaspeech-2023-12-12", modelFileNames = new[] { "decoder-epoch-30-avg-1.onnx", "decoder-epoch-30-avg-1.int8.onnx", "encoder-epoch-30-avg-1.onnx","encoder-epoch-30-avg-1.int8.onnx","joiner-epoch-30-avg-1.onnx","joiner-epoch-30-avg-1.int8.onnx", "tokens.txt" } },
                new SherpaOnnxModelMetadata { modelId = "sherpa-onnx-zipformer-multi-zh-hans-2023-9-2", modelFileNames = new[] { "decoder-epoch-20-avg-1.onnx", "decoder-epoch-20-avg-1.int8.onnx","encoder-epoch-20-avg-1.onnx","encoder-epoch-20-avg-1.int8.onnx","joiner-epoch-20-avg-1.onnx", "joiner-epoch-20-avg-1.int8.onnx", "tokens.txt" } },
                new SherpaOnnxModelMetadata { modelId = "icefall-asr-cv-corpus-13.0-2023-03-09-en-pruned-transducer-stateless7-2023-04-17", modelFileNames = new[] { "exp/decoder-epoch-60-avg-20.onnx", "exp/decoder-epoch-60-avg-20.int8.onnx", "exp/encoder-epoch-60-avg-20.onnx", "exp/encoder-epoch-60-avg-20.int8.onnx","exp/joiner-epoch-60-avg-20.onnx","exp/joiner-epoch-60-avg-20.int8.onnx","/data/lang_bpe_500/tokens.txt"} },
                new SherpaOnnxModelMetadata { modelId = "icefall-asr-zipformer-wenetspeech-20230615", modelFileNames = new[] { "exp/decoder-epoch-12-avg-4.onnx", "exp/decoder-epoch-12-avg-4.int8.onnx", "exp/encoder-epoch-12-avg-4.onnx","exp/encoder-epoch-12-avg-4.int8.onnx","exp/joiner-epoch-12-avg-4.onnx", "exp/joiner-epoch-12-avg-4.int8.onnx","/data/lang_char/tokens.txt" } },
                new SherpaOnnxModelMetadata { modelId = "sherpa-onnx-zipformer-large-en-2023-06-26", modelFileNames = new[] { "decoder-epoch-99-avg-1.onnx", "encoder-epoch-99-avg-1.onnx", "joiner-epoch-99-avg-1.onnx", "tokens.txt" } },
                new SherpaOnnxModelMetadata { modelId = "sherpa-onnx-zipformer-small-en-2023-06-26", modelFileNames = new[] { "decoder-epoch-99-avg-1.onnx", "encoder-epoch-99-avg-1.onnx", "joiner-epoch-99-avg-1.onnx", "tokens.txt" } },
                new SherpaOnnxModelMetadata { modelId = "icefall-asr-multidataset-pruned_transducer_stateless7-2023-05-04", modelFileNames = new[] { "/exp/decoder-epoch-30-avg-4.onnx" , "/exp/decoder-epoch-30-avg-4.int8.onnx", "/exp/encoder-epoch-30-avg-4.onnx", "/exp/encoder-epoch-30-avg-4.int8.onnx","/exp/joiner-epoch-30-avg-4.onnx","/exp/joiner-epoch-30-avg-4.int8.onnx","/data/lang_bpe_500/tokens.txt" }},
                new SherpaOnnxModelMetadata { modelId = "sherpa-onnx-zipformer-en-2023-06-26", modelFileNames = new[] { "decoder-epoch-99-avg-1.onnx","decoder-epoch-99-avg-1.int8.onnx","encoder-epoch-99-avg-1.onnx", "encoder-epoch-99-avg-1.int8.onnx","joiner-epoch-99-avg-1.onnx", "joiner-epoch-99-avg-1.int8.onnx", "tokens.txt" } },
                new SherpaOnnxModelMetadata { modelId = "sherpa-onnx-zipformer-en-2023-04-01", modelFileNames = new[] { "decoder-epoch-99-avg-1.onnx","decoder-epoch-99-avg-1.int8.onnx","encoder-epoch-99-avg-1.onnx", "encoder-epoch-99-avg-1.int8.onnx","joiner-epoch-99-avg-1.onnx", "joiner-epoch-99-avg-1.int8.onnx", "tokens.txt" } },
                new SherpaOnnxModelMetadata { modelId = "sherpa-onnx-zipformer-en-2023-03-30",  modelFileNames = new[] { "decoder-epoch-99-avg-1.onnx","decoder-epoch-99-avg-1.int8.onnx","encoder-epoch-99-avg-1.onnx", "encoder-epoch-99-avg-1.int8.onnx","joiner-epoch-99-avg-1.onnx", "joiner-epoch-99-avg-1.int8.onnx", "tokens.txt" } },
                new SherpaOnnxModelMetadata { modelId = "sherpa-onnx-conformer-zh-stateless2-2023-05-23", modelFileNames = new[] { "decoder-epoch-99-avg-1.onnx","decoder-epoch-99-avg-1.int8.onnx","encoder-epoch-99-avg-1.onnx", "encoder-epoch-99-avg-1.int8.onnx","joiner-epoch-99-avg-1.onnx", "joiner-epoch-99-avg-1.int8.onnx", "tokens.txt" }},
                new SherpaOnnxModelMetadata { modelId = "sherpa-onnx-conformer-zh-2023-05-23", modelFileNames = new[] { "decoder-epoch-99-avg-1.onnx","decoder-epoch-99-avg-1.int8.onnx","encoder-epoch-99-avg-1.onnx", "encoder-epoch-99-avg-1.int8.onnx","joiner-epoch-99-avg-1.onnx", "joiner-epoch-99-avg-1.int8.onnx", "tokens.txt" } },
                new SherpaOnnxModelMetadata { modelId = "sherpa-onnx-conformer-en-2023-03-18",modelFileNames = new[] { "decoder-epoch-99-avg-1.onnx","decoder-epoch-99-avg-1.int8.onnx","encoder-epoch-99-avg-1.onnx", "encoder-epoch-99-avg-1.int8.onnx","joiner-epoch-99-avg-1.onnx", "joiner-epoch-99-avg-1.int8.onnx", "tokens.txt" }},
                new SherpaOnnxModelMetadata { modelId = "sherpa-onnx-nemo-parakeet-tdt-0.6b-v2-int8", modelFileNames = new[] { "decoder.int8.onnx","encoder.int8.onnx","joiner.int8.onnx", "tokens.txt" } },
                new SherpaOnnxModelMetadata { modelId = "sherpa-onnx-nemo-transducer-giga-am-v2-russian-2025-04-19", modelFileNames = new[] { "decoder.onnx","encoder.int8.onnx","joiner.onnx", "tokens.txt" } },
                new SherpaOnnxModelMetadata { modelId = "sherpa-onnx-nemo-transducer-giga-am-russian-2024-10-24", modelFileNames = new[] { "decoder.onnx","encoder.int8.onnx","joiner.onnx", "tokens.txt" } },
                new SherpaOnnxModelMetadata { modelId = "sherpa-onnx-paraformer-trilingual-zh-cantonese-en", modelFileNames = new[] { "model.onnx", "model.int8.onnx", "tokens.txt" } },
                new SherpaOnnxModelMetadata { modelId = "sherpa-onnx-paraformer-en-2024-03-09", modelFileNames = new[] { "model.onnx", "model.int8.onnx", "tokens.txt" } },
                new SherpaOnnxModelMetadata { modelId = "sherpa-onnx-paraformer-zh-small-2024-03-09", modelFileNames = new[] { "model.int8.onnx", "tokens.txt" } },
                new SherpaOnnxModelMetadata { modelId = "sherpa-onnx-paraformer-zh-2024-03-09", modelFileNames = new[] { "model.onnx", "model.int8.onnx", "tokens.txt" } , downloadFileHash ="8c6724d0a86bd867217d353db1eaa11f2f143bca446a1f2752e8c551a6f2bde0"},
                new SherpaOnnxModelMetadata { modelId = "sherpa-onnx-paraformer-zh-2023-03-28", modelFileNames = new[] { "model.onnx", "model.int8.onnx", "tokens.txt" } },
                new SherpaOnnxModelMetadata { modelId = "sherpa-onnx-paraformer-zh-2023-09-14", modelFileNames = new[] { "model.int8.onnx", "tokens.txt" } },
                new SherpaOnnxModelMetadata { modelId = "sherpa-onnx-nemo-parakeet_tdt_ctc_110m-en-36000-int8", modelFileNames = new[] { "model.int8.onnx", "tokens.txt" } },
                new SherpaOnnxModelMetadata { modelId = "sherpa-onnx-nemo-ctc-en-citrinet-512", modelFileNames = new[] { "model.onnx", "model.int8.onnx", "tokens.txt" } },
                new SherpaOnnxModelMetadata { modelId = "sherpa-onnx-nemo-ctc-en-conformer-small", modelFileNames = new[] { "model.onnx", "model.int8.onnx", "tokens.txt" } },
                new SherpaOnnxModelMetadata { modelId = "sherpa-onnx-nemo-ctc-en-conformer-medium", modelFileNames = new[] { "model.onnx","model.int8.onnx", "tokens.txt" } },
                new SherpaOnnxModelMetadata { modelId = "sherpa-onnx-nemo-ctc-en-conformer-large", modelFileNames = new[] { "model.onnx","model.int8.onnx", "tokens.txt" } },
                new SherpaOnnxModelMetadata { modelId = "sherpa-onnx-nemo-ctc-giga-am-v2-russian-2025-04-19", modelFileNames = new[] { "model.int8.onnx", "tokens.txt" } },
                new SherpaOnnxModelMetadata { modelId = "sherpa-onnx-nemo-ctc-giga-am-russian-2024-10-24", modelFileNames = new[] { "model.int8.onnx", "tokens.txt" } },
                new SherpaOnnxModelMetadata { modelId = "sherpa-onnx-nemo-parakeet-tdt_ctc-0.6b-ja-35000-int8", modelFileNames = new[] { "model.int8.onnx", "tokens.txt" } },
                new SherpaOnnxModelMetadata { modelId = "sherpa-onnx-nemo-canary-180m-flash-en-es-de-fr-int8", modelFileNames = new[] { "decoder.int8.onnx","encoder.int8.onnx", "tokens.txt" } },
                new SherpaOnnxModelMetadata { modelId = "sherpa-onnx-tdnn-yesno", modelFileNames = new[] { "model-epoch-14-avg-2.onnx", "tokens.txt" } },
                new SherpaOnnxModelMetadata { modelId = "sherpa-onnx-telespeech-ctc-int8-zh-2024-06-04", modelFileNames = new[] { "model.int8.onnx", "tokens.txt" } },
                new SherpaOnnxModelMetadata { modelId = "sherpa-onnx-whisper-tiny.en", modelFileNames = new[] { "tiny.en-decoder.onnx", "tiny.en-decoder.int8.onnx", "tiny.en-encoder.onnx" ,"tiny.en-encoder.int8.onnx" , "tiny.en-tokens.txt" } },
                new SherpaOnnxModelMetadata { modelId = "sherpa-onnx-whisper-small.en", modelFileNames = new[] { "small.en-decoder.onnx", "small.en-encoder.onnx", "small.en-encoder.onnx", "small.en-tokens.int8.onnx", "small.en-tokens.txt" } },
                new SherpaOnnxModelMetadata { modelId = "sherpa-onnx-whisper-medium.en", modelFileNames = new[] { "medium.en-decoder.onnx", "medium.en-decoder.int8.onnx", "medium.en-encoder.onnx", "medium.en-encoder.int8.onnx", "medium.en-tokens.txt" } },
                new SherpaOnnxModelMetadata { modelId = "sherpa-onnx-whisper-distil-small.en", modelFileNames = new[] { "distil-small.en-decoder.onnx","distil-small.en-decoder.int8.onnx", "distil-small.en-encoder.onnx","distil-small.en-encoder.int8.onnx", "distil-small.en-tokens.txt" } },
                new SherpaOnnxModelMetadata { modelId = "sherpa-onnx-whisper-tiny", modelFileNames = new[] { "tiny-decoder.onnx", "tiny-decoder.int8.onnx", "tiny-encoder.onnx", "tiny-encoder.int8.onnx", "tiny-tokens.txt" } },
                new SherpaOnnxModelMetadata { modelId = "sherpa-onnx-whisper-base", modelFileNames = new[] { "base-decoder.onnx","base-decoder.int8.onnx", "base-encoder.onnx","base-encoder.int8.onnx", "base-tokens.txt" } },
                new SherpaOnnxModelMetadata { modelId = "sherpa-onnx-whisper-small", modelFileNames = new[] { "small-decoder.onnx","small-decoder.int8.onnx", "small-encoder.onnx","small-encoder.int8.onnx", "small-tokens.txt" } },
                new SherpaOnnxModelMetadata { modelId = "sherpa-onnx-whisper-medium", modelFileNames = new[] { "medium-decoder.onnx","medium-decoder.int8.onnx", "medium-encoder.onnx","medium-encoder.int8.onnx", "medium-tokens.txt" } },
                new SherpaOnnxModelMetadata { modelId = "sherpa-onnx-moonshine-tiny-en-int8", modelFileNames = new[] { "preprocess.onnx", "cached_decode.int8.onnx","uncached_decode.int8.onnx","encode.int8.onnx","tokens.txt" } },
                new SherpaOnnxModelMetadata { modelId = "sherpa-onnx-moonshine-base-en-int8", modelFileNames = new[] { "preprocess.onnx", "cached_decode.int8.onnx","uncached_decode.int8.onnx","encode.int8.onnx","tokens.txt" } },
                new SherpaOnnxModelMetadata { modelId = "sherpa-onnx-sense-voice-zh-en-ja-ko-yue-2024-07-17", modelFileNames = new[] { "model.onnx", "model.int8.onnx", "tokens.txt" } },
                new SherpaOnnxModelMetadata { modelId = "sherpa-onnx-sense-voice-zh-en-ja-ko-yue-int8-2025-09-09", modelFileNames = new[] { "model.int8.onnx", "tokens.txt" } },
                new SherpaOnnxModelMetadata { modelId = "sherpa-onnx-fire-red-asr-large-zh_en-2025-02-16", modelFileNames = new[] { "decoder.int8.onnx", "encoder.int8.onnx", "tokens.txt" } },
                new SherpaOnnxModelMetadata { modelId = "sherpa-onnx-dolphin-base-ctc-multi-lang-int8-2025-04-02", modelFileNames = new[] { "model.int8.onnx", "tokens.txt" } },
                new SherpaOnnxModelMetadata { modelId = "sherpa-onnx-dolphin-base-ctc-multi-lang-2025-04-02", modelFileNames = new[] { "model.onnx", "tokens.txt" } },
                new SherpaOnnxModelMetadata { modelId = "sherpa-onnx-dolphin-small-ctc-multi-lang-int8-2025-04-02", modelFileNames = new[] { "model.int8.onnx", "tokens.txt" } },
                new SherpaOnnxModelMetadata { modelId = "sherpa-onnx-dolphin-small-ctc-multi-lang-2025-04-02", modelFileNames = new[] { "model.onnx", "tokens.txt" } }
            };


            public static readonly SherpaOnnxModelMetadata[] VAD_MODELS_METADATA_TABLES = new[]
            {
                new SherpaOnnxModelMetadata {modelId = "silero-vad", modelFileNames = new[]{"silero_vad.onnx"}, modelFileHashes=new[]{"9e2449e1087496d8d4caba907f23e0bd3f78d91fa552479bb9c23ac09cbb1fd6"}, downloadUrl = "https://github.com/k2-fsa/sherpa-onnx/releases/download/asr-models/silero_vad.onnx",downloadFileHash = "9e2449e1087496d8d4caba907f23e0bd3f78d91fa552479bb9c23ac09cbb1fd6"},
                new SherpaOnnxModelMetadata {modelId = "silero-vad-int8", modelFileNames = new[]{"silero_vad.int8.onnx"}, modelFileHashes = new[]{"c36d490aff5ab924ca6c7aeec4d8f6bd3d22db6fa17611b9c5b17eae58ac3a20"}, downloadUrl = "https://github.com/k2-fsa/sherpa-onnx/releases/download/asr-models/silero_vad.int8.onnx", downloadFileHash = "c36d490aff5ab924ca6c7aeec4d8f6bd3d22db6fa17611b9c5b17eae58ac3a20"},
                new SherpaOnnxModelMetadata {modelId = "silero-vad-v4",modelFileNames = new[]{"silero_vad.onnx"},modelFileHashes = new[]{"a35ebf52fd3ce5f1469b2a36158dba761bc47b973ea3382b3186ca15b1f5af28"}, downloadUrl = "https://raw.githubusercontent.com/snakers4/silero-vad/refs/tags/v4.0/files/silero_vad.onnx", downloadFileHash = "a35ebf52fd3ce5f1469b2a36158dba761bc47b973ea3382b3186ca15b1f5af28"},


                new SherpaOnnxModelMetadata {modelId = "silero-vad-v5", modelFileNames = new[]{"silero_vad.onnx"}, modelFileHashes = new[] {"6b99cbfd39246b6706f98ec13c7c50c6b299181f2474fa05cbc8046acc274396"}, downloadUrl = "https://github.com/snakers4/silero-vad/raw/refs/tags/v5.0/files/silero_vad.onnx", downloadFileHash = "6b99cbfd39246b6706f98ec13c7c50c6b299181f2474fa05cbc8046acc274396"},


                new SherpaOnnxModelMetadata {modelId = "silero-vad-latest", modelFileNames = new[]{"silero_vad.onnx"}, downloadUrl = "https://github.com/snakers4/silero-vad/raw/refs/heads/master/src/silero_vad/data/silero_vad.onnx"},
                new SherpaOnnxModelMetadata {modelId = "ten-vad", modelFileNames = new[]{"ten-vad.onnx"}, modelFileHashes = new[] {"718cb7eef47e3cf5ddbe7e967a7503f46b8b469c0706872f494dfa921b486206"}, downloadUrl="https://github.com/k2-fsa/sherpa-onnx/releases/download/asr-models/ten-vad.onnx", downloadFileHash="718cb7eef47e3cf5ddbe7e967a7503f46b8b469c0706872f494dfa921b486206"},
                new SherpaOnnxModelMetadata {modelId = "ten-vad-int8", modelFileNames = new[]{"ten-vad.int8.onnx"}, modelFileHashes = new[] {"880c072f188efa169ea028b2159d1b3a438e153d080b87eac31b74ecad511e61"}, downloadUrl="https://github.com/k2-fsa/sherpa-onnx/releases/download/asr-models/ten-vad.int8.onnx", downloadFileHash="880c072f188efa169ea028b2159d1b3a438e153d080b87eac31b74ecad511e61"}
            };

            public static readonly SherpaOnnxModelMetadata[] TTS_MODELS_METADATA_TABLES = new[]
            {
                //vits_model
                new SherpaOnnxModelMetadata { modelId = "vits-melo-tts-zh_en", modelFileNames = new[] { "model.onnx","model.int8.onnx", "date.fst", "number.fst", "phone.fst", "lexicon.txt", "tokens.txt","dict" }, modelFileHashes= new string[]{"bf30582eb1b012250a35b1a4a80e7dfbcf8485e7bb9de0d95efbbeef0e4ad86d","0a1462eef98f05c15a6f57569bf2fb57a6b3995891bb17956187ff5e2694273e","eb8aa079ae3cb81d8f4404992f39d61a0cb990947512b5b8d1e54d1f6980e718","743f402181fcfebf76cc2f0546b71fa26476e626fbe4e460fb7b4c3a7a8bd5bd","1ac2b6fa56b1442320c4de7db08353bab8963a2b57f365eebcdd3a2d3562f8d7","7236884b02435ac5d10cf69b4be40a61b45aa676b5300f0e412f185748fee528","d18664a7e12bd7ea1022ddaf951e534e136815016c5a809d6b64156bffb4369d",string.Empty},downloadFileHash = "e58351ed7149f290a54534538badd4077cdbe6fddc964b24d0bee870415d1514", sampleRate=44100 },

                #region  Arabic
                new SherpaOnnxModelMetadata { modelId ="vits-piper-ar_JO-SA_dii-high", modelFileNames = new []{"ar_JO-SA_dii-high.onnx","tokens.txt","espeak-ng-data"}, numberOfSpeakers = 1, sampleRate = 22050},
                new SherpaOnnxModelMetadata { modelId ="vits-piper-ar_JO-SA_miro-high", modelFileNames = new []{"ar_JO-SA_miro-high.onnx","tokens.txt","espeak-ng-data"}, numberOfSpeakers = 1 , sampleRate = 22050},
                new SherpaOnnxModelMetadata { modelId ="vits-piper-ar_JO-SA_miro_V2-high", modelFileNames = new []{"ar_JO-SA_miro_V2-high.onnx","tokens.txt","espeak-ng-data"}, numberOfSpeakers = 1 , sampleRate = 22050},
                new SherpaOnnxModelMetadata { modelId ="vits-piper-ar_JO-kareem-low", modelFileNames = new []{"ar_JO-kareem-low.onnx","tokens.txt","espeak-ng-data"}, numberOfSpeakers = 1 , sampleRate = 16000},
                new SherpaOnnxModelMetadata { modelId ="vits-piper-ar_JO-kareem-medium", modelFileNames = new []{"ar_JO-kareem-medium.onnx","tokens.txt","espeak-ng-data"}, numberOfSpeakers = 1 ,sampleRate = 22050},
                #endregion

                #region Catalan
                new SherpaOnnxModelMetadata { modelId ="vits-piper-ca_ES-upc_ona-medium", modelFileNames = new []{"ca_ES-upc_ona-medium.onnx","tokens.txt","espeak-ng-data"}, numberOfSpeakers = 1 , sampleRate = 22050},
                new SherpaOnnxModelMetadata { modelId ="vits-piper-ca_ES-upc_ona-x_low", modelFileNames = new []{"ca_ES-upc_ona-x_low.onnx","tokens.txt","espeak-ng-data"}, numberOfSpeakers = 1 , sampleRate = 16000},
                new SherpaOnnxModelMetadata { modelId ="vits-piper-ca_ES-upc_pau-x_low", modelFileNames = new []{"ca_ES-upc_pau-x_low.onnx","tokens.txt","espeak-ng-data"}, numberOfSpeakers = 1 , sampleRate = 16000},
                #endregion

                #region Chinese
                new SherpaOnnxModelMetadata {modelId = "matcha-icefall-zh-baker", modelFileNames = new[] { "model-steps-3.onnx","date.fst","number.fst","phone.fst","lexicon.txt", "tokens.txt" , "dict"},modelFileHashes = new string[]{"ef7ebdf5987e16a5836136a51d6f3560ca997ffd33d06a40daab5af92b4b86e5","eb8aa079ae3cb81d8f4404992f39d61a0cb990947512b5b8d1e54d1f6980e718","743f402181fcfebf76cc2f0546b71fa26476e626fbe4e460fb7b4c3a7a8bd5bd","1ac2b6fa56b1442320c4de7db08353bab8963a2b57f365eebcdd3a2d3562f8d7","38b886d46aefa50da6322a64d72fd595d5f4fae1051adb160d647541b1e0a4a2","56209b2bf609d5ac1d66ede6dae7bf5254bd3f8aa24c4a6823713d5b884d87ba",string.Empty}, downloadFileHash="d9b417a8f52d481a4c9abd540e6f38b18ded6730f67cbffb7f133e196830e09e", numberOfSpeakers = 1 ,sampleRate=22050 },
                new SherpaOnnxModelMetadata { modelId = "sherpa-onnx-vits-zh-ll", modelFileNames = new[] { "model.onnx", "date.fst", "number.fst", "phone.fst", "new_heteronym.fst", "lexicon.txt", "tokens.txt" , "dict"} , numberOfSpeakers = 5 ,sampleRate=16000},
                new SherpaOnnxModelMetadata { modelId = "vits-zh-hf-fanchen-C", modelFileNames = new[] { "vits-zh-hf-fanchen-C.onnx", "date.fst", "number.fst", "phone.fst", "new_heteronym.fst", "lexicon.txt", "tokens.txt" , "dict"} ,numberOfSpeakers = 187,sampleRate=16000},
                new SherpaOnnxModelMetadata { modelId = "vits-zh-hf-fanchen-wnj", modelFileNames = new[] { "vits-zh-hf-fanchen-wnj.onnx", "date.fst", "number.fst", "phone.fst", "new_heteronym.fst", "lexicon.txt", "tokens.txt" , "dict"}, numberOfSpeakers = 1 ,sampleRate=16000 },
                new SherpaOnnxModelMetadata { modelId = "vits-zh-hf-theresa", modelFileNames = new[] { "theresa.onnx", "date.fst", "number.fst", "phone.fst", "new_heteronym.fst", "lexicon.txt", "tokens.txt" , "dict"}, numberOfSpeakers = 804 ,sampleRate=22050},
                new SherpaOnnxModelMetadata { modelId = "vits-zh-hf-eula", modelFileNames = new[] { "eula.onnx", "date.fst", "number.fst", "phone.fst", "new_heteronym.fst", "lexicon.txt", "tokens.txt" , "dict"}, numberOfSpeakers = 804 ,sampleRate=22050 },
                new SherpaOnnxModelMetadata { modelId = "vits-icefall-zh-aishell3", modelFileNames = new[] { "model.onnx", "date.fst", "number.fst", "phone.fst", "lexicon.txt", "tokens.txt" }, numberOfSpeakers = 174 ,sampleRate=8000},
                #endregion

                #region Czech
                new SherpaOnnxModelMetadata { modelId ="vits-piper-cs_CZ-jirka-low", modelFileNames = new []{"cs_CZ-jirka-low.onnx","tokens.txt","espeak-ng-data"}, numberOfSpeakers = 1 ,sampleRate = 16000},
                new SherpaOnnxModelMetadata { modelId ="vits-piper-cs_CZ-jirka-medium", modelFileNames = new []{"cs_CZ-jirka-medium.onnx","tokens.txt","espeak-ng-data"}, numberOfSpeakers = 1 , sampleRate = 22050},
                #endregion
                
                #region Danish
                new SherpaOnnxModelMetadata { modelId ="vits-piper-da_DK-talesyntese-medium", modelFileNames = new []{"da_DK-talesyntese-medium.onnx","tokens.txt","espeak-ng-data"}, numberOfSpeakers = 1 , sampleRate = 22050},
                #endregion
                
                #region Dutch
                new SherpaOnnxModelMetadata { modelId ="vits-piper-nl_BE-nathalie-medium", modelFileNames = new []{"nl_BE-nathalie-medium.onnx","tokens.txt","espeak-ng-data"}, numberOfSpeakers = 1 , sampleRate = 22050},
                new SherpaOnnxModelMetadata { modelId ="vits-piper-nl_BE-nathalie-x_low", modelFileNames = new []{"nl_BE-nathalie-x_low.onnx","tokens.txt","espeak-ng-data"}, numberOfSpeakers = 1 ,sampleRate = 16000},
                new SherpaOnnxModelMetadata { modelId ="vits-piper-nl_NL-dii-high", modelFileNames = new []{"nl_NL-dii-high.onnx","tokens.txt","espeak-ng-data"}, numberOfSpeakers = 1 ,sampleRate = 22050},
                new SherpaOnnxModelMetadata { modelId ="vits-piper-nl_NL-miro-high", modelFileNames = new []{"nl_NL-miro-high.onnx","tokens.txt","espeak-ng-data"}, numberOfSpeakers = 1 ,sampleRate = 22050},
                new SherpaOnnxModelMetadata { modelId ="vits-piper-nl_NL-pim-medium", modelFileNames = new []{"nl_NL-pim-medium.onnx","tokens.txt","espeak-ng-data"}, numberOfSpeakers = 1 ,sampleRate = 22050},
                new SherpaOnnxModelMetadata { modelId ="vits-piper-nl_NL-ronnie-medium", modelFileNames = new []{"nl_NL-ronnie-medium.onnx","tokens.txt","espeak-ng-data"}, numberOfSpeakers = 1 ,sampleRate = 22050},
                #endregion
                #region English
                new SherpaOnnxModelMetadata { modelId = "vits-ljs", modelFileNames = new[] { "vits-ljs.onnx", "lexicon.txt", "tokens.txt" }, numberOfSpeakers = 1 ,sampleRate=22050},
                new SherpaOnnxModelMetadata { modelId = "vits-vctk", modelFileNames = new[] { "vits-vctk.onnx", "vits-vctk.int8.onnx", "lexicon.txt", "tokens.txt" }, numberOfSpeakers = 109 ,sampleRate=22050},
                new SherpaOnnxModelMetadata {modelId = "matcha-icefall-en_US-ljspeech", modelFileNames = new[] { "model-steps-3.onnx", "tokens.txt" , "espeak-ng-data"}, numberOfSpeakers = 1 ,sampleRate=22050},
                new SherpaOnnxModelMetadata {modelId = "kitten-nano-en-v0_1-fp16", modelFileNames = new[] { "model.fp16.onnx","voices.bin", "tokens.txt" , "espeak-ng-data"}, numberOfSpeakers = 8 ,sampleRate=24000},
                new SherpaOnnxModelMetadata {modelId = "kitten-nano-en-v0_2-fp16", modelFileNames = new[] { "model.fp16.onnx","voices.bin", "tokens.txt" , "espeak-ng-data"}, numberOfSpeakers = 8 ,sampleRate=24000},
                new SherpaOnnxModelMetadata {modelId = "kitten-mini-en-v0_1-fp16", modelFileNames = new[] { "model.fp16.onnx","voices.bin", "tokens.txt" , "espeak-ng-data"}, numberOfSpeakers = 8 ,sampleRate=24000},
                new SherpaOnnxModelMetadata { modelId = "kokoro-en-v0_19", modelFileNames = new[] { "model.onnx","voices.bin", "tokens.txt" ,"espeak-ng-data"}, numberOfSpeakers = 11,sampleRate=24000},
                new SherpaOnnxModelMetadata { modelId = "vits-piper-en_GB-alan-low", modelFileNames = new[] { "en_GB-alan-low.onnx","tokens.txt","espeak-ng-data"}, numberOfSpeakers = 1,sampleRate=16000},
                new SherpaOnnxModelMetadata { modelId = "vits-piper-en_GB-alan-medium", modelFileNames = new[] { "en_GB-alan-medium.onnx","tokens.txt","espeak-ng-data"}, numberOfSpeakers = 1,sampleRate=22050},
                new SherpaOnnxModelMetadata { modelId = "vits-piper-en_GB-alba-medium", modelFileNames = new[] { "en_GB-alba-medium.onnx","tokens.txt","espeak-ng-data"}, numberOfSpeakers = 1,sampleRate=22050},
                new SherpaOnnxModelMetadata { modelId = "vits-piper-en_GB-aru-medium", modelFileNames = new[] { "en_GB-aru-medium.onnx","tokens.txt","espeak-ng-data"}, numberOfSpeakers = 1,sampleRate=22050},
                new SherpaOnnxModelMetadata { modelId = "vits-piper-en_GB-cori-high", modelFileNames = new[] { "en_GB-cori-high.onnx","tokens.txt","espeak-ng-data"}, numberOfSpeakers = 1,sampleRate=22050},
                new SherpaOnnxModelMetadata { modelId = "vits-piper-en_GB-cori-medium", modelFileNames = new[] { "en_GB-cori-medium.onnx","tokens.txt","espeak-ng-data"}, numberOfSpeakers = 1,sampleRate=22050},
                new SherpaOnnxModelMetadata { modelId = "vits-piper-en_GB-dii-high", modelFileNames = new[] { "en_GB-dii-high.onnx","tokens.txt","espeak-ng-data"}, numberOfSpeakers = 1,sampleRate=22050},
                new SherpaOnnxModelMetadata { modelId = "vits-piper-en_GB-jenny_dioco-medium", modelFileNames = new[] { "en_GB-jenny_dioco-medium.onnx","tokens.txt","espeak-ng-data"}, numberOfSpeakers = 1,sampleRate=22050},
                new SherpaOnnxModelMetadata { modelId = "vits-piper-en_GB-miro-high", modelFileNames = new[] { "en_GB-miro-high.onnx","tokens.txt","espeak-ng-data"}, numberOfSpeakers = 1,sampleRate=22050},
                new SherpaOnnxModelMetadata { modelId = "vits-piper-en_GB-northern_english_male-medium", modelFileNames = new[] { "en_GB-northern_english_male-medium.onnx","tokens.txt","espeak-ng-data"}, numberOfSpeakers = 1,sampleRate=22050},
                new SherpaOnnxModelMetadata { modelId = "vits-piper-en_GB-semaine-medium", modelFileNames = new[] { "en_GB-semaine-medium.onnx","tokens.txt","espeak-ng-data"}, numberOfSpeakers = 4,sampleRate=22050},
                new SherpaOnnxModelMetadata { modelId = "vits-piper-en_GB-southern_english_female-low", modelFileNames = new[] { "en_GB-southern_english_female-low.onnx","tokens.txt","espeak-ng-data"}, numberOfSpeakers = 1,sampleRate=16000},
                new SherpaOnnxModelMetadata { modelId = "vits-piper-en_GB-southern_english_female-medium", modelFileNames = new[] { "southern_english_female-medium.onnx","tokens.txt","espeak-ng-data"}, numberOfSpeakers = 6,sampleRate=22050},
                new SherpaOnnxModelMetadata { modelId = "vits-piper-en_GB-southern_english_male-medium", modelFileNames = new[] { "en_GB-southern_english_male-medium.onnx","tokens.txt","espeak-ng-data"}, numberOfSpeakers = 8,sampleRate=22050},
                new SherpaOnnxModelMetadata { modelId = "vits-piper-en_GB-vctk-medium", modelFileNames = new[] { "en_GB-vctk-medium.onnx","tokens.txt","espeak-ng-data"}, numberOfSpeakers = 109,sampleRate=22050},
                new SherpaOnnxModelMetadata { modelId = "vits-piper-en_US-amy-low", modelFileNames = new[] { "en_US-amy-low.onnx","tokens.txt","espeak-ng-data"}, numberOfSpeakers = 1,sampleRate=22050},
                new SherpaOnnxModelMetadata { modelId = "vits-piper-en_US-arctic-medium", modelFileNames = new[] { "en_US-arctic-medium.onnx","tokens.txt","espeak-ng-data"}, numberOfSpeakers = 18,sampleRate=22050},
                new SherpaOnnxModelMetadata { modelId = "vits-piper-en_US-bryce-medium", modelFileNames = new[] { "en_US-bryce-medium.onnx","tokens.txt","espeak-ng-data"}, numberOfSpeakers = 1,sampleRate=22050},
                new SherpaOnnxModelMetadata { modelId = "vits-piper-en_US-danny-low", modelFileNames = new[] { "en_US-danny-low.onnx","tokens.txt","espeak-ng-data"}, numberOfSpeakers = 1,sampleRate=16000},
                new SherpaOnnxModelMetadata { modelId = "vits-piper-en_US-glados", modelFileNames = new[] { "en_US-glados.onnx",  "tokens.txt", "espeak-ng-data"}, numberOfSpeakers = 1 ,sampleRate=22050},
                new SherpaOnnxModelMetadata { modelId = "vits-piper-en_US-glados-high", modelFileNames = new[] { "en_US-glados-high.onnx","tokens.txt", "espeak-ng-data"}, numberOfSpeakers = 1 ,sampleRate=22050},
                new SherpaOnnxModelMetadata { modelId = "vits-piper-en_US-hfc_female-medium", modelFileNames = new[] { "en_US-hfc_female-medium.onnx","tokens.txt", "espeak-ng-data"}, numberOfSpeakers = 1 ,sampleRate=22050},
                new SherpaOnnxModelMetadata { modelId = "vits-piper-en_US-hfc_male-medium", modelFileNames = new[] { "en_US-hfc_male-medium.onnx","tokens.txt", "espeak-ng-data"}, numberOfSpeakers = 1 ,sampleRate=22050},
                new SherpaOnnxModelMetadata { modelId = "vits-piper-en_US-joe-medium", modelFileNames = new[] { "en_US-joe-medium.onnx","tokens.txt", "espeak-ng-data"}, numberOfSpeakers = 1 ,sampleRate=22050},
                new SherpaOnnxModelMetadata { modelId = "vits-piper-en_US-john-medium", modelFileNames = new[] { "en_US-john-medium.onnx","tokens.txt", "espeak-ng-data"}, numberOfSpeakers = 1 ,sampleRate=22050},
                new SherpaOnnxModelMetadata { modelId = "vits-piper-en_US-kathleen-low", modelFileNames = new[] { "en_US-kathleen-low.onnx","tokens.txt", "espeak-ng-data"}, numberOfSpeakers = 1 ,sampleRate=16000},
                new SherpaOnnxModelMetadata { modelId = "vits-piper-en_US-kristin-medium", modelFileNames = new[] { "en_US-kristin-medium.onnx","tokens.txt", "espeak-ng-data"}, numberOfSpeakers = 1 ,sampleRate=22050},
                new SherpaOnnxModelMetadata { modelId = "vits-piper-en_US-kusal-medium", modelFileNames = new[] { "en_US-kusal-medium.onnx","tokens.txt", "espeak-ng-data"}, numberOfSpeakers = 1 ,sampleRate=22050},
                new SherpaOnnxModelMetadata { modelId = "vits-piper-en_US-l2arctic-medium", modelFileNames = new[] { "en_US-l2arctic-medium.onnx","tokens.txt", "espeak-ng-data"}, numberOfSpeakers = 24 ,sampleRate=22050},
                new SherpaOnnxModelMetadata { modelId = "vits-piper-en_US-lessac-high", modelFileNames = new[] { "en_US-lessac-high.onnx","tokens.txt", "espeak-ng-data"}, numberOfSpeakers = 1 ,sampleRate=22050},
                new SherpaOnnxModelMetadata { modelId = "vits-piper-en_US-lessac-low", modelFileNames = new[] { "en_US-lessac-low.onnx","tokens.txt", "espeak-ng-data"}, numberOfSpeakers = 1 ,sampleRate=16000},
                new SherpaOnnxModelMetadata { modelId = "vits-piper-en_US-lessac-medium", modelFileNames = new[] { "en_US-lessac-medium.onnx","tokens.txt", "espeak-ng-data"}, numberOfSpeakers = 1 ,sampleRate=22050},
                new SherpaOnnxModelMetadata { modelId = "vits-piper-en_US-libritts-high", modelFileNames = new[] { "en_US-libritts-high.onnx","tokens.txt", "espeak-ng-data"}, numberOfSpeakers = 904 ,sampleRate=22050},
                new SherpaOnnxModelMetadata { modelId = "vits-piper-en_US-libritts_r-medium", modelFileNames = new[] { "en_US-libritts_r-medium.onnx","tokens.txt", "espeak-ng-data"}, numberOfSpeakers = 904 ,sampleRate=22050},
                new SherpaOnnxModelMetadata { modelId = "vits-piper-en_US-ljspeech-high", modelFileNames = new[] { "en_US-ljspeech-high.onnx","tokens.txt", "espeak-ng-data"}, numberOfSpeakers = 1 ,sampleRate=22050},
                new SherpaOnnxModelMetadata { modelId = "vits-piper-en_US-ljspeech-medium", modelFileNames = new[] { "en_US-ljspeech-medium.onnx","tokens.txt", "espeak-ng-data"}, numberOfSpeakers = 1 ,sampleRate=22050},
                new SherpaOnnxModelMetadata { modelId = "vits-piper-en_US-miro-high", modelFileNames = new[] { "en_US-miro-high.onnx","tokens.txt", "espeak-ng-data"}, numberOfSpeakers = 1 ,sampleRate=22050},
                new SherpaOnnxModelMetadata { modelId = "vits-piper-en_US-norman-medium", modelFileNames = new[] { "en_US-norman-medium.onnx","tokens.txt", "espeak-ng-data"}, numberOfSpeakers = 1 ,sampleRate=22050},
                new SherpaOnnxModelMetadata { modelId = "vits-piper-en_US-reza_ibrahim-medium", modelFileNames = new[] { "en_US-reza_ibrahim-medium.onnx","tokens.txt", "espeak-ng-data"}, numberOfSpeakers = 1 ,sampleRate=22050},
                new SherpaOnnxModelMetadata { modelId = "vits-piper-en_US-ryan-high", modelFileNames = new[] { "en_US-ryan-high.onnx","tokens.txt", "espeak-ng-data"}, numberOfSpeakers = 1 ,sampleRate=22050},
                new SherpaOnnxModelMetadata { modelId = "vits-piper-en_US-ryan-low", modelFileNames = new[] { "en_US-ryan-low.onnx","tokens.txt", "espeak-ng-data"}, numberOfSpeakers = 1 ,sampleRate=16000},
                new SherpaOnnxModelMetadata { modelId = "vits-piper-en_US-ryan-medium", modelFileNames = new[] { "en_US-ryan-medium.onnx","tokens.txt", "espeak-ng-data"}, numberOfSpeakers = 1 ,sampleRate=22050},
                new SherpaOnnxModelMetadata { modelId = "vits-piper-en_US-sam-medium", modelFileNames = new[] { "en_US-sam-medium.onnx","tokens.txt", "espeak-ng-data"}, numberOfSpeakers = 1 ,sampleRate=22050},
                #endregion

                #region Finnish
                new SherpaOnnxModelMetadata { modelId = "vits-piper-fi_FI-harri-low", modelFileNames = new[] { "fi_FI-harri-low.onnx","tokens.txt", "espeak-ng-data"}, numberOfSpeakers = 1 ,sampleRate=16000},
                new SherpaOnnxModelMetadata { modelId = "vits-piper-fi_FI-harri-medium", modelFileNames = new[] { "fi_FI-harri-medium.onnx","tokens.txt", "espeak-ng-data"}, numberOfSpeakers = 1 ,sampleRate=22050},
                #endregion
                
                #region French
                new SherpaOnnxModelMetadata { modelId = "vits-piper-fr_FR-gilles-low", modelFileNames = new[] { "fr_FR-gilles-low.onnx","tokens.txt", "espeak-ng-data"}, numberOfSpeakers = 1 ,sampleRate=16000},
                new SherpaOnnxModelMetadata { modelId = "vits-piper-fr_FR-miro-high", modelFileNames = new[] { "fr_FR-miro-high.onnx","tokens.txt", "espeak-ng-data"}, numberOfSpeakers = 1 ,sampleRate=22050},
                new SherpaOnnxModelMetadata { modelId = "vits-piper-fr_FR-siwis-low", modelFileNames = new[] { "fr_FR-siwis-low.onnx","tokens.txt", "espeak-ng-data"}, numberOfSpeakers = 1 ,sampleRate=16000},
                new SherpaOnnxModelMetadata { modelId = "vits-piper-fr_FR-siwis-medium", modelFileNames = new[] { "fr_FR-siwis-medium.onnx","tokens.txt", "espeak-ng-data"}, numberOfSpeakers = 1 ,sampleRate=22050},
                new SherpaOnnxModelMetadata { modelId = "vits-piper-fr_FR-tjiho-model1", modelFileNames = new[] { "fr_FR-tjiho-model1.onnx","tokens.txt", "espeak-ng-data"}, numberOfSpeakers = 1 ,sampleRate=44100},
                new SherpaOnnxModelMetadata { modelId = "vits-piper-fr_FR-tjiho-model2", modelFileNames = new[] { "fr_FR-tjiho-model2.onnx","tokens.txt", "espeak-ng-data"}, numberOfSpeakers = 1 ,sampleRate=44100},
                new SherpaOnnxModelMetadata { modelId = "vits-piper-fr_FR-tjiho-model3", modelFileNames = new[] { "fr_FR-tjiho-model3.onnx","tokens.txt", "espeak-ng-data"}, numberOfSpeakers = 1 ,sampleRate=44100},
                new SherpaOnnxModelMetadata { modelId = "vits-piper-fr_FR-tom-medium", modelFileNames = new[] { "fr_FR-tom-medium.onnx","tokens.txt", "espeak-ng-data"}, numberOfSpeakers = 1 ,sampleRate=44100},
                new SherpaOnnxModelMetadata { modelId = "vits-piper-fr_FR-upmc-medium", modelFileNames = new[] { "fr_FR-upmc-medium.onnx","tokens.txt", "espeak-ng-data"}, numberOfSpeakers = 2 ,sampleRate=22050},
                #endregion

                #region Georgian 
                new SherpaOnnxModelMetadata { modelId = "vits-piper-ka_GE-natia-medium", modelFileNames = new[] { "ka_GE-natia-medium.onnx","tokens.txt", "espeak-ng-data"}, numberOfSpeakers = 1 ,sampleRate=22050},
                #endregion

                #region German
                new SherpaOnnxModelMetadata { modelId = "vits-piper-de_DE-dii-high", modelFileNames = new[] { "de_DE-dii-high.onnx","tokens.txt", "espeak-ng-data"}, numberOfSpeakers = 1 ,sampleRate=22050},
                new SherpaOnnxModelMetadata { modelId = "vits-piper-de_DE-eva_k-x_low", modelFileNames = new[] { "de_DE-eva_k-x_low.onnx","tokens.txt", "espeak-ng-data"}, numberOfSpeakers = 1 ,sampleRate=16000},
                new SherpaOnnxModelMetadata { modelId = "vits-piper-de_DE-glados-high", modelFileNames = new[] { "de_DE-glados-high.onnx","tokens.txt", "espeak-ng-data"}, numberOfSpeakers = 1 ,sampleRate=22050},
                new SherpaOnnxModelMetadata { modelId = "vits-piper-de_DE-glados-low", modelFileNames = new[] { "de_DE-glados-low.onnx","tokens.txt", "espeak-ng-data"}, numberOfSpeakers = 1 ,sampleRate=16000},
                new SherpaOnnxModelMetadata { modelId = "vits-piper-de_DE-glados-medium", modelFileNames = new[] { "de_DE-glados-medium.onnx","tokens.txt", "espeak-ng-data"}, numberOfSpeakers = 1 ,sampleRate=22050},
                new SherpaOnnxModelMetadata { modelId = "vits-piper-de_DE-glados_turret-high", modelFileNames = new[] { "de_DE-glados_turret-high.onnx","tokens.txt", "espeak-ng-data"}, numberOfSpeakers = 1 ,sampleRate=22050},
                new SherpaOnnxModelMetadata { modelId = "vits-piper-de_DE-glados_turret-low", modelFileNames = new[] { "de_DE-glados_turret-low.onnx","tokens.txt", "espeak-ng-data"}, numberOfSpeakers = 1 ,sampleRate=16000},
                new SherpaOnnxModelMetadata { modelId = "vits-piper-de_DE-glados_turret-medium", modelFileNames = new[] { "de_DE-glados_turret-medium.onnx","tokens.txt", "espeak-ng-data"}, numberOfSpeakers = 1 ,sampleRate=22050},
                new SherpaOnnxModelMetadata { modelId = "vits-piper-de_DE-karlsson-low", modelFileNames = new[] { "de_DE-karlsson-low.onnx","tokens.txt", "espeak-ng-data"}, numberOfSpeakers = 1 ,sampleRate=16000},
                new SherpaOnnxModelMetadata { modelId = "vits-piper-de_DE-kerstin-low", modelFileNames = new[] { "de_DE-kerstin-low.onnx","tokens.txt", "espeak-ng-data"}, numberOfSpeakers = 1 ,sampleRate=16000},
                new SherpaOnnxModelMetadata { modelId = "vits-piper-de_DE-miro-high", modelFileNames = new[] { "de_DE-miro-high.onnx","tokens.txt", "espeak-ng-data"}, numberOfSpeakers = 1 ,sampleRate=22050},
                new SherpaOnnxModelMetadata { modelId = "vits-piper-de_DE-pavoque-low", modelFileNames = new[] { "de_DE-pavoque-low.onnx","tokens.txt", "espeak-ng-data"}, numberOfSpeakers = 1 ,sampleRate=16000},
                new SherpaOnnxModelMetadata { modelId = "vits-piper-de_DE-ramona-low", modelFileNames = new[] { "de_DE-ramona-low.onnx","tokens.txt", "espeak-ng-data"}, numberOfSpeakers = 1 ,sampleRate=16000},
                new SherpaOnnxModelMetadata { modelId = "vits-piper-de_DE-thorsten-high", modelFileNames = new[] { "de_DE-thorsten-high.onnx","tokens.txt", "espeak-ng-data"}, numberOfSpeakers = 1 ,sampleRate=22050},
                new SherpaOnnxModelMetadata { modelId = "vits-piper-de_DE-thorsten-low", modelFileNames = new[] { "de_DE-thorsten-low.onnx","tokens.txt", "espeak-ng-data"}, numberOfSpeakers = 1 ,sampleRate=16000},
                new SherpaOnnxModelMetadata { modelId = "vits-piper-de_DE-thorsten-medium", modelFileNames = new[] { "de_DE-thorsten-medium.onnx","tokens.txt", "espeak-ng-data"}, numberOfSpeakers = 1 ,sampleRate=22050},
                new SherpaOnnxModelMetadata { modelId = "vits-piper-de_DE-thorsten_emotional-medium", modelFileNames = new[] { "de_DE-thorsten_emotional-medium.onnx","tokens.txt", "espeak-ng-data"}, numberOfSpeakers = 8 ,sampleRate=22050},
                #endregion

                #region Greek
                new SherpaOnnxModelMetadata { modelId = "vits-piper-el_GR-rapunzelina-low", modelFileNames = new[] { "el_GR-rapunzelina-low.onnx","tokens.txt", "espeak-ng-data"}, numberOfSpeakers = 1 ,sampleRate=16000},
                #endregion
                
                #region Hindi
                new SherpaOnnxModelMetadata { modelId = "vits-piper-hi_IN-pratham-medium", modelFileNames = new[] { "hi_IN-pratham-medium.onnx","tokens.txt", "espeak-ng-data"}, numberOfSpeakers = 1 ,sampleRate=22050},
                new SherpaOnnxModelMetadata { modelId = "vits-piper-hi_IN-priyamvada-medium", modelFileNames = new[] { "hi_IN-priyamvada-medium.onnx","tokens.txt", "espeak-ng-data"}, numberOfSpeakers = 1 ,sampleRate=22050},
                new SherpaOnnxModelMetadata { modelId = "vits-piper-hi_IN-rohan-medium", modelFileNames = new[] { "hi_IN-rohan-medium.onnx","tokens.txt", "espeak-ng-data"}, numberOfSpeakers = 1 ,sampleRate=22050},
                #endregion

                #region Hungarian
                new SherpaOnnxModelMetadata { modelId = "vits-piper-hu_HU-anna-medium", modelFileNames = new[] { "hu_HU-anna-medium.onnx","tokens.txt", "espeak-ng-data"}, numberOfSpeakers = 1 ,sampleRate=22050},
                new SherpaOnnxModelMetadata { modelId = "vits-piper-hu_HU-berta-medium", modelFileNames = new[] { "hu_HU-berta-medium.onnx","tokens.txt", "espeak-ng-data"}, numberOfSpeakers = 1 ,sampleRate=22050},
                new SherpaOnnxModelMetadata { modelId = "vits-piper-hu_HU-imre-medium", modelFileNames = new[] { "hu_HU-imre-medium.onnx","tokens.txt", "espeak-ng-data"}, numberOfSpeakers = 1 ,sampleRate=22050},
                #endregion

                #region icelandic
                new SherpaOnnxModelMetadata { modelId = "vits-piper-is_IS-bui-medium", modelFileNames = new[] { "is_IS-bui-medium.onnx","tokens.txt", "espeak-ng-data"}, numberOfSpeakers = 1 ,sampleRate=22050},
                new SherpaOnnxModelMetadata { modelId = "vits-piper-is_IS-salka-medium", modelFileNames = new[] { "is_IS-salka-medium.onnx","tokens.txt", "espeak-ng-data"}, numberOfSpeakers = 1 ,sampleRate=22050},
                new SherpaOnnxModelMetadata { modelId = "vits-piper-is_IS-steinn-medium", modelFileNames = new[] { "is_IS-steinn-medium.onnx","tokens.txt", "espeak-ng-data"}, numberOfSpeakers = 1 ,sampleRate=22050},
                new SherpaOnnxModelMetadata { modelId = "vits-piper-is_IS-ugla-medium", modelFileNames = new[] { "is_IS-ugla-medium.onnx","tokens.txt", "espeak-ng-data"}, numberOfSpeakers = 1 ,sampleRate=22050},
                #endregion

                #region Indonesian
                new SherpaOnnxModelMetadata { modelId = "vits-piper-id_ID-news_tts-medium", modelFileNames = new[] { "id_ID-news_tts-medium.onnx","tokens.txt", "espeak-ng-data"}, numberOfSpeakers = 1 ,sampleRate=22050},
                #endregion

                #region Italian
                new SherpaOnnxModelMetadata { modelId = "vits-piper-it_IT-dii-high", modelFileNames = new[] { "it_IT-dii-high.onnx","tokens.txt", "espeak-ng-data"}, numberOfSpeakers = 1 ,sampleRate=22050},
                new SherpaOnnxModelMetadata { modelId = "vits-piper-it_IT-miro-high", modelFileNames = new[] { "it_IT-miro-high.onnx","tokens.txt", "espeak-ng-data"}, numberOfSpeakers = 1 ,sampleRate=22050},
                new SherpaOnnxModelMetadata { modelId = "vits-piper-it_IT-paola-medium", modelFileNames = new[] { "it_IT-paola-medium.onnx","tokens.txt", "espeak-ng-data"}, numberOfSpeakers = 1 ,sampleRate=22050},
                new SherpaOnnxModelMetadata { modelId = "vits-piper-it_IT-riccardo-x_low", modelFileNames = new[] { "it_IT-riccardo-x_low.onnx","tokens.txt", "espeak-ng-data"}, numberOfSpeakers = 1 ,sampleRate=16000},
                #endregion

                #region Kazakh
                new SherpaOnnxModelMetadata { modelId = "vits-piper-kk_KZ-iseke-x_low", modelFileNames = new[] { "kk_KZ-iseke-x_low.onnx","tokens.txt", "espeak-ng-data"}, numberOfSpeakers = 1 ,sampleRate=16000},
                new SherpaOnnxModelMetadata { modelId = "vits-piper-kk_KZ-issai-high", modelFileNames = new[] { "kk_KZ-issai-high.onnx","tokens.txt", "espeak-ng-data"}, numberOfSpeakers = 6 ,sampleRate=22050},
                new SherpaOnnxModelMetadata { modelId = "vits-piper-kk_KZ-raya-x_low", modelFileNames = new[] { "kk_KZ-raya-x_low.onnx","tokens.txt", "espeak-ng-data"}, numberOfSpeakers = 1 ,sampleRate=16000},
                #endregion

                #region Latvian
                new SherpaOnnxModelMetadata { modelId = "vits-piper-lv_LV-aivars-medium", modelFileNames = new[] { "lv_LV-aivars-medium.onnx","tokens.txt", "espeak-ng-data"}, numberOfSpeakers = 1 ,sampleRate=22050},
                #endregion

                #region Luxembourgish
                new SherpaOnnxModelMetadata { modelId = "vits-piper-lb_LU-marylux-medium", modelFileNames = new[] { "lb_LU-marylux-medium.onnx","tokens.txt", "espeak-ng-data"}, numberOfSpeakers = 1 ,sampleRate=22050},
                #endregion

                #region Malayalam
                new SherpaOnnxModelMetadata { modelId = "vits-piper-ml_IN-arjun-medium", modelFileNames = new[] { "ml_IN-arjun-medium.onnx","tokens.txt", "espeak-ng-data"}, numberOfSpeakers = 1 ,sampleRate=22050},
                new SherpaOnnxModelMetadata { modelId = "vits-piper-ml_IN-meera-medium", modelFileNames = new[] { "ml_IN-meera-medium.onnx","tokens.txt", "espeak-ng-data"}, numberOfSpeakers = 1 ,sampleRate=22050},
                #endregion

                #region Nepali
                new SherpaOnnxModelMetadata { modelId = "vits-piper-ne_NP-chitwan-medium", modelFileNames = new[] { "ne_NP-chitwan-medium.onnx","tokens.txt", "espeak-ng-data"}, numberOfSpeakers = 1 ,sampleRate=22050},
                new SherpaOnnxModelMetadata { modelId = "vits-piper-ne_NP-google-medium", modelFileNames = new[] { "ne_NP-google-medium.onnx","tokens.txt", "espeak-ng-data"}, numberOfSpeakers = 18 ,sampleRate=22050},
                new SherpaOnnxModelMetadata { modelId = "vits-piper-ne_NP-google-x_low", modelFileNames = new[] { "ne_NP-google-x_low.onnx","tokens.txt", "espeak-ng-data"}, numberOfSpeakers = 18 ,sampleRate=16000},
                #endregion

                #region Norwegian
                new SherpaOnnxModelMetadata { modelId = "vits-piper-no_NO-talesyntese-medium", modelFileNames = new[] { "no_NO-talesyntese-medium.onnx","tokens.txt", "espeak-ng-data"}, numberOfSpeakers = 1 ,sampleRate=22050},
                #endregion

                #region Persian
                new SherpaOnnxModelMetadata { modelId = "vits-piper-fa_IR-amir-medium", modelFileNames = new[] { "fa_IR-amir-medium.onnx","tokens.txt", "espeak-ng-data"}, numberOfSpeakers = 1 ,sampleRate=22050},
                new SherpaOnnxModelMetadata { modelId = "vits-piper-fa_IR-ganji-medium", modelFileNames = new[] { "fa_IR-ganji-medium.onnx","tokens.txt", "espeak-ng-data"}, numberOfSpeakers = 1 ,sampleRate=22050},
                new SherpaOnnxModelMetadata { modelId = "vits-piper-fa_IR-ganji_adabi-medium", modelFileNames = new[] { "fa_IR-ganji_adabi-medium.onnx","tokens.txt", "espeak-ng-data"}, numberOfSpeakers = 1 ,sampleRate=22050},
                new SherpaOnnxModelMetadata { modelId = "vits-piper-fa_IR-gyro-medium", modelFileNames = new[] { "fa_IR-gyro-medium.onnx","tokens.txt", "espeak-ng-data"}, numberOfSpeakers = 1 ,sampleRate=22050},
                new SherpaOnnxModelMetadata { modelId = "vits-piper-fa_IR-reza_ibrahim-medium", modelFileNames = new[] { "fa_IR-reza_ibrahim-medium.onnx","tokens.txt", "espeak-ng-data"}, numberOfSpeakers = 1 ,sampleRate=22050},
                #endregion

                #region Polish
                new SherpaOnnxModelMetadata { modelId = "vits-piper-pl_PL-darkman-medium", modelFileNames = new[] { "pl_PL-darkman-medium.onnx","tokens.txt", "espeak-ng-data"}, numberOfSpeakers = 1 ,sampleRate=22050},
                new SherpaOnnxModelMetadata { modelId = "vits-piper-pl_PL-gosia-medium", modelFileNames = new[] { "pl_PL-gosia-medium.onnx","tokens.txt", "espeak-ng-data"}, numberOfSpeakers = 1 ,sampleRate=22050},
                new SherpaOnnxModelMetadata { modelId = "vits-piper-pl_PL-jarvis_wg_glos-medium", modelFileNames = new[] { "pl_PL-jarvis_wg_glos-medium.onnx","tokens.txt", "espeak-ng-data"}, numberOfSpeakers = 1 ,sampleRate=22050},
                new SherpaOnnxModelMetadata { modelId = "vits-piper-pl_PL-justyna_wg_glos-medium", modelFileNames = new[] { "pl_PL-justyna_wg_glos-medium.onnx","tokens.txt", "espeak-ng-data"}, numberOfSpeakers = 1 ,sampleRate=22050},
                new SherpaOnnxModelMetadata { modelId = "vits-piper-pl_PL-mc_speech-medium", modelFileNames = new[] { "pl_PL-mc_speech-medium.onnx","tokens.txt", "espeak-ng-data"}, numberOfSpeakers = 1 ,sampleRate=22050},
                new SherpaOnnxModelMetadata { modelId = "vits-piper-pl_PL-meski_wg_glos-medium", modelFileNames = new[] { "pl_PL-meski_wg_glos-medium.onnx","tokens.txt", "espeak-ng-data"}, numberOfSpeakers = 1 ,sampleRate=22050},
                new SherpaOnnxModelMetadata { modelId = "vits-piper-pl_PL-zenski_wg_glos-medium", modelFileNames = new[] { "pl_PL-zenski_wg_glos-medium.onnx","tokens.txt", "espeak-ng-data"}, numberOfSpeakers = 1 ,sampleRate=22050},
                #endregion

                #region Portuguese
                new SherpaOnnxModelMetadata { modelId = "vits-piper-pt_BR-cadu-medium", modelFileNames = new[] { "pt_BR-cadu-medium.onnx","tokens.txt", "espeak-ng-data"}, numberOfSpeakers = 1 ,sampleRate=22050},
                new SherpaOnnxModelMetadata { modelId = "vits-piper-pt_BR-dii-high", modelFileNames = new[] { "pt_BR-dii-high.onnx","tokens.txt", "espeak-ng-data"}, numberOfSpeakers = 1 ,sampleRate=22050},
                new SherpaOnnxModelMetadata { modelId = "vits-piper-pt_BR-edresson-low", modelFileNames = new[] { "pt_BR-edresson-low.onnx","tokens.txt", "espeak-ng-data"}, numberOfSpeakers = 1 ,sampleRate=16000},
                new SherpaOnnxModelMetadata { modelId = "vits-piper-pt_BR-faber-medium", modelFileNames = new[] { "pt_BR-faber-medium.onnx","tokens.txt", "espeak-ng-data"}, numberOfSpeakers = 1 ,sampleRate=22050},
                new SherpaOnnxModelMetadata { modelId = "vits-piper-pt_BR-jeff-medium", modelFileNames = new[] { "pt_BR-jeff-medium.onnx","tokens.txt", "espeak-ng-data"}, numberOfSpeakers = 1 ,sampleRate=22050},
                new SherpaOnnxModelMetadata { modelId = "vits-piper-pt_BR-miro-high", modelFileNames = new[] { "pt_BR-miro-high.onnx","tokens.txt", "espeak-ng-data"}, numberOfSpeakers = 1 ,sampleRate=22050},
                new SherpaOnnxModelMetadata { modelId = "vits-piper-pt_PT-dii-high", modelFileNames = new[] { "pt_PT-dii-high.onnx","tokens.txt", "espeak-ng-data"}, numberOfSpeakers = 1 ,sampleRate=22050},
                new SherpaOnnxModelMetadata { modelId = "vits-piper-pt_PT-miro-high", modelFileNames = new[] { "pt_PT-miro-high.onnx","tokens.txt", "espeak-ng-data"}, numberOfSpeakers = 1 ,sampleRate=22050},
                new SherpaOnnxModelMetadata { modelId = "vits-piper-pt_PT-tugao-medium", modelFileNames = new[] { "pt_PT-tugao-medium.onnx","tokens.txt", "espeak-ng-data"}, numberOfSpeakers = 1 ,sampleRate=22050},
                #endregion

                #region Romanian
                new SherpaOnnxModelMetadata { modelId = "vits-piper-ro_RO-mihai-medium", modelFileNames = new[] { "ro_RO-mihai-medium.onnx","tokens.txt", "espeak-ng-data"}, numberOfSpeakers = 1 ,sampleRate=22050},
                #endregion

                #region Russian
                new SherpaOnnxModelMetadata { modelId = "vits-piper-ru_RU-denis-medium", modelFileNames = new[] { "ru_RU-denis-medium.onnx","tokens.txt", "espeak-ng-data"}, numberOfSpeakers = 1 ,sampleRate=22050},
                new SherpaOnnxModelMetadata { modelId = "vits-piper-ru_RU-dmitri-medium", modelFileNames = new[] { "ru_RU-dmitri-medium.onnx","tokens.txt", "espeak-ng-data"}, numberOfSpeakers = 1 ,sampleRate=22050},
                new SherpaOnnxModelMetadata { modelId = "vits-piper-ru_RU-irina-medium", modelFileNames = new[] { "ru_RU-irina-medium.onnx","tokens.txt", "espeak-ng-data"}, numberOfSpeakers = 1 ,sampleRate=22050},
                new SherpaOnnxModelMetadata { modelId = "vits-piper-ru_RU-ruslan-medium", modelFileNames = new[] { "ru_RU-ruslan-medium.onnx","tokens.txt", "espeak-ng-data"}, numberOfSpeakers = 1 ,sampleRate=22050},
                #endregion

                #region Serbian
                new SherpaOnnxModelMetadata { modelId = "vits-piper-sr_RS-serbski_institut-medium", modelFileNames = new[] { "sr_RS-serbski_institut-medium.onnx","tokens.txt", "espeak-ng-data"}, numberOfSpeakers = 2 ,sampleRate=22050},
                #endregion

                #region Slovak
                new SherpaOnnxModelMetadata { modelId = "vits-piper-sk_SK-lili-medium", modelFileNames = new[] { "sk_SK-lili-medium.onnx","tokens.txt", "espeak-ng-data"}, numberOfSpeakers = 1 ,sampleRate=22050},
                #endregion

                #region Slovenian
                new SherpaOnnxModelMetadata { modelId = "vits-piper-sl_SI-artur-medium", modelFileNames = new[] { "sl_SI-artur-medium.onnx","tokens.txt", "espeak-ng-data"}, numberOfSpeakers = 1 ,sampleRate=22050},
                #endregion

                #region Spanish
                new SherpaOnnxModelMetadata { modelId = "vits-piper-es_AR-daniela-high", modelFileNames = new[] { "es_AR-daniela-high.onnx","tokens.txt", "espeak-ng-data"}, numberOfSpeakers = 1 ,sampleRate=22050},
                new SherpaOnnxModelMetadata { modelId = "vits-piper-es_ES-carlfm-x_low", modelFileNames = new[] { "es_ES-carlfm-x_low.onnx","tokens.txt", "espeak-ng-data"}, numberOfSpeakers = 1 ,sampleRate=16000},
                new SherpaOnnxModelMetadata { modelId = "vits-piper-es_ES-davefx-medium", modelFileNames = new[] { "es_ES-davefx-medium.onnx","tokens.txt", "espeak-ng-data"}, numberOfSpeakers = 1 ,sampleRate=22050},
                new SherpaOnnxModelMetadata { modelId = "vits-piper-es_ES-glados-medium", modelFileNames = new[] { "es_ES-glados-medium.onnx","tokens.txt", "espeak-ng-data"}, numberOfSpeakers = 1 ,sampleRate=22050},
                new SherpaOnnxModelMetadata { modelId = "vits-piper-es_ES-miro-high", modelFileNames = new[] { "es_ES-miro-high.onnx","tokens.txt", "espeak-ng-data"}, numberOfSpeakers = 1 ,sampleRate=22050},
                new SherpaOnnxModelMetadata { modelId = "vits-piper-es_ES-sharvard-medium", modelFileNames = new[] { "es_ES-sharvard-medium.onnx","tokens.txt", "espeak-ng-data"}, numberOfSpeakers = 2 ,sampleRate=22050},
                new SherpaOnnxModelMetadata { modelId = "vits-piper-es_MX-ald-medium", modelFileNames = new[] { "es_MX-ald-medium.onnx","tokens.txt", "espeak-ng-data"}, numberOfSpeakers = 1 ,sampleRate=22050},
                new SherpaOnnxModelMetadata { modelId = "vits-piper-es_MX-claude-high", modelFileNames = new[] { "es_MX-claude-high.onnx","tokens.txt", "espeak-ng-data"}, numberOfSpeakers = 1 ,sampleRate=22050},
                #endregion

                #region Swahili
                new SherpaOnnxModelMetadata { modelId = "vits-piper-sw_CD-lanfrica-medium", modelFileNames = new[] { "sw_CD-lanfrica-medium.onnx","tokens.txt", "espeak-ng-data"}, numberOfSpeakers = 1 ,sampleRate=22050},
                #endregion
                
                #region Swedish
                new SherpaOnnxModelMetadata { modelId = "vits-piper-sv_SE-lisa-medium", modelFileNames = new[] { "sv_SE-lisa-medium.onnx","tokens.txt", "espeak-ng-data"}, numberOfSpeakers = 1 ,sampleRate=22050},
                new SherpaOnnxModelMetadata { modelId = "vits-piper-sv_SE-nst-medium", modelFileNames = new[] { "sv_SE-nst-medium.onnx","tokens.txt", "espeak-ng-data"}, numberOfSpeakers = 1 ,sampleRate=22050},
                #endregion

                #region Turkish
                new SherpaOnnxModelMetadata { modelId = "vits-piper-tr_TR-dfki-medium", modelFileNames = new[] { "tr_TR-dfki-medium.onnx","tokens.txt", "espeak-ng-data"}, numberOfSpeakers = 1 ,sampleRate=22050},
                new SherpaOnnxModelMetadata { modelId = "vits-piper-tr_TR-fahrettin-medium", modelFileNames = new[] { "tr_TR-fahrettin-medium.onnx","tokens.txt", "espeak-ng-data"}, numberOfSpeakers = 1 ,sampleRate=22050},
                new SherpaOnnxModelMetadata { modelId = "vits-piper-tr_TR-fettah-medium", modelFileNames = new[] { "tr_TR-fettah-medium.onnx","tokens.txt", "espeak-ng-data"}, numberOfSpeakers = 1 ,sampleRate=22050},
                #endregion

                #region Ukrainlan
                new SherpaOnnxModelMetadata { modelId = "vits-piper-uk_UA-lada-x_low", modelFileNames = new[] { "uk_UA-lada-x_low.onnx","tokens.txt", "espeak-ng-data"}, numberOfSpeakers = 1 ,sampleRate=16000},
                new SherpaOnnxModelMetadata { modelId = "vits-piper-uk_UA-ukrainian_tts-medium", modelFileNames = new[] { "uk_UA-ukrainian_tts-medium.onnx","tokens.txt", "espeak-ng-data"}, numberOfSpeakers = 3 ,sampleRate=22050},
                #endregion

                #region Vietnamese
                new SherpaOnnxModelMetadata { modelId = "vits-piper-vi_VN-25hours_single-low", modelFileNames = new[] { "vi_VN-25hours_single-low.onnx","tokens.txt", "espeak-ng-data"}, numberOfSpeakers = 1 ,sampleRate=16000},
                new SherpaOnnxModelMetadata { modelId = "vits-piper-vi_VN-vais1000-medium", modelFileNames = new[] { "vi_VN-vais1000-medium.onnx","tokens.txt", "espeak-ng-data"}, numberOfSpeakers = 1 ,sampleRate=22050},
                new SherpaOnnxModelMetadata { modelId = "vits-piper-vi_VN-vivos-x_low", modelFileNames = new[] { "vi_VN-vivos-x_low.onnx","tokens.txt", "espeak-ng-data"}, numberOfSpeakers = 65 ,sampleRate=16000},
                #endregion
                #region Weish
                new SherpaOnnxModelMetadata { modelId = "vits-piper-cy_GB-bu_tts-medium", modelFileNames = new[] { "cy_GB-bu_tts-medium.onnx","tokens.txt", "espeak-ng-data"}, numberOfSpeakers = 7 ,sampleRate=22050},
                new SherpaOnnxModelMetadata { modelId = "vits-piper-cy_GB-gwryw_gogleddol-medium", modelFileNames = new[] { "cy_GB-gwryw_gogleddol-medium.onnx","tokens.txt", "espeak-ng-data"}, numberOfSpeakers = 1 ,sampleRate=22050},
                #endregion

                //matcha
                new SherpaOnnxModelMetadata {modelId = "vocos-22khz-univ", modelFileNames = new[] { "vocos-22khz-univ.onnx" }, modelFileHashes= new string[]{"0574a135aa1db2de6e181050db2ec528496cacd4a4701fc5d7faf9f9804c0081"}, downloadUrl="https://github.com/k2-fsa/sherpa-onnx/releases/download/vocoder-models/vocos-22khz-univ.onnx", downloadFileHash="0574a135aa1db2de6e181050db2ec528496cacd4a4701fc5d7faf9f9804c0081" ,sampleRate=22050},
                #region Chinese+English
                //kokoro
                new SherpaOnnxModelMetadata { modelId = "kokoro-multi-lang-v1_1", modelFileNames = new[] { "model.onnx","voices.bin","date-zh.fst","number-zh.fst","phone-zh.fst","lexicon-gb-en.txt","lexicon-us-en.txt","lexicon-zh.txt", "tokens.txt" ,"dict","espeak-ng-data"}, modelFileHashes = new string[]{"acc4adc175b9d9986106cd20060329673ad5a2e12ef3c557d2d3745b694f8b38","e64a5a581d8c2a350d848f51c3121657cd83aa07ed6109172177345874a7244c","eb8aa079ae3cb81d8f4404992f39d61a0cb990947512b5b8d1e54d1f6980e718","743f402181fcfebf76cc2f0546b71fa26476e626fbe4e460fb7b4c3a7a8bd5bd","1ac2b6fa56b1442320c4de7db08353bab8963a2b57f365eebcdd3a2d3562f8d7","c4cbb37316f62210dff52718a7afcaae24f50c032cc75ab47ae67b831d1049e7","7daaab53a181be9885b853a8582bf1838186317e5dadacbcef9c426d6fa0da14","11111d8cd695fba2ace1367a1d0a708b586e6ef5c1f9be91da5d7eef129b651c","931ab2df2400cd65d580a22402024c2347ced8ae9ea300e545144b1aacc48e14",string.Empty,string.Empty}, downloadFileHash="a3f4c73d043860e3fd2e5b06f36795eb81de0fc8e8de6df703245edddd87dbad",sampleRate=24000},
                new SherpaOnnxModelMetadata { modelId = "kokoro-int8-multi-lang-v1_1", modelFileNames = new[] { "model.int8.onnx","voices.bin","date-zh.fst","number-zh.fst","phone-zh.fst","lexicon-gb-en.txt","lexicon-us-en.txt","lexicon-zh.txt", "tokens.txt" ,"dict","espeak-ng-data"}, modelFileHashes = new string[]{"bda15858163726a492d02a9a727bc263551b86ac77f90812c4b30ff41d380e26","e64a5a581d8c2a350d848f51c3121657cd83aa07ed6109172177345874a7244c","eb8aa079ae3cb81d8f4404992f39d61a0cb990947512b5b8d1e54d1f6980e718","743f402181fcfebf76cc2f0546b71fa26476e626fbe4e460fb7b4c3a7a8bd5bd","1ac2b6fa56b1442320c4de7db08353bab8963a2b57f365eebcdd3a2d3562f8d7","c4cbb37316f62210dff52718a7afcaae24f50c032cc75ab47ae67b831d1049e7","7daaab53a181be9885b853a8582bf1838186317e5dadacbcef9c426d6fa0da14","11111d8cd695fba2ace1367a1d0a708b586e6ef5c1f9be91da5d7eef129b651c","931ab2df2400cd65d580a22402024c2347ced8ae9ea300e545144b1aacc48e14",string.Empty,string.Empty} , downloadFileHash = "a1e94694776049035c4f2c6529f003aaece993c76aae9a78995831c3c4dcafc6",sampleRate=24000},
                new SherpaOnnxModelMetadata { modelId = "kokoro-multi-lang-v1_0", modelFileNames = new[] { "model.onnx","voices.bin","date-zh.fst","number-zh.fst","phone-zh.fst","lexicon-gb-en.txt","lexicon-us-en.txt","lexicon-zh.txt", "tokens.txt" ,"dict","espeak-ng-data"},sampleRate=24000},
                #endregion
            };


            public static readonly SherpaOnnxModelMetadata[] KWS_MODELS_METADATA_TABLES = new[]
            {
                //for chinese
                new SherpaOnnxModelMetadata { modelId = "sherpa-onnx-kws-zipformer-wenetspeech-3.3M-2024-01-01",modelFileNames = new[] {"configuration.json","decoder-epoch-12-avg-2-chunk-16-left-64.int8.onnx","decoder-epoch-12-avg-2-chunk-16-left-64.onnx","decoder-epoch-99-avg-1-chunk-16-left-64.int8.onnx","decoder-epoch-99-avg-1-chunk-16-left-64.onnx","encoder-epoch-12-avg-2-chunk-16-left-64.int8.onnx","encoder-epoch-12-avg-2-chunk-16-left-64.onnx","encoder-epoch-99-avg-1-chunk-16-left-64.int8.onnx","encoder-epoch-99-avg-1-chunk-16-left-64.onnx","joiner-epoch-12-avg-2-chunk-16-left-64.int8.onnx","joiner-epoch-12-avg-2-chunk-16-left-64.onnx","joiner-epoch-99-avg-1-chunk-16-left-64.int8.onnx","joiner-epoch-99-avg-1-chunk-16-left-64.onnx","keywords.txt","keywords_raw.txt","tokens.txt"},modelFileHashes = new string[] {"63cd5920dc448cf6416aa533e4de43f3066f646ddfca8ed19e05b1b265f37cb6","ed83454004d5bd16d831eaf00adcd181ed7734886aab6ef440f3ffa5aa3cfe3b","fb581d6734511676e246e0dff2fea01b31b0913176cb3ca64576dbab0a177774","ed83454004d5bd16d831eaf00adcd181ed7734886aab6ef440f3ffa5aa3cfe3b","fb581d6734511676e246e0dff2fea01b31b0913176cb3ca64576dbab0a177774","dd784973fc9d2fabb3b800d6dcd20fc3b0ca84f8e2415afe54b032878e447f4d","859cd6decc23f35e6ddb3a6ddb7172a57f6de7b54288728c2433ab94b7635c59","dd784973fc9d2fabb3b800d6dcd20fc3b0ca84f8e2415afe54b032878e447f4d","859cd6decc23f35e6ddb3a6ddb7172a57f6de7b54288728c2433ab94b7635c59","f79760052b87239e325f0567c752ad3130b30d92effb847d4307743c20c59a24","fcf43a2edf687e2e1bc8a2b2cf53129d3b0d693d90dcc4202e2d53b43db6c43c","f79760052b87239e325f0567c752ad3130b30d92effb847d4307743c20c59a24","fcf43a2edf687e2e1bc8a2b2cf53129d3b0d693d90dcc4202e2d53b43db6c43c",null,null,"72316508d9119696145abc6f1f8cdc46287535c34e5ce7e595f845cb1499cf2e"}, downloadFileHash="b2f7c89690dc8ce4c6ed6afeab7cd800c36ad1421fb6b6302b4a4b194cf7f35f"},
                 //for english
                new SherpaOnnxModelMetadata { modelId = "sherpa-onnx-kws-zipformer-gigaspeech-3.3M-2024-01-01",modelFileNames = new[] {"bpe.model","decoder-epoch-12-avg-2-chunk-16-left-64.int8.onnx","decoder-epoch-12-avg-2-chunk-16-left-64.onnx","encoder-epoch-12-avg-2-chunk-16-left-64.int8.onnx","encoder-epoch-12-avg-2-chunk-16-left-64.onnx","joiner-epoch-12-avg-2-chunk-16-left-64.int8.onnx","joiner-epoch-12-avg-2-chunk-16-left-64.onnx","keywords.txt","keywords_raw.txt","tokens.txt"},modelFileHashes = new string[] {"c8a2a0129c4ab8e463164c142f82d25649661b122c8cd0b7aab5c9e80b90ad24","e40ff43297abe815e8898494c17e71bba2152d9d40fa3eb803f75d0f7533329a","f61ebd3eed3773a44d088d53dfae92dbb6aec4839f4dcaee2d402414741663a3","1e721676515bcd42a186979733981213c66c80db680e1cc582dfedf3be76e678","063fbc1aeae8a9b574607a331a00e60371846ef9eaa3c1d9ea48176665dfc693","eae9da0c7e1e6c6a3f4cc42d167899c388f6c6701b94cb96320e4f55df79624c","0d7a37e749d8055223029318d6ffae82db1dae2d315d0892a68ba5dad17c1d2d",null,null,"fd2ded4050a55d2b1578870ba8697d02371980217806b7558bd0a5cc60f3ba53"}, downloadFileHash ="f170013b4716e41b62b9bfd809687c207cef798ef9bc6534d524e17af9b6561a"
                }
            };

            public static readonly SherpaOnnxModelMetadata[] SPEECH_ENHANCEMENT_MODELS_METADATA_TABLES = new[]
            {
                // GTCRN speech enhancement models
                new SherpaOnnxModelMetadata { modelId = "gtcrn-simple", modelFileNames = new[] { "gtcrn_simple.onnx" },modelFileHashes= new string[]{"e77603ac0c23dac3227dd2d7135b3a585cbee2679048aecfa886657d3ae1b534"},downloadFileHash= "e77603ac0c23dac3227dd2d7135b3a585cbee2679048aecfa886657d3ae1b534",downloadUrl = "https://github.com/k2-fsa/sherpa-onnx/releases/download/speech-enhancement-models/gtcrn_simple.onnx"},
            };


            public static readonly SherpaOnnxModelMetadata[] SPOKEN_LANGUAGEIDENTIFICATION_MODELS_METADATA_TABLES = new[]{
              new SherpaOnnxModelMetadata { modelId ="sherpa-onnx-whisper-tiny", modelFileNames = new []{"tiny-decoder.onnx","tiny-decoder.int8.onnx","tiny-encoder.onnx","tiny-encoder.int8.onnx","tiny-tokens.txt"}, modelFileHashes = new string[] {"e144c07dc6b55cece24392811f2d934b97013811f5e677d1315d341a0a74a25d","d2fece8dd42771f1df975c6c0445770d0c292bf7547c2cae04a6c0cc57540925","42c1d4cbf889632ba21ab6f0d4064c80209755f265ce5cd630db4a6793e7089c","d24fb083ae3b1041fc24e97971d60e280c9342201fbb67b0ab428a8b4a51a434","b34b360dbb493e781e479794586d661700670d65564001f23024971d1f2fa126"}, downloadFileHash = "c46116994e539aa165266d96b325252728429c12535eb9d8b6a2b10f129e66b1"},
              new SherpaOnnxModelMetadata { modelId ="sherpa-onnx-whisper-base", modelFileNames = new []{"base-decoder.onnx","base-decoder.int8.onnx","base-encoder.onnx","base-encoder.int8.onnx","base-tokens.txt"},modelFileHashes = new string[]{
                "8a12c3f6ad65bb5b86d7e6eccc302378f20f9fb2df6cb10747c62895da7ac194","9759d217388a01b3a4c7c15533201067b48ae819c4daafc8624e64b9409dc02d","5a6b87cb313993f6c9fefec9e7027556f6cb30becddf49655bee36c50ecc12d7","0b8fb1304b6109976038efff5ace81720e00386f3ff6b54ee8c75291ca0a1e11","b34b360dbb493e781e479794586d661700670d65564001f23024971d1f2fa126"
              }, downloadFileHash = "911b2083efd7c0dca2ac3b358b75222660dc09fb716d64fbfc417ba6c99ff3de"},
              new SherpaOnnxModelMetadata { modelId ="sherpa-onnx-whisper-small", modelFileNames = new []{"small-decoder.onnx","small-decoder.int8.onnx","small-encoder.onnx","small-encoder.int8.onnx","small-tokens.txt"}},
              new SherpaOnnxModelMetadata { modelId ="sherpa-onnx-whisper-medium", modelFileNames = new []{"medium-decoder.onnx","medium-decoder.int8.onnx","medium-encoder.onnx","medium-encoder.int8.onnx","medium-tokens.txt"}}
            };

            public static readonly SherpaOnnxModelMetadata[] PUNCTUATION_MODELS_METADATA_TABLES = new[] {
                // new SherpaOnnxModelMetadata { modelId ="sherpa-onnx-online-punct-en-2024-08-06", modelFileNames = new []{"model.onnx","model.int8.onnx","bpe.vocab"}}, // not supported
                new SherpaOnnxModelMetadata
                {
                    modelId = "sherpa-onnx-punct-ct-transformer-zh-en-vocab272727-2024-04-12",
                    modelFileNames = new[] { "model.onnx", "tokens.json" },
                    modelFileHashes = new[]
                    {
                        "e93593a6dbd69a07f8734ef269dbe861a379755f8d1c8354719432116f2c44bd",
                        "c960ab87bccea4aa15cf49a59f71973c2c330b46668048cd8da253749ec71ee3"
                    }
                },
                new SherpaOnnxModelMetadata
                {
                    modelId = "sherpa-onnx-punct-ct-transformer-zh-en-vocab272727-2024-04-12-int8",
                    modelFileNames = new[] { "model.int8.onnx", "tokens.json" }
                }

            };
        }
    }
}
