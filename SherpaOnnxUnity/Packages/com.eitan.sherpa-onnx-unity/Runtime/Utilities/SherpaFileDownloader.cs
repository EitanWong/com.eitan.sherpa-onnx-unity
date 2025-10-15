using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Eitan.SherpaOnnxUnity.Runtime.Utilities
{
    internal static class UnityMainThreadScheduler
    {
        private static readonly object InitLock = new object();
        private static SynchronizationContext _context;
        private static int _mainThreadId;
        private static bool _initialized;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            lock (InitLock)
            {
                _context = null;
                _mainThreadId = 0;
                _initialized = false;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void InitializeOnLoad()
        {
            EnsureInitialized();
        }

        public static void EnsureInitialized()
        {
            if (_initialized) { return; }

            lock (InitLock)
            {
                if (_initialized) { return; }

                _context = SynchronizationContext.Current ?? new SynchronizationContext();
                _mainThreadId = Thread.CurrentThread.ManagedThreadId;
                _initialized = true;
            }
        }

        public static bool IsMainThread => _initialized && Thread.CurrentThread.ManagedThreadId == _mainThreadId;

        public static void Post(Action action)
        {
            if (action == null) { return; }
            EnsureInitialized();

            if (IsMainThread)
            {
                action();
                return;
            }

            _context.Post(static state =>
            {
                try
                {
                    ((Action)state)?.Invoke();
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            }, action);
        }

        public static Task Run(Action action)
        {
            EnsureInitialized();

            if (IsMainThread)
            {
                action();
                return Task.CompletedTask;
            }

            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            _context.Post(state =>
            {
                try
                {
                    ((Action)state)?.Invoke();
                    tcs.SetResult(true);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            }, action);

            return tcs.Task;
        }

        public static Task Run(Func<Task> func)
        {
            EnsureInitialized();

            if (IsMainThread)
            {
                return func();
            }

            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            _context.Post(async state =>
            {
                try
                {
                    await ((Func<Task>)state)().ConfigureAwait(false);
                    tcs.SetResult(true);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            }, func);

            return tcs.Task;
        }

        public static Task<T> Run<T>(Func<T> func)
        {
            EnsureInitialized();

            if (IsMainThread)
            {
                return Task.FromResult(func());
            }

            var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);

            _context.Post(state =>
            {
                try
                {
                    tcs.SetResult(((Func<T>)state)());
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            }, func);

            return tcs.Task;
        }

        public static Task<T> Run<T>(Func<Task<T>> func)
        {
            EnsureInitialized();

            if (IsMainThread)
            {
                return func();
            }

            var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);

            _context.Post(async state =>
            {
                try
                {
                    var result = await ((Func<Task<T>>)state)().ConfigureAwait(false);
                    tcs.SetResult(result);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            }, func);

            return tcs.Task;
        }

        public static Task AwaitAsyncOperation(AsyncOperation operation, CancellationToken token)
        {
            if (operation == null) { throw new ArgumentNullException(nameof(operation)); }

            if (operation.isDone)
            {
                token.ThrowIfCancellationRequested();
                return Task.CompletedTask;
            }

            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            void Completed(AsyncOperation _)
            {
                operation.completed -= Completed;
                tcs.TrySetResult(true);
            }

            operation.completed += Completed;

            if (token.CanBeCanceled)
            {
                token.Register(() =>
                {
                    operation.completed -= Completed;
                    tcs.TrySetCanceled(token);
                });
            }

            return tcs.Task;
        }
    }

    internal static class UnityPauseWatcher
    {
        public static event Action<bool> PauseChanged;

        private static bool _subscribed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            if (!_subscribed) { return; }
            Application.focusChanged -= OnPauseStateChanged;
            _subscribed = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void Initialize()
        {
            if (_subscribed) { return; }
            Application.focusChanged += OnPauseStateChanged;
            _subscribed = true;
        }

        private static void OnPauseStateChanged(bool focus)
        {
            PauseChanged?.Invoke(!focus);
        }
    }

    internal sealed class AsyncManualResetEvent
    {
        private volatile TaskCompletionSource<bool> _tcs;

        public AsyncManualResetEvent(bool initialState = false)
        {
            _tcs = CreateTaskSource(initialState);
        }

        public Task WaitAsync(CancellationToken token)
        {
            if (!token.CanBeCanceled)
            {
                return _tcs.Task;
            }

            return WaitWithCancellationAsync(token);
        }

        public void Set()
        {
            var tcs = _tcs;
            if (!tcs.Task.IsCompleted)
            {
                tcs.TrySetResult(true);
            }
        }

        public void Reset()
        {
            while (true)
            {
                var tcs = _tcs;
                if (!tcs.Task.IsCompleted)
                {
                    return;
                }

                var newSource = CreateTaskSource(false);
                if (Interlocked.CompareExchange(ref _tcs, newSource, tcs) == tcs)
                {
                    return;
                }
            }
        }

        private async Task WaitWithCancellationAsync(CancellationToken token)
        {
            using (token.Register(() => _tcs.TrySetCanceled(token), useSynchronizationContext: false))
            {
                await _tcs.Task.ConfigureAwait(false);
            }
        }

        private static TaskCompletionSource<bool> CreateTaskSource(bool set)
        {
            var source = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            if (set)
            {
                source.TrySetResult(true);
            }

            return source;
        }
    }

    [Serializable]
    internal class ChunkInfo
    {
        public int Index;
        public long Start;
        public long End;
        public long Downloaded;
        public bool IsCompleted;
        public string TempFileName;
        public string ErrorMessage;
        public int RetryCount;

        public long ExpectedLength => End - Start + 1;
    }

    [Serializable]
    internal class DownloadMetadata : ISerializationCallbackReceiver
    {
        public string Url;
        public string FileName;
        public long TotalSize;
        public long ChunkSize;
        public List<ChunkInfo> Chunks = new List<ChunkInfo>();
        public bool SupportsRangeRequests;
        public string WorkingDirectory;
        public string CreatedTimeString;
        public string LastModifiedTimeString;

        [NonSerialized] public DateTime CreatedTime;
        [NonSerialized] public DateTime LastModifiedTime;

        public void OnBeforeSerialize()
        {
            CreatedTimeString = CreatedTime.ToString("o", CultureInfo.InvariantCulture);
            LastModifiedTimeString = LastModifiedTime.ToString("o", CultureInfo.InvariantCulture);
        }

        public void OnAfterDeserialize()
        {
            if (!string.IsNullOrEmpty(CreatedTimeString))
            {
                DateTime.TryParse(CreatedTimeString, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out CreatedTime);
            }

            if (!string.IsNullOrEmpty(LastModifiedTimeString))
            {
                DateTime.TryParse(LastModifiedTimeString, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out LastModifiedTime);
            }
        }
    }

    internal sealed class ChunkDownloadHandler : DownloadHandlerScript, IDisposable
    {
        private const int BufferSize = 64 * 1024;

        private readonly FileStream _stream;
        private readonly ChunkInfo _chunk;
        private readonly long _expectedLength;
        private readonly long _initialDownloaded;
        private readonly Action<long> _onProgress;

        private long _bytesWritten;

        public long BytesWritten => _bytesWritten;

        public ChunkDownloadHandler(FileStream stream, ChunkInfo chunk, long expectedLength, Action<long> onProgress)
            : base(new byte[BufferSize])
        {
            _stream = stream ?? throw new ArgumentNullException(nameof(stream));
            _chunk = chunk ?? throw new ArgumentNullException(nameof(chunk));
            _expectedLength = expectedLength;
            _initialDownloaded = Math.Min(chunk.Downloaded, expectedLength);
            _onProgress = onProgress;
        }

        protected override bool ReceiveData(byte[] data, int dataLength)
        {
            if (data == null || dataLength <= 0)
            {
                return true;
            }

            var remaining = _expectedLength - _bytesWritten;
            if (remaining <= 0)
            {
                return false;
            }

            var bytesToWrite = (int)Math.Min(remaining, dataLength);

            _stream.Write(data, 0, bytesToWrite);
            _bytesWritten += bytesToWrite;

            var downloaded = _initialDownloaded + _bytesWritten;
            Volatile.Write(ref _chunk.Downloaded, Math.Min(downloaded, _expectedLength));
            if (_chunk.Downloaded >= _expectedLength)
            {
                _chunk.IsCompleted = true;
            }

            _onProgress?.Invoke(bytesToWrite);
            return true;
        }

        protected override void CompleteContent()
        {
            try
            {
                _stream.Flush(flushToDisk: false);
            }
            catch (IOException)
            {
                // Ignore flush errors; they will surface when we reopen the stream.
            }

            base.CompleteContent();
        }

        public override void Dispose()
        {
            // if (disposing)
            // {
            _stream.Dispose();
            // }

            base.Dispose();
        }
    }

    internal readonly struct UnityWebRequestResponse
    {
        public readonly UnityWebRequest.Result Result;
        public readonly long ResponseCode;
        public readonly string Error;
        public readonly Dictionary<string, string> Headers;

        public UnityWebRequestResponse(
            UnityWebRequest.Result result,
            long responseCode,
            string error,
            Dictionary<string, string> headers)
        {
            Result = result;
            ResponseCode = responseCode;
            Error = error;
            Headers = headers;
        }
    }

    internal readonly struct ChunkRequestResult
    {
        public readonly UnityWebRequest.Result Result;
        public readonly long ResponseCode;
        public readonly string Error;
        public readonly string AcceptRanges;
        public readonly string ContentRange;
        public readonly long BytesDownloaded;

        public ChunkRequestResult(
            UnityWebRequest.Result result,
            long responseCode,
            string error,
            string acceptRanges,
            string contentRange,
            long bytesDownloaded)
        {
            Result = result;
            ResponseCode = responseCode;
            Error = error;
            AcceptRanges = acceptRanges;
            ContentRange = contentRange;
            BytesDownloaded = bytesDownloaded;
        }
    }

    internal sealed class RangeDowngradeException : Exception
    {
        public static readonly RangeDowngradeException Instance = new RangeDowngradeException();

        private RangeDowngradeException() : base("Server ignored range request; downgrading to single-threaded download.") { }
    }

    internal class SherpaFileDownloader : IDisposable
    {
        private const string MetadataFileExtension = ".download.metadata";
        private const string DownloadTempFileExtension = ".download";
        private const string ChunkDirectorySuffix = ".chunks";

        private static readonly object InstancesLock = new object();
        private static readonly HashSet<WeakReference<SherpaFileDownloader>> Instances = new HashSet<WeakReference<SherpaFileDownloader>>();

        static SherpaFileDownloader()
        {
            UnityMainThreadScheduler.EnsureInitialized();
            UnityPauseWatcher.PauseChanged += HandleGlobalPauseChanged;
        }

        private readonly WeakReference<SherpaFileDownloader> _selfReference;
        private readonly SherpaOnnxModelMetadata _modelMetadata;
        private readonly int _maxConcurrentChunks;
        private readonly long _defaultChunkSize;
        private readonly int _maxRetryAttempts;
        private readonly int _timeoutSeconds;
        private readonly string _userAgent;
        private readonly TimeSpan _baseRetryDelay = TimeSpan.FromSeconds(2);

        private readonly object _stateLock = new object();
        private readonly AsyncManualResetEvent _pauseSignal = new AsyncManualResetEvent(true);
        private readonly object _progressLock = new object();

        private DownloadMetadata _metadata;
        private string _finalFilePath;
        private string _tempFilePath;
        private string _metadataFilePath;
        private string _chunkDirectory;

        private CancellationTokenSource _manualCancellationSource = new CancellationTokenSource();
        private CancellationTokenSource _pauseCancellationSource = new CancellationTokenSource();
        private SemaphoreSlim _concurrencyLimiter;

        private volatile bool _isPaused;
        private volatile bool _isDisposed;
        private double _currentSpeed;
        private long _lastReportedBytes;
        private DateTime _lastProgressTimestamp = DateTime.UtcNow;

        public event Action<IFeedback> Feedback;

        public SherpaFileDownloader(
            SherpaOnnxModelMetadata metadata = null,
            int maxConcurrentChunks = 4,
            long chunkSizeMB = 10,
            int maxRetryAttempts = 3,
            int timeoutSeconds = 60)
        {
            _modelMetadata = metadata;
            _maxConcurrentChunks = Mathf.Clamp(maxConcurrentChunks, 1, 8);
            _defaultChunkSize = Math.Max(1024 * 1024, chunkSizeMB * 1024 * 1024);
            _maxRetryAttempts = Mathf.Max(1, maxRetryAttempts);

            var platformTimeout = Application.platform == RuntimePlatform.IPhonePlayer || Application.platform == RuntimePlatform.Android
                ? Math.Max(timeoutSeconds, 120)
                : Math.Max(timeoutSeconds, 60);
            _timeoutSeconds = platformTimeout;
            _userAgent = BuildUserAgent();

            ResetConcurrencyLimiter(_maxConcurrentChunks);

            _selfReference = new WeakReference<SherpaFileDownloader>(this);
            lock (InstancesLock)
            {
                Instances.Add(_selfReference);
            }
        }

        public async Task<bool> DownloadAsync(string url, string filePath, CancellationToken cancellationToken = default)
        {
            if (_isDisposed) { throw new ObjectDisposedException(nameof(SherpaFileDownloader)); }
            if (string.IsNullOrEmpty(url)) { throw new ArgumentNullException(nameof(url)); }
            if (string.IsNullOrEmpty(filePath)) { throw new ArgumentNullException(nameof(filePath)); }

            EnsureWritablePath(filePath);

            using var linkedUserCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _manualCancellationSource.Token);
            var userToken = linkedUserCancellation.Token;

            try
            {
                await InitializeDownloadAsync(url, filePath, userToken).ConfigureAwait(false);
                ReportProgress();

                while (true)
                {
                    userToken.ThrowIfCancellationRequested();
                    await _pauseSignal.WaitAsync(userToken).ConfigureAwait(false);

                    if (IsDownloadCompleted())
                    {
                        await FinalizeDownloadAsync().ConfigureAwait(false);
                        ReportProgress();
                        return true;
                    }

                    var pendingChunks = _metadata.Chunks.Where(c => !c.IsCompleted).OrderBy(c => c.Index).ToList();
                    if (pendingChunks.Count == 0)
                    {
                        await SaveMetadataAsync().ConfigureAwait(false);
                        continue;
                    }

                    try
                    {
                        await DownloadChunksAsync(pendingChunks, userToken).ConfigureAwait(false);
                    }
                    catch (RangeDowngradeException)
                    {
                        await HandleRangeDowngradeAsync().ConfigureAwait(false);
                        CalculateDownloadedBytes();
                        ReportProgress();
                        continue;
                    }
                    catch (OperationCanceledException) when (_isPaused && !userToken.IsCancellationRequested)
                    {
                        await SaveMetadataAsync().ConfigureAwait(false);
                        await WaitForResumeAsync(userToken).ConfigureAwait(false);
                        continue;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                ReportProgress();
                return false;
            }
            catch (Exception ex)
            {
                ReportProgress(ex.ToString());
                return false;
            }
        }

        private async Task DownloadChunksAsync(IEnumerable<ChunkInfo> chunks, CancellationToken userToken)
        {
            var concurrency = GetAllowedConcurrency();
            if (concurrency <= 1)
            {
                foreach (var chunk in chunks)
                {
                    await DownloadChunkWithRetryAsync(chunk, userToken).ConfigureAwait(false);
                }
                return;
            }

            var tasks = new List<Task>();
            foreach (var chunk in chunks)
            {
                tasks.Add(DownloadChunkAsync(chunk, userToken));
            }

            await Task.WhenAll(tasks).ConfigureAwait(false);
        }

        private async Task DownloadChunkAsync(ChunkInfo chunk, CancellationToken userToken)
        {
            await _concurrencyLimiter.WaitAsync(userToken).ConfigureAwait(false);

            try
            {
                await DownloadChunkWithRetryAsync(chunk, userToken).ConfigureAwait(false);
            }
            finally
            {
                _concurrencyLimiter.Release();
            }
        }

        private async Task DownloadChunkWithRetryAsync(ChunkInfo chunk, CancellationToken userToken)
        {
            for (int attempt = 0; attempt < _maxRetryAttempts; attempt++)
            {
                userToken.ThrowIfCancellationRequested();
                await _pauseSignal.WaitAsync(userToken).ConfigureAwait(false);

                using var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
                    userToken,
                    _pauseCancellationSource.Token);
                var token = linkedTokenSource.Token;

                try
                {
                    var outcome = await ExecuteChunkRequestAsync(chunk, token).ConfigureAwait(false);
                    token.ThrowIfCancellationRequested();

                    await ProcessChunkOutcomeAsync(chunk, outcome).ConfigureAwait(false);
                    await SaveMetadataAsync().ConfigureAwait(false);
                    return;
                }
                catch (RangeDowngradeException)
                {
                    throw;
                }
                catch (OperationCanceledException)
                {
                    if (_isPaused && !_manualCancellationSource.IsCancellationRequested && !userToken.IsCancellationRequested)
                    {
                        throw;
                    }

                    token.ThrowIfCancellationRequested();
                    throw;
                }
                catch (Exception ex) when (attempt < _maxRetryAttempts - 1)
                {
                    chunk.ErrorMessage = ex.Message;
                    chunk.RetryCount = attempt + 1;
                    var delay = GetBackoffDelay(attempt);

                    await Task.Delay(delay, userToken).ConfigureAwait(false);
                }
            }

            throw new InvalidOperationException($"Chunk {chunk.Index} failed after {_maxRetryAttempts} attempts.");
        }

        private async Task<ChunkRequestResult> ExecuteChunkRequestAsync(ChunkInfo chunk, CancellationToken token)
        {
            var chunkPath = GetChunkFilePath(chunk);
            Directory.CreateDirectory(Path.GetDirectoryName(chunkPath));

            var expectedLength = chunk.ExpectedLength;

            return await UnityMainThreadScheduler.Run(async () =>
            {
                using var stream = new FileStream(chunkPath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Read);

                if (stream.Length > expectedLength)
                {
                    stream.SetLength(expectedLength);
                }

                var currentDownloaded = Math.Min(stream.Length, expectedLength);
                chunk.Downloaded = currentDownloaded;
                chunk.IsCompleted = currentDownloaded >= expectedLength;
                stream.Seek(currentDownloaded, SeekOrigin.Begin);

                if (chunk.IsCompleted)
                {
                    return new ChunkRequestResult(UnityWebRequest.Result.Success, 206, null, "bytes", $"bytes {chunk.Start}-{chunk.End}/{_metadata.TotalSize}", 0);
                }

                using var request = UnityWebRequest.Get(_metadata.Url);
                request.timeout = _timeoutSeconds;
                request.SetRequestHeader("User-Agent", _userAgent);

                var useRange = _metadata.SupportsRangeRequests;
                var rangeStart = chunk.Start + currentDownloaded;
                var rangeEnd = chunk.End;
                if (useRange)
                {
                    request.SetRequestHeader("Range", $"bytes={rangeStart}-{rangeEnd}");
                }

                var handler = new ChunkDownloadHandler(stream, chunk, expectedLength, OnChunkProgress);
                request.downloadHandler = handler;

                using var cancellationRegistration = token.Register(() =>
                {
                    UnityMainThreadScheduler.Post(() =>
                    {
                        if (!request.isDone)
                        {
                            request.Abort();
                        }
                    });
                });

                var operation = request.SendWebRequest();
                await UnityMainThreadScheduler.AwaitAsyncOperation(operation, token);

                var outcome = new ChunkRequestResult(
                    request.result,
                    request.responseCode,
                    request.error,
                    request.GetResponseHeader("Accept-Ranges"),
                    request.GetResponseHeader("Content-Range"),
                    handler.BytesWritten);

                return outcome;
            }).ConfigureAwait(false);
        }

        private Task ProcessChunkOutcomeAsync(ChunkInfo chunk, ChunkRequestResult outcome)
        {
            var expectedLength = chunk.ExpectedLength;
            chunk.Downloaded = Math.Min(chunk.Downloaded, expectedLength);

            Debug.Log($"[SherpaFileDownloader] Chunk {chunk.Index} result={outcome.Result} code={outcome.ResponseCode} acceptRanges='{outcome.AcceptRanges}' contentRange='{outcome.ContentRange}'");

            if (outcome.Result == UnityWebRequest.Result.Success)
            {
                if (_metadata.SupportsRangeRequests)
                {
                    if (outcome.ResponseCode == 206)
                    {
                        ValidateContentRange(chunk, outcome.ContentRange);
                        chunk.IsCompleted = chunk.Downloaded >= expectedLength;
                        return Task.CompletedTask;
                    }

                    if (outcome.ResponseCode == 200)
                    {
                        throw RangeDowngradeException.Instance;
                    }
                }
                else
                {
                    if (outcome.ResponseCode == 200 || outcome.ResponseCode == 201)
                    {
                        chunk.IsCompleted = chunk.Downloaded >= expectedLength;
                        return Task.CompletedTask;
                    }
                }
            }

            if (outcome.ResponseCode == 416)
            {
                if (VerifyChunkOnDisk(chunk))
                {
                    chunk.Downloaded = expectedLength;
                    chunk.IsCompleted = true;
                    return Task.CompletedTask;
                }

                ResetChunkFile(chunk);
                return Task.CompletedTask;
            }

            throw new InvalidOperationException($"Chunk {chunk.Index} download failed. Result: {outcome.Result}, Code: {outcome.ResponseCode}, Error: {outcome.Error}");
        }

        private async Task InitializeDownloadAsync(string url, string filePath, CancellationToken token)
        {
            _finalFilePath = filePath;
            _tempFilePath = filePath + DownloadTempFileExtension;
            _metadataFilePath = filePath + MetadataFileExtension;
            _chunkDirectory = filePath + ChunkDirectorySuffix;

            if (File.Exists(_metadataFilePath))
            {
                try
                {
                    await LoadMetadataAsync().ConfigureAwait(false);
                    if (string.Equals(_metadata.Url, url, StringComparison.OrdinalIgnoreCase))
                    {
                        CalculateDownloadedBytes();
                        return;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"Failed to load metadata; starting new download. {ex}");
                }
            }

            var (fileSize, supportsRange) = await GetFileInfoAsync(url, token).ConfigureAwait(false);
            var chunkSize = supportsRange
                ? Math.Min(_defaultChunkSize, Math.Max(1024 * 1024, fileSize / _maxConcurrentChunks))
                : fileSize;

            var chunks = new List<ChunkInfo>();
            long position = 0;
            int index = 0;
            while (position < fileSize)
            {
                var end = Math.Min(position + chunkSize - 1, fileSize - 1);
                chunks.Add(new ChunkInfo
                {
                    Index = index,
                    Start = position,
                    End = end,
                    Downloaded = 0,
                    IsCompleted = false,
                    TempFileName = $"chunk_{index:D4}.part"
                });

                index++;
                position = end + 1;
            }

            _metadata = new DownloadMetadata
            {
                Url = url,
                FileName = Path.GetFileName(filePath),
                TotalSize = fileSize,
                ChunkSize = chunkSize,
                CreatedTime = DateTime.UtcNow,
                LastModifiedTime = DateTime.UtcNow,
                SupportsRangeRequests = supportsRange,
                WorkingDirectory = _chunkDirectory,
                Chunks = chunks
            };

            if (!supportsRange)
            {
                ResetConcurrencyLimiter(1);
            }

            Directory.CreateDirectory(_chunkDirectory);
            CalculateDownloadedBytes();
            await SaveMetadataAsync().ConfigureAwait(false);
        }

        private async Task<UnityWebRequestResponse> SendSimpleRequestAsync(UnityWebRequest request, CancellationToken token)
        {
            return await UnityMainThreadScheduler.Run(async () =>
            {
                using (request)
                {
                    request.timeout = _timeoutSeconds;
                    request.SetRequestHeader("User-Agent", _userAgent);

                    using var registration = token.Register(() =>
                    {
                        UnityMainThreadScheduler.Post(() =>
                        {
                            if (!request.isDone)
                            {
                                request.Abort();
                            }
                        });
                    });

                    var operation = request.SendWebRequest();
                    await UnityMainThreadScheduler.AwaitAsyncOperation(operation, token);

                    var headers = request.GetResponseHeaders() ?? new Dictionary<string, string>();
                    return new UnityWebRequestResponse(request.result, request.responseCode, request.error, headers);
                }
            }).ConfigureAwait(false);
        }

        private async Task<(long fileSize, bool supportsRangeRequests)> GetFileInfoAsync(string url, CancellationToken token)
        {
            WarnIfInsecureUrl(url);

            var headResponse = await SendSimpleRequestAsync(UnityWebRequest.Head(url), token).ConfigureAwait(false);
            if (headResponse.Result == UnityWebRequest.Result.Success &&
                headResponse.Headers.TryGetValue("Content-Length", out var contentLengthHeader) &&
                long.TryParse(contentLengthHeader, NumberStyles.Integer, CultureInfo.InvariantCulture, out var sizeFromHead))
            {
                var supportsRange = headResponse.Headers.TryGetValue("Accept-Ranges", out var acceptRanges) &&
                                    !string.IsNullOrEmpty(acceptRanges) &&
                                    acceptRanges.IndexOf("bytes", StringComparison.OrdinalIgnoreCase) >= 0;

                if (sizeFromHead > 0)
                {
                    return (sizeFromHead, supportsRange);
                }
            }

            var probeRequest = UnityWebRequest.Get(url);
            probeRequest.SetRequestHeader("Range", "bytes=0-0");
            var probeResponse = await SendSimpleRequestAsync(probeRequest, token).ConfigureAwait(false);

            if (probeResponse.Result == UnityWebRequest.Result.Success && probeResponse.ResponseCode == 206)
            {
                if (probeResponse.Headers.TryGetValue("Content-Range", out var contentRange) &&
                    TryParseContentRange(contentRange, out _, out _, out var total))
                {
                    return (total, true);
                }
            }

            if (probeResponse.Headers.TryGetValue("Content-Length", out var probeLength) &&
                long.TryParse(probeLength, NumberStyles.Integer, CultureInfo.InvariantCulture, out var sizeFromProbe))
            {
                return (sizeFromProbe, false);
            }

            throw new InvalidOperationException("Unable to determine remote file size.");
        }

        private void CalculateDownloadedBytes()
        {
            if (_metadata == null) { return; }

            long total = 0;
            foreach (var chunk in _metadata.Chunks)
            {
                var clamped = Math.Min(chunk.Downloaded, chunk.ExpectedLength);
                chunk.Downloaded = clamped;
                if (clamped >= chunk.ExpectedLength)
                {
                    chunk.IsCompleted = true;
                }

                total += clamped;
            }

            _lastReportedBytes = total;
        }

        private bool IsDownloadCompleted()
        {
            if (_metadata == null) { return false; }

            if (_metadata.Chunks.Any(chunk => !chunk.IsCompleted))
            {
                return false;
            }

            var sum = _metadata.Chunks.Sum(chunk => chunk.ExpectedLength);
            return sum == _metadata.TotalSize;
        }

        private async Task FinalizeDownloadAsync()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_finalFilePath));

            using (var output = new FileStream(_tempFilePath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                foreach (var chunk in _metadata.Chunks.OrderBy(c => c.Index))
                {
                    var chunkPath = GetChunkFilePath(chunk);
                    using var input = new FileStream(chunkPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                    await input.CopyToAsync(output).ConfigureAwait(false);
                }
            }

            var fileInfo = new FileInfo(_tempFilePath);
            if (fileInfo.Length != _metadata.TotalSize)
            {
                throw new InvalidOperationException($"File size mismatch. Expected {_metadata.TotalSize}, actual {fileInfo.Length}.");
            }

            if (File.Exists(_finalFilePath))
            {
                File.Delete(_finalFilePath);
            }

            File.Move(_tempFilePath, _finalFilePath);

            foreach (var chunk in _metadata.Chunks)
            {
                var path = GetChunkFilePath(chunk);
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }

            if (Directory.Exists(_chunkDirectory) && !Directory.EnumerateFileSystemEntries(_chunkDirectory).Any())
            {
                Directory.Delete(_chunkDirectory, recursive: true);
            }

            if (File.Exists(_metadataFilePath))
            {
                File.Delete(_metadataFilePath);
            }
        }

        private void ReportProgress(string errorMessage = null)
        {
            if (!string.IsNullOrEmpty(errorMessage))
            {
                Feedback?.Invoke(new FailedFeedback(_modelMetadata, errorMessage));
                return;
            }

            if (_metadata == null || _metadata.TotalSize <= 0)
            {
                return;
            }

            var downloaded = _metadata.Chunks.Sum(c => Math.Min(c.Downloaded, c.ExpectedLength));

            lock (_progressLock)
            {
                var now = DateTime.UtcNow;
                var elapsed = now - _lastProgressTimestamp;
                if (elapsed.TotalSeconds > 0.1)
                {
                    var deltaBytes = downloaded - _lastReportedBytes;
                    if (deltaBytes >= 0)
                    {
                        _currentSpeed = deltaBytes / Math.Max(elapsed.TotalSeconds, 0.1);
                        _lastReportedBytes = downloaded;
                        _lastProgressTimestamp = now;
                    }
                }
            }

            var feedback = new DownloadFeedback(
                _modelMetadata,
                _metadata.FileName,
                downloaded,
                _metadata.TotalSize,
                _currentSpeed);

            Feedback?.Invoke(feedback);
        }

        private void OnChunkProgress(long bytesReceived)
        {
            if (bytesReceived <= 0) { return; }
            ReportProgress();
        }

        private void ValidateContentRange(ChunkInfo chunk, string contentRange)
        {
            if (!TryParseContentRange(contentRange, out var start, out var end, out var total))
            {
                throw new InvalidOperationException($"Invalid Content-Range header: {contentRange}");
            }

            if (start != chunk.Start || end != chunk.End || total != _metadata.TotalSize)
            {
                throw new InvalidOperationException($"Content-Range mismatch for chunk {chunk.Index}. Expected {chunk.Start}-{chunk.End}/{_metadata.TotalSize}, got {contentRange}");
            }
        }

        private bool VerifyChunkOnDisk(ChunkInfo chunk)
        {
            var path = GetChunkFilePath(chunk);
            if (!File.Exists(path))
            {
                return false;
            }

            var fileInfo = new FileInfo(path);
            return fileInfo.Length == chunk.ExpectedLength;
        }

        private void ResetChunkFile(ChunkInfo chunk)
        {
            var path = GetChunkFilePath(chunk);
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            chunk.Downloaded = 0;
            chunk.IsCompleted = false;
        }

        private async Task SaveMetadataAsync()
        {
            if (_metadata == null) { return; }

            _metadata.LastModifiedTime = DateTime.UtcNow;
            Directory.CreateDirectory(Path.GetDirectoryName(_metadataFilePath));
            var json = JsonUtility.ToJson(_metadata, true);
            await File.WriteAllTextAsync(_metadataFilePath, json).ConfigureAwait(false);
        }

        private async Task LoadMetadataAsync()
        {
            var json = await File.ReadAllTextAsync(_metadataFilePath).ConfigureAwait(false);
            _metadata = JsonUtility.FromJson<DownloadMetadata>(json);

            if (!string.IsNullOrEmpty(_metadata.WorkingDirectory))
            {
                _chunkDirectory = _metadata.WorkingDirectory;
            }
            else
            {
                _metadata.WorkingDirectory = _chunkDirectory;
            }

            Directory.CreateDirectory(_metadata.WorkingDirectory);

            foreach (var chunk in _metadata.Chunks)
            {
                if (string.IsNullOrEmpty(chunk.TempFileName))
                {
                    chunk.TempFileName = $"chunk_{chunk.Index:D4}.part";
                }
            }

            if (!_metadata.SupportsRangeRequests)
            {
                ResetConcurrencyLimiter(1);
            }
        }

        private async Task HandleRangeDowngradeAsync()
        {
            if (_metadata == null) { return; }
            if (!_metadata.SupportsRangeRequests) { return; }

            foreach (var chunk in _metadata.Chunks)
            {
                var path = GetChunkFilePath(chunk);
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }

            _metadata.SupportsRangeRequests = false;
            _metadata.Chunks = new List<ChunkInfo>
            {
                new ChunkInfo
                {
                    Index = 0,
                    Start = 0,
                    End = _metadata.TotalSize - 1,
                    Downloaded = 0,
                    IsCompleted = false,
                    TempFileName = "chunk_0000.part"
                }
            };

            ResetConcurrencyLimiter(1);
            CalculateDownloadedBytes();
            await SaveMetadataAsync().ConfigureAwait(false);
        }

        private async Task WaitForResumeAsync(CancellationToken token)
        {
            while (_isPaused && !token.IsCancellationRequested)
            {
                await _pauseSignal.WaitAsync(token).ConfigureAwait(false);
            }
        }

        private static void HandleGlobalPauseChanged(bool paused)
        {
            lock (InstancesLock)
            {
                var dead = new List<WeakReference<SherpaFileDownloader>>();
                foreach (var weak in Instances)
                {
                    if (weak.TryGetTarget(out var downloader))
                    {
                        downloader.OnApplicationPause(paused);
                    }
                    else
                    {
                        dead.Add(weak);
                    }
                }

                foreach (var weak in dead)
                {
                    Instances.Remove(weak);
                }
            }
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused)
            {
                RequestPause();
            }
            else
            {
                ResumeFromPause();
            }
        }

        private void RequestPause()
        {
            lock (_stateLock)
            {
                if (_isPaused) { return; }

                _isPaused = true;
                _pauseSignal.Reset();
                _pauseCancellationSource.Cancel();
            }

            _ = SaveMetadataAsync();
        }

        private void ResumeFromPause()
        {
            lock (_stateLock)
            {
                if (!_isPaused) { return; }

                _pauseCancellationSource.Dispose();
                _pauseCancellationSource = new CancellationTokenSource();
                _isPaused = false;
                _pauseSignal.Set();
            }
        }

        private void ResetConcurrencyLimiter(int maxConcurrency)
        {
            _concurrencyLimiter?.Dispose();
            _concurrencyLimiter = new SemaphoreSlim(Math.Max(1, maxConcurrency), Math.Max(1, maxConcurrency));
        }

        private int GetAllowedConcurrency()
        {
            return _metadata != null && _metadata.SupportsRangeRequests ? _maxConcurrentChunks : 1;
        }

        private string GetChunkFilePath(ChunkInfo chunk)
        {
            var fileName = string.IsNullOrEmpty(chunk.TempFileName) ? $"chunk_{chunk.Index:D4}.part" : chunk.TempFileName;
            var directory = _metadata?.WorkingDirectory ?? _chunkDirectory;
            return Path.Combine(directory, fileName);
        }

        private static bool TryParseContentRange(string header, out long start, out long end, out long total)
        {
            start = 0;
            end = 0;
            total = 0;

            if (string.IsNullOrEmpty(header))
            {
                return false;
            }

            // Format: bytes start-end/total
            var spaceIndex = header.IndexOf(' ');
            var slashIndex = header.IndexOf('/');

            if (spaceIndex < 0 || slashIndex < 0 || slashIndex <= spaceIndex)
            {
                return false;
            }

            var rangePart = header.Substring(spaceIndex + 1, slashIndex - spaceIndex - 1);
            var totalPart = header.Substring(slashIndex + 1);

            var dashIndex = rangePart.IndexOf('-');
            if (dashIndex < 0)
            {
                return false;
            }

            if (!long.TryParse(rangePart.Substring(0, dashIndex), NumberStyles.Integer, CultureInfo.InvariantCulture, out start))
            {
                return false;
            }

            if (!long.TryParse(rangePart.Substring(dashIndex + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out end))
            {
                return false;
            }

            if (totalPart == "*")
            {
                total = -1;
                return true;
            }

            return long.TryParse(totalPart, NumberStyles.Integer, CultureInfo.InvariantCulture, out total);
        }

        private TimeSpan GetBackoffDelay(int attempt)
        {
            var multiplier = Math.Pow(2, Math.Min(attempt, 5));
            var seconds = Math.Min(30, _baseRetryDelay.TotalSeconds * multiplier);
            return TimeSpan.FromSeconds(seconds);
        }

        private static string BuildUserAgent()
        {
            var deviceModel = SystemInfo.deviceModel;
            if (string.IsNullOrEmpty(deviceModel))
            {
                deviceModel = "UnityPlayer";
            }

            switch (Application.platform)
            {
                case RuntimePlatform.IPhonePlayer:
                    var iosVersion = ExtractVersionSegment(SystemInfo.operatingSystem, "iOS", "16_0", replaceDotsWithUnderscore: true);
                    return $"Mozilla/5.0 (iPhone; CPU iPhone OS {iosVersion} like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/16.0 Mobile/15E148 Safari/604.1";
                case RuntimePlatform.Android:
                    var androidVersion = ExtractVersionSegment(SystemInfo.operatingSystem, "Android", "13", replaceDotsWithUnderscore: false);
                    return $"Mozilla/5.0 (Linux; Android {androidVersion}; {deviceModel}) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/119.0.0.0 Mobile Safari/537.36";
                case RuntimePlatform.OSXPlayer:
                case RuntimePlatform.OSXEditor:
                    return "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/16.0 Safari/605.1.15";
                case RuntimePlatform.WindowsPlayer:
                case RuntimePlatform.WindowsEditor:
                    return "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/119.0.0.0 Safari/537.36";
                case RuntimePlatform.LinuxPlayer:
                case RuntimePlatform.LinuxEditor:
                    return "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/119.0.0.0 Safari/537.36";
                default:
                    return $"Mozilla/5.0 ({deviceModel}) AppleWebKit/605.1.15 (KHTML, like Gecko)";
            }
        }

        private static string ExtractVersionSegment(string source, string token, string fallback, bool replaceDotsWithUnderscore)
        {
            fallback = string.IsNullOrEmpty(fallback) ? "1.0" : fallback;
            source ??= string.Empty;

            var index = source.IndexOf(token, StringComparison.OrdinalIgnoreCase);
            if (index >= 0)
            {
                index += token.Length;
                while (index < source.Length && (source[index] == ' ' || source[index] == ':' || source[index] == '_'))
                {
                    index++;
                }

                var end = index;
                while (end < source.Length)
                {
                    var c = source[end];
                    if (!(char.IsDigit(c) || c == '.' || c == '_'))
                    {
                        break;
                    }
                    end++;
                }

                if (end > index)
                {
                    var segment = source.Substring(index, end - index);
                    if (!string.IsNullOrEmpty(segment))
                    {
                        return replaceDotsWithUnderscore
                            ? segment.Replace('.', '_')
                            : segment.Replace('_', '.');
                    }
                }
            }

            return replaceDotsWithUnderscore ? fallback.Replace('.', '_') : fallback.Replace('_', '.');
        }

        private void WarnIfInsecureUrl(string url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                Debug.LogWarning($"Downloader received invalid URL: {url}");
                return;
            }

            if (!uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                Debug.LogWarning($"URL '{url}' is not HTTPS. Ensure ATS exceptions are configured if targeting iOS.");
            }
        }

        private void EnsureWritablePath(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                throw new ArgumentNullException(nameof(filePath));
            }

            if (Application.isEditor)
            {
                return;
            }

            var platform = Application.platform;
            var requiresPersistent = platform == RuntimePlatform.IPhonePlayer ||
                                     platform == RuntimePlatform.Android ||
                                     platform == RuntimePlatform.tvOS;
            if (!requiresPersistent)
            {
                return;
            }

            var persistentPath = Application.persistentDataPath;
            if (string.IsNullOrEmpty(persistentPath))
            {
                throw new InvalidOperationException("Application.persistentDataPath is not available on this platform.");
            }

            var fullPath = Path.GetFullPath(filePath);
            var persistentFull = Path.GetFullPath(persistentPath);

            if (!fullPath.StartsWith(persistentFull, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"File path '{filePath}' must be located under Application.persistentDataPath on mobile platforms.");
            }
        }

        public static string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }

            return $"{len:0.##} {sizes[order]}";
        }

        public static string FormatSpeed(double bytesPerSecond)
        {
            return $"{FormatFileSize((long)Math.Max(0, bytesPerSecond))}/s";
        }

        public void Cancel()
        {
            _manualCancellationSource.Cancel();
            _pauseSignal.Set();
        }

        public void Dispose()
        {
            if (_isDisposed) { return; }
            _isDisposed = true;

            _manualCancellationSource.Cancel();
            _pauseSignal.Set();

            _concurrencyLimiter?.Dispose();
            _manualCancellationSource.Dispose();
            _pauseCancellationSource.Dispose();

            lock (InstancesLock)
            {
                Instances.RemoveWhere(weak => !weak.TryGetTarget(out var target) || target == this);
            }
        }
    }
}
