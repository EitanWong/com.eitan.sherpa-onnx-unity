// File: Packages/com.eitan.sherpa-onnx-unity/Tests/SherpaMetadataExtensionsTests.cs
// Purpose: Verify filename/keyword/extension matching against actual files on disk

using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Eitan.SherpaONNXUnity.Runtime;
using Eitan.SherpaONNXUnity.Runtime.Utilities;

namespace Eitan.SherpaONNXUnity.Tests
{
    public class SherpaMetadataExtensionsTests
    {
        private string _modelId;
        private string _modelRoot;
        private SherpaONNXModelMetadata _metadata;

        [SetUp]
        public void SetUp()
        {
            _modelId = "unittests-" + Guid.NewGuid().ToString("N");
            _modelRoot = SherpaPathResolver.GetModelRootPath(_modelId);

            // Ensure directory exists
            Directory.CreateDirectory(_modelRoot);

            // --- Create a comprehensive set of files reflecting real module usage ---
            // Common assets
            Touch("tokens.txt");
            Touch("en-us.lexicon");
            Touch("hotwords.txt");
            Touch("keywords.txt");
            Touch("readme.MD");
            Touch("config.json");

            // Generic models
            Touch("model.onnx");
            Touch("some_name-v1.int8.onnx");
            Touch("offline-tdt.ctc.int8.onnx");

            // Transducer triplet used by Online/Offline Transducer models & KWS
            Touch("encoder-99.onnx");
            Touch("decoder-99.onnx");
            Touch("joiner-99.onnx");

            // CTC / Zipformer specific names
            Touch("zipformer2-ctc-model-int8.onnx");

            // GTCRN (SpeechEnhancement) typical naming
            Touch("gtcrn-model-int8.onnx");

            // Whisper & SLI (SpokenLanguageIdentification) style assets
            Touch("whisper-encoder-int8.onnx");
            Touch("whisper-decoder-int8.onnx");

            _metadata = new SherpaONNXModelMetadata { modelId = _modelId };

            SherpaLog.Info($"[SetUp] ModelId: {_modelId}\nRoot: {_modelRoot}\nFiles:\n" +
                           string.Join("\n", Directory.GetFiles(_modelRoot).Select(Path.GetFileName)));
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                if (Directory.Exists(_modelRoot))
                {
                    Directory.Delete(_modelRoot, recursive: true);
                }
            }
            catch (Exception ex)
            {
                SherpaLog.Warning($"[TearDown] Cleanup failed: {ex}", category: "Tests");
            }
        }

        [Test]
        public void ListModelFiles_FileNameOnly_SortedAndComplete()
        {
            var names = _metadata.ListModelFiles(fileNameOnly: true);
            Assert.IsNotNull(names);
            Assert.GreaterOrEqual(names.Length, 15, "Expected the seeded files to be listed.");

            // Ensure sorted, case-insensitive
            var sorted = names.OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToArray();
            CollectionAssert.AreEqual(sorted, names);

            // Spot-check a subset
            CollectionAssert.Contains(names, "tokens.txt");
            CollectionAssert.Contains(names, "model.onnx");
            CollectionAssert.Contains(names, "encoder-99.onnx");
            CollectionAssert.Contains(names, "gtcrn-model-int8.onnx");

            SherpaLog.Info("[ListModelFiles:fileNameOnly] Results:\n" + string.Join("\n", names), category: "Tests");
        }

        [Test]
        public void GetModelFilesByExtensionName_FiltersAndReturnsFullPaths()
        {
            // Query by extension: .onnx and txt (allow both ".ext" and "ext" forms)
            var results = _metadata.GetModelFilesByExtensionName(".onnx", "txt");


            Assert.IsNotNull(results);
            Assert.IsTrue(results.All(Path.IsPathRooted), "Expected full paths.");

            // Expect all .onnx plus tokens.txt
            var expected = _metadata.ListModelFiles(fileNameOnly: true)
                                     .Where(n => Path.GetExtension(n).Equals(".onnx", StringComparison.OrdinalIgnoreCase) ||
                                                 n.Equals("tokens.txt", StringComparison.OrdinalIgnoreCase))
                                     .Select(Full)
                                     .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                                     .ToArray();

            var ordered = results.OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToArray();
            CollectionAssert.AreEqual(expected, ordered);

            SherpaLog.Info("[GetModelFilesByExtensionName] Matched full paths:\n" + string.Join("\n", results), category: "Tests");
        }

