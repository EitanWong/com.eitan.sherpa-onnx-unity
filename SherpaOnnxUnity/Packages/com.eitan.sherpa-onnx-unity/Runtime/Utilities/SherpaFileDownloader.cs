using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Networking;

namespace Eitan.SherpaOnnxUnity.Runtime.Utilities
{

    /// <summary>
    /// Chunk download information for persistence
    /// </summary>
    [Serializable]
    internal class ChunkInfo
    {
        public int Index;
        public long Start;
        public long End;
        public long Downloaded;
        public bool IsCompleted;
        public string ErrorMessage;
        public int RetryCount;
    }

    /// <summary>
    /// Download metadata for persistence
    /// </summary>
    [Serializable]
    internal class DownloadMetadata : ISerializationCallbackReceiver
    {
        public string Url;
        public string FileName;
        public long TotalSize;
        public long ChunkSize;
        public List<ChunkInfo> Chunks = new List<ChunkInfo>();
        // public SherpaDownloadFeedback.DownloadStatus Status;
        public string CreatedTimeString;
        public string LastModifiedTimeString;

        [NonSerialized]
        public DateTime CreatedTime;
        [NonSerialized]
        public DateTime LastModifiedTime;

        public void OnBeforeSerialize()
        {
            CreatedTimeString = CreatedTime.ToString("o");
            LastModifiedTimeString = LastModifiedTime.ToString("o");
        }

        public void OnAfterDeserialize()
        {
            if (!string.IsNullOrEmpty(CreatedTimeString))
            { DateTime.TryParse(CreatedTimeString, null, System.Globalization.DateTimeStyles.RoundtripKind, out CreatedTime); }
            if (!string.IsNullOrEmpty(LastModifiedTimeString))
            { DateTime.TryParse(LastModifiedTimeString, null, System.Globalization.DateTimeStyles.RoundtripKind, out LastModifiedTime); }
        }
    }

    /// <summary>
    /// Memory-safe download handler for chunked downloads
    /// </summary>
    internal class ChunkDownloadHandler : DownloadHandlerScript
    {
        private readonly FileStream _fileStream;
        private readonly long _startPosition;
        private readonly long _endPosition;
        private readonly ChunkInfo _chunkInfo;
        private readonly Action<long> _onProgressUpdate;
        private readonly object _fileLock;

        private long _receivedBytes;
        private readonly byte[] _buffer;
        private const int BufferSize = 64 * 1024; // 64KB buffer to minimize GC

        public ChunkDownloadHandler(FileStream fileStream, ChunkInfo chunkInfo, Action<long> onProgressUpdate, object fileLock)
            : base(new byte[BufferSize])
        {
            _fileStream = fileStream;
            _chunkInfo = chunkInfo;
            _startPosition = chunkInfo.Start + chunkInfo.Downloaded;
            _endPosition = chunkInfo.End;
            _onProgressUpdate = onProgressUpdate;
            _fileLock = fileLock;
            _buffer = new byte[BufferSize];
        }

        protected override bool ReceiveData(byte[] data, int dataLength)
        {
            if (data == null || dataLength <= 0)
            { return false; }

            try
            {
                // Write to file at correct position
                lock (_fileLock)
                {
                    _fileStream.Seek(_startPosition + _receivedBytes, SeekOrigin.Begin);
                    _fileStream.Write(data, 0, dataLength);
                    _fileStream.Flush();
                }

                _receivedBytes += dataLength;
                _chunkInfo.Downloaded += dataLength;
                _onProgressUpdate?.Invoke(dataLength);

                // Check if chunk is completed
                if (_startPosition + _receivedBytes > _endPosition)
                {
                    _chunkInfo.IsCompleted = true;
                    return false; // Stop receiving data
                }

                return true;
            }
            catch (Exception ex)
            {
                _chunkInfo.ErrorMessage = ex.Message;
                return false;
            }
        }

        protected override void CompleteContent()
        {
            _chunkInfo.IsCompleted = _receivedBytes >= (_endPosition - _startPosition + 1);
            base.CompleteContent();
        }

