using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Eitan.SherpaONNXUnity.Runtime.Utilities
{
    public partial class SherpaUtils
    {
        public partial class Prepare
        {
            private static async Task<bool> VerifyExistingModelAsync(
                SherpaONNXModelMetadata metadata,
                ModelPaths paths,
                SherpaONNXFeedbackReporter reporter,
                int attempt,
                CancellationToken cancellationToken)
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
                    bool hasSignature = false;

                    foreach (var ext in MODEL_SIGNATURE_EXTENSIONS)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

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
                        ReportSafe(reporter, new FailedFeedback(metadata, "Model verification failed.", errorCode: PrepareErrorCode.VerificationFailed));
                        SherpaLog.Trace($"[Prepare] No signature files found for {metadata.modelId} in {paths.ModelDirectory}", category: "Prepare");
                        return false;
                    }

                    ReportSafe(reporter, new VerifyFeedback(
                        metadata,
                        message: "Model files detected. Verification succeeded.",
                        filePath: paths.ModelDirectory,
                        progress: 1f));

                    if (paths.IsCompressed && SherpaFileUtils.PathExists(paths.DownloadFilePath))
                    {
                        ReportSafe(reporter, new CleanFeedback(metadata, filePath: paths.DownloadFilePath, message: $"Cleaning up {paths.DownloadFilePath}"));
                        SherpaFileUtils.Delete(paths.DownloadFilePath);
                    }

                    await Task.Yield();
                    return true;
                }
                catch (OperationCanceledException)
                {
                    ReportSafe(reporter, new CancelFeedback(metadata, message: "Verification canceled"));
                    throw;
                }
                catch (Exception ex)
                {
                    ReportSafe(reporter, new FailedFeedback(metadata, message: ex.Message, exception: ex, errorCode: PrepareErrorCode.VerificationFailed));
                    return false;
                }
            }

            private static async Task<(int Index, FileVerificationEventArgs Result)> VerifyFileWithIndexAsync(
                SherpaONNXModelMetadata metadata,
                int index,
                string filePath,
                string expectedSha256,
                SherpaONNXFeedbackReporter reporter,
                CancellationToken cancellationToken)
            {
                Progress<FileVerificationEventArgs> progressAdapter = new Progress<FileVerificationEventArgs>(args =>
                {
                    ReportSafe(reporter, new VerifyFeedback(metadata, message: args.Message, filePath: filePath, progress: args.Progress));
                });

                var result = await SherpaFileUtils.VerifyFileAsync(filePath, expectedSha256, progress: progressAdapter, cancellationToken: cancellationToken).ConfigureAwait(false);

                ReportSafe(reporter, new VerifyFeedback(metadata, message: result.Message, filePath: filePath, progress: result.Progress));
                return (index, result);
            }

            private static async Task<PrepareErrorCode> DownloadModelAsync(
                SherpaONNXModelMetadata metadata,
                string downloadFilePath,
                SherpaONNXFeedbackReporter reporter,
                int retryCount,
                CancellationToken cancellationToken)
            {
                CancellationTokenSource attemptTimeoutCts = null;
                var effectiveCancellationToken = cancellationToken;
                try
                {
                    if (!IsAutoDownloadEnabled())
                    {
                        var directory = Path.GetDirectoryName(downloadFilePath) ?? downloadFilePath;
                        ReportAutoDownloadDisabled(metadata, reporter, directory);
                        return PrepareErrorCode.AutoDownloadDisabled;
                    }

                    var (_, downloadedFileCheckResult) = await VerifyFileWithIndexAsync(metadata, 0, downloadFilePath, metadata.downloadFileHash, reporter, cancellationToken).ConfigureAwait(false);
                    if (downloadedFileCheckResult.Status == FileVerificationStatus.Success)
                    {
                        SherpaLog.Info($"[Prepare] Reusing previously downloaded archive for {metadata.modelId}.", category: "Prepare");
                        return PrepareErrorCode.None;
                    }

                    if (!TryResolveDownloadUri(metadata, reporter, out var downloadUri, out var resolveError))
                    {
                        return resolveError == PrepareErrorCode.None
                            ? PrepareErrorCode.DownloadUrlInvalid
                            : resolveError;
                    }

                    var timeoutSeconds = GetDownloadAttemptTimeoutSeconds();
                    if (timeoutSeconds > 0)
                    {
                        attemptTimeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                        attemptTimeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
                        effectiveCancellationToken = attemptTimeoutCts.Token;
                    }

                    using var downloader = new SherpaFileDownloader(metadata);
                    SherpaLog.Verbose($"[Prepare] Downloading '{metadata.modelId}' from {downloadUri} -> {downloadFilePath}", category: "Prepare");

                    if (reporter != null)
                    {
                        downloader.Feedback += reporter.Report;
                    }

                    try
                    {
                        var downloadSuccess = await downloader.DownloadAsync(downloadUri.ToString(), downloadFilePath, cancellationToken: effectiveCancellationToken).ConfigureAwait(false);

                        if (downloader.WasCanceled || effectiveCancellationToken.IsCancellationRequested)
                        {
                            if (IsTimeoutCancellation(attemptTimeoutCts, cancellationToken))
                            {
                                return ReportDownloadTimeout(metadata, reporter, timeoutSeconds);
                            }

                            ReportSafe(reporter, new CancelFeedback(metadata, message: "Download canceled."));
                            return PrepareErrorCode.Cancelled;
                        }

                        if (!downloadSuccess)
                        {
                            var unityError = MapDownloaderFailure(downloader);
                            SherpaLog.Warning($"[{metadata.modelId}] UnityWebRequest download failed. Falling back to HttpClient.");
                            var httpError = await DownloadWithHttpClientAsync(metadata, downloadUri.ToString(), downloadFilePath, reporter, effectiveCancellationToken).ConfigureAwait(false);
                            if (httpError == PrepareErrorCode.None)
                            {
                                return PrepareErrorCode.None;
                            }

                            var finalError = httpError != PrepareErrorCode.DownloadFailed
                                ? httpError
                                : unityError != PrepareErrorCode.None ? unityError : PrepareErrorCode.DownloadFailed;

                            SherpaFileUtils.Delete(downloadFilePath);
                            ReportSafe(reporter, new FailedFeedback(metadata, message: $"Failed downloading {downloadUri} to {downloadFilePath}", errorCode: finalError));
                            SherpaLog.Error($"[Prepare] Download failed for {metadata.modelId} from {downloadUri}", category: "Prepare");
                            return finalError;
                        }

                        SherpaLog.Info($"[Prepare] Download complete for {metadata.modelId}. Verifying...", category: "Prepare");
                        return PrepareErrorCode.None;
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
                    if (IsTimeoutCancellation(attemptTimeoutCts, cancellationToken))
                    {
                        var timeoutSeconds = GetDownloadAttemptTimeoutSeconds();
                        return ReportDownloadTimeout(metadata, reporter, timeoutSeconds);
                    }

                    ReportSafe(reporter, new CancelFeedback(metadata, message: ex.Message, exception: ex));
                    throw;
                }
                catch (Exception ex)
                {
                    ReportSafe(reporter, new FailedFeedback(metadata, message: ex.Message, exception: ex, errorCode: PrepareErrorCode.DownloadFailed));
                    SherpaFileUtils.Delete(downloadFilePath);
                    SherpaLog.Exception(ex, category: "Prepare", message: $"[Prepare] Download pipeline crashed for {metadata?.modelId ?? "<unknown>"}.");
                    return PrepareErrorCode.DownloadFailed;
                }
                finally
                {
                    attemptTimeoutCts?.Dispose();
                }
            }

            private static bool IsTimeoutCancellation(CancellationTokenSource timeoutCts, CancellationToken callerCancellationToken)
            {
                return timeoutCts != null &&
                       timeoutCts.IsCancellationRequested &&
                       !callerCancellationToken.IsCancellationRequested;
            }

            private static PrepareErrorCode ReportDownloadTimeout(
                SherpaONNXModelMetadata metadata,
                SherpaONNXFeedbackReporter reporter,
                int timeoutSeconds)
            {
                var key = SherpaONNXEnvironment.BuiltinKeys.DownloadAttemptTimeoutSeconds;
                var message = $"Download timeout after {timeoutSeconds}s for {metadata.modelId}. You can adjust timeout via '{key}'.";
                ReportSafe(reporter, new FailedFeedback(metadata, message: message, errorCode: PrepareErrorCode.DownloadTimeout));
                SherpaLog.Warning($"[Prepare] {message}", category: "Prepare");
                return PrepareErrorCode.DownloadTimeout;
            }

            private static PrepareErrorCode MapDownloaderFailure(SherpaFileDownloader downloader)
            {
                if (downloader == null)
                {
                    return PrepareErrorCode.DownloadFailed;
                }

                if (downloader.LastWasTimeout)
                {
                    return PrepareErrorCode.DownloadTimeout;
                }

                if (downloader.LastResponseCode > 0)
                {
                    return MapHttpStatus((int)downloader.LastResponseCode);
                }

                if (downloader.LastResult == UnityEngine.Networking.UnityWebRequest.Result.ConnectionError)
                {
                    return PrepareErrorCode.DownloadConnectionError;
                }

                if (downloader.LastResult == UnityEngine.Networking.UnityWebRequest.Result.ProtocolError)
                {
                    return PrepareErrorCode.DownloadProtocolError;
                }

                if (downloader.LastResult == UnityEngine.Networking.UnityWebRequest.Result.DataProcessingError)
                {
                    return PrepareErrorCode.DownloadDataProcessingError;
                }

                return PrepareErrorCode.DownloadFailed;
            }

            private static PrepareErrorCode MapHttpStatus(int statusCode)
            {
                if (statusCode == 401)
                {
                    return PrepareErrorCode.DownloadUnauthorized;
                }
                if (statusCode == 403)
                {
                    return PrepareErrorCode.DownloadForbidden;
                }
                if (statusCode == 404)
                {
                    return PrepareErrorCode.DownloadNotFound;
                }
                if (statusCode == 408)
                {
                    return PrepareErrorCode.DownloadTimeout;
                }
                if (statusCode == 429)
                {
                    return PrepareErrorCode.DownloadRateLimited;
                }
                if (statusCode >= 400 && statusCode <= 499)
                {
                    return PrepareErrorCode.DownloadClientError;
                }
                if (statusCode >= 500 && statusCode <= 599)
                {
                    return PrepareErrorCode.DownloadServerError;
                }

                return PrepareErrorCode.DownloadFailed;
            }

            private static async Task<bool> ExtractModelAsync(
                SherpaONNXModelMetadata metadata,
                string zipFilePath,
                string zipFileHash,
                string moduleDirectoryPath,
                string zipFileName,
                SherpaONNXFeedbackReporter reporter,
                int retryCount,
                CancellationToken cancellationToken)
            {
                try
                {
                    var (_, zipVerifyResult) = await VerifyFileWithIndexAsync(metadata, 0, zipFilePath, zipFileHash, reporter, cancellationToken).ConfigureAwait(false);

                    if (zipVerifyResult.Status != FileVerificationStatus.Success)
                    {
                        ReportSafe(reporter, new FailedFeedback(metadata, message: zipVerifyResult.Message, errorCode: PrepareErrorCode.VerificationFailed));
                        SherpaLog.Warning($"[Prepare] Zip verification failed for {metadata.modelId}: {zipVerifyResult.Message}", category: "Prepare");
                        return false;
                    }

                    var progressAdapter = new Progress<DecompressionEventArgs>(args =>
                    {
                        ReportSafe(reporter, new DecompressFeedback(metadata, filePath: zipFilePath, progress: args.Progress, message: $"Extracting {zipFileName} ({args.Progress * 100:F1}%) Duration: [{args.ElapsedTime}]"));
                    });
                    SherpaLog.Trace($"[Prepare] Extracting archive for {metadata.modelId}: {zipFileName}", category: "Prepare");
                    var result = await SherpaDecompressHelper.DecompressAsync(zipFilePath, moduleDirectoryPath, progressAdapter, cancellationToken: cancellationToken).ConfigureAwait(false);

                    if (result.Success)
                    {
                        ReportSafe(reporter, new DecompressFeedback(metadata, filePath: zipFilePath, progress: result.Progress, message: $"Extract Success: {zipFileName} Duration: [{result.ElapsedTime}]"));
                        SherpaLog.Info($"[Prepare] Extracted archive for {metadata.modelId} in {result.ElapsedTime}.", category: "Prepare");
                        return true;
                    }

                    throw new InvalidOperationException(result.ErrorMessage);
                }
                catch (OperationCanceledException)
                {
                    ReportSafe(reporter, new CancelFeedback(metadata, message: $"Extract: {zipFileHash} Canceled"));
                    throw;
                }
                catch (Exception ex)
                {
                    ReportSafe(reporter, new FailedFeedback(metadata, message: ex.Message, exception: ex, errorCode: PrepareErrorCode.ExtractionFailed));
                    SherpaLog.Exception(ex, category: "Prepare", message: $"[Prepare] Extraction failed for {metadata.modelId}.");
                    throw;
                }
            }

            private static async Task<PrepareErrorCode> DownloadWithHttpClientAsync(
                SherpaONNXModelMetadata metadata,
                string url,
                string destinationPath,
                SherpaONNXFeedbackReporter reporter,
                CancellationToken cancellationToken)
            {
                try
                {
                    var tempPath = destinationPath + ".tmp";
                    var directory = Path.GetDirectoryName(destinationPath);
                    if (!string.IsNullOrEmpty(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    using var httpClient = new HttpClient();
                    using var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
                    if (!response.IsSuccessStatusCode)
                    {
                        var statusCode = (int)response.StatusCode;
                        var errorCode = MapHttpStatus(statusCode);
                        ReportSafe(reporter, new FailedFeedback(metadata, message: $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}", errorCode: errorCode));
                        return errorCode;
                    }

                    var total = response.Content.Headers.ContentLength ?? -1;
                    await using var input = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
                    await using var output = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);

                    var buffer = new byte[81920];
                    long written = 0;
                    int read;
                    while ((read = await input.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) > 0)
                    {
                        await output.WriteAsync(buffer, 0, read, cancellationToken).ConfigureAwait(false);
                        written += read;
                        if (total > 0)
                        {
                            ReportSafe(reporter, new DownloadFeedback(metadata, Path.GetFileName(destinationPath), written, total, 0));
                        }
                    }

                    output.Close();

                    if (File.Exists(destinationPath))
                    {
                        File.Delete(destinationPath);
                    }
                    File.Move(tempPath, destinationPath);
                    return PrepareErrorCode.None;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (IOException ex)
                {
                    ReportSafe(reporter, new FailedFeedback(metadata, message: ex.Message, exception: ex, errorCode: PrepareErrorCode.IoError));
                    try { if (File.Exists(destinationPath)) File.Delete(destinationPath); } catch { }
                    return PrepareErrorCode.IoError;
                }
                catch (Exception ex)
                {
                    ReportSafe(reporter, new FailedFeedback(metadata, message: ex.Message, exception: ex, errorCode: PrepareErrorCode.DownloadFailed));
                    try { if (File.Exists(destinationPath)) File.Delete(destinationPath); } catch { }
                    return PrepareErrorCode.DownloadFailed;
                }
            }

            private static async Task CleanPathAsync(
                SherpaONNXModelMetadata metadata,
                string[] filePaths,
                SherpaONNXFeedbackReporter reporter,
                CancellationToken cancellationToken)
            {
                if (filePaths == null || filePaths.Length == 0)
                { return; }

                try
                {
                    var expanded = new System.Collections.Generic.List<string>();
                    foreach (var path in filePaths)
                    {
                        if (string.IsNullOrEmpty(path))
                        { continue; }

                        expanded.Add(path);
                        expanded.Add(path + ".download");
                        expanded.Add(path + ".download.metadata");
                        expanded.Add(path + ".chunks");
                    }

                    var distinctPaths = expanded
                        .Where(path => !string.IsNullOrEmpty(path))
                        .Distinct()
                        .Where(SherpaFileUtils.PathExists)
                        .ToArray();

                    if (distinctPaths.Length == 0)
                    { return; }

                    var deletionTasks = distinctPaths.Select(path =>
                        Task.Run(() =>
                        {
                            cancellationToken.ThrowIfCancellationRequested();

                            ReportSafe(reporter, new CleanFeedback(metadata, filePath: path, message: $"Cleaning up: {path}"));

                            try
                            {
                                SherpaFileUtils.Delete(path);
                                SherpaLog.Trace($"[Prepare] Deleted artifact: {path}", category: "Prepare");
                            }
                            catch (Exception ex)
                            {
                                ReportSafe(reporter, new FailedFeedback(metadata, message: ex.Message, exception: ex, errorCode: PrepareErrorCode.IoError));
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
                    ReportSafe(reporter, new FailedFeedback(metadata, message: ex.Message, exception: ex, errorCode: PrepareErrorCode.IoError));
                    throw;
                }
            }

            private static bool TryResolveDownloadUri(
                SherpaONNXModelMetadata metadata,
                SherpaONNXFeedbackReporter reporter,
                out Uri downloadUri,
                out PrepareErrorCode errorCode)
            {
                downloadUri = null;
                errorCode = PrepareErrorCode.None;

                if (metadata == null)
                {
                    ReportSafe(reporter, new FailedFeedback(metadata, message: "Cannot resolve download URL without metadata.", errorCode: PrepareErrorCode.MetadataMissing));
                    errorCode = PrepareErrorCode.MetadataMissing;
                    return false;
                }

                var rawUrl = metadata.downloadUrl?.Trim();
                if (string.IsNullOrEmpty(rawUrl))
                {
                    ReportSafe(reporter, new FailedFeedback(metadata, message: $"{metadata.modelId}: Download URL is empty.", errorCode: PrepareErrorCode.DownloadUrlMissing));
                    errorCode = PrepareErrorCode.DownloadUrlMissing;
                    return false;
                }

                if (!Uri.TryCreate(rawUrl, UriKind.Absolute, out var resolvedUri))
                {
                    ReportSafe(reporter, new FailedFeedback(metadata, message: $"Invalid download URL: {rawUrl}", errorCode: PrepareErrorCode.DownloadUrlInvalid));
                    errorCode = PrepareErrorCode.DownloadUrlInvalid;
                    return false;
                }

                if (!IsSecureDownloadScheme(resolvedUri))
                {
                    if (!IsInsecureDownloadAllowed())
                    {
                        var allowInsecureKey = SherpaONNXEnvironment.BuiltinKeys.AllowInsecureModelDownload;
                        ReportSafe(reporter, new FailedFeedback(metadata, message: $"Rejected insecure download scheme '{resolvedUri.Scheme}'. Set {allowInsecureKey}=true to override.", errorCode: PrepareErrorCode.DownloadInsecureRejected));
                        errorCode = PrepareErrorCode.DownloadInsecureRejected;
                        return false;
                    }

                    var insecureEnabledKey = SherpaONNXEnvironment.BuiltinKeys.AllowInsecureModelDownload;
                    ReportSafe(reporter, new VerifyFeedback(metadata, message: $"Allowing insecure download for {resolvedUri} because {insecureEnabledKey}=true.", filePath: resolvedUri.ToString()));
                    SherpaLog.Warning($"[{metadata.modelId}] Allowing insecure download for {resolvedUri} (override enabled).");
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
        }
    }
}
