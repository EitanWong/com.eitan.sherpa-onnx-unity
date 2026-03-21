using System;
using System.IO;
using NUnit.Framework;
using Eitan.SherpaONNXUnity.Runtime.Utilities;

namespace Eitan.SherpaONNXUnity.Tests
{
    public class OnnxModelValidatorTests
    {
        [Test]
        public void TryValidateBinaryOnnxSanity_ReturnsTrue_ForBinaryPayload()
        {
            var bytes = new byte[]
            {
                0x08, 0x0A, 0x12, 0x04, 0x6F, 0x6E, 0x6E, 0x78,
                0x00, 0xFF, 0x81, 0x7F,
            };

            using var stream = new MemoryStream(bytes, writable: false);
            var valid = OnnxModelValidator.TryValidateBinaryOnnxSanity(stream, out var errorMessage);

            Assert.IsTrue(valid, errorMessage);
            Assert.IsTrue(string.IsNullOrEmpty(errorMessage), errorMessage);
        }

        [Test]
        public void TryValidateBinaryOnnxSanity_ReturnsFalse_ForGitLfsPointer()
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(
                "version https://git-lfs.github.com/spec/v1\noid sha256:123\nsize 999\n");

            using var stream = new MemoryStream(bytes, writable: false);
            var valid = OnnxModelValidator.TryValidateBinaryOnnxSanity(stream, out var errorMessage);

            Assert.IsFalse(valid);
            StringAssert.Contains("git-lfs", errorMessage.ToLowerInvariant());
        }

        [Test]
        public void ValidateFileOrThrow_Throws_ForHtmlPayload()
        {
            var filePath = Path.Combine(Path.GetTempPath(), $"invalid-onnx-{Guid.NewGuid():N}.onnx");
            try
            {
                File.WriteAllText(filePath, "<!DOCTYPE html><html><body>404 Not Found</body></html>");

                var ex = Assert.Throws<InvalidOperationException>(() => OnnxModelValidator.ValidateFileOrThrow(filePath, "Whisper decoder"));
                StringAssert.Contains("Whisper decoder ONNX validation failed", ex.Message);
                StringAssert.Contains("does not look like a usable binary ONNX model", ex.Message);
            }
            finally
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
        }
    }
}
