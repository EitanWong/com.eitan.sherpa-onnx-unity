using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

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

            private static readonly string[] COMPRESSED_EXTENSIONS = {
            ".zip", ".tar", ".tar.gz", ".tar.bz2", ".rar", ".7z",
            ".gz", ".bz2", ".xz", ".lz4", ".tgz", ".tbz2", ".zst"
        };
            #endregion


            #region Public Methods


            //TODO: 重构PrepareModelAsync方法，要求按照步骤准备模型
            // 1.检测模型的文件夹和需要的文件是否都以及存在，并且对文件进行校验, 如果所有校验通过则表示模型以及准备则直接返回true, 如果有文件不存在或者校验失败则进入步骤2
            // 2.检查模型的压缩包是否存在，如果存在对其进行校验，校验通过后，解压这个文件夹，然后再执行步骤1， 如果压缩包不存在或者校验不通过(哈希值不匹配或文件不存在),则进入步骤3
            // 3.下载对应的模型压缩文件，下载完毕后执行步骤2.
            // 1->2->3 为完整的一组，最多重试3组，如果3组都失败了，则直接准备失败，report failed 并且message里提示 要求手动下载解压模型文件到对应的路径中去。(执行清理操作，清理下载或者解压失败等文件以及文件夹)
            // (确保PrepareModelAsync异步方法可以随时被中断）
            // 提升代码可读性，完善注释

            /// <summary>
            /// Verifies existing model files or downloads and extracts the model if needed.
            /// </summary>
            /// <param name="reporter">Callback for progress feedback. Can be null.</param>
            /// <param name="cancellationToken">Token to cancel the operation.</param>
            /// <returns>True if the model was successfully prepared, false otherwise.</returns>
            /// <exception cref="ObjectDisposedException">Thrown when the object has been disposed.</exception>
            /// <exception cref="OperationCanceledException">Thrown when the operation is canceled.</exception>
            /// 
            /// 
            public static async Task<bool> PrepareModelAsync(SherpaOnnxModelMetadata metadata, SherpaOnnxFeedbackReporter reporter, CancellationToken cancellationToken = default)
            {


                var paths = GetModelPaths(metadata);
                try
                {

                    // var metadata = await SherpaOnnxModelRegistry.Instance.GetMetadataAsync(ModelId);
                    reporter?.Report(new PrepareFeedback(metadata, message: $"Preparing {metadata.modelId} model"));
                    if (!ValidateMetadata(metadata, reporter))
                    {
                        return false;
                    }



                    if (!CheckDiskSpace(metadata, paths.ModuleDirectoryPath, reporter, cancellationToken))
                    {
                        reporter?.Report(new FailedFeedback(metadata, message: $"Insufficient disk space for model {metadata.modelId}. Minimum required: {MIN_DISK_SPACE_GB}GB."));
                        return false;
                    }



                    for (int attempt = 0; attempt < MAX_ATTEMPTS; attempt++)
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                        }
                        // 步骤1: 校验查模型文件
                        if (await VerifyExistingModelAsync(metadata, paths, reporter, attempt, cancellationToken))
                        {
                            return true;
                        }
                        // UnityEngine.Debug.Log("Model verify failed");

                        // UnityEngine.Debug.Log("Start download");
                        if (!await DownloadModelAsync(metadata, paths.DownloadFilePath, reporter, attempt, cancellationToken))
                        {
                            UnityEngine.Debug.Log("Download failed");
                            await ApplyExponentialBackoffAsync(attempt, cancellationToken);
                            continue;
                        }

                        // UnityEngine.Debug.Log($"Compressed file:{paths.IsCompressed}");
                        if (paths.IsCompressed)
                        {

                            if (!await ExtractModelAsync(metadata, paths.DownloadFilePath, metadata.downloadFileHash,
                                paths.ModuleDirectoryPath, paths.DownloadFileName, reporter, attempt, cancellationToken))
                            {
                                // UnityEngine.Debug.Log($"Extract model failed");
                                await ApplyExponentialBackoffAsync(attempt, cancellationToken);
                                continue;
                            }
                            
                            // Verify extracted model files
                            if (!await VerifyExistingModelAsync(metadata, paths, reporter, attempt, cancellationToken))
                            {
                                await ApplyExponentialBackoffAsync(attempt, cancellationToken);
                                continue;
                            }
                            
                            // All verification passed, model is ready
                            return true;
                        }
                        else
                        {
                            // For non-compressed files, verify the downloaded file is the correct model
                            if (!await VerifyExistingModelAsync(metadata, paths, reporter, attempt, cancellationToken))
                            {
                                await ApplyExponentialBackoffAsync(attempt, cancellationToken);
                                continue;
                            }
                            
                            // All verification passed, model is ready
                            return true;
                        }
                    }

                    await CleanPathAsync(metadata, new[] { paths.ModelDirectoryPath, paths.DownloadFilePath }, reporter, cancellationToken);

                    return false;
                }
                catch (OperationCanceledException)
                {
                    reporter?.Report(new CancelFeedback(metadata, message: "PrepareModel Canceled"));
                    throw;
                }
                catch (Exception ex)
                {
                    // _logger.LogError($"Failed to prepare model {ModelId}: {ex.Message}");

                    reporter?.Report(new FailedFeedback(metadata, message: ex.Message, exception: ex));
                    await CleanPathAsync(metadata, new[] { paths.ModelDirectoryPath, paths.DownloadFilePath }, reporter, cancellationToken);
                    throw;

                }
                
            }
            #endregion

            #region Private Methods

            private static bool ValidateMetadata(SherpaOnnxModelMetadata metadata, SherpaOnnxFeedbackReporter reporter)
            {
                if (metadata == null)
                {
                    reporter?.Report(new FailedFeedback(metadata, message: "no model to verify, please provide the metadata."));
                    return false;
                }
                if (metadata?.modelFileNames == null || metadata.modelFileNames.Length == 0)
                {
                    reporter?.Report(new FailedFeedback(metadata, message: $"{metadata.modelId}: No model files to verify, please check configuration."));
                    return false;
                }

                return true;
            }

            private static (string ModuleDirectoryPath, string ModelDirectoryPath, string DownloadFilePath, string DownloadFileName, bool IsCompressed) GetModelPaths(SherpaOnnxModelMetadata metadata)
            {
                var moduleDirectoryPath = SherpaPathResolver.GetModuleRootPath(metadata.moduleType);
                var modelDirectoryPath = Path.Combine(moduleDirectoryPath, metadata.modelId);

                var effectiveUrl = metadata.downloadUrl;
                if (SherpaOnnxEnvironment.Contains(SherpaOnnxEnvironment.BuiltinKeys.GithubProxy))
                {
                    var proxy = SherpaOnnxEnvironment.Get(SherpaOnnxEnvironment.BuiltinKeys.GithubProxy)?.Trim();
                    if (!string.IsNullOrEmpty(proxy))
                    {
                        // Ensure the proxy ends with a slash before concatenation
                        if (!proxy.EndsWith("/", StringComparison.Ordinal))
                        {
                            proxy += "/";
                        }

                        // Trim any leading slash on the download path to avoid double slashes
                        effectiveUrl = proxy + metadata.downloadUrl.TrimStart('/');
                    }
                }
                // Validate and create the URI, throwing a clear exception on failure
                if (!Uri.TryCreate(effectiveUrl, UriKind.Absolute, out var downloadUri))
                {
                    throw new UriFormatException($"Invalid download URL: {effectiveUrl}");
                }
                // var downloadUri = new Uri(SherpaOnnxEnvironment.Contains(SherpaOnnxEnvironment.BuiltinKeys.GithubProxy)? $"${SherpaOnnxEnvironment.Get(SherpaOnnxEnvironment.BuiltinKeys.GithubProxy)}{metadata.downloadUrl}" : metadata.downloadUrl);
                var downloadFileName = Path.GetFileName(downloadUri.LocalPath);
                var isCompressFile = IsCompressedFile(downloadFileName);
                var downloadFilePath = Path.Combine(isCompressFile ? moduleDirectoryPath : modelDirectoryPath, downloadFileName);

                // Sanitize paths against directory traversal
                downloadFilePath = SanitizePath(downloadFilePath);
                modelDirectoryPath = SanitizePath(modelDirectoryPath);

                return (moduleDirectoryPath, modelDirectoryPath, downloadFilePath, downloadFileName, isCompressFile);
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
                        reporter?.Report(new VerifyFeedback(metadata, message: "Cannot write to storage, insufficient space or permissions", filePath: directoryPath));
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
                        reporter?.Report(new VerifyFeedback(metadata, message: $"Insufficient disk space: {availableSpaceMB}MB available, {requiredSpaceMB}MB required", filePath: directoryPath));
                        return false;
                    }

                    return true;
