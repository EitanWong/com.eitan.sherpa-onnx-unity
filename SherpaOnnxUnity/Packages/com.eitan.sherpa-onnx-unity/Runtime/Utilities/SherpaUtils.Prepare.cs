using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Eitan.SherpaOnnxUnity.Runtime.Utilities
{
    /// <summary>
    /// Base class for SherpaOnnx models with improved error handling and resource management.
    /// Implements IDisposable pattern for proper resource cleanup.
    /// </summary>
    public partial class SherpaUtils
    {
        public class Prepare
        {
            #region Constants
            private const int MAX_ATTEMPTS = 3;
            private const int INITIAL_RETRY_DELAY_MS = 1000;
            private const int MAX_RETRY_DELAY_MS = 16000;
            private const double RETRY_MULTIPLIER = 2.0;
            private const long MIN_DISK_SPACE_GB = 2;
            private const long BYTES_PER_MB = 1024 * 1024;
            private const string ALLOW_INSECURE_DOWNLOAD_KEY = "SherpaOnnx.AllowInsecureModelDownload";
            private const string FORCE_HASH_VALIDATION_KEY = "SherpaOnnx.ForceModelHashValidation";

            private static readonly string[] COMPRESSED_EXTENSIONS = {
            ".zip", ".tar", ".tar.gz", ".tar.bz2", ".rar", ".7z",
            ".gz", ".bz2", ".xz", ".lz4", ".tgz", ".tbz2", ".zst"
        };
            private static readonly string[] MODEL_SIGNATURE_EXTENSIONS = {
                ".onnx"
            };
            #endregion

            private readonly struct ModelPaths
            {
                public string ModuleDirectory { get; }
                public string ModelDirectory { get; }
                public string DownloadFilePath { get; }
                public string DownloadFileName { get; }
                public bool IsCompressed { get; }

                public string DownloadDirectory => Path.GetDirectoryName(DownloadFilePath) ?? ModuleDirectory;

                public ModelPaths(string moduleDirectory, string modelDirectory, string downloadFilePath, string downloadFileName, bool isCompressed)
                {
                    ModuleDirectory = moduleDirectory;
                    ModelDirectory = modelDirectory;
                    DownloadFilePath = downloadFilePath;
                    DownloadFileName = downloadFileName;
                    IsCompressed = isCompressed;
                }
            }


            /// <summary>
            /// Resolve the expected download file path and target directories following the same logic
            /// used internally by Prepare (module root vs model directory; compressed vs plain).
            /// This lets Editor tooling save the archive where the runtime pipeline expects it.
            /// </summary>
            /// <returns>Absolute file path for the download archive</returns>
            public static string ResolveDownloadFilePath(
                SherpaOnnxModelMetadata metadata,
                out string moduleDirectory,
                out string modelDirectory,
                out string downloadFileName,
                out bool isCompressed)
            {
                if (metadata == null)
                {

                    throw new ArgumentNullException(nameof(metadata));
                }


                var paths = GetModelPaths(metadata);
                moduleDirectory = paths.ModuleDirectory;
                modelDirectory = paths.ModelDirectory;
                downloadFileName = paths.DownloadFileName;
                isCompressed = paths.IsCompressed;
                return paths.DownloadFilePath;
            }

            #region Public Methods

            /// <summary>
            /// Makes sure Unity-specific download infrastructure captures the main thread context before any background work.
            /// Call this from the Unity main thread prior to invoking asynchronous preparation APIs.
            /// </summary>
            public static void EnsureUnityThreadInfrastructure()
            {
#if UNITY
                if (SynchronizationContext.Current == null)
                {
                    throw new InvalidOperationException("EnsureUnityThreadInfrastructure must be invoked from the Unity main thread.");
                }

                RuntimeHelpers.RunClassConstructor(typeof(SherpaFileDownloader).TypeHandle);
#endif
            }

            /// <summary>
            /// Verifies existing model files or downloads and extracts the model if needed.
            /// </summary>
            /// <param name="reporter">Callback for progress feedback. Can be null.</param>
            /// <param name="cancellationToken">Token to cancel the operation.</param>
            /// <returns>True if the model was successfully prepared, false otherwise.</returns>
            /// <exception cref="ObjectDisposedException">Thrown when the object has been disposed.</exception>
            /// <exception cref="OperationCanceledException">Thrown when the operation is canceled.</exception>
            public static async Task<bool> PrepareAndLoadModelAsync(SherpaOnnxModelMetadata metadata, SherpaOnnxFeedbackReporter reporter, CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Yield();

                if (!ValidateMetadata(metadata, reporter))
                {
                    return false;
                }

                var paths = GetModelPaths(metadata);

                ReportSafe(reporter, new PrepareFeedback(metadata, message: $"Preparing {metadata.modelId} model"));

                try
                {
                    EnsureTargetDirectories(paths);

                    if (!CheckDiskSpace(metadata, paths.ModuleDirectory, reporter, cancellationToken))
                    {
                        ReportSafe(reporter, new FailedFeedback(metadata, message: $"Insufficient disk space for model {metadata.modelId}. Minimum required: {MIN_DISK_SPACE_GB}GB."));
                        return false;
                    }

                    for (var attempt = 0; attempt < MAX_ATTEMPTS; attempt++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        if (await VerifyExistingModelAsync(metadata, paths, reporter, attempt, cancellationToken).ConfigureAwait(false))
                        {
                            return true;
                        }

                        var downloadSucceeded = await DownloadModelAsync(metadata, paths.DownloadFilePath, reporter, attempt, cancellationToken).ConfigureAwait(false);

                        if (!downloadSucceeded)
                        {
                            await ApplyExponentialBackoffAsync(attempt, cancellationToken).ConfigureAwait(false);
                            continue;
                        }

                        if (paths.IsCompressed)
                        {
                            var extracted = await ExtractModelAsync(
                                metadata,
                                paths.DownloadFilePath,
                                metadata.downloadFileHash,
                                paths.ModuleDirectory,
                                paths.DownloadFileName,
                                reporter,
                                attempt,
                                cancellationToken).ConfigureAwait(false);

                            if (!extracted)
                            {
                                await ApplyExponentialBackoffAsync(attempt, cancellationToken).ConfigureAwait(false);
                                continue;
                            }
                        }

                        if (await VerifyExistingModelAsync(metadata, paths, reporter, attempt, cancellationToken).ConfigureAwait(false))
                        {
                            return true;
                        }

                        await ApplyExponentialBackoffAsync(attempt, cancellationToken).ConfigureAwait(false);
                    }

                    ReportSafe(reporter, new FailedFeedback(metadata, message: $"Failed to prepare model {metadata.modelId} after {MAX_ATTEMPTS} attempts. Please download and install the model manually."));
                    await CleanPathAsync(metadata, new[] { paths.ModelDirectory, paths.DownloadFilePath }, reporter, cancellationToken).ConfigureAwait(false);

                    return false;
                }
                catch (OperationCanceledException)
                {
                    ReportSafe(reporter, new CancelFeedback(metadata, message: "PrepareModel canceled"));
                    throw;
                }
                catch (Exception ex)
                {
                    ReportSafe(reporter, new FailedFeedback(metadata, message: ex.Message, exception: ex));
                    await CleanPathAsync(metadata, new[] { paths.ModelDirectory, paths.DownloadFilePath }, reporter, cancellationToken).ConfigureAwait(false);
                    throw;
                }
            }
            #endregion

            public static async Task<bool> CheckIsModelDownloadedAsync(SherpaOnnxModelMetadata metadata, CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Yield();
                var paths = GetModelPaths(metadata);

                try
                {
                    if (!Directory.Exists(paths.ModuleDirectory) || !Directory.Exists(paths.ModelDirectory) || !Directory.Exists(paths.DownloadDirectory))
                    {
                        // EnsureTargetDirectories(paths);
                        return false;
                    }

                    return await VerifyExistingModelAsync(metadata, paths, null, 0, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception)
                {
                    throw;
                }

            }

            #region Private Methods

            private static bool ValidateMetadata(SherpaOnnxModelMetadata metadata, SherpaOnnxFeedbackReporter reporter)
            {
                var forceHashValidation = IsHashValidationForced();

                if (metadata == null)
                {
                    ReportSafe(reporter, new FailedFeedback(metadata, message: "No model metadata supplied."));
                    return false;
                }

                if (string.IsNullOrWhiteSpace(metadata.modelId))
                {
                    ReportSafe(reporter, new FailedFeedback(metadata, message: "Model metadata is missing a modelId."));
                    return false;
                }

                if (string.IsNullOrWhiteSpace(metadata.downloadUrl))
                {
                    ReportSafe(reporter, new FailedFeedback(metadata, message: $"{metadata.modelId}: Download URL is missing."));
                    return false;
                }

                // We no longer require listing specific model file names or per-file hashes.
                // When hash enforcement is enabled, we require ONLY the archive hash.
                var downloadHashMissing = string.IsNullOrWhiteSpace(metadata.downloadFileHash);

                if (forceHashValidation)
                {
                    if (downloadHashMissing)
                    {
                        ReportSafe(reporter, new FailedFeedback(metadata, message: $"{metadata.modelId}: Download file hash is required when {FORCE_HASH_VALIDATION_KEY}=true."));
                        return false;
                    }
                }
                else
                {
                    if (downloadHashMissing)
                    {
                        var message = $"{metadata.modelId}: Hash verification for the downloaded archive will be skipped because no hash is configured. Enable {FORCE_HASH_VALIDATION_KEY}=true to enforce.";
                        ReportSafe(reporter, new VerifyFeedback(metadata, message: message, filePath: metadata.downloadUrl));
                        UnityEngine.Debug.LogWarning(message);
                    }
                }

                return true;
            }

            private static ModelPaths GetModelPaths(SherpaOnnxModelMetadata metadata)
            {
                var moduleDirectoryPath = SanitizePath(SherpaPathResolver.GetModuleRootPath(metadata.moduleType));
                var modelDirectoryPath = SanitizePath(Path.Combine(moduleDirectoryPath, metadata.modelId));

                string downloadFileName = string.Empty;
                if (Uri.TryCreate(metadata.downloadUrl, UriKind.Absolute, out var downloadUri))
                {
                    downloadFileName = Path.GetFileName(downloadUri.LocalPath);
                }

                if (string.IsNullOrEmpty(downloadFileName))
                {
                    downloadFileName = Path.GetFileName(metadata.downloadUrl);
                }

                if (string.IsNullOrEmpty(downloadFileName))
                {
                    throw new InvalidOperationException($"Could not determine download file name for model {metadata.modelId}.");
                }

                var isCompressed = IsCompressedFile(downloadFileName);
                var downloadRoot = isCompressed ? moduleDirectoryPath : modelDirectoryPath;
                var downloadFilePath = SanitizePath(Path.Combine(downloadRoot, downloadFileName));

                return new ModelPaths(moduleDirectoryPath, modelDirectoryPath, downloadFilePath, downloadFileName, isCompressed);
            }

            private static string SanitizePath(string path)
            {
                if (string.IsNullOrEmpty(path))
                { return path; }

                // Get the full path to resolve any relative path components
                var fullPath = Path.GetFullPath(path);

                // Additional validation could be added here based on security requirements
                return fullPath;
            }

            private static bool IsCompressedFile(string fileName)
            {
                if (string.IsNullOrEmpty(fileName))
                { return false; }

                var lowerFileName = fileName.ToLowerInvariant();
                return COMPRESSED_EXTENSIONS.Any(ext => lowerFileName.EndsWith(ext));
            }


            private static bool CheckDiskSpace(SherpaOnnxModelMetadata metadata, string directoryPath, SherpaOnnxFeedbackReporter reporter, CancellationToken cancellationToken)
            {
                try
                {
#if UNITY_ANDROID && !UNITY_EDITOR
                    // On Android, test write access to the actual target directory
                    // as DriveInfo doesn't work reliably on Android
                    
                    // Ensure the directory exists for testing
                    if (!Directory.Exists(directoryPath))
                    {
                        Directory.CreateDirectory(directoryPath);
                    }
                    
                    // Try to create a small test file to verify write access and available space
                    var testFilePath = Path.Combine(directoryPath, $"space_test_{System.Guid.NewGuid()}.tmp");
                    
                    try
                    {
                        // Create a small test file (1KB) to verify space availability
                        var testData = new byte[1024];
                        File.WriteAllBytes(testFilePath, testData);
                        File.Delete(testFilePath);
                        
                        // If we can write a small file, assume we have enough space
                        // This is a pragmatic approach since Android's storage APIs are limited
                        return true;
                    }
                    catch (Exception)
                    {
                        // If we can't even write a small test file, assume insufficient space
                        ReportSafe(reporter, new VerifyFeedback(metadata, message: "Cannot write to storage, insufficient space or permissions", filePath: directoryPath));
                        return false;
                    }
                    finally
                    {
                        // Clean up test file if it still exists
                        if (File.Exists(testFilePath))
                        {
                            try { File.Delete(testFilePath); } catch { }
                        }
                    }
#else
                    // On non-Android platforms, use DriveInfo
                    var rootPath = Path.GetPathRoot(directoryPath);
                    if (string.IsNullOrEmpty(rootPath))
                    {
                        // Fallback: assume sufficient space if we can't determine the root
                        return true;
                    }

                    var drive = new DriveInfo(rootPath);
                    var availableSpaceMB = drive.AvailableFreeSpace / BYTES_PER_MB;
                    var requiredSpaceMB = MIN_DISK_SPACE_GB * 1024; // Convert GB to MB

                    if (availableSpaceMB < requiredSpaceMB)
                    {
                        ReportSafe(reporter, new VerifyFeedback(metadata, message: $"Insufficient disk space: {availableSpaceMB}MB available, {requiredSpaceMB}MB required", filePath: directoryPath));
                        return false;
                    }

                    return true;
#endif
                }
                catch (Exception ex)
                {
                    // On any error, log it but assume sufficient space to avoid blocking legitimate operations
                    ReportSafe(reporter, new VerifyFeedback(metadata, message: $"Could not check disk space: {ex.Message}. Proceeding with operation.", filePath: directoryPath));
                    return true;
                }
            }


            private static async Task<bool> VerifyExistingModelAsync(SherpaOnnxModelMetadata metadata,
                ModelPaths paths,
                SherpaOnnxFeedbackReporter reporter, int attempt, CancellationToken cancellationToken)
            {
                ReportSafe(reporter, new VerifyFeedback(
                    metadata,
                    message: $"Validating model {metadata.modelId} (attempt {attempt + 1}/{MAX_ATTEMPTS})",
                    filePath: paths.ModelDirectory,
                    progress: 0));

                cancellationToken.ThrowIfCancellationRequested();

                if (!Directory.Exists(paths.ModelDirectory))
                {
                    ReportSafe(reporter, new VerifyFeedback(
                        metadata,
                        message: $"Model directory does not exist (attempt {attempt + 1}/{MAX_ATTEMPTS}): {paths.ModelDirectory}",
                        filePath: paths.ModelDirectory,
                        progress: 0));
                    return false;
                }

                try
                {
                    // Detection rule: consider the model "installed" if we can find at least one
                    // signature model file (e.g., *.onnx) inside the model directory (recursive scan).
                    bool hasSignature = false;

                    foreach (var ext in MODEL_SIGNATURE_EXTENSIONS)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        // Enumerate lazily to reduce memory pressure on large folders.
                        var any = Directory.EnumerateFiles(paths.ModelDirectory, "*" + ext, SearchOption.AllDirectories)
                                           .Any(p => !p.EndsWith(".meta", StringComparison.OrdinalIgnoreCase));
                        if (any)
                        {
                            hasSignature = true;
                            break;
                        }
                    }

                    if (!hasSignature)
                    {
                        ReportSafe(reporter, new VerifyFeedback(
                            metadata,
                            message: $"No signature model files found (looking for {string.Join(", ", MODEL_SIGNATURE_EXTENSIONS)}) in {paths.ModelDirectory}.",
                            filePath: paths.ModelDirectory,
                            progress: 0));
                        return false;
                    }

                    ReportSafe(reporter, new VerifyFeedback(
                        metadata,
                        message: "Model files detected. Verification succeeded.",
                        filePath: paths.ModelDirectory,
                        progress: 100));

                    // Optional cleanup: if the download was a compressed archive and it still exists, delete it.
                    if (paths.IsCompressed && SherpaFileUtils.PathExists(paths.DownloadFilePath))
                    {
                        ReportSafe(reporter, new CleanFeedback(metadata, filePath: paths.DownloadFilePath, message: $"Cleaning up {paths.DownloadFilePath}"));
                        SherpaFileUtils.Delete(paths.DownloadFilePath);
                    }

                    await Task.Yield(); // keep method truly async
                    return true;
                }
                catch (OperationCanceledException)
                {
                    ReportSafe(reporter, new CancelFeedback(metadata, message: "Verification canceled"));
                    throw;
                }
                catch (Exception ex)
                {
                    ReportSafe(reporter, new FailedFeedback(metadata, message: ex.Message, exception: ex));
                    return false;
                }
            }

            // TODO: 重构VerifyFileWithIndexAsync 使其作为SherpaOnnxModel层的通用文件验证方法，可以批量传入filePaths 以及expiatedSha256Array,进行批量验证，等待全部验证完毕后再返回结果。
            private static async Task<(int Index, FileVerificationEventArgs Result)> VerifyFileWithIndexAsync(SherpaOnnxModelMetadata metadata,
                int index, string filePath, string expectedSha256, SherpaOnnxFeedbackReporter reporter, CancellationToken cancellationToken)
            {
                Progress<FileVerificationEventArgs> progressAdapter = new Progress<FileVerificationEventArgs>(args =>
                {
                    ReportSafe(reporter, new VerifyFeedback(metadata, message: args.Message, filePath: filePath, progress: args.Progress));
                });

                var result = await SherpaFileUtils.VerifyFileAsync(filePath, expectedSha256, progress: progressAdapter, cancellationToken: cancellationToken).ConfigureAwait(false);

                ReportSafe(reporter, new VerifyFeedback(metadata, message: result.Message, filePath: filePath, progress: result.Progress));
                return (index, result);
            }

            private static async Task<bool> DownloadModelAsync(SherpaOnnxModelMetadata metadata, string downloadFilePath,
                SherpaOnnxFeedbackReporter reporter, int retryCount, CancellationToken cancellationToken)
            {
                try
                {
                    // Check if the file is already downloaded with hash verification
                    var (_, downloadedFileCheckResult) = await VerifyFileWithIndexAsync(metadata, 0, downloadFilePath, metadata.downloadFileHash, reporter, cancellationToken).ConfigureAwait(false);
                    if (downloadedFileCheckResult.Status == FileVerificationStatus.Success)
                    {

                        return true;
                    }

                    if (!TryResolveDownloadUri(metadata, reporter, out var downloadUri))
                    {
                        return false;
                    }
                    using var downloader = new SherpaFileDownloader(metadata);

                    if (reporter != null)
                    {
                        downloader.Feedback += reporter.Report;
                    }

                    try
                    {
                        var downloadSuccess = await downloader.DownloadAsync(downloadUri.ToString(), downloadFilePath, cancellationToken: cancellationToken).ConfigureAwait(false);
                        if (!downloadSuccess)
                        {
                            SherpaFileUtils.Delete(downloadFilePath);
                            ReportSafe(reporter, new FailedFeedback(metadata, message: $"Failed downloading {downloadUri} to {downloadFilePath}"));
                            return false;
                        }

                        return true;
                    }
                    finally
                    {
                        if (reporter != null)
                        {
                            downloader.Feedback -= reporter.Report;
                        }
                    }
                }
                catch (OperationCanceledException ex)
                {
                    ReportSafe(reporter, new CancelFeedback(metadata, message: ex.Message, exception: ex));
                    throw;
                }
                catch (Exception ex)
                {
                    ReportSafe(reporter, new FailedFeedback(metadata, message: ex.Message, exception: ex));
                    SherpaFileUtils.Delete(downloadFilePath);
                    return false;
                }
            }

            private static async Task<bool> ExtractModelAsync(SherpaOnnxModelMetadata metadata, string zipFilePath, string zipFileHash,
                string moduleDirectoryPath, string zipFileName, SherpaOnnxFeedbackReporter reporter,
                int retryCount, CancellationToken cancellationToken)
            {
                try
                {
                    var (_, zipVerifyResult) = await VerifyFileWithIndexAsync(metadata, 0, zipFilePath, zipFileHash, reporter, cancellationToken).ConfigureAwait(false);

                    if (zipVerifyResult.Status != FileVerificationStatus.Success)
                    {
                        return false;
                    }

                    // UnityEngine.Debug.Log($"zip VerifyResult {zipVerifyResult.Status} : {zipVerifyResult.Message}");
                    var progressAdapter = new Progress<DecompressionEventArgs>(args =>
                    {
                        ReportSafe(reporter, new UncompressFeedback(metadata, filePath: zipFilePath, progress: args.Progress, message: $"Extracting {zipFileName} ({args.Progress * 100:F1}%) Duration: [{args.ElapsedTime}]"));
                    });

                    var result = await SherpaUncompressHelper.DecompressAsync(zipFilePath, moduleDirectoryPath, progressAdapter, cancellationToken: cancellationToken).ConfigureAwait(false);

                    if (result.Success)
                    {
                        ReportSafe(reporter, new UncompressFeedback(metadata, filePath: zipFilePath, progress: result.Progress, message: $"Extract Success: {zipFileName} Duration: [{result.ElapsedTime}]"));
                        return true;
                    }
                    else
                    {
                        throw new InvalidOperationException(result.ErrorMessage);
                    }
                }
                catch (OperationCanceledException)
                {
                    ReportSafe(reporter, new CancelFeedback(metadata, message: $"Extract: {zipFileHash} Canceled"));
                    throw;
                }
                catch (Exception ex)
                {
                    // _logger.LogError($"Extraction failed: {ex.Message}");
                    ReportSafe(reporter, new FailedFeedback(metadata, message: ex.Message, exception: ex));
                    throw;
                }
            }

            private static async Task CleanPathAsync(SherpaOnnxModelMetadata metadata, string[] filePaths, SherpaOnnxFeedbackReporter reporter, CancellationToken cancellationToken)
            {
                if (filePaths == null || filePaths.Length == 0)
                { return; }

                try
                {
                    // Remove duplicates and filter existing paths
                    var distinctPaths = filePaths
                        .Where(path => !string.IsNullOrEmpty(path))
                        .Distinct()
                        .Where(SherpaFileUtils.PathExists)
                        .ToArray();

                    if (distinctPaths.Length == 0)
                    { return; }

                    // Create tasks for parallel deletion
                    var deletionTasks = distinctPaths.Select(path =>
                        Task.Run(() =>
                        {
                            cancellationToken.ThrowIfCancellationRequested();

                            ReportSafe(reporter, new CleanFeedback(metadata, filePath: path, message: $"Cleaning up: {path}"));

                            try
                            {
                                SherpaFileUtils.Delete(path);
                            }
                            catch (Exception ex)
                            {
                                // _logger.LogWarning($"Failed to delete path {path}: {ex.Message}");
                                ReportSafe(reporter, new FailedFeedback(metadata, message: ex.Message, exception: ex));
                                throw;
                            }
                        }, cancellationToken));

                    await Task.WhenAll(deletionTasks).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    // _logger.LogError($"Error during cleanup: {ex.Message}");
                    ReportSafe(reporter, new FailedFeedback(metadata, message: ex.Message, exception: ex));
                    throw;
                }
            }

            private static bool TryResolveDownloadUri(SherpaOnnxModelMetadata metadata, SherpaOnnxFeedbackReporter reporter, out Uri downloadUri)
            {
                downloadUri = null;

                if (metadata == null)
                {
                    ReportSafe(reporter, new FailedFeedback(metadata, message: "Cannot resolve download URL without metadata."));
                    return false;
                }

                var rawUrl = metadata.downloadUrl?.Trim();
                if (string.IsNullOrEmpty(rawUrl))
                {
                    ReportSafe(reporter, new FailedFeedback(metadata, message: $"{metadata.modelId}: Download URL is empty."));
                    return false;
                }

                if (!Uri.TryCreate(rawUrl, UriKind.Absolute, out var resolvedUri))
                {
                    ReportSafe(reporter, new FailedFeedback(metadata, message: $"Invalid download URL: {rawUrl}"));
                    return false;
                }

                if (!IsSecureDownloadScheme(resolvedUri))
                {
                    if (!IsInsecureDownloadAllowed())
                    {
                        ReportSafe(reporter, new FailedFeedback(metadata, message: $"Rejected insecure download scheme '{resolvedUri.Scheme}'. Set {ALLOW_INSECURE_DOWNLOAD_KEY}=true to override."));
                        return false;
                    }

                    ReportSafe(reporter, new VerifyFeedback(metadata, message: $"Allowing insecure download for {resolvedUri} because {ALLOW_INSECURE_DOWNLOAD_KEY}=true.", filePath: resolvedUri.ToString()));
                    UnityEngine.Debug.LogWarning($"[{metadata.modelId}] Allowing insecure download for {resolvedUri} (override enabled).");
                }

                downloadUri = resolvedUri;
                return true;
            }

            private static bool IsSecureDownloadScheme(Uri uri)
            {
                if (uri is null)
                {
                    return false;
                }

                return uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeFile;
            }

            private static bool IsInsecureDownloadAllowed() =>
                SherpaOnnxEnvironment.GetBool(ALLOW_INSECURE_DOWNLOAD_KEY, @default: false);

            private static bool IsHashValidationForced() =>
                SherpaOnnxEnvironment.GetBool(FORCE_HASH_VALIDATION_KEY, @default: false);

            private static void EnsureTargetDirectories(ModelPaths paths)
            {
                if (!Directory.Exists(paths.ModuleDirectory))
                {
                    Directory.CreateDirectory(paths.ModuleDirectory);
                }
                if (!Directory.Exists(paths.ModelDirectory))
                {
                    Directory.CreateDirectory(paths.ModelDirectory);
                }
                if (!Directory.Exists(paths.DownloadDirectory))
                {
                    Directory.CreateDirectory(paths.DownloadDirectory);
                }
            }

            private static void ReportSafe(SherpaOnnxFeedbackReporter reporter, IFeedback feedback)
            {
                if (reporter == null || feedback == null)
                {
                    return;
                }

                try
                {
                    reporter.Report(feedback);
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogWarning($"SherpaUtils.Prepare feedback dispatch failed: {ex.Message}");
                }
            }

            private static async Task ApplyExponentialBackoffAsync(int attempt, CancellationToken cancellationToken)
            {
                if (attempt >= MAX_ATTEMPTS - 1)
                { return; }

                var delay = Math.Min(
                    INITIAL_RETRY_DELAY_MS * Math.Pow(RETRY_MULTIPLIER, attempt),
                    MAX_RETRY_DELAY_MS);

                await Task.Delay(TimeSpan.FromMilliseconds(delay), cancellationToken).ConfigureAwait(false);
            }




            #endregion

        }

    }


}