        protected override byte[] GetData() => null;
        protected override string GetText() => null;
        protected override float GetProgress() =>
            _endPosition > _startPosition ? (float)_receivedBytes / (_endPosition - _startPosition + 1) : 0f;
    }

    /// <summary>
    /// High-performance chunked file downloader with breakpoint resumption
    /// </summary>
    internal class SherpaFileDownloader : IDisposable
    {
        private readonly SemaphoreSlim _concurrencyLimiter;
        private readonly ConcurrentDictionary<int, UnityWebRequest> _activeRequests;
        private readonly object _progressLock = new object();
        private readonly object _fileLock = new object();
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly Stopwatch _stopwatch;

        // Configuration
        private readonly int _maxConcurrentChunks;
        private readonly long _defaultChunkSize;
        private readonly int _maxRetryAttempts;
        private readonly TimeSpan _retryDelay;
        private readonly int _timeoutSeconds;

        // State
        private DownloadMetadata _metadata;
        private FileStream _fileStream;
        private string _tempFilePath;
        private string _metadataFilePath;
        private volatile bool _isDisposed;
        private long _totalDownloadedBytes;
        private DateTime _lastProgressUpdate;
        private long _lastDownloadedBytes;
        private double _currentSpeed;

        private SherpaOnnxModelMetadata _modelMetadata;

        #region Constants
        private const string MetadataFileExtension = ".download.metadata";
        private const string DownloadTempFileExtension = ".download";
        private const string UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36";
        #endregion

        // Events
        public event Action<IFeedback> Feedback;

        public SherpaFileDownloader(
            SherpaOnnxModelMetadata metadata = null,
            int maxConcurrentChunks = 4,
            long chunkSizeMB = 10,
            int maxRetryAttempts = 3,
            int timeoutSeconds = 30)
        {
            _modelMetadata = metadata;
            _maxConcurrentChunks = Math.Max(1, Math.Min(maxConcurrentChunks, 8)); // Limit to reasonable range
            _defaultChunkSize = Math.Max(1024 * 1024, chunkSizeMB * 1024 * 1024); // Min 1MB
            _maxRetryAttempts = maxRetryAttempts;
            _retryDelay = TimeSpan.FromSeconds(2);
            _timeoutSeconds = timeoutSeconds;

            _concurrencyLimiter = new SemaphoreSlim(_maxConcurrentChunks, _maxConcurrentChunks);
            _activeRequests = new ConcurrentDictionary<int, UnityWebRequest>();
            _cancellationTokenSource = new CancellationTokenSource();
            _stopwatch = new Stopwatch();
        }

        /// <summary>
        /// Download file with chunked approach and breakpoint resumption
        /// </summary>
        public async Task<bool> DownloadAsync(string url, string filePath, CancellationToken cancellationToken = default)
        {
            if (_isDisposed) { throw new ObjectDisposedException(nameof(SherpaFileDownloader)); }

            try
            {
                var linkedToken = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken, _cancellationTokenSource.Token).Token;

                await InitializeDownloadAsync(url, filePath, linkedToken);

                // Check if download is already completed
                if (IsDownloadCompleted())
                {
                    await FinalizeDownloadAsync();
                    return true;
                }

                _stopwatch.Start();
                ReportProgress();

                // Start concurrent chunk downloads
                var downloadTasks = new List<Task>();

                for (int i = 0; i < _metadata.Chunks.Count; i++)
                {
                    var chunk = _metadata.Chunks[i];
                    if (!chunk.IsCompleted)
                    {
                        var task = DownloadChunkAsync(chunk, linkedToken);
                        downloadTasks.Add(task);
                    }
                }

                // Wait for all chunks to complete
                await Task.WhenAll(downloadTasks);

                // Verify download completion
                if (IsDownloadCompleted())
                {
                    await FinalizeDownloadAsync();
                    ReportProgress();
                    return true;
                }
                else
                {
                    ReportProgress("Download incomplete");
                    return false;
                }
            }
            catch (OperationCanceledException)
            {
                ReportProgress();
                return false;
            }
            catch (Exception ex)
            {
                ReportProgress(ex.Message);
                return false;
            }
            finally
            {
                _stopwatch.Stop();
                CleanupActiveRequests();
            }
        }

