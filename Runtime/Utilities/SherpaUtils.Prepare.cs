using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Eitan.SherpaONNXUnity.Runtime.Constants;

namespace Eitan.SherpaONNXUnity.Runtime.Utilities
{
    /// <summary>
    /// Base class for SherpaONNX models with improved error handling and resource management.
    /// Implements IDisposable pattern for proper resource cleanup.
    /// </summary>
    public partial class SherpaUtils
    {
        public partial class Prepare
        {
            #region Constants
            private const int MAX_ATTEMPTS = 3;
            private const int INITIAL_RETRY_DELAY_MS = 1000;
            private const int MAX_RETRY_DELAY_MS = 16000;
            private const double RETRY_MULTIPLIER = 2.0;
            private const long MIN_DISK_SPACE_GB = 2;
            private const long BYTES_PER_MB = 1024 * 1024;
            private const string ALLOW_INSECURE_DOWNLOAD_KEY = "SherpaONNX.AllowInsecureModelDownload";
            private const string FORCE_HASH_VALIDATION_KEY = "SherpaONNX.ForceModelHashValidation";

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
                SherpaONNXModelMetadata metadata,
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

                SherpaPathResolver.PrimeUnityPaths();
                RuntimeHelpers.RunClassConstructor(typeof(SherpaFileDownloader).TypeHandle);
#endif
            }

            /// <summary>
            /// Verifies existing model files or downloads and extracts the model if needed.
            /// Returns a structured result with an error code for easier diagnostics.
            /// </summary>
            public static async Task<PrepareResult> PrepareAndLoadModelWithResultAsync(
                SherpaONNXModelMetadata metadata,
                SherpaONNXFeedbackReporter reporter,
                CancellationToken cancellationToken = default,
                PrepareOptions options = null)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Yield();

                if (!ValidateMetadata(metadata, reporter, out var validationError, out var validationMessage))
                {
                    var errorCode = validationError == PrepareErrorCode.None
                        ? PrepareErrorCode.MetadataMissing
                        : validationError;
                    var message = string.IsNullOrWhiteSpace(validationMessage)
                        ? "Invalid or missing model metadata."
                        : validationMessage;
                    ReportSafe(reporter, new FailedFeedback(metadata, message, errorCode: errorCode));
                    return PrepareResult.Fail(errorCode, message);
                }

                var paths = GetModelPaths(metadata);
                options ??= new PrepareOptions();
                var context = new PrepareContext(
                    metadata,
                    paths.ModuleDirectory,
                    paths.ModelDirectory,
                    paths.DownloadFilePath,
                    paths.DownloadFileName,
                    paths.IsCompressed);
                var verifyExistingAsync = options.VerifyExistingAsync;
                var downloadAsync = options.DownloadAsync;
                var extractAsync = options.ExtractAsync;
                var cleanupAsync = options.CleanupAsync;
                var modelDirectoryExisted = Directory.Exists(paths.ModelDirectory);
                var downloadAttempted = false;
                var lastFailure = PrepareErrorCode.UnexpectedError;

                SherpaLog.Verbose(
                    $"[Prepare] Begin model prepare for '{metadata.modelId}'. Archive={paths.DownloadFileName} Target={paths.ModelDirectory}",
                    category: "Prepare");

                ReportSafe(reporter, new PrepareFeedback(metadata, message: $"Preparing {metadata.modelId} model"));

