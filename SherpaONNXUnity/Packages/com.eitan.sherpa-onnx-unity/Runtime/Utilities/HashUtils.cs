namespace Eitan.SherpaONNXUnity.Runtime.Utilities
{
    using System;
    using System.Buffers;
    using System.IO;
    using System.Security.Cryptography;
    using System.Threading;
    using System.Threading.Tasks;

    public class HashUtils
    {
        /// <summary>
        /// Computes the SHA256 hash of a file asynchronously. This version is simplified to be
        /// compatible with Unity's .NET runtime by removing the System.Threading.Channels dependency.
        /// It reads the file in chunks and computes the hash in a single async loop.
        /// </summary>
        /// <param name="filePath">The path to the file to hash.</param>
        /// <param name="progress">An optional progress reporter that receives values from 0.0 to 1.0.</param>
        /// <param name="cancellationToken">An optional token to cancel the operation.</param>
        /// <returns>A Task that represents the asynchronous operation, yielding the lowercase hex string of the hash.</returns>
        public static async Task<string> ComputeFileHashAsync(string filePath, IProgress<float> progress = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(filePath))
            {

                throw new ArgumentException("File path cannot be null or empty", nameof(filePath));
            }


            if (!File.Exists(filePath))
            {

                throw new FileNotFoundException($"File not found: {filePath}");
            }

            // Define a buffer size for reading the file in chunks. 64KB is a common choice.

            const int BufferSize = 64 * 1024;

            // Rent a buffer from the shared pool to avoid frequent memory allocations.
            var buffer = ArrayPool<byte>.Shared.Rent(BufferSize);

            try
            {
                using var sha256 = SHA256.Create();
                using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read,
                    BufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan);

                var fileLength = stream.Length;
                long totalBytesRead = 0;

                // Handle empty files as a special case.
                if (fileLength == 0)
                {
                    sha256.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                    progress?.Report(1.0f);
                    return ToHexString(sha256.Hash);
                }

                // Read the file in chunks until the end is reached.
                int bytesRead;
                while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
                {
                    // The ReadAsync call will throw OperationCanceledException if cancellation is requested.

                    // Update the hash with the chunk of data read from the file.
                    sha256.TransformBlock(buffer, 0, bytesRead, null, 0);

                    totalBytesRead += bytesRead;

                    // Report progress if a progress reporter is provided.
                    progress?.Report((float)totalBytesRead / fileLength);
                }

                // Finalize the hash calculation.
                sha256.TransformFinalBlock(Array.Empty<byte>(), 0, 0);

                // Convert the final hash byte array to a hex string.
                return ToHexString(sha256.Hash);
            }
            catch (OperationCanceledException)
            {
                // This is an expected exception when cancellation is requested.
                // We re-throw it so the caller can handle it.
                throw;
            }
            finally
            {
                // IMPORTANT: Always return the buffer to the pool to prevent memory leaks.
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        /// <summary>
        /// Converts a byte array to a lowercase hex string with minimal memory allocations.
        /// </summary>
        /// <param name="bytes">The byte array to convert.</param>
        /// <returns>A lowercase hexadecimal string representation of the byte array.</returns>
        private static string ToHexString(byte[] bytes)
        {
            if (bytes == null)
            {

                throw new ArgumentNullException(nameof(bytes));
            }

            // Use string.Create for a zero-allocation conversion.

            return string.Create(bytes.Length * 2, bytes, (span, state) =>
            {
                for (int i = 0; i < state.Length; i++)
                {
                    byte b = state[i];
                    // Split the byte into its high and low nibbles.
                    int high = b >> 4;
                    int low = b & 0x0F;
                    // Convert nibbles to their character representation.
                    span[i * 2] = (char)(high < 10 ? '0' + high : 'a' + high - 10);
                    span[i * 2 + 1] = (char)(low < 10 ? '0' + low : 'a' + low - 10);
                }
            });
        }
    }
}