        /// <summary>
        /// Initialize download metadata and file streams
        /// </summary>
        private async Task InitializeDownloadAsync(string url, string filePath, CancellationToken cancellationToken)
        {

            _tempFilePath = filePath + DownloadTempFileExtension;
            _metadataFilePath = filePath + MetadataFileExtension;
            ReportProgress();


            // Try to resume from existing metadata
            if (File.Exists(_metadataFilePath))
            {
                try
                {
                    await LoadMetadataAsync();
                    if (_metadata.Url == url)
                    {
                        // Resume existing download
                        await OpenFileStreamAsync();
                        CalculateDownloadedBytes();
                        ReportProgress();
                        return;
                    }
                }
                // 捕获特定的文件I/O异常 (Sharing Violation会在此被捕获)
                catch (System.IO.IOException ioEx)
                {
                    UnityEngine.Debug.LogWarning($"Could not access temp file to resume, likely due to a file lock. Error: {ioEx.Message}. Starting fresh download.");
                }
                // 捕获其他所有未预料到的异常
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogWarning($"An unexpected error occurred while trying to resume download: {ex.Message}. Starting fresh download.");
                }
            }

            // Start new download
            await InitializeNewDownloadAsync(url, filePath, cancellationToken);
        }

        /// <summary>
        /// Initialize new download
        /// </summary>
        private async Task InitializeNewDownloadAsync(string url, string filePath, CancellationToken cancellationToken)
        {

            // Get file size and check if server supports range requests
            var (fileSize, supportsRangeRequests) = await GetFileInfoAsync(url, cancellationToken);

            if (!supportsRangeRequests)
            {
                // Fall back to single-threaded download
                // _maxConcurrentChunks = 1;
                UnityEngine.Debug.LogWarning("Server does not support range requests. Using single-threaded download.");
            }

            // Calculate optimal chunk size
            var chunkSize = supportsRangeRequests ?
                Math.Min(_defaultChunkSize, Math.Max(1024 * 1024, fileSize / _maxConcurrentChunks)) :
                fileSize;

            var chunks = new List<ChunkInfo>();
            long currentPosition = 0;
            int chunkIndex = 0;

            while (currentPosition < fileSize)
            {
                var chunkEnd = Math.Min(currentPosition + chunkSize - 1, fileSize - 1);
                chunks.Add(new ChunkInfo
                {
                    Index = chunkIndex++,
                    Start = currentPosition,
                    End = chunkEnd,
                    Downloaded = 0,
                    IsCompleted = false
                });
                currentPosition = chunkEnd + 1;
            }

            // Create metadata
            _metadata = new DownloadMetadata
            {
                Url = url,
                FileName = Path.GetFileName(filePath),
                TotalSize = fileSize,
                ChunkSize = chunkSize,
                CreatedTime = DateTime.Now,
                LastModifiedTime = DateTime.Now,
                Chunks = chunks,
            };

            // Create temp file and save metadata
            await CreateTempFileAsync(fileSize);
            await SaveMetadataAsync();
            await OpenFileStreamAsync();
        }

        /// <summary>
        /// Get file information from server
        /// </summary>
        private async Task<(long fileSize, bool supportsRangeRequests)> GetFileInfoAsync(string url, CancellationToken cancellationToken)
        {
            // Try HEAD request first
            using var headRequest = UnityWebRequest.Head(url);
            headRequest.timeout = _timeoutSeconds;
            headRequest.SetRequestHeader("User-Agent", UserAgent);

            var headOperation = headRequest.SendWebRequest();

            while (!headOperation.isDone)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Delay(50, cancellationToken);
            }

            long fileSize = 0;
            bool supportsRangeRequests = false;

            if (headRequest.result == UnityWebRequest.Result.Success)
            {
                var contentLengthHeader = headRequest.GetResponseHeader("Content-Length");
                if (long.TryParse(contentLengthHeader, out fileSize))
                {
                    var acceptRangesHeader = headRequest.GetResponseHeader("Accept-Ranges");
                    supportsRangeRequests = !string.IsNullOrEmpty(acceptRangesHeader) && acceptRangesHeader.Contains("bytes");
                }
            }

            // If HEAD request failed, try range request
            if (fileSize == 0)
            {
                using var rangeRequest = UnityWebRequest.Get(url);
                rangeRequest.timeout = _timeoutSeconds;
                rangeRequest.SetRequestHeader("User-Agent", UserAgent);
                rangeRequest.SetRequestHeader("Range", "bytes=0-1023");

                var rangeOperation = rangeRequest.SendWebRequest();

                while (!rangeOperation.isDone)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await Task.Delay(50, cancellationToken);
                }

                if (rangeRequest.result == UnityWebRequest.Result.Success &&
                    rangeRequest.responseCode == 206)
                {
                    var contentRangeHeader = rangeRequest.GetResponseHeader("Content-Range");
                    if (!string.IsNullOrEmpty(contentRangeHeader))
                    {
                        var parts = contentRangeHeader.Split('/');
                        if (parts.Length == 2 && long.TryParse(parts[1], out fileSize))
                        {
                            supportsRangeRequests = true;
                        }
                    }
                }
            }

            if (fileSize == 0)
            {
                throw new InvalidOperationException("Unable to determine file size");
            }

            return (fileSize, supportsRangeRequests);
        }

        /// <summary>
        /// Create temporary file with specified size
        /// </summary>
        private async Task CreateTempFileAsync(long fileSize)
        {
            var directory = Path.GetDirectoryName(_tempFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using var fs = new FileStream(_tempFilePath, FileMode.Create, FileAccess.Write, FileShare.None);
            fs.SetLength(fileSize);
            await fs.FlushAsync();
        }

        /// <summary>
        /// Open file stream for writing
        /// </summary>
        private async Task OpenFileStreamAsync()
        {
            const int maxRetries = 3;
            const int delayMs = 100;

            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    _fileStream = new FileStream(_tempFilePath, FileMode.Open, FileAccess.Write, FileShare.ReadWrite);
                    await Task.CompletedTask;
                    return; // Success
                }
                catch (IOException)
                {
                    if (i == maxRetries - 1)
                    {
                        throw; // Last attempt failed, re-throw the exception
                    }
                    await Task.Delay(delayMs * (i + 1)); // Wait a bit longer each time
                }
            }
        }

        /// <summary>
        /// Download individual chunk
        /// </summary>
        private async Task DownloadChunkAsync(ChunkInfo chunk, CancellationToken cancellationToken)
        {
            await _concurrencyLimiter.WaitAsync(cancellationToken);

            try
            {
                for (int retry = 0; retry < _maxRetryAttempts; retry++)
                {
                    try
                    {
                        await DownloadChunkWithRetryAsync(chunk, cancellationToken);
                        break;
                    }
                    catch (Exception ex) when (retry < _maxRetryAttempts - 1)
                    {
                        chunk.ErrorMessage = ex.Message;
                        chunk.RetryCount = retry + 1;
                        await Task.Delay(_retryDelay, cancellationToken);
                    }
                }

                if (!chunk.IsCompleted)
                {
                    throw new InvalidOperationException($"Chunk {chunk.Index} failed after {_maxRetryAttempts} attempts");
                }
            }
            finally
            {
                _concurrencyLimiter.Release();
            }
        }

        /// <summary>
        /// Download chunk with retry logic
        /// </summary>
        private async Task DownloadChunkWithRetryAsync(ChunkInfo chunk, CancellationToken cancellationToken)
        {
            var currentStart = chunk.Start + chunk.Downloaded;
            var currentEnd = chunk.End;

            if (currentStart > currentEnd)
            {
                chunk.IsCompleted = true;
                return;
            }

            using var request = UnityWebRequest.Get(_metadata.Url);
            request.timeout = _timeoutSeconds;
            request.SetRequestHeader("User-Agent", UserAgent);
            request.SetRequestHeader("Range", $"bytes={currentStart}-{currentEnd}");

            // Use custom download handler
            var downloadHandler = new ChunkDownloadHandler(_fileStream, chunk, OnChunkProgress, _fileLock);
            request.downloadHandler = downloadHandler;

            _activeRequests.TryAdd(chunk.Index, request);

            try
            {
                var operation = request.SendWebRequest();

                while (!operation.isDone)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await Task.Delay(50, cancellationToken);
                }

                if (request.result == UnityWebRequest.Result.Success)
                {
                    if (request.responseCode == 206 || request.responseCode == 200)
                    {
                        chunk.IsCompleted = true;
                    }
                    else
                    {
                        throw new InvalidOperationException($"Unexpected response code: {request.responseCode}");
                    }
                }
                else if (request.responseCode == 416)
                {
                    // Range not satisfiable - chunk might already be completed
                    chunk.IsCompleted = true;
                }
                else
                {
                    throw new InvalidOperationException($"Download failed: {request.error}");
                }
            }
            finally
            {
                _activeRequests.TryRemove(chunk.Index, out _);
                downloadHandler?.Dispose();
            }

            if (chunk.IsCompleted)
            {
                await SaveMetadataAsync();
            }
        }

        /// <summary>
        /// Handle chunk progress updates
        /// </summary>
        private void OnChunkProgress(long bytesReceived)
        {
            Interlocked.Add(ref _totalDownloadedBytes, bytesReceived);

            if (DateTime.Now - _lastProgressUpdate > TimeSpan.FromMilliseconds(500))
            {
                UpdateProgress();
            }
        }

        /// <summary>
        /// Update download progress
        /// </summary>
        private void UpdateProgress()
        {
            lock (_progressLock)
            {
                var now = DateTime.Now;
                var elapsed = now - _lastProgressUpdate;

                if (elapsed.TotalMilliseconds > 0)
                {
                    var bytesDiff = _totalDownloadedBytes - _lastDownloadedBytes;
                    _currentSpeed = bytesDiff / elapsed.TotalSeconds;

                    _lastProgressUpdate = now;
                    _lastDownloadedBytes = _totalDownloadedBytes;
                }

                ReportProgress();
            }
        }

        /// <summary>
        /// 向回调函数报告进度 (已修改)
        /// Report progress to the callback (Refactored)
        /// </summary>
        /// <param name="errorMessage">如果发生错误，则提供错误信息</param>
        private void ReportProgress(string errorMessage = null)
        {
            // 1. 首先处理失败状态
            // 如果存在错误信息，意味着任务失败，应发送 FailedFeedback 并立即返回。
            if (!string.IsNullOrEmpty(errorMessage))
            {
                Feedback?.Invoke(new FailedFeedback(_modelMetadata, message: errorMessage));
                return;
            }

            // 确保元数据已加载，避免除零错误
            if (_metadata == null || _metadata.TotalSize <= 0)
            {
                // 在下载真正开始前可以不发送任何消息，或发送一个“准备中”的消息
                return;
            }

            // 2. 创建新的、具体的 DownloadFeedback 对象
            var downloadFeedback = new DownloadFeedback(_modelMetadata, filePath: _metadata.FileName, downloadedBytes: _totalDownloadedBytes, totalBytes: _metadata.TotalSize, speedBytesPerSecond: _currentSpeed);

            // 3. 通过通用的回调函数发送反馈对象
            Feedback?.Invoke(downloadFeedback);

        }

        /// <summary>
        /// Check if download is completed
        /// </summary>
        private bool IsDownloadCompleted()
        {
            if (_metadata == null) { return false; }
            return _metadata.Chunks.All(c => c.IsCompleted) && _totalDownloadedBytes >= _metadata.TotalSize;
        }

        /// <summary>
        /// Calculate total downloaded bytes from chunks
        /// </summary>
        private void CalculateDownloadedBytes()
        {
            _totalDownloadedBytes = _metadata.Chunks.Sum(c => c.Downloaded);
        }

        /// <summary>
        /// Finalize download by renaming temp file
        /// </summary>
        private async Task FinalizeDownloadAsync()
        {
            _fileStream?.Dispose();
            _fileStream = null;

            var finalPath = _tempFilePath.Replace(DownloadTempFileExtension, "");

            // Verify file size
            var fileInfo = new FileInfo(_tempFilePath);
            if (fileInfo.Length != _metadata.TotalSize)
            {
                throw new InvalidOperationException($"File size mismatch. Expected: {_metadata.TotalSize}, Actual: {fileInfo.Length}");
            }

            // Move temp file to final location
            if (File.Exists(finalPath))
            {
                File.Delete(finalPath);
            }

            File.Move(_tempFilePath, finalPath);

            // Clean up metadata file
            if (File.Exists(_metadataFilePath))
            {
                File.Delete(_metadataFilePath);
            }

            await Task.CompletedTask;
        }

        /// <summary>
        /// Save metadata to file
        /// </summary>
        private async Task SaveMetadataAsync()
        {
            if (_metadata == null) { return; }

            _metadata.LastModifiedTime = DateTime.Now;
            var json = JsonUtility.ToJson(_metadata, true);
            await File.WriteAllTextAsync(_metadataFilePath, json);
        }

        /// <summary>
        /// Load metadata from file
        /// </summary>
        private async Task LoadMetadataAsync()
        {
            var json = await File.ReadAllTextAsync(_metadataFilePath);
            _metadata = JsonUtility.FromJson<DownloadMetadata>(json);
        }

        /// <summary>
        /// Clean up active requests
        /// </summary>
        private void CleanupActiveRequests()
        {
            foreach (var kvp in _activeRequests)
            {
                try
                {
                    kvp.Value?.Dispose();
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
            _activeRequests.Clear();
        }

        /// <summary>
        /// Cancel current download
        /// </summary>
        public void Cancel()
        {
            _cancellationTokenSource?.Cancel();
        }

        public void Dispose()
        {
            if (_isDisposed) { return; }

            _isDisposed = true;
            _cancellationTokenSource?.Cancel();
            _fileStream?.Dispose();
            _concurrencyLimiter?.Dispose();
            _cancellationTokenSource?.Dispose();
            CleanupActiveRequests();
        }
    }
}