                try
                {
                    EnsureTargetDirectories(paths);

                    if (!CheckDiskSpace(metadata, paths.ModuleDirectory, reporter, cancellationToken))
                    {
                        var message = $"Insufficient disk space for model {metadata.modelId}. Minimum required: {MIN_DISK_SPACE_GB}GB.";
                        ReportSafe(reporter, new FailedFeedback(metadata, message, errorCode: PrepareErrorCode.InsufficientDiskSpace));
                        return PrepareResult.Fail(PrepareErrorCode.InsufficientDiskSpace, message);
                    }

                    var autoDownloadEnabled = IsAutoDownloadEnabled();

                    for (var attempt = 0; attempt < MAX_ATTEMPTS; attempt++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        SherpaLog.Trace(
                            $"[Prepare] Attempt {attempt + 1}/{MAX_ATTEMPTS} for {metadata.modelId} (autoDownload={autoDownloadEnabled}, compressed={paths.IsCompressed})",
                            category: "Prepare");

                        var verified = verifyExistingAsync != null
                            ? await verifyExistingAsync(context, reporter, attempt, cancellationToken).ConfigureAwait(false)
                            : await VerifyExistingModelAsync(metadata, paths, reporter, attempt, cancellationToken).ConfigureAwait(false);

                        if (verified)
                        {
                            SherpaLog.Info($"[Prepare] Model '{metadata.modelId}' already verified on disk.", category: "Prepare");
                            return PrepareResult.Ok();
                        }

                        if (!autoDownloadEnabled)
                        {
                            ReportAutoDownloadDisabled(metadata, reporter, paths.ModelDirectory);
                            var message = $"Automatic download is disabled for {metadata.modelId}.";
                            lastFailure = PrepareErrorCode.AutoDownloadDisabled;
                            return PrepareResult.Fail(lastFailure, message);
                        }

                        if (string.IsNullOrWhiteSpace(metadata.downloadUrl))
                        {
                            var message = $"Download URL is missing for {metadata.modelId}. Please install the model locally.";
                            ReportSafe(reporter, new FailedFeedback(metadata, message, errorCode: PrepareErrorCode.DownloadUrlMissing));
                            lastFailure = PrepareErrorCode.DownloadUrlMissing;
                            return PrepareResult.Fail(lastFailure, message);
                        }

                        downloadAttempted = true;
                        var downloadError = downloadAsync != null
                            ? await downloadAsync(context, reporter, attempt, cancellationToken).ConfigureAwait(false)
                            : await DownloadModelAsync(metadata, paths.DownloadFilePath, reporter, attempt, cancellationToken).ConfigureAwait(false);
                        if (downloadError != PrepareErrorCode.None)
                        {
                            lastFailure = downloadError;
                            if (downloadError == PrepareErrorCode.Cancelled)
                            {
                                var message = "Download canceled.";
                                return PrepareResult.Fail(downloadError, message);
                            }
                            if (downloadError == PrepareErrorCode.DownloadUrlInvalid ||
                                downloadError == PrepareErrorCode.DownloadInsecureRejected)
                            {
                                var message = $"Download URL rejected for {metadata.modelId}.";
                                ReportSafe(reporter, new FailedFeedback(metadata, message, errorCode: downloadError));
                                return PrepareResult.Fail(downloadError, message);
                            }

                            await ApplyExponentialBackoffAsync(attempt, cancellationToken).ConfigureAwait(false);
                            continue;
                        }

                        if (paths.IsCompressed)
                        {
                            var extracted = extractAsync != null
                                ? await extractAsync(context, reporter, attempt, cancellationToken).ConfigureAwait(false)
                                : await ExtractModelAsync(
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
                                SherpaLog.Warning($"[Prepare] Extraction failed for '{metadata.modelId}'. Retrying.", category: "Prepare");
                                lastFailure = PrepareErrorCode.ExtractionFailed;
                                await ApplyExponentialBackoffAsync(attempt, cancellationToken).ConfigureAwait(false);
                                continue;
                            }
                        }

                        verified = verifyExistingAsync != null
                            ? await verifyExistingAsync(context, reporter, attempt, cancellationToken).ConfigureAwait(false)
                            : await VerifyExistingModelAsync(metadata, paths, reporter, attempt, cancellationToken).ConfigureAwait(false);

                        if (verified)
                        {
                            SherpaLog.Info($"[Prepare] Model '{metadata.modelId}' prepared successfully after download.", category: "Prepare");
                            return PrepareResult.Ok();
                        }

                        lastFailure = PrepareErrorCode.VerificationFailed;
                        await ApplyExponentialBackoffAsync(attempt, cancellationToken).ConfigureAwait(false);
                    }

                    var exhaustedMessage = $"Failed to prepare model {metadata.modelId} after {MAX_ATTEMPTS} attempts. Please download and install the model manually.";
                    ReportSafe(reporter, new FailedFeedback(metadata, exhaustedMessage, errorCode: lastFailure));
                    var cleanupTargets = GetCleanupTargets(paths, modelDirectoryExisted, downloadAttempted);
                    var cleanupAttempted = false;
                    if (cleanupTargets.Length > 0 && IsAutoDeleteCorruptedEnabled())
                    {
                        cleanupAttempted = true;
                        if (cleanupAsync != null)
                        {
                            await cleanupAsync(context, cleanupTargets, reporter, cancellationToken).ConfigureAwait(false);
                        }
                        else
                        {
                            await CleanPathAsync(metadata, cleanupTargets, reporter, cancellationToken).ConfigureAwait(false);
                        }
                    }
                    SherpaLog.Error($"[Prepare] Exhausted retries while preparing '{metadata.modelId}'. Cleaned temp data.", category: "Prepare");
                    return PrepareResult.Fail(lastFailure, exhaustedMessage, cleanupAttempted: cleanupAttempted);
                }
                catch (OperationCanceledException)
                {
                    var message = "PrepareModel canceled";
                    ReportSafe(reporter, new CancelFeedback(metadata, message));
                    return PrepareResult.Fail(PrepareErrorCode.Cancelled, message);
                }
                catch (Exception ex)
                {
                    ReportSafe(reporter, new FailedFeedback(metadata, ex.Message, ex, PrepareErrorCode.UnexpectedError));
                    var cleanupTargets = GetCleanupTargets(paths, modelDirectoryExisted, downloadAttempted);
                    var cleanupAttempted = false;
                    if (cleanupTargets.Length > 0 && IsAutoDeleteCorruptedEnabled())
                    {
                        cleanupAttempted = true;
                        if (cleanupAsync != null)
                        {
                            await cleanupAsync(context, cleanupTargets, reporter, cancellationToken).ConfigureAwait(false);
                        }
                        else
                        {
                            await CleanPathAsync(metadata, cleanupTargets, reporter, cancellationToken).ConfigureAwait(false);
                        }
                    }
                    SherpaLog.Exception(ex, category: "Prepare", message: $"[Prepare] Unexpected failure for '{metadata.modelId}'.");
                    return PrepareResult.Fail(PrepareErrorCode.UnexpectedError, ex.Message, ex, cleanupAttempted);
                }
            }
            #endregion

