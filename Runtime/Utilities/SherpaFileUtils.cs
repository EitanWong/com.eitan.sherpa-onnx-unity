using System;
using System.Buffers;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using System.Collections.Generic;

namespace Eitan.SherpaONNXUnity.Runtime.Utilities
{
    /// <summary>
    /// Represents the status of a file verification operation.
    /// </summary>
    public enum FileVerificationStatus
    {
        Success,
        Prepare,
        InProgress,
        Done,
        FileNotFound,
        HashMismatch,
        Error,
        CacheHit  // New status for when hash is read from cache
    }

    /// <summary>
    /// Provides event data for the verification completed event.
    /// </summary>
    public class FileVerificationEventArgs : EventArgs
    {
        public string FilePath { get; }
        public FileVerificationStatus Status { get; }
        public string Message { get; }
        public float Progress { get; }
        public string CalculatedHash { get; }
        public string ExpectedHash { get; }
        public bool UsedCache { get; }  // New property to indicate if cache was used

        public FileVerificationEventArgs(string filePath, FileVerificationStatus status, float progress = 0, string calculatedHash = null, string expectedHash = null, string message = "", bool usedCache = false)
        {
            this.FilePath = filePath;
            this.Status = status;
            this.Progress = progress;
            this.Message = message;
            this.CalculatedHash = calculatedHash;
            this.ExpectedHash = expectedHash;
            this.UsedCache = usedCache;
        }
    }

    /// <summary>
    /// A static utility class for high-performance file operations, optimized to minimize GC allocations.
    /// </summary>
    internal static class SherpaFileUtils
    {
        // Use a 64KB buffer for better I/O performance.
        private const int BufferSize = 65536;

        // Cache file extension
        private const string HashCacheExtension = ".sha256";

        /// <summary>
        /// Generates the cache file path for a given file path.
        /// </summary>
        /// <param name="filePath">The original file path.</param>
        /// <returns>The cache file path.</returns>
        private static string GetCacheFilePath(string filePath)
        {
            return filePath + HashCacheExtension;
        }

        /// <summary>
        /// Reads the cached hash value if it exists and is valid.
        /// </summary>
        /// <param name="filePath">The original file path.</param>
        /// <param name="fileLastWriteTime">The last write time of the original file.</param>
        /// <returns>The cached hash if valid, null otherwise.</returns>
        private static async Task<string> ReadCachedHashAsync(string filePath, DateTime fileLastWriteTime)
        {
            try
            {
                string cacheFilePath = GetCacheFilePath(filePath);

                if (!File.Exists(cacheFilePath))
                {
                    return null;
                }

                // Check if cache file is newer than the original file
                DateTime cacheLastWriteTime = File.GetLastWriteTime(cacheFilePath);
                if (cacheLastWriteTime < fileLastWriteTime)
                {
                    // Cache is outdated, delete it
                    File.Delete(cacheFilePath);
                    return null;
                }

                // Read the cached hash
                string cachedContent = await File.ReadAllTextAsync(cacheFilePath);
                string[] lines = cachedContent.Split('\n');

                if (lines.Length >= 2)
                {
                    // First line should be the file's last write time
                    // Second line should be the hash
                    if (DateTime.TryParse(lines[0], out DateTime cachedFileTime) &&
                        cachedFileTime == fileLastWriteTime)
                    {
                        return lines[1].Trim();
                    }
                }

                // Invalid cache format or timestamp mismatch
                File.Delete(cacheFilePath);
                return null;
            }
            catch (Exception ex)
            {
                // If anything goes wrong reading the cache, just return null
                throw ex;
            }
        }

        /// <summary>
        /// Saves the computed hash to a cache file.
        /// </summary>
        /// <param name="filePath">The original file path.</param>
        /// <param name="hash">The computed hash.</param>
        /// <param name="fileLastWriteTime">The last write time of the original file.</param>
        private static async Task SaveCachedHashAsync(string filePath, string hash, DateTime fileLastWriteTime)
        {
            try
            {
                string cacheFilePath = GetCacheFilePath(filePath);
                string cacheContent = $"{fileLastWriteTime:O}\n{hash}";
                await File.WriteAllTextAsync(cacheFilePath, cacheContent);
            }
            catch (Exception)
            {
                // If saving cache fails, just continue without caching
                // This shouldn't break the main functionality
            }
        }

