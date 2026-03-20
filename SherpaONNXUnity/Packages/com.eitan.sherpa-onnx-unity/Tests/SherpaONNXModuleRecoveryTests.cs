using System;
using NUnit.Framework;
using Eitan.SherpaONNXUnity.Runtime;

namespace Eitan.SherpaONNXUnity.Tests
{
    public class SherpaONNXModuleRecoveryTests
    {
        [Test]
        public void IsLikelyCorruptedModelFailure_ReturnsFalse_ForPrepareVerificationFailure()
        {
            var ex = new InvalidOperationException(
                "Model sherpa-onnx-moonshine-base-zh-quantized-2026-02-27 initialization failed (VerificationFailed)");
            ex.Data["PrepareErrorCode"] = PrepareErrorCode.VerificationFailed;

            Assert.IsFalse(SherpaONNXModule.IsLikelyCorruptedModelFailure(ex));
        }

        [Test]
        public void IsLikelyCorruptedModelFailure_ReturnsFalse_WhenOnlyModelIdContainsOnnx()
        {
            var ex = new InvalidOperationException(
                "Model sherpa-onnx-moonshine-base-zh-quantized-2026-02-27 initialization failed");

            Assert.IsFalse(SherpaONNXModule.IsLikelyCorruptedModelFailure(ex));
        }

        [Test]
        public void IsLikelyCorruptedModelFailure_ReturnsTrue_ForValidatorCorruptionMessage()
        {
            var ex = new InvalidOperationException(
                "Decoder ONNX validation failed for '/tmp/model.onnx'. Encountered protobuf tag 0 at byte offset 0. The model file appears corrupted or incomplete and would crash the native loader.");

            Assert.IsTrue(SherpaONNXModule.IsLikelyCorruptedModelFailure(ex));
        }
    }
}
