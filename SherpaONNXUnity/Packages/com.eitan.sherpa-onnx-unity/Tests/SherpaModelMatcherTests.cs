
namespace Eitan.SherpaONNXUnity.Tests
{

    using System.Collections.Generic;
    using NUnit.Framework;
    using Eitan.SherpaONNXUnity.Runtime;
    using Eitan.SherpaONNXUnity.Runtime.Constants;
    using Eitan.SherpaONNXUnity.Runtime.Utilities;


    public class SherpaModelMatcherTests
    {
        // ===== Dynamic UseCases generated from Constants =====
        public static IEnumerable<TestCaseData> AllAsrIds_FromConstants()
        {
            foreach (var meta in SherpaONNXConstants.Models.ASR_MODELS_METADATA_TABLES)
            {
                if (meta == null || string.IsNullOrEmpty(meta.modelId))
                {
                    continue;
                }


                yield return new TestCaseData(meta.modelId).SetName($"ASR::CONST::{meta.modelId}");
            }
        }

        public static IEnumerable<TestCaseData> AllVadIds_FromConstants()
        {
            foreach (var meta in SherpaONNXConstants.Models.VAD_MODELS_METADATA_TABLES)
            {
                if (meta == null || string.IsNullOrEmpty(meta.modelId))
                {
                    continue;
                }


                yield return new TestCaseData(meta.modelId).SetName($"VAD::CONST::{meta.modelId}");
            }
        }

        public static IEnumerable<TestCaseData> AllTtsIds_FromConstants()
        {
            foreach (var meta in SherpaONNXConstants.Models.TTS_MODELS_METADATA_TABLES)
            {
                if (meta == null || string.IsNullOrEmpty(meta.modelId))
                {
                    continue;
                }


                yield return new TestCaseData(meta.modelId).SetName($"TTS::CONST::{meta.modelId}");
            }
        }

        private static SpeechRecognitionModelType ExpectAsrTypeFromId(string modelId)
        {
            var s = modelId.ToLowerInvariant();
            bool isStreaming = s.Contains("streaming") || s.Contains("online");

            // Family-first expectations (keep order specific -> generic)
            if (s.Contains("whisper"))
            {
                return SpeechRecognitionModelType.Whisper;
            }


            if (s.Contains("moonshine"))
            {
                return SpeechRecognitionModelType.Moonshine;
            }


            if (s.Contains("sense-voice") || s.Contains("sense_voice") || s.Contains("sensevoice"))
            {
                return SpeechRecognitionModelType.SenseVoice;
            }


            if (s.Contains("fire-red-asr"))
            {
                return SpeechRecognitionModelType.FireRedAsr;
            }


            if (s.Contains("dolphin"))
            {
                return SpeechRecognitionModelType.Dolphin;
            }


            if (s.Contains("telespeech"))
            {
                return SpeechRecognitionModelType.TeleSpeech;
            }


            if (s.Contains("tdnn"))
            {
                return SpeechRecognitionModelType.Tdnn;
            }

            // Nemo CTC

            if (s.Contains("nemo-ctc") || s.Contains("parakeet_tdt_ctc") || s.Contains("_ctc-"))
            {

                return SpeechRecognitionModelType.Offline_Nemo_Ctc;
            }

            // Paraformer

            if (s.Contains("paraformer"))
            {

                return isStreaming ? SpeechRecognitionModelType.Online_Paraformer : SpeechRecognitionModelType.Offline_Paraformer;
            }

            // Generic CTC (non-Nemo)

            if (s.Contains("-ctc-"))
            {

                return isStreaming ? SpeechRecognitionModelType.Online_Ctc : SpeechRecognitionModelType.Offline_ZipformerCtc;
            }

            // Nemo Transducer

            if (s.Contains("nemo-transducer"))
            {

                return isStreaming ? SpeechRecognitionModelType.Online_Transducer : SpeechRecognitionModelType.Offline_Transducer;
            }

            // Zipformer default -> Transducer (when not CTC)

            if (s.Contains("zipformer"))
            {

                return isStreaming ? SpeechRecognitionModelType.Online_Transducer : SpeechRecognitionModelType.Offline_Transducer;
            }

            // Fallbacks

            return isStreaming ? SpeechRecognitionModelType.Online_Transducer : SpeechRecognitionModelType.Offline_Transducer;
        }