        /// <summary>
        /// Asynchronously verifies a file's existence and optionally its hash.
        /// This method is optimized for performance and minimal memory allocation.
        /// Now includes hash caching to avoid repeated calculations.
        /// </summary>
        /// <param name="path">The absolute path to the file to verify.</param>
        /// <param name="expectedHash">The optional expected SHA256 hash of the file (hex string).</param>
        /// <param name="progress">An optional callback to report hash computation progress (from 0.0 to 1.0).</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>A Task that resolves to a VerificationCompletedEventArgs object with the final status.</returns>
        public static async Task<FileVerificationEventArgs> VerifyFileAsync(string path, string expectedHash = null, IProgress<FileVerificationEventArgs> progress = null, CancellationToken? cancellationToken = null)
        {
            var token = cancellationToken ?? CancellationToken.None;
            FileVerificationEventArgs eventArgs = null;
            try
            {
                if (string.IsNullOrEmpty(path))
                {
                    eventArgs = new FileVerificationEventArgs(path, FileVerificationStatus.Error, message: "Path cannot be null or empty.");
                    return eventArgs;
                }

                // Check if it's a directory first.
                if (Directory.Exists(path))
                {
                    eventArgs = new FileVerificationEventArgs(path, FileVerificationStatus.Success, message: "Path exists and is a directory.");
                    return eventArgs;
                }

                // Check if it's a file.
                if (File.Exists(path))
                {
                    // If no hash is provided, we're done.
                    if (string.IsNullOrEmpty(expectedHash))
                    {
                        eventArgs = new FileVerificationEventArgs(path, FileVerificationStatus.Success, message: "File exists. No hash verification was requested.");
                        return eventArgs;
                    }

                    // Get file info for cache validation
                    DateTime fileLastWriteTime = File.GetLastWriteTime(path);

                    // Try to read cached hash first
                    string actualHash = await ReadCachedHashAsync(path, fileLastWriteTime);
                    bool usedCache = actualHash != null;

                    if (usedCache)
                    {
                        progress?.Report(new FileVerificationEventArgs(path, FileVerificationStatus.CacheHit, progress: 1, message: $"Using cached hash for {path}", usedCache: true));
                    }
                    else
                    {
                        // Compute the hash and compare.
                        progress?.Report(new FileVerificationEventArgs(path, FileVerificationStatus.Prepare, progress: 0, message: $"Ready to compute {path} hash"));

                        Progress<float> hashProgressAdapter = new Progress<float>(progressValue =>
                        {
                            progress?.Report(new FileVerificationEventArgs(path, FileVerificationStatus.InProgress, progress: progressValue, message: $"Computing {path} hash ({progressValue * 100:F1}%)"));
                        });

                        actualHash = await HashUtils.ComputeFileHashAsync(path, hashProgressAdapter, token);

                        progress?.Report(new FileVerificationEventArgs(path, FileVerificationStatus.Done, progress: 1, message: $"{path} hash compute complete"));

                        // Save the computed hash to cache
                        await SaveCachedHashAsync(path, actualHash, fileLastWriteTime);
                    }

                    if (string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase))
                    {
                        eventArgs = new FileVerificationEventArgs(path, FileVerificationStatus.Success, calculatedHash: actualHash, expectedHash: expectedHash, message: $"{path} hash matches the expected hash:({expectedHash}).", usedCache: usedCache);

                        return eventArgs;
                    }
                    else
                    {
                        eventArgs = new FileVerificationEventArgs(path, FileVerificationStatus.HashMismatch, calculatedHash: actualHash, expectedHash: expectedHash, message: string.Concat("Expected hash: ", expectedHash, ", Actual hash: ", actualHash), usedCache: usedCache);
                        // progress?.Report(eventArgs);
                        return eventArgs;
                    }
                }

                // If neither a file nor a directory, it's not found.
                eventArgs = new FileVerificationEventArgs(path, FileVerificationStatus.FileNotFound, message: "The specified path does not exist.");
                // progress?.Report(eventArgs);
                return eventArgs;
            }
            catch (Exception ex)
            {
                eventArgs = new FileVerificationEventArgs(path, FileVerificationStatus.Error, message: string.Concat("An exception occurred: ", ex.Message));
                progress?.Report(eventArgs);
                return eventArgs;
            }
        }

