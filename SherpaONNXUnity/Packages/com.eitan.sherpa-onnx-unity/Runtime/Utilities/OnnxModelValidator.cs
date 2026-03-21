using System;
using System.IO;
using System.Linq;
using System.Text;

namespace Eitan.SherpaONNXUnity.Runtime.Utilities
{
    internal static class OnnxModelValidator
    {
        private const int ProbeByteCount = 512;
        private static readonly string[] s_TextMarkers =
        {
            "version https://git-lfs.github.com/spec/",
            "oid sha256:",
            "<!doctype html",
            "<html",
            "<?xml",
            "accessdenied",
            "no such key",
            "not found",
            "\"error\"",
            "'error'",
        };

        public static void ValidateFileOrThrow(string path, string description)
        {
            if (TryValidateFile(path, out var errorMessage))
            {
                return;
            }

            throw new InvalidOperationException(
                $"{description} ONNX validation failed for '{path}'. {errorMessage} " +
                "The file does not look like a usable binary ONNX model and would likely crash the native loader.");
        }

        internal static bool TryValidateFile(string path, out string errorMessage)
        {
            errorMessage = string.Empty;

            if (string.IsNullOrWhiteSpace(path))
            {
                errorMessage = "Path is null or empty.";
                return false;
            }

            if (!File.Exists(path))
            {
                errorMessage = "File does not exist.";
                return false;
            }

            try
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                if (stream.Length <= 0)
                {
                    errorMessage = "File is empty.";
                    return false;
                }

                return TryValidateBinaryOnnxSanity(stream, out errorMessage);
            }
            catch (Exception ex)
            {
                errorMessage = $"Failed to read file: {ex.Message}";
                return false;
            }
        }

        internal static bool TryValidateBinaryOnnxSanity(Stream stream, out string errorMessage)
        {
            errorMessage = string.Empty;

            if (stream == null)
            {
                errorMessage = "Stream is null.";
                return false;
            }

            if (!stream.CanRead)
            {
                errorMessage = "Stream is not readable.";
                return false;
            }

            if (!stream.CanSeek)
            {
                errorMessage = "Stream must support seeking.";
                return false;
            }

            if (stream.Length <= 0)
            {
                errorMessage = "Stream is empty.";
                return false;
            }

            var originalPosition = stream.Position;
            try
            {
                stream.Seek(0, SeekOrigin.Begin);
                var probeLength = (int)Math.Min(ProbeByteCount, stream.Length);
                var buffer = new byte[probeLength];
                var bytesRead = stream.Read(buffer, 0, probeLength);
                if (bytesRead <= 0)
                {
                    errorMessage = "Unable to read file header.";
                    return false;
                }

                if (buffer.Take(bytesRead).All(b => b == 0))
                {
                    errorMessage = "File header is all zeros.";
                    return false;
                }

                var textHeader = DecodeHeader(buffer, bytesRead).TrimStart('\uFEFF', ' ', '\t', '\r', '\n', '\0');
                if (LooksLikeTextPayload(buffer, bytesRead, textHeader, out errorMessage))
                {
                    return false;
                }

                return true;
            }
            finally
            {
                stream.Seek(originalPosition, SeekOrigin.Begin);
            }
        }

        private static string DecodeHeader(byte[] buffer, int length)
        {
            try
            {
                return Encoding.UTF8.GetString(buffer, 0, length);
            }
            catch
            {
                return Encoding.ASCII.GetString(buffer, 0, length);
            }
        }

        private static bool LooksLikeTextPayload(byte[] buffer, int length, string textHeader, out string errorMessage)
        {
            errorMessage = string.Empty;

            if (string.IsNullOrWhiteSpace(textHeader))
            {
                return false;
            }

            var lower = textHeader.ToLowerInvariant();
            foreach (var marker in s_TextMarkers)
            {
                if (lower.Contains(marker))
                {
                    errorMessage = $"File header looks like text content ('{marker}') instead of a binary ONNX model.";
                    return true;
                }
            }

            var firstChar = lower[0];
            if (firstChar == '{' || firstChar == '[' || firstChar == '<')
            {
                errorMessage = "File header starts like JSON/XML/HTML text instead of a binary ONNX model.";
                return true;
            }

            var printableCount = 0;
            var nulCount = 0;
            for (var i = 0; i < length; i++)
            {
                var b = buffer[i];
                if (b == 0)
                {
                    nulCount++;
                }

                if (b == 9 || b == 10 || b == 13 || (b >= 32 && b <= 126))
                {
                    printableCount++;
                }
            }

            var printableRatio = printableCount / (double)length;
            if (nulCount == 0 && printableRatio > 0.95)
            {
                errorMessage = "File header looks like plain text rather than a binary ONNX model.";
                return true;
            }

            return false;
        }
    }
}