#endif
                }
                catch (Exception ex)
                {
                    // On any error, log it but assume sufficient space to avoid blocking legitimate operations
                    reporter?.Report(new VerifyFeedback(metadata, message: $"Could not check disk space: {ex.Message}. Proceeding with operation.", filePath: directoryPath));
                    return true;
                }
            }
            
            
            private static async Task<bool> VerifyExistingModelAsync(SherpaOnnxModelMetadata metadata,
                (string ModuleDirectoryPath, string ModelDirectoryPath, string DownloadFilePath, string DownloadFileName, bool IsCompressed) paths,
                SherpaOnnxFeedbackReporter reporter, int attempt, CancellationToken cancellationToken)
            {
                reporter?.Report(new VerifyFeedback(metadata, message: $"Validating model {metadata.modelId} (attempt {attempt + 1}/{MAX_ATTEMPTS})", filePath: paths.ModelDirectoryPath, progress: 0));

                if (!Directory.Exists(paths.ModelDirectoryPath))
                {
                    reporter?.Report(new VerifyFeedback(metadata, message: $"Model directory does not exist (attempt {attempt + 1}/{MAX_ATTEMPTS}): {paths.ModelDirectoryPath}", filePath: paths.ModelDirectoryPath, progress: 0));
                    return false;
                }

                try
                {

                    var verificationTasks = new List<Task<(int Index, FileVerificationEventArgs Result)>>();

                    for (int i = 0; i < metadata.modelFileNames.Length; i++)
                    {
                        var index = i;
                        var relativeFilePath = metadata.modelFileNames[index];
                        var fullFilePath = Path.Combine(paths.ModelDirectoryPath, relativeFilePath);
                        var expectedSha256 = (metadata.modelFileHashes != null && index < metadata.modelFileHashes.Length)
                            ? metadata.modelFileHashes[index]
                            : null;

                        var task = VerifyFileWithIndexAsync(metadata, index, fullFilePath, expectedSha256, reporter, cancellationToken);
                        verificationTasks.Add(task);
                    }

                    var verificationResults = await Task.WhenAll(verificationTasks);

                    foreach (var (index, result) in verificationResults)
                    {
                        if (result.Status != FileVerificationStatus.Success)
                        {
                            var modelDirectoryPath = paths.ModelDirectoryPath;
                            // if verify failed, delete the modelDirectory, and keep going net progress to redownload and extract.
                            if (SherpaFileUtils.PathExists(modelDirectoryPath))
                            {
                                SherpaFileUtils.Delete(modelDirectoryPath);
                            }

                            return false;
                        }
                    }

                    // Final completion report
                    reporter?.Report(new VerifyFeedback(metadata, message: "All files verified successfully", filePath: paths.ModelDirectoryPath, progress: 100));

                    if (paths.IsCompressed && SherpaFileUtils.PathExists(paths.DownloadFilePath))
                    {
                        reporter?.Report(new CleanFeedback(metadata, filePath: paths.DownloadFilePath, message: $"Cleaning up {paths.DownloadFilePath}"));
                        SherpaFileUtils.Delete(paths.DownloadFilePath);
                    }

                    return true;
                }
                catch (OperationCanceledException)
                {
                    reporter?.Report(new CancelFeedback(metadata, message: $"Verification Canceled"));
                    throw;
                }
                catch (Exception ex)
                {
                    // _logger.LogError($"Error during model verification: {ex.Message}");
                    reporter?.Report(new FailedFeedback(metadata, message: ex.Message, exception: ex));
                    return false;
                }
            }

            // TODO: 重构VerifyFileWithIndexAsync 使其作为SherpaOnnxModel层的通用文件验证方法，可以批量传入filePaths 以及expiatedSha256Array,进行批量验证，等待全部验证完毕后再返回结果。
            private static async Task<(int Index, FileVerificationEventArgs Result)> VerifyFileWithIndexAsync(SherpaOnnxModelMetadata metadata,
                int index, string filePath, string expectedSha256, SherpaOnnxFeedbackReporter reporter, CancellationToken cancellationToken)
            {
                Progress<FileVerificationEventArgs> progressAdapter = new Progress<FileVerificationEventArgs>(args =>
                {
                    reporter?.Report(new VerifyFeedback(metadata, message: args.Message, filePath: filePath, progress: args.Progress));
                });

                var result = await SherpaFileUtils.VerifyFileAsync(filePath, expectedSha256, progress: progressAdapter, cancellationToken: cancellationToken);

                reporter?.Report(new VerifyFeedback(metadata, message: result.Message, filePath: filePath, progress: result.Progress));
                return (index, result);
            }

            private static async Task<bool> DownloadModelAsync(SherpaOnnxModelMetadata metadata, string downloadFilePath,
                SherpaOnnxFeedbackReporter reporter, int retryCount, CancellationToken cancellationToken)
            {
                try
                {
                    // Check if the file is already downloaded with hash verification
                    var (index, downloadedFileCheckResult) = await VerifyFileWithIndexAsync(metadata, 0, downloadFilePath, metadata.downloadFileHash, reporter, cancellationToken);
                    if (downloadedFileCheckResult.Status == FileVerificationStatus.Success)
                    {

                        return true;
                    }

                    using (var downloader = new SherpaFileDownloader(metadata))
                    {
                        if (reporter != null)
                        {

                            downloader.Feedback += reporter.Report;
                        }

                        var downloadSuccess = await downloader.DownloadAsync(metadata.downloadUrl, downloadFilePath, cancellationToken: cancellationToken);
                        if (!downloadSuccess)
                        {
                            SherpaFileUtils.Delete(downloadFilePath);
                            throw new Exception($"Failed download {metadata.downloadUrl} to {downloadFilePath}");
                        }


                        return downloadSuccess;
                    }
                }
                catch (OperationCanceledException ex)
                {
                    reporter?.Report(new CancelFeedback(metadata, message: ex.Message, exception: ex));
                    throw;
                }
                catch (Exception ex)
                {
                    reporter?.Report(new FailedFeedback(metadata, message: ex.Message, exception: ex));
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
                    var (index, zipVerifyResult) = await VerifyFileWithIndexAsync(metadata, 0, zipFilePath, zipFileHash, reporter, cancellationToken);

                    if (zipVerifyResult.Status != FileVerificationStatus.Success)
                    {
                        return false;
                    }

                    // UnityEngine.Debug.Log($"zip VerifyResult {zipVerifyResult.Status} : {zipVerifyResult.Message}");
                    var progressAdapter = new Progress<DecompressionEventArgs>(args =>
                    {
                        reporter?.Report(new UncompressFeedback(metadata, filePath: zipFilePath, progress: args.Progress, message: $"Extracting {zipFileName} ({args.Progress * 100:F1}%) Duration: [{args.ElapsedTime}]"));
                    });

                    var result = await SherpaUncompressHelper.DecompressAsync(zipFilePath, moduleDirectoryPath, progressAdapter, cancellationToken: cancellationToken);

                    if (result.Success)
                    {
                        reporter?.Report(new UncompressFeedback(metadata, filePath: zipFilePath, progress: result.Progress, message: $"Extract Success: {zipFileName} Duration: [{result.ElapsedTime}]"));
                        return true;
                    }
                    else
                    {
                        throw new InvalidOperationException(result.ErrorMessage);
                    }
                }
                catch (OperationCanceledException)
                {
                    reporter?.Report(new CancelFeedback(metadata, message: $"Extract: {zipFileHash} Canceled"));
                    throw;
                }
                catch (Exception ex)
                {
                    // _logger.LogError($"Extraction failed: {ex.Message}");
                    reporter?.Report(new FailedFeedback(metadata, message: ex.Message, exception: ex));
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

                            reporter?.Report(new CleanFeedback(metadata, filePath: path, message: $"Cleaning up: {path}"));

                            try
                            {
                                SherpaFileUtils.Delete(path);
                            }
                            catch (Exception ex)
                            {
                                // _logger.LogWarning($"Failed to delete path {path}: {ex.Message}");
                                reporter.Report(new FailedFeedback(metadata, message: ex.Message, exception: ex));
                                throw;
                            }
                        }, cancellationToken));

                    await Task.WhenAll(deletionTasks);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    // _logger.LogError($"Error during cleanup: {ex.Message}");
                    reporter.Report(new FailedFeedback(metadata, message: ex.Message, exception: ex));
                    throw;
                }
            }

            private static async Task ApplyExponentialBackoffAsync(int attempt, CancellationToken cancellationToken)
            {
                if (attempt >= MAX_ATTEMPTS - 1)
                { return; }

                var delay = Math.Min(
                    INITIAL_RETRY_DELAY_MS * Math.Pow(RETRY_MULTIPLIER, attempt),
                    MAX_RETRY_DELAY_MS);

                await Task.Delay(TimeSpan.FromMilliseconds(delay), cancellationToken);
            }




            #endregion

        }

    }


}