        /// <summary>
        /// Safely deletes a file or directory and its associated cache file.
        /// </summary>
        /// <param name="path">The absolute path to the file or directory to delete.</param>
        /// <returns>A boolean indicating success or failure.</returns>
        public static bool Delete(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new IOException($"{path} can't be null");
            }
            try
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, true); // Recursive delete
                }
                else if (File.Exists(path))
                {
                    File.Delete(path);

                    // Also delete the cache file if it exists
                    string cacheFilePath = GetCacheFilePath(path);
                    if (File.Exists(cacheFilePath))
                    {
                        File.Delete(cacheFilePath);
                    }
                }
                else
                {
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        /// <summary>
        /// Clears the hash cache for a specific file.
        /// </summary>
        /// <param name="filePath">The file path whose cache should be cleared.</param>
        /// <returns>True if cache was cleared or didn't exist, false if an error occurred.</returns>
        public static bool ClearHashCache(string filePath)
        {
            try
            {
                string cacheFilePath = GetCacheFilePath(filePath);
                if (File.Exists(cacheFilePath))
                {
                    File.Delete(cacheFilePath);
                }
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Removes model artifacts (model directory and download artifacts like .download/.metadata/.chunks) safely.
        /// Used when a model needs to be re-downloaded after corruption.
        /// </summary>
        public static void DeleteModelArtifacts(string modelDirectory, string downloadFilePath)
        {
            try
            {
                if (!string.IsNullOrEmpty(downloadFilePath))
                {
                    Delete(downloadFilePath);
                    Delete(downloadFilePath + ".download");
                    Delete(downloadFilePath + ".download.metadata");
                    Delete(downloadFilePath + ".chunks");
                }
            }
            catch
            {
                // ignore
            }

            try
            {
                if (!string.IsNullOrEmpty(modelDirectory))
                {
                    Delete(modelDirectory);
                }
            }
            catch
            {
                // ignore
            }
        }

        /// <summary>
        /// Validates that the provided model files exist and are non-empty. Returns false if any are missing or too small.
        /// </summary>
        public static bool ValidateModelFiles(IEnumerable<string> filePaths, long minSizeBytes = 4096)
        {
            if (filePaths == null)
            {
                return false;
            }

            var allOk = true;

            foreach (var path in filePaths)
            {
                if (string.IsNullOrWhiteSpace(path))
                {
                    continue;
                }

                try
                {
                    var fileInfo = new FileInfo(path);
                    if (!fileInfo.Exists || fileInfo.Length < minSizeBytes)
                    {
                        allOk = false;
                        SherpaLog.Warning($"Model file invalid or too small: {path} (size={fileInfo.Length} bytes)");
                    }
                }
                catch
                {
                    allOk = false;
                }
            }

            return allOk;
        }

        /// <summary>
        /// Clears all hash cache files in a directory.
        /// </summary>
        /// <param name="directoryPath">The directory path to clear cache files from.</param>
        /// <param name="recursive">Whether to clear cache files recursively in subdirectories.</param>
        /// <returns>The number of cache files cleared.</returns>
        public static int ClearHashCacheInDirectory(string directoryPath, bool recursive = false)
        {
            int clearedCount = 0;
            try
            {
                if (!Directory.Exists(directoryPath))
                {
                    return 0;
                }

                SearchOption searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                string[] cacheFiles = Directory.GetFiles(directoryPath, "*" + HashCacheExtension, searchOption);

                foreach (string cacheFile in cacheFiles)
                {
                    try
                    {
                        File.Delete(cacheFile);
                        clearedCount++;
                    }
                    catch (Exception)
                    {
                        // Continue with other files even if one fails
                    }
                }
            }
            catch (Exception)
            {
                // Return the count of files cleared so far
            }
            return clearedCount;
        }

        public static bool PathExists(string path)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    return true;
                }
                else if (File.Exists(path))
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }
    }
}