        private static bool ExpectOnline(string modelId)
        {
            var s = modelId.ToLowerInvariant();
            return s.Contains("streaming") || s.Contains("online");
        }

        // --- Test-side keyword segment matching (mirrors production ContainsSegment semantics) ---
        private static bool ContainsSeg(string text, string keyword)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(keyword))
            {
                return false;
            }

            int index = text.IndexOf(keyword, System.StringComparison.Ordinal);
            while (index >= 0)
            {
                bool leftOk = index == 0 || !char.IsLetterOrDigit(text[index - 1]);
                int end = index + keyword.Length;
                bool rightOk = end == text.Length || !char.IsLetterOrDigit(text[end]);
                if (leftOk && rightOk)
                {
                    return true;
                }


                index = text.IndexOf(keyword, index + 1, System.StringComparison.Ordinal);
            }
            return false;
        }

        // --- Expectations for VAD/TTS subtypes from modelId (to enable strict tests) ---
        private static VoiceActivityDetectionModelType ExpectVadTypeFromId(string modelId)
        {
            var s = modelId.ToLowerInvariant();
            if (ContainsSeg(s, "silero"))
            {
                return VoiceActivityDetectionModelType.SileroVad;
            }


            if (ContainsSeg(s, "ten"))
            {
                return VoiceActivityDetectionModelType.TenVad;
            }


            return VoiceActivityDetectionModelType.None;
        }

        private static SpeechSynthesisModelType ExpectTtsTypeFromId(string modelId)
        {
            var s = modelId.ToLowerInvariant();
            if (ContainsSeg(s, "vits"))
            {
                return SpeechSynthesisModelType.Vits;
            }


            if (ContainsSeg(s, "matcha") || ContainsSeg(s, "vocos"))
            {
                return SpeechSynthesisModelType.Matcha;
            }


            if (ContainsSeg(s, "kokoro"))
            {
                return SpeechSynthesisModelType.Kokoro;
            }


            if (ContainsSeg(s, "kitten"))
            {
                return SpeechSynthesisModelType.KittenTTS;
            }


            return SpeechSynthesisModelType.None;
        }

        // --- Sanity: for a detected ASR subtype, at least one of its identifying keywords should exist in id ---
        private static void AssertDetectedAsrKeywordsAppear(string modelId, SpeechRecognitionModelType detected)
        {
            var idLower = modelId.ToLowerInvariant();
            var kws = SherpaUtils.Model.GetModelTypeKeywords(detected);
            if (kws == null || kws.Length == 0)
            {
                // Some special families may not expose distinct keywords in GetModelTypeKeywords; allow pass
                Assert.Pass("No keywords declared for this subtype; skipping keyword presence check.");
                return;
            }
            bool ok = false;
            foreach (var kw in kws)
            {
                if (ContainsSeg(idLower, kw)) { ok = true; break; }
            }
            Assert.IsTrue(ok, $"[ASR][Keywords] {modelId} 未包含 {detected} 的任何关键字: [{string.Join(", ", kws)}]");
        }
        // --------------- ASR 类型判定用例 ---------------

        public static IEnumerable<TestCaseData> AsrTypeCases()
        {
            yield return Case("sherpa-onnx-streaming-zipformer-small-ctc-zh-int8-2025-04-01",
                SpeechRecognitionModelType.Online_Ctc);

            yield return Case("sherpa-onnx-streaming-zipformer-small-ctc-zh-2025-04-01",
                SpeechRecognitionModelType.Online_Ctc);

            // streaming + zipformer -> Online_Transducer（未出现 ctc/paraformer）
            yield return Case("sherpa-onnx-streaming-zipformer-en-2023-06-26",
                SpeechRecognitionModelType.Online_Transducer);

            // offline ctc
            yield return Case("sherpa-onnx-zipformer-ctc-zh-int8-2025-07-03",
                SpeechRecognitionModelType.Offline_ZipformerCtc);

            // offline zipformer (无 ctc/streaming 关键词) -> Offline_Transducer
            yield return Case("sherpa-onnx-zipformer-ru-2024-09-18",
                SpeechRecognitionModelType.Offline_Transducer);

            yield return Case("sherpa-onnx-zipformer-ja-reazonspeech-2024-08-01",
                SpeechRecognitionModelType.Offline_Transducer);

            // nemo-ctc 明确归为 Nemo CTC
            yield return Case("sherpa-onnx-nemo-ctc-en-conformer-small",
                SpeechRecognitionModelType.Offline_Nemo_Ctc);

            // nemo-transducer -> 按 transducer 归类（Offline_Transducer）
            yield return Case("sherpa-onnx-nemo-transducer-giga-am-russian-2024-10-24",
                SpeechRecognitionModelType.Offline_Transducer);

            // paraformer
            yield return Case("sherpa-onnx-paraformer-trilingual-zh-cantonese-en",
                SpeechRecognitionModelType.Offline_Paraformer);

            yield return Case("sherpa-onnx-streaming-paraformer-bilingual-zh-en",
                SpeechRecognitionModelType.Online_Paraformer);

            // 家族/特殊关键词优先
            yield return Case("sherpa-onnx-whisper-tiny.en",
                SpeechRecognitionModelType.Whisper);

            yield return Case("sherpa-onnx-sense-voice-zh-en-ja-ko-yue-2024-07-17",
                SpeechRecognitionModelType.SenseVoice);

            yield return Case("sherpa-onnx-telespeech-ctc-int8-zh-2024-06-04",
                SpeechRecognitionModelType.TeleSpeech);

            yield return Case("sherpa-onnx-fire-red-asr-large-zh_en-2025-02-16",
                SpeechRecognitionModelType.FireRedAsr);

            yield return Case("sherpa-onnx-dolphin-base-ctc-multi-lang-2025-04-02",
                SpeechRecognitionModelType.Dolphin);

            yield return Case("sherpa-onnx-tdnn-yesno",
                SpeechRecognitionModelType.Tdnn);

            // zipformer + offline -> Offline_Transducer
            yield return Case("sherpa-onnx-zipformer-vi-2025-04-20",
                SpeechRecognitionModelType.Offline_Transducer);

            // ---- 负例：分段匹配校验 ----
            // "context" 不应误命中 "ctc"
            yield return Case("foo-context-bar",
                SpeechRecognitionModelType.None);

            // "transducerx" 不应命中 "transducer"（右侧需边界）
            yield return Case("sherpa-onnx-transducerx-foo",
                SpeechRecognitionModelType.None);
        }

        // --------------- ModuleType 判定用例 ---------------

        public static IEnumerable<TestCaseData> ModuleTypeCases()
        {
            // 典型 ASR 都应映射为 SpeechRecognition
            yield return ModCase("sherpa-onnx-streaming-zipformer-small-ctc-zh-int8-2025-04-01",
                SherpaONNXModuleType.SpeechRecognition);
            yield return ModCase("sherpa-onnx-zipformer-ctc-zh-int8-2025-07-03",
                SherpaONNXModuleType.SpeechRecognition);
            yield return ModCase("sherpa-onnx-zipformer-ru-2024-09-18",
                SherpaONNXModuleType.SpeechRecognition);
            yield return ModCase("sherpa-onnx-nemo-ctc-en-conformer-small",
                SherpaONNXModuleType.SpeechRecognition);
            yield return ModCase("sherpa-onnx-paraformer-trilingual-zh-cantonese-en",
                SherpaONNXModuleType.SpeechRecognition);
            yield return ModCase("sherpa-onnx-whisper-tiny.en",
                SherpaONNXModuleType.SpeechRecognition);

            // VAD
            yield return ModCase("silero-vad",
                SherpaONNXModuleType.VoiceActivityDetection);

            // Punct
            yield return ModCase("my-awesome-punct-zh",
                SherpaONNXModuleType.AddPunctuation);

            // KWS
            yield return ModCase("toy-kws-demo",
                SherpaONNXModuleType.KeywordSpotting);

            // Speech Enhancement
            yield return ModCase("gtcrn-speech-enhance",
                SherpaONNXModuleType.SpeechEnhancement);

            // LID（命中 langid/language-id/lid 等关键词）
            yield return ModCase("some-langid-model",
                SherpaONNXModuleType.SpokenLanguageIdentification);

            // 负例：无任何关键词
            yield return ModCase("pure-random-model-id",
                SherpaONNXModuleType.Undefined);
        }

        // --------------- Online/Offline 判定用例 ---------------

        public static IEnumerable<TestCaseData> OnlineFlagCases()
        {
            yield return BoolCase("sherpa-onnx-streaming-zipformer-small-ctc-zh-int8-2025-04-01", true);
            yield return BoolCase("sherpa-onnx-streaming-paraformer-bilingual-zh-en", true);
            yield return BoolCase("sherpa-onnx-zipformer-ctc-zh-int8-2025-07-03", false);
            yield return BoolCase("sherpa-onnx-zipformer-ru-2024-09-18", false);
        }

        // --------------- Extra boundary & subtype cases ---------------

        [Test]
        public void SegmentMatcher_BoundaryCases_Work()
        {
            // Positive boundaries
            Assert.IsTrue(ContainsSeg("foo-ctc-bar", "ctc"), "Hyphen-delimited segment should match.");
            Assert.IsTrue(ContainsSeg("ctc", "ctc"), "Whole-token match should work.");
            Assert.IsTrue(ContainsSeg("silero_vad", "silero"), "Underscore delimiter should act as a boundary.");
            Assert.IsTrue(ContainsSeg("silero-vad", "silero"), "Hyphen is a boundary.");

            // Negative boundaries (no right/left boundary)
            Assert.IsFalse(ContainsSeg("context", "ctc"), "'ctc' inside 'context' must not match.");
            Assert.IsFalse(ContainsSeg("whispers", "whisper"), "'whisper' followed by 's' (a letter) is not a boundary.");
        }

        [Test]
        public void ModuleType_LID_Boundaries_Work()
        {
            // Positive: multiple accepted variants
            Assert.AreEqual(SherpaONNXModuleType.SpokenLanguageIdentification,
                SherpaUtils.Model.GetModuleTypeByModelId("foo-langid-bar"),
                "langid should map to SpokenLanguageIdentification");

            Assert.AreEqual(SherpaONNXModuleType.SpokenLanguageIdentification,
                SherpaUtils.Model.GetModuleTypeByModelId("foo-language-id-bar"),
                "language-id should map to SpokenLanguageIdentification");

            Assert.AreEqual(SherpaONNXModuleType.SpokenLanguageIdentification,
                SherpaUtils.Model.GetModuleTypeByModelId("foo-lid-bar"),
                "lid should map to SpokenLanguageIdentification");

            // Negative: 'lid' as part of 'lidar' should NOT match
            Assert.AreEqual(SherpaONNXModuleType.Undefined,
                SherpaUtils.Model.GetModuleTypeByModelId("foo-lidar-sensor"),
                "'lidar' must not be treated as 'lid'");
        }

        [Test]
        public void VAD_Ten_Subtype_Boundaries_Work()
        {
            // Positive boundaries for TEN
            Assert.AreEqual(VoiceActivityDetectionModelType.TenVad,
                SherpaUtils.Model.GetVoiceActivityDetectionModelType("ten-vad"),
                "ten-vad should map to TenVad");
            Assert.AreEqual(VoiceActivityDetectionModelType.TenVad,
                SherpaUtils.Model.GetVoiceActivityDetectionModelType("vad-ten"),
                "vad-ten should map to TenVad");
            Assert.AreEqual(VoiceActivityDetectionModelType.TenVad,
                SherpaUtils.Model.GetVoiceActivityDetectionModelType("foo-ten-bar"),
                "foo-ten-bar should map to TenVad");

            // Negative: 'ten' inside another token should NOT match
            Assert.AreEqual(VoiceActivityDetectionModelType.None,
                SherpaUtils.Model.GetVoiceActivityDetectionModelType("attention-vad"),
                "'ten' inside 'attention' must not produce TenVad");

            // Silero sanity
            Assert.AreEqual(VoiceActivityDetectionModelType.SileroVad,
                SherpaUtils.Model.GetVoiceActivityDetectionModelType("silero-vad"),
                "silero-vad should map to SileroVad");
        }

        [Test]
        public void TTS_Subtype_Keyword_Matches()
        {
            Assert.AreEqual(SpeechSynthesisModelType.Matcha,
                SherpaUtils.Model.GetSpeechSynthesisModelType("foo-matcha-bar"),
                "matcha should map to Matcha");

            Assert.AreEqual(SpeechSynthesisModelType.Matcha,
                SherpaUtils.Model.GetSpeechSynthesisModelType("foo-vocos-bar"),
                "vocos should map to Matcha (vocos backend)");

            Assert.AreEqual(SpeechSynthesisModelType.Vits,
                SherpaUtils.Model.GetSpeechSynthesisModelType("foo-vits-bar"),
                "vits should map to VITS");

            Assert.AreEqual(SpeechSynthesisModelType.Kokoro,
                SherpaUtils.Model.GetSpeechSynthesisModelType("foo-kokoro-bar"),
                "kokoro should map to Kokoro");

            Assert.AreEqual(SpeechSynthesisModelType.KittenTTS,
                SherpaUtils.Model.GetSpeechSynthesisModelType("foo-kitten-tts-bar"),
                "kitten-tts should map to KittenTTS");
        }

        // ---------------- Tests ----------------

        [Test, TestCaseSource(nameof(AsrTypeCases))]
        public void GetSpeechRecognitionModelType_Works(string modelId, SpeechRecognitionModelType expected)
        {
            var actual = SherpaUtils.Model.GetSpeechRecognitionModelType(modelId);
            Assert.AreEqual(expected, actual, $"ASR 类型不匹配: {modelId}");
        }

        [Test, TestCaseSource(nameof(ModuleTypeCases))]
        public void GetModuleTypeByModelId_Works(string modelId, SherpaONNXModuleType expected)
        {
            var actual = SherpaUtils.Model.GetModuleTypeByModelId(modelId);
            Assert.AreEqual(expected, actual, $"ModuleType 不匹配: {modelId}");
        }

        [Test, TestCaseSource(nameof(OnlineFlagCases))]
        public void IsOnlineModel_Works(string modelId, bool expectedOnline)
        {
            var actual = SherpaUtils.Model.IsOnlineModel(modelId);
            Assert.AreEqual(expectedOnline, actual, $"Online 标记不匹配: {modelId}");
        }

        [Test, TestCaseSource(nameof(AllAsrIds_FromConstants))]
        public void All_ASR_Models_Are_Mapped_Correctly(string modelId)
        {
            // Module type must be ASR
            var module = SherpaUtils.Model.GetModuleTypeByModelId(modelId);
            Assert.AreEqual(SherpaONNXModuleType.SpeechRecognition, module, $"[ASR][ModuleType] 应为 SpeechRecognition: {modelId}");

            // ASR subtype must not be None
            var asr = SherpaUtils.Model.GetSpeechRecognitionModelType(modelId);
            Assert.AreNotEqual(SpeechRecognitionModelType.None, asr, $"[ASR][Subtype] 不能为 None: {modelId}");

            // Subtype should match expectation derived from id
            var expectedAsr = ExpectAsrTypeFromId(modelId);
            Assert.AreEqual(expectedAsr, asr, $"[ASR][Subtype] 不匹配: {modelId} -> {asr}, 期望 {expectedAsr}");

            // Online flag check
            var expectedOnline = ExpectOnline(modelId);
            var actualOnline = SherpaUtils.Model.IsOnlineModel(modelId);
            Assert.AreEqual(expectedOnline, actualOnline, $"[ASR][Online] 不匹配: {modelId}");

            // Keywords of detected subtype should appear in id (when declared)
            AssertDetectedAsrKeywordsAppear(modelId, asr);
        }

        [Test, TestCaseSource(nameof(AllVadIds_FromConstants))]
        public void All_VAD_Models_Are_Mapped_Correctly(string modelId)
        {
            var module = SherpaUtils.Model.GetModuleTypeByModelId(modelId);
            Assert.AreEqual(SherpaONNXModuleType.VoiceActivityDetection, module, $"[VAD][ModuleType] 应为 VoiceActivityDetection: {modelId}");

            var vad = SherpaUtils.Model.GetVoiceActivityDetectionModelType(modelId);
            Assert.AreNotEqual(VoiceActivityDetectionModelType.None, vad, $"[VAD][Subtype] 不能为 None: {modelId}");
            var expected = ExpectVadTypeFromId(modelId);
            // If we can infer an expected subtype from id, enforce exact match
            if (expected != VoiceActivityDetectionModelType.None)
            {
                Assert.AreEqual(expected, vad, $"[VAD][Subtype] 不匹配: {modelId} -> {vad}, 期望 {expected}");
            }
        }

        [Test, TestCaseSource(nameof(AllTtsIds_FromConstants))]
        public void All_TTS_Models_Are_Mapped_Correctly(string modelId)
        {
            var module = SherpaUtils.Model.GetModuleTypeByModelId(modelId);
            Assert.AreEqual(SherpaONNXModuleType.SpeechSynthesis, module, $"[TTS][ModuleType] 应为 SpeechSynthesis: {modelId}");

            var tts = SherpaUtils.Model.GetSpeechSynthesisModelType(modelId);
            Assert.AreNotEqual(SpeechSynthesisModelType.None, tts, $"[TTS][Subtype] 不能为 None: {modelId}");
            var expected = ExpectTtsTypeFromId(modelId);
            if (expected != SpeechSynthesisModelType.None)
            {
                Assert.AreEqual(expected, tts, $"[TTS][Subtype] 不匹配: {modelId} -> {tts}, 期望 {expected}");
            }
        }

        [Test]
        public void Nemo_Parakeet_And_Canary_Family_Are_Mapped_Strictly()
        {
            // Explicit CTC variants
            var ctcCases = new[] {
                "sherpa-onnx-nemo-parakeet_tdt_ctc_110m-en-36000-int8",
                "sherpa-onnx-nemo-parakeet-tdt_ctc-0.6b-ja-35000-int8",
                "sherpa-onnx-parakeet-tdt-ctc-en-90m"
            };
            foreach (var id in ctcCases)
            {
                var asr = SherpaUtils.Model.GetSpeechRecognitionModelType(id);
                Assert.AreEqual(SpeechRecognitionModelType.Offline_Nemo_Ctc, asr, $"[NEMO][CTC] 不匹配: {id} -> {asr}");
                AssertDetectedAsrKeywordsAppear(id, asr);
            }

            // Non-CTC Parakeet TDT → Transducer
            var tdtTransducer = new[] {
                "sherpa-onnx-nemo-parakeet-tdt-0.6b-v2-int8",
                "sherpa-onnx-parakeet-tdt-1.1b-en"
            };
            foreach (var id in tdtTransducer)
            {
                var asr = SherpaUtils.Model.GetSpeechRecognitionModelType(id);
                Assert.IsTrue(asr == SpeechRecognitionModelType.Online_Transducer || asr == SpeechRecognitionModelType.Offline_Transducer,
                    $"[NEMO][TDT] 应映射为 Transducer: {id} -> {asr}");
            }

            // Canary → Transducer
            var canary = new[] {
                "sherpa-onnx-nemo-canary-180m-flash-en-es-de-fr-int8",
                "sherpa-onnx-canary-240m-multi"
            };
            foreach (var id in canary)
            {
                var asr = SherpaUtils.Model.GetSpeechRecognitionModelType(id);
                Assert.IsTrue(asr == SpeechRecognitionModelType.Online_Transducer || asr == SpeechRecognitionModelType.Offline_Transducer,
                    $"[NEMO][CANARY] 应映射为 Transducer: {id} -> {asr}");
            }
        }

        [Test]
        public void All_Constants_ModelIds_Are_NonEmpty_And_Unique()
        {
            var set = new HashSet<string>();
            void Check(string id)
            {
                Assert.IsFalse(string.IsNullOrEmpty(id), "常量表中存在空的 modelId");
                Assert.IsTrue(set.Add(id), $"常量表中发现重复的 modelId: {id}");
            }

            foreach (var m in SherpaONNXConstants.Models.ASR_MODELS_METADATA_TABLES)
            {
                if (m != null)
                {
                    Check(m.modelId);
                }
            }


            foreach (var m in SherpaONNXConstants.Models.VAD_MODELS_METADATA_TABLES)
            {
                if (m != null)
                {
                    Check(m.modelId);
                }
            }


            foreach (var m in SherpaONNXConstants.Models.TTS_MODELS_METADATA_TABLES)
            {
                if (m != null)
                {
                    Check(m.modelId);
                }
            }

        }

        // --------------- helpers ---------------

        private static TestCaseData Case(string id, SpeechRecognitionModelType expect)
            => new TestCaseData(id, expect).SetName($"ASR::{id} -> {expect}");

        private static TestCaseData ModCase(string id, SherpaONNXModuleType expect)
            => new TestCaseData(id, expect).SetName($"MOD::{id} -> {expect}");

        private static TestCaseData BoolCase(string id, bool expect)
            => new TestCaseData(id, expect).SetName($"ONLINE::{id} -> {expect}");
    }

    /// <summary>
    /// 如果你暂时不使用 Test Runner，也可以直接在任意入口调用这个 Smoke，
    /// 它会打印出不匹配的条目（使用 NUnit 断言更规范，这里仅作兜底）。
    /// </summary>
    public static class SherpaModelMatcherSmoke
    {
        public static void Run()
        {
            var samples = new Dictionary<string, SpeechRecognitionModelType>
            {
                { "sherpa-onnx-streaming-zipformer-small-ctc-zh-int8-2025-04-01", SpeechRecognitionModelType.Online_Ctc },
                { "sherpa-onnx-streaming-zipformer-en-2023-06-26", SpeechRecognitionModelType.Online_Transducer },
                { "sherpa-onnx-zipformer-ctc-zh-int8-2025-07-03", SpeechRecognitionModelType.Offline_ZipformerCtc },
                { "sherpa-onnx-zipformer-ru-2024-09-18", SpeechRecognitionModelType.Offline_Transducer },
                { "sherpa-onnx-nemo-ctc-en-conformer-small", SpeechRecognitionModelType.Offline_Nemo_Ctc },
                { "sherpa-onnx-paraformer-trilingual-zh-cantonese-en", SpeechRecognitionModelType.Offline_Paraformer },
                { "sherpa-onnx-streaming-paraformer-bilingual-zh-en", SpeechRecognitionModelType.Online_Paraformer },
                { "sherpa-onnx-whisper-tiny.en", SpeechRecognitionModelType.Whisper },
                { "sherpa-onnx-sense-voice-zh-en-ja-ko-yue-2024-07-17", SpeechRecognitionModelType.SenseVoice },
                { "sherpa-onnx-telespeech-ctc-int8-zh-2024-06-04", SpeechRecognitionModelType.TeleSpeech },
                { "sherpa-onnx-fire-red-asr-large-zh_en-2025-02-16", SpeechRecognitionModelType.FireRedAsr },
                { "sherpa-onnx-dolphin-base-ctc-multi-lang-2025-04-02", SpeechRecognitionModelType.Dolphin },
                { "sherpa-onnx-tdnn-yesno", SpeechRecognitionModelType.Tdnn },
                { "sherpa-onnx-zipformer-vi-2025-04-20", SpeechRecognitionModelType.Offline_Transducer },
                // 负例
                { "foo-context-bar", SpeechRecognitionModelType.None },
                { "sherpa-onnx-transducerx-foo", SpeechRecognitionModelType.None },
            };

            int fail = 0;
            foreach (var kv in samples)
            {
                var actual = SherpaUtils.Model.GetSpeechRecognitionModelType(kv.Key);
                if (actual != kv.Value)
                {
                    fail++;
                    SherpaLog.Error($"[SMOKE][ASR] {kv.Key} => {actual}, EXPECT {kv.Value}", category: "Tests");
                }
            }

            var modSamples = new Dictionary<string, SherpaONNXModuleType>
            {
                { "silero-vad", SherpaONNXModuleType.VoiceActivityDetection },
                { "my-awesome-punct-zh", SherpaONNXModuleType.AddPunctuation },
                { "toy-kws-demo", SherpaONNXModuleType.KeywordSpotting },
                { "gtcrn-speech-enhance", SherpaONNXModuleType.SpeechEnhancement },
                { "some-langid-model", SherpaONNXModuleType.SpokenLanguageIdentification },
                { "pure-random-model-id", SherpaONNXModuleType.Undefined },
            };

            foreach (var kv in modSamples)
            {
                var actual = SherpaUtils.Model.GetModuleTypeByModelId(kv.Key);
                if (actual != kv.Value)
                {
                    fail++;
                    SherpaLog.Error($"[SMOKE][MOD] {kv.Key} => {actual}, EXPECT {kv.Value}", category: "Tests");
                }
            }

            SherpaLog.Info(fail == 0
                ? "[SMOKE] All Sherpa model mapping cases passed."
                : $"[SMOKE] {fail} case(s) failed. Check logs above.",
                category: "Tests");
        }
    }
}