// Example usage:
/*
public class ExampleDownloader
{
    private SherpaFileDownloader downloader;
    
    public async Task StartDownload()
    {
        downloader = new SherpaFileDownloader(
            maxConcurrentChunks: 4,
            chunkSizeMB: 10,
            maxRetryAttempts: 3,
            timeoutSeconds: 30
        );
        
        downloader.DownloadFeedback += OnDownloadProgress;
        
        string url = "https://example.com/largefile.zip";
        string path = Path.Combine(Application.persistentDataPath, "largefile.zip");
        
        bool success = await downloader.DownloadAsync(url, path);
        
        if (success)
        {
            Debug.Log("Download completed successfully!");
        }
        else
        {
            Debug.Log("Download failed!");
        }
    }
    
    private void OnDownloadProgress(SherpaDownloadFeedback progress)
    {
        Debug.Log($"Progress: {progress.ProgressPercentage:F1}% " +
                  $"({SherpaFileDownloader.FormatFileSize(progress.DownloadedBytes)}/" +
                  $"{SherpaFileDownloader.FormatFileSize(progress.TotalBytes)}) " +
                  $"Speed: {SherpaFileDownloader.FormatSpeed(progress.SpeedBytesPerSecond)} " +
                  $"Chunks: {progress.CompletedChunks}/{progress.TotalChunks}");
    }
    
    public void Cleanup()
    {
        downloader?.Dispose();
    }
}
*/