        [Test]
        public void Keywords_Tokens_And_Lexicon()
        {
            // Mirrors usage in multiple modules (tokens / lexicon lookups)
            var matches = _metadata.GetModelFilePathByKeywords("tokens", "lexicon");
            Assert.IsNotNull(matches);
            CollectionAssert.IsSubsetOf(new[] { Full("tokens.txt"), Full("en-us.lexicon") }, matches);
            SherpaLog.Info("[Keywords_Tokens_And_Lexicon] →\n" + string.Join("\n", matches), category: "Tests");
        }

        [Test]
        public void Keywords_Int8_Finds_Int8_Models()
        {
            var matches = _metadata.GetModelFilePathByKeywords("int8");
            Assert.IsNotNull(matches);
            // Assert at least a couple of typical int8 models exist
            CollectionAssert.IsSubsetOf(
                new[] { Full("some_name-v1.int8.onnx"), Full("offline-tdt.ctc.int8.onnx") },
                matches
            );
            SherpaLog.Info("[Keywords_Int8_Finds_Int8_Models] →\n" + string.Join("\n", matches), category: "Tests");
        }

        [Test]
        public void Keywords_Transducer_Triplet_Encoder_Decoder_Joiner()
        {
            var enc = _metadata.GetModelFilePathByKeywords("encoder", "99");
            var dec = _metadata.GetModelFilePathByKeywords("decoder", "99");
            var joi = _metadata.GetModelFilePathByKeywords("joiner", "99");

            Assert.IsNotNull(enc);
            Assert.IsNotNull(dec);
            Assert.IsNotNull(joi);

            StringAssert.EndsWith(Path.DirectorySeparatorChar + "encoder-99.onnx", enc.First());
            StringAssert.EndsWith(Path.DirectorySeparatorChar + "decoder-99.onnx", dec.First());
            StringAssert.EndsWith(Path.DirectorySeparatorChar + "joiner-99.onnx", joi.First());

            SherpaLog.Info("[Keywords_Transducer_Triplet] encoder → " + enc.First(), category: "Tests");
            SherpaLog.Info("[Keywords_Transducer_Triplet] decoder → " + dec.First(), category: "Tests");
            SherpaLog.Info("[Keywords_Transducer_Triplet] joiner  → " + joi.First(), category: "Tests");
        }

        [Test]
        public void Keywords_Gtcrn_Model_For_SpeechEnhancement()
        {
            // Mirrors SpeechEnhancement: keywords {"gtcrn","model", maybe "int8"}
            var matches = _metadata.GetModelFilePathByKeywords("gtcrn", "model", "int8");
            Assert.IsNotNull(matches);
            StringAssert.EndsWith(Path.DirectorySeparatorChar + "gtcrn-model-int8.onnx", matches.First());
            SherpaLog.Info("[Keywords_Gtcrn_Model_For_SpeechEnhancement] →\n" + string.Join("\n", matches), category: "Tests");
        }

        [Test]
        public void Keywords_Whisper_Encoder_Decoder_For_SLI()
        {
            var enc = _metadata.GetModelFilePathByKeywords("whisper", "encoder", "int8");
            var dec = _metadata.GetModelFilePathByKeywords("whisper", "decoder", "int8");

            Assert.IsNotNull(enc);
            Assert.IsNotNull(dec);

            StringAssert.EndsWith(Path.DirectorySeparatorChar + "whisper-encoder-int8.onnx", enc.First());
            StringAssert.EndsWith(Path.DirectorySeparatorChar + "whisper-decoder-int8.onnx", dec.First());

            SherpaLog.Info("[Keywords_Whisper_For_SLI] encoder → " + enc.First(), category: "Tests");
            SherpaLog.Info("[Keywords_Whisper_For_SLI] decoder → " + dec.First(), category: "Tests");
        }

        [Test]
        public void Keywords_NoMatch_ReturnsNull()
        {
            var none = _metadata.GetModelFilePathByKeywords("this-keyword-should-not-exist");
            Assert.IsNull(none);
            SherpaLog.Info("[Keywords_NoMatch_ReturnsNull] → (null)", category: "Tests");
        }

        // ---------- helpers ----------
        private void Touch(string name)
        {
            var full = Path.Combine(_modelRoot, name);
            File.WriteAllBytes(full, Array.Empty<byte>());
        }

        private string Full(string name) => Path.Combine(_modelRoot, name);
    }
}