            public static async Task<bool> CheckIsModelDownloadedAsync(SherpaONNXModelMetadata metadata, CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Yield();
                var paths = GetModelPaths(metadata);

                try
                {
                    if (!Directory.Exists(paths.ModuleDirectory) || !Directory.Exists(paths.ModelDirectory) || !Directory.Exists(paths.DownloadDirectory))
                    {
                        // EnsureTargetDirectories(paths);
                        SherpaLog.Trace($"[Prepare] Model '{metadata.modelId}' not downloaded (missing directories).", category: "Prepare");
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

            private static bool ValidateMetadata(
                SherpaONNXModelMetadata metadata,
                SherpaONNXFeedbackReporter reporter,
                out PrepareErrorCode errorCode,
                out string errorMessage)
            {
                errorCode = PrepareErrorCode.None;
                errorMessage = string.Empty;
                var forceHashValidation = IsHashValidationForced();

                if (metadata == null)
                {
                    errorCode = PrepareErrorCode.MetadataMissing;
                    errorMessage = "No model metadata supplied.";
                    ReportSafe(reporter, new FailedFeedback(metadata, message: errorMessage, errorCode: errorCode));
                    SherpaLog.Error("[Prepare] Metadata missing for prepare call.", category: "Prepare");
                    return false;
                }

                if (string.IsNullOrWhiteSpace(metadata.modelId))
                {
                    errorCode = PrepareErrorCode.ModelIdMissing;
                    errorMessage = "Model metadata is missing a modelId.";
                    ReportSafe(reporter, new FailedFeedback(metadata, message: errorMessage, errorCode: errorCode));
                    SherpaLog.Error("[Prepare] Model metadata is missing modelId.", category: "Prepare");
                    return false;
                }

                if (string.IsNullOrWhiteSpace(metadata.downloadUrl))
                {
                    ReportSafe(reporter, new VerifyFeedback(metadata, message: $"{metadata.modelId}: Download URL is missing. Assuming local-only model.", filePath: SherpaPathResolver.GetModelRootPath(metadata.modelId)));
                    SherpaLog.Warning($"[Prepare] {metadata.modelId} has empty download URL. Assuming local-only model.", category: "Prepare");
                    return true;
                }

                // We no longer require listing specific model file names or per-file hashes.
                // When hash enforcement is enabled, we require ONLY the archive hash.
                var downloadHashMissing = string.IsNullOrWhiteSpace(metadata.downloadFileHash);

                if (forceHashValidation)
                {
                    if (downloadHashMissing)
                    {
                        errorCode = PrepareErrorCode.HashMissing;
                        errorMessage = $"{metadata.modelId}: Download file hash is required when {FORCE_HASH_VALIDATION_KEY}=true.";
                        ReportSafe(reporter, new FailedFeedback(metadata, message: errorMessage, errorCode: errorCode));
                        return false;
                    }
                }
                else
                {
                    if (downloadHashMissing)
                    {
                        // Try to populate the hash from checksum.txt to prevent using corrupted archives.
                        if (SherpaONNXConstants.TryPopulateDownloadHash(metadata))
                        {
                            downloadHashMissing = string.IsNullOrWhiteSpace(metadata.downloadFileHash);
                        }

                        if (downloadHashMissing)
                        {
                            errorCode = PrepareErrorCode.HashMissing;
                            errorMessage = $"{metadata.modelId}: Missing download hash; cannot safely load model. Please provide downloadFileHash or set {FORCE_HASH_VALIDATION_KEY}=true to enforce verification.";
                            ReportSafe(reporter, new FailedFeedback(metadata, message: errorMessage, errorCode: errorCode));
                            SherpaLog.Warning(errorMessage);
                            return false;
                        }
                    }
                }

                return true;
            }

            private static ModelPaths GetModelPaths(SherpaONNXModelMetadata metadata)
            {
                // Avoid "undefined" module folders by auto-inferring when moduleType is not set.
                var moduleType = metadata.moduleType != SherpaONNXModuleType.Undefined
                    ? metadata.moduleType
                    : SherpaUtils.Model.GetModuleTypeByModelId(metadata.modelId);
                if (metadata.moduleType == SherpaONNXModuleType.Undefined)
                {
                    metadata.moduleType = moduleType;
                }

                var moduleDirectoryPath = SanitizePath(SherpaPathResolver.GetModuleRootPath(moduleType));
                var modelDirectoryPath = SanitizePath(Path.Combine(moduleDirectoryPath, metadata.modelId));

                string downloadFileName = string.Empty;
                if (!string.IsNullOrWhiteSpace(metadata.downloadUrl))
                {
                    if (Uri.TryCreate(metadata.downloadUrl, UriKind.Absolute, out var downloadUri))
                    {
                        downloadFileName = Path.GetFileName(downloadUri.LocalPath);
                    }

                    if (string.IsNullOrEmpty(downloadFileName))
                    {
                        downloadFileName = Path.GetFileName(metadata.downloadUrl);
                    }
                }

                if (string.IsNullOrEmpty(downloadFileName))
                {
                    downloadFileName = metadata.modelId + ".onnx";
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


            private static bool CheckDiskSpace(SherpaONNXModelMetadata metadata, string directoryPath, SherpaONNXFeedbackReporter reporter, CancellationToken cancellationToken)
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


            private static bool IsInsecureDownloadAllowed() =>
                SherpaONNXEnvironment.GetBool(ALLOW_INSECURE_DOWNLOAD_KEY, @default: false);

            private static bool IsHashValidationForced() =>
                SherpaONNXEnvironment.GetBool(FORCE_HASH_VALIDATION_KEY, @default: false);

            private static bool IsAutoDownloadEnabled() =>
                SherpaONNXEnvironment.GetBool(SherpaONNXEnvironment.BuiltinKeys.AutoDownloadModels, @default: true);

            private static bool IsAutoDeleteCorruptedEnabled() =>
                SherpaONNXEnvironment.GetBool(SherpaONNXEnvironment.BuiltinKeys.AutoDeleteCorruptedModels, @default: true);

            private static void ReportAutoDownloadDisabled(SherpaONNXModelMetadata metadata, SherpaONNXFeedbackReporter reporter, string targetDirectory)
            {
                var key = SherpaONNXEnvironment.BuiltinKeys.AutoDownloadModels;
                var message = $"Automatic download skipped because {key}=false. Ensure the model files exist under {targetDirectory}.";
                ReportSafe(reporter, new VerifyFeedback(metadata, message: message, filePath: targetDirectory));
                SherpaLog.Warning($"[Prepare] Auto-download disabled. Expecting {metadata?.modelId ?? "<unknown>"} at {targetDirectory}", category: "Prepare");
            }

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

            private static string[] GetCleanupTargets(ModelPaths paths, bool modelDirectoryExisted, bool downloadAttempted)
            {
                if (!downloadAttempted)
                {
                    return Array.Empty<string>();
                }

                var targets = new List<string> { paths.DownloadFilePath };
                if (!modelDirectoryExisted)
                {
                    targets.Add(paths.ModelDirectory);
                }

                return targets.ToArray();
            }

            private static void ReportSafe(SherpaONNXFeedbackReporter reporter, IFeedback feedback)
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
                    SherpaLog.Warning($"SherpaUtils.Prepare feedback dispatch failed: {ex.Message}");
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
