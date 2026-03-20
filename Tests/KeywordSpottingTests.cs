using System;
using System.IO;
using NUnit.Framework;
using Eitan.SherpaONNXUnity.Runtime;
using Eitan.SherpaONNXUnity.Runtime.Modules;
using Eitan.SherpaONNXUnity.Runtime.Utilities;

namespace Eitan.SherpaONNXUnity.Tests
{
    public class KeywordSpottingTests
    {
        private string _tempDir;
        private string _tokensPath;
        private string _lexiconPath;
        private string _modelRoot;

        [SetUp]
        public void SetUp()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "SherpaKeywordSpottingTests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);

            _tokensPath = Path.Combine(_tempDir, "tokens.txt");
            File.WriteAllText(
                _tokensPath,
                string.Join(
                    "\n",
                    "L 1",
                    "AY1 2",
                    "T 3",
                    "AH1 4",
                    "P 5",
                    "n 6",
                    "ǚ 7",
                    "ér 8") + "\n");

            _lexiconPath = Path.Combine(_tempDir, "en.phone");
            File.WriteAllText(
                _lexiconPath,
                string.Join(
                    "\n",
                    "LIGHT L AY1 T",
                    "UP AH1 P",
                    "CHILD CH AY1 L D",
                    "LOVELY L AH1 V L IY0",
                    "LIGHT(2) L AY1 T") + "\n");

            var modelId = "sherpa-onnx-kws-unittest-" + Guid.NewGuid().ToString("N");
            _modelRoot = SherpaPathResolver.GetModelRootPath(modelId);
            Directory.CreateDirectory(Path.Combine(_modelRoot, "test_wavs"));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }

            if (!string.IsNullOrEmpty(_modelRoot) && Directory.Exists(_modelRoot))
            {
                Directory.Delete(_modelRoot, recursive: true);
            }
        }

        [Test]
        public void BuildCustomKeywordsPayload_Supports_Bilingual_Keywords()
        {
            var configs = new[]
            {
                new KeywordSpotting.KeywordRegistration("LIGHT UP", 2f, 0.25f),
                new KeywordSpotting.KeywordRegistration("女儿", 1.5f, 0.4f),
            };

            var payload = KeywordSpotting.BuildCustomKeywordsPayload(configs, _tokensPath, _lexiconPath, out var registeredKeywords);

            Assert.IsNotNull(payload);
            Assert.AreEqual(2, registeredKeywords.Length);
            StringAssert.Contains("L AY1 T AH1 P :2 #0.25 @LIGHT UP", payload);
            StringAssert.Contains("n ǚ ér :1.5 #0.4 @女儿", payload);
        }

        [Test]
        public void BuildCustomKeywordsPayload_EnglishKeywordWithoutLexicon_IsIgnored()
        {
            var configs = new[]
            {
                new KeywordSpotting.KeywordRegistration("LIGHT UP", 2f, 0.25f),
            };

            var payload = KeywordSpotting.BuildCustomKeywordsPayload(configs, _tokensPath, englishLexiconPath: null, out var registeredKeywords);

            Assert.IsNull(payload);
            Assert.IsEmpty(registeredKeywords);
        }

        [Test]
        public void LoadEnglishPhoneLexicon_Strips_Alternate_Pronunciation_Suffix()
        {
            var lexicon = KeywordSpotting.LoadEnglishPhoneLexicon(_lexiconPath);

            Assert.IsNotNull(lexicon);
            Assert.IsTrue(lexicon.ContainsKey("LIGHT"));
            CollectionAssert.AreEqual(new[] { "L", "AY1", "T" }, lexicon["LIGHT"]);
        }

        [Test]
        public void KeywordSpotterVariantKey_Strips_Component_And_Int8_Suffix()
        {
            var key = KeywordSpotting.GetKeywordSpotterVariantKey(
                "/tmp/encoder-epoch-13-avg-2-chunk-8-left-64.int8.onnx",
                "encoder");

            Assert.AreEqual("epoch-13-avg-2-chunk-8-left-64", key);
        }

        [Test]
        public void KeywordSpotterVariantScore_Prefers_Epoch99_Then_Chunk16()
        {
            var oldScore = KeywordSpotting.ScoreKeywordSpotterVariant("epoch-99-avg-1-chunk-16-left-64");
            var newScore = KeywordSpotting.ScoreKeywordSpotterVariant("epoch-13-avg-2-chunk-16-left-64");
            var chunk8Score = KeywordSpotting.ScoreKeywordSpotterVariant("epoch-13-avg-2-chunk-8-left-64");

            Assert.Greater(oldScore, newScore);
            Assert.Greater(newScore, chunk8Score);
        }

        [Test]
        public void ResolveKeywordListFile_Falls_Back_To_TestWavs_Keywords()
        {
            var keywordsPath = Path.Combine(_modelRoot, "test_wavs", "keywords.txt");
            File.WriteAllText(keywordsPath, "L AY1 T AH1 P @LIGHT_UP\n");

            var metadata = new SherpaONNXModelMetadata
            {
                modelId = Path.GetFileName(_modelRoot),
                moduleType = SherpaONNXModuleType.KeywordSpotting,
            };

            var resolved = KeywordSpotting.ResolveKeywordListFile(metadata, null);

            Assert.AreEqual(keywordsPath, resolved);
        }
    }
}
