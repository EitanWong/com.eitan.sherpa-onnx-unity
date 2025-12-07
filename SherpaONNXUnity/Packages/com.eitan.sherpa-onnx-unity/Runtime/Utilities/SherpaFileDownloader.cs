using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Eitan.SherpaONNXUnity.Runtime.Utilities
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
                    SherpaLog.Exception(ex);
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

    internal static class UnityApplicationEventWatcher
    {
        internal struct Options
        {
            public bool ListenToFocusChanged;
            public bool ListenToWantsToQuit;
            public bool ListenToQuitting;

            public static Options CreateDefault()
            {
                return new Options
                {
                    ListenToFocusChanged = false,
                    ListenToWantsToQuit = true,
                    ListenToQuitting = true
                };
            }
        }

        public static event Action<bool> PauseChanged;
        public static event Action<bool> FocusChanged;
        public static event Action QuitRequested;
        public static event Action ApplicationQuitting;

        private static readonly object InitLock = new object();
        private static Options _options = Options.CreateDefault();
        private static bool _initialized;
        private static bool _focusSubscribed;
        private static bool _pauseSubscribed;
        private static bool _wantsToQuitSubscribed;
        private static bool _quittingSubscribed;
        private static bool _pausedFromFocus;
        private static bool _pausedFromQuit;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            lock (InitLock)
            {
                UnsubscribeAll();
                _initialized = false;
                _pausedFromFocus = false;
                _pausedFromQuit = false;
                _options = Options.CreateDefault();
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void Initialize()
        {
            EnsureInitialized();
        }

        public static void EnsureInitialized()
        {
            lock (InitLock)
            {
                if (_initialized) { return; }

                SubscribeAll();
                _initialized = true;
            }
        }

        public static void Configure(Options options)
        {
            lock (InitLock)
            {
                _options = Normalize(options);
                if (_initialized)
                {
                    UnsubscribeAll();
                    SubscribeAll();
                }
            }
        }

        public static void Configure(
            bool? listenToFocusChanged = null,
            bool? listenToWantsToQuit = null,
            bool? listenToQuitting = null)
        {
            lock (InitLock)
            {
                var updated = _options;
                if (listenToFocusChanged.HasValue) { updated.ListenToFocusChanged = listenToFocusChanged.Value; }
                if (listenToWantsToQuit.HasValue) { updated.ListenToWantsToQuit = listenToWantsToQuit.Value; }
                if (listenToQuitting.HasValue) { updated.ListenToQuitting = listenToQuitting.Value; }

                _options = Normalize(updated);
                if (_initialized)
                {
                    UnsubscribeAll();
                    SubscribeAll();
                }
            }
        }

        private static Options Normalize(Options options)
        {
#if !UNITY_2019_1_OR_NEWER
            options.ListenToFocusChanged = false;
#endif
#if !UNITY_2017_2_OR_NEWER
            options.ListenToPauseState = false;
#endif
            return options;
        }

        private static void SubscribeAll()
        {
#if UNITY_2019_1_OR_NEWER
            if (_options.ListenToFocusChanged && !_focusSubscribed)
            {
                Application.focusChanged += OnFocusChanged;
                _focusSubscribed = true;
            }
#endif
            if (_options.ListenToWantsToQuit && !_wantsToQuitSubscribed)
            {
                Application.wantsToQuit += OnWantsToQuit;
                _wantsToQuitSubscribed = true;
            }

            if (_options.ListenToQuitting && !_quittingSubscribed)
            {
                Application.quitting += OnQuitting;
                _quittingSubscribed = true;
            }
        }

        private static void UnsubscribeAll()
        {
#if UNITY_2019_1_OR_NEWER
            if (_focusSubscribed)
            {
                Application.focusChanged -= OnFocusChanged;
                _focusSubscribed = false;
            }
#endif
            if (_wantsToQuitSubscribed)
            {
                Application.wantsToQuit -= OnWantsToQuit;
                _wantsToQuitSubscribed = false;
            }

            if (_quittingSubscribed)
            {
                Application.quitting -= OnQuitting;
                _quittingSubscribed = false;
            }
        }

#if UNITY_2019_1_OR_NEWER
        private static void OnFocusChanged(bool hasFocus)
        {
            _pausedFromFocus = !hasFocus;
            FocusChanged?.Invoke(hasFocus);
            RaisePauseChanged();
        }
#endif

        private static bool OnWantsToQuit()
        {
            _pausedFromQuit = true;
            RaisePauseChanged();
            QuitRequested?.Invoke();
            return true;
        }

        private static void OnQuitting()
        {
            _pausedFromQuit = true;
            RaisePauseChanged();
            ApplicationQuitting?.Invoke();
        }

        private static void RaisePauseChanged()
        {
            PauseChanged?.Invoke(_pausedFromFocus || _pausedFromQuit);
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
                _stream.Flush(flushToDisk: true);
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

    internal sealed class ChunkDownloadException : Exception
    {
        public int ChunkIndex { get; }
        public UnityWebRequest.Result Result { get; }
        public long ResponseCode { get; }
        public string WebError { get; }
        public bool IsTimeout { get; }
        public bool IsTransient { get; }

        public ChunkDownloadException(int chunkIndex, ChunkRequestResult outcome, string message)
            : base(message)
        {
            ChunkIndex = chunkIndex;
            Result = outcome.Result;
            ResponseCode = outcome.ResponseCode;
            WebError = outcome.Error;
            IsTimeout = DetermineTimeout(outcome);
            IsTransient = DetermineTransient(outcome);
        }

        private static bool DetermineTimeout(ChunkRequestResult outcome)
        {
            if (outcome.ResponseCode == 408)
            {
                return true;
            }

            if (outcome.Result != UnityWebRequest.Result.ConnectionError)
            {
                return false;
            }

            if (!string.IsNullOrEmpty(outcome.Error) &&
                outcome.Error.IndexOf("timed out", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            return false;
        }

        private static bool DetermineTransient(ChunkRequestResult outcome)
        {
            if (outcome.Result == UnityWebRequest.Result.ConnectionError ||
                outcome.Result == UnityWebRequest.Result.DataProcessingError)
            {
                return true;
            }

            if (outcome.ResponseCode == 0)
            {
                return true;
            }

            if (outcome.ResponseCode >= 500 || outcome.ResponseCode == 429)
            {
                return true;
            }

            return false;
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
        private static readonly object RetryRandomLock = new object();
        private static readonly System.Random RetryRandom = new System.Random(unchecked(Environment.TickCount * 397));
        private static readonly TimeSpan NetworkBackoffMinWaitSlice = TimeSpan.FromMilliseconds(250);
        private static readonly TimeSpan NetworkBackoffMaxWaitSlice = TimeSpan.FromSeconds(5);

        static SherpaFileDownloader()
        {
            UnityMainThreadScheduler.EnsureInitialized();
            UnityApplicationEventWatcher.EnsureInitialized();
            UnityApplicationEventWatcher.PauseChanged += HandleGlobalPauseChanged;
            UnityApplicationEventWatcher.ApplicationQuitting += HandleGlobalApplicationQuitting;
        }

        private readonly WeakReference<SherpaFileDownloader> _selfReference;
        private readonly SherpaONNXModelMetadata _modelMetadata;
        private readonly int _maxConcurrentChunks;
        private readonly long _defaultChunkSize;
        private readonly int _maxRetryAttempts;
        private readonly int _timeoutSeconds;
        private readonly string _userAgent;
        private readonly TimeSpan _baseRetryDelay = TimeSpan.FromSeconds(2);
        private volatile bool _wasCancelled;
        private volatile bool _cancelFeedbackSent;

        private readonly object _stateLock = new object();
        private readonly AsyncManualResetEvent _pauseSignal = new AsyncManualResetEvent(true);
        private readonly object _progressLock = new object();
        private readonly SemaphoreSlim _metadataWriteLock = new SemaphoreSlim(1, 1);
        private readonly object _networkResilienceLock = new object();

        private DownloadMetadata _metadata;
        private string _finalFilePath;
        private string _tempFilePath;
        private string _metadataFilePath;
        private string _chunkDirectory;

        private CancellationTokenSource _manualCancellationSource = new CancellationTokenSource();
        private CancellationTokenSource _pauseCancellationSource = new CancellationTokenSource();
        private readonly object _concurrencyLock = new object();
        private int _currentConcurrency;
        private int _consecutiveSuccessfulChunks;
        private int _consecutiveTransientFailures;
        private DateTime _networkBackoffUntilUtc = DateTime.MinValue;
        private int _currentTimeoutSeconds;
        private readonly int _maxTimeoutSeconds;

        private volatile bool _isPaused;
        private volatile bool _isDisposed;
        private volatile bool _shutdownRequested;
        private double _currentSpeed;
        private long _lastReportedBytes;
        private DateTime _lastProgressTimestamp = DateTime.UtcNow;

        public event Action<IFeedback> Feedback;

        public SherpaFileDownloader(
            SherpaONNXModelMetadata metadata = null,
            int maxConcurrentChunks = 4,
            long chunkSizeMB = 10,
            int maxRetryAttempts = 3,
            int timeoutSeconds = 60)
        {
            _modelMetadata = metadata;
            _maxConcurrentChunks = Mathf.Clamp(maxConcurrentChunks, 1, 8);
            _defaultChunkSize = Math.Max(1024 * 1024, chunkSizeMB * 1024 * 1024);
            _maxRetryAttempts = Mathf.Max(1, maxRetryAttempts);
            var initialConcurrency = ComputeAdaptiveConcurrencyHint();
            SetConcurrency(initialConcurrency);

            var platformTimeout = Application.platform == RuntimePlatform.IPhonePlayer || Application.platform == RuntimePlatform.Android
                ? Math.Max(timeoutSeconds, 120)
                : Math.Max(timeoutSeconds, 60);
            _timeoutSeconds = platformTimeout;
            _userAgent = BuildUserAgent();
            _currentTimeoutSeconds = _timeoutSeconds;
            _maxTimeoutSeconds = Mathf.Max(_timeoutSeconds, 600);

            _selfReference = new WeakReference<SherpaFileDownloader>(this);
            lock (InstancesLock)
            {
                Instances.Add(_selfReference);
            }
        }

        public bool WasCanceled => _wasCancelled;

        public async Task<bool> DownloadAsync(string url, string filePath, CancellationToken cancellationToken = default)
        {
            if (_isDisposed) { throw new ObjectDisposedException(nameof(SherpaFileDownloader)); }
            if (string.IsNullOrEmpty(url)) { throw new ArgumentNullException(nameof(url)); }
            if (string.IsNullOrEmpty(filePath)) { throw new ArgumentNullException(nameof(filePath)); }

            EnsureWritablePath(filePath);
            SherpaLog.Verbose($"[SherpaFileDownloader] Start download {( _modelMetadata?.modelId ?? "<unknown>")} from {url} -> {filePath}", category: "Download");

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
                        SherpaLog.Info($"[SherpaFileDownloader] Download complete for {_modelMetadata?.modelId ?? "<unknown>"} ({_finalFilePath})", category: "Download");
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
                _wasCancelled = true;
                ReportCancellationFeedback();
                SherpaLog.Warning($"[SherpaFileDownloader] Download canceled for {_modelMetadata?.modelId ?? "<unknown>"} ({url})", category: "Download");
                return false;
            }
            catch (Exception ex)
            {
                ReportProgress(ex.ToString());
                SherpaLog.Exception(ex, category: "Download", message: $"[SherpaFileDownloader] Download failed for {_modelMetadata?.modelId ?? "<unknown>"} from {url}");
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

            using var limiter = new SemaphoreSlim(concurrency, concurrency);
            var tasks = new List<Task>();
            foreach (var chunk in chunks)
            {
                await limiter.WaitAsync(userToken).ConfigureAwait(false);
                tasks.Add(RunChunkWithLimiterAsync(chunk, limiter, userToken));
            }

            await Task.WhenAll(tasks).ConfigureAwait(false);
        }

        private async Task RunChunkWithLimiterAsync(ChunkInfo chunk, SemaphoreSlim limiter, CancellationToken userToken)
        {
            try
            {
                await DownloadChunkWithRetryAsync(chunk, userToken).ConfigureAwait(false);
            }
            finally
            {
                limiter.Release();
            }
        }

        private async Task DownloadChunkWithRetryAsync(ChunkInfo chunk, CancellationToken userToken)
        {
            for (int attempt = 0; attempt < _maxRetryAttempts; attempt++)
            {
                userToken.ThrowIfCancellationRequested();
                await _pauseSignal.WaitAsync(userToken).ConfigureAwait(false);
                await WaitForNetworkHealthAsync(userToken).ConfigureAwait(false);

                using var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
                    userToken,
                    _pauseCancellationSource.Token);
                var token = linkedTokenSource.Token;

                try
                {
                    var outcome = await ExecuteChunkRequestAsync(chunk, token).ConfigureAwait(false);
                    token.ThrowIfCancellationRequested();

                    await ProcessChunkOutcomeAsync(chunk, outcome).ConfigureAwait(false);
                    chunk.ErrorMessage = null;
                    chunk.RetryCount = 0;
                    RegisterChunkSuccess();
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
                catch (Exception ex)
                {
                    if (await HandleChunkFailureAsync(chunk, ex, attempt, userToken).ConfigureAwait(false))
                    {
                        continue;
                    }

                    throw;
                }
            }

            var lastError = string.IsNullOrEmpty(chunk.ErrorMessage) ? string.Empty : $" Last error: {chunk.ErrorMessage}";
            throw new InvalidOperationException($"Chunk {chunk.Index} failed after {_maxRetryAttempts} attempts.{lastError}");
        }

        private async Task<bool> HandleChunkFailureAsync(ChunkInfo chunk, Exception exception, int attempt, CancellationToken userToken)
        {
            if (attempt >= _maxRetryAttempts - 1)
            {
                return false;
            }

            var shouldRetry = false;
            ChunkDownloadException chunkException = null;
            var resetChunk = false;

            if (exception is ChunkDownloadException downloadException)
            {
                chunkException = downloadException;
                shouldRetry = downloadException.IsTransient || downloadException.IsTimeout || downloadException.ResponseCode == 429;
            }
            else if (exception is InvalidOperationException ioe &&
                     ioe.Message.IndexOf("Content-Range mismatch", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                // A mismatched range usually means the on-disk chunk is stale or corrupt.
                // Reset the chunk so the next retry re-downloads it from scratch.
                resetChunk = true;
                shouldRetry = true;
            }
            else if (exception is IOException)
            {
                shouldRetry = true;
            }
            else if (exception is UnityException)
            {
                shouldRetry = true;
            }
            else
            {
                shouldRetry = true;
            }

            if (!shouldRetry)
            {
                return false;
            }

            RegisterTransientFailure(chunkException);

            var delay = GetBackoffDelay(attempt, exception);
            RecordNetworkFailure(exception, chunkException, delay);

            if (resetChunk)
            {
                ResetChunkFile(chunk);
            }

            chunk.ErrorMessage = exception.Message;
            chunk.RetryCount = attempt + 1;
            await SaveMetadataAsync().ConfigureAwait(false);

            SherpaLog.Warning($"[SherpaFileDownloader] Retrying chunk {chunk.Index} (attempt {chunk.RetryCount}/{_maxRetryAttempts}). Waiting {delay.TotalSeconds:0.##}s. Reason: {exception.Message}");
            await Task.Delay(delay, userToken).ConfigureAwait(false);
            return true;
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
                request.timeout = GetCurrentTimeoutSeconds();
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

            // SherpaLog.Info($"[SherpaFileDownloader] Chunk {chunk.Index} result={outcome.Result} code={outcome.ResponseCode} acceptRanges='{outcome.AcceptRanges}' contentRange='{outcome.ContentRange}'");

            if (outcome.Result == UnityWebRequest.Result.Success)
            {
                if (_metadata.SupportsRangeRequests)
                {
                    if (outcome.ResponseCode == 206)
                    {
                        var bytesFromThisRequest = Math.Max(0, outcome.BytesDownloaded);
                        var previouslyDownloaded = Math.Max(0, chunk.Downloaded - bytesFromThisRequest);
                        var expectedStart = chunk.Start + previouslyDownloaded;
                        var expectedEnd = chunk.End;

                        if (bytesFromThisRequest > 0)
                        {
                            ValidateContentRange(chunk, outcome.ContentRange, expectedStart, expectedEnd);
                        }

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

            throw new ChunkDownloadException(chunk.Index, outcome, $"Chunk {chunk.Index} download failed. Result: {outcome.Result}, Code: {outcome.ResponseCode}, Error: {outcome.Error}");
        }

        private async Task InitializeDownloadAsync(string url, string filePath, CancellationToken token)
        {
            _finalFilePath = filePath;
            _tempFilePath = filePath + DownloadTempFileExtension;
            _metadataFilePath = filePath + MetadataFileExtension;
            _chunkDirectory = filePath + ChunkDirectorySuffix;

            if (!File.Exists(_metadataFilePath) && Directory.Exists(_chunkDirectory))
            {
                CleanupDownloadArtifacts(includeMetadata: false);
            }

            if (File.Exists(_metadataFilePath))
            {
                try
                {
                    await LoadMetadataAsync().ConfigureAwait(false);
                    if (_metadata != null && string.Equals(_metadata.Url, url, StringComparison.OrdinalIgnoreCase))
                    {
                        CalculateDownloadedBytes();
                        SherpaLog.Trace($"[SherpaFileDownloader] Resuming download {_modelMetadata?.modelId ?? "<unknown>"} from persisted metadata. Bytes={_lastReportedBytes}/{_metadata.TotalSize}", category: "Download");
                        return;
                    }

                    SherpaLog.Warning($"Existing download metadata targets '{_metadata?.Url}' which does not match requested '{url}'. Resetting download state.");
                }
                catch (Exception ex)
                {
                    SherpaLog.Warning($"Failed to load metadata; starting new download. {ex}");
                }

                CleanupDownloadArtifacts(includeMetadata: true);
                _metadata = null;
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
                SetConcurrency(1);
            }

            SherpaLog.Trace(
                $"[SherpaFileDownloader] Prepared download {_modelMetadata?.modelId ?? "<unknown>"} size={fileSize} bytes chunks={_metadata.Chunks.Count} range={supportsRange}",
                category: "Download");

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
                    request.timeout = GetCurrentTimeoutSeconds();
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
            var finalDirectory = Path.GetDirectoryName(_finalFilePath);
            if (!string.IsNullOrEmpty(finalDirectory))
            {
                Directory.CreateDirectory(finalDirectory);
            }

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
            SherpaLog.Verbose($"[SherpaFileDownloader] Finalizing file {_finalFilePath} ({fileInfo.Length} bytes)", category: "Download");

            if (File.Exists(_finalFilePath))
            {
                File.Delete(_finalFilePath);
            }

            File.Move(_tempFilePath, _finalFilePath);

            CleanupDownloadArtifacts(includeMetadata: true);
            SherpaLog.Trace($"[SherpaFileDownloader] Cleanup complete for {_finalFilePath}", category: "Download");
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

        private void ReportCancellationFeedback()
        {
            if (_cancelFeedbackSent) { return; }
            _cancelFeedbackSent = true;
            Feedback?.Invoke(new CancelFeedback(_modelMetadata, message: "Download canceled."));
        }

        private void ValidateContentRange(ChunkInfo chunk, string contentRange, long expectedStart, long expectedEnd)
        {
            if (!TryParseContentRange(contentRange, out var start, out var end, out var total))
            {
                throw new InvalidOperationException($"Invalid Content-Range header: {contentRange}");
            }

            if (start != expectedStart || end != expectedEnd || total != _metadata.TotalSize)
            {
                throw new InvalidOperationException(
                    $"Content-Range mismatch for chunk {chunk.Index}. Expected {expectedStart}-{expectedEnd}/{_metadata.TotalSize}, got {contentRange}");
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

        private void CleanupDownloadArtifacts(bool includeMetadata)
        {
            TryDeleteFile(_tempFilePath);

            var workingDirectory = _metadata?.WorkingDirectory ?? _chunkDirectory;
            if (!string.IsNullOrEmpty(workingDirectory))
            {
                TryDeleteDirectory(workingDirectory);
                TryDeleteFile(workingDirectory + ".meta");
            }

            if (!string.IsNullOrEmpty(_chunkDirectory) &&
                !string.Equals(_chunkDirectory, workingDirectory, StringComparison.OrdinalIgnoreCase))
            {
                TryDeleteDirectory(_chunkDirectory);
                TryDeleteFile(_chunkDirectory + ".meta");
            }

            if (includeMetadata)
            {
                TryDeleteFile(_metadataFilePath);
            }
        }

        private static void TryDeleteFile(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            try
            {
                if (File.Exists(path))
                {
                    File.SetAttributes(path, FileAttributes.Normal);
                    File.Delete(path);
                }
            }
            catch (Exception ex)
            {
                SherpaLog.Warning($"[SherpaFileDownloader] Failed to delete file '{path}': {ex.Message}");
            }
        }

        private static void TryDeleteDirectory(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            try
            {
                var fullPath = Path.GetFullPath(path);
                if (string.IsNullOrEmpty(fullPath))
                {
                    return;
                }

                var root = Path.GetPathRoot(fullPath);
                if (!string.IsNullOrEmpty(root))
                {
                    var trimmedFull = fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    var trimmedRoot = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    if (string.Equals(trimmedFull, trimmedRoot, StringComparison.OrdinalIgnoreCase))
                    {
                        SherpaLog.Warning($"[SherpaFileDownloader] Refusing to delete root directory '{path}'.");
                        return;
                    }
                }

                if (Directory.Exists(fullPath))
                {
                    foreach (var directory in Directory.GetDirectories(fullPath, "*", SearchOption.AllDirectories))
                    {
                        try
                        {
                            File.SetAttributes(directory, FileAttributes.Normal);
                        }
                        catch
                        {
                            // Ignore attribute failures; deletion will report if necessary.
                        }
                    }

                    foreach (var file in Directory.GetFiles(fullPath, "*", SearchOption.AllDirectories))
                    {
                        try
                        {
                            File.SetAttributes(file, FileAttributes.Normal);
                        }
                        catch
                        {
                            // Ignore attribute failures; deletion will report if necessary.
                        }
                    }

                    Directory.Delete(fullPath, recursive: true);
                }
            }
            catch (Exception ex)
            {
                SherpaLog.Warning($"[SherpaFileDownloader] Failed to delete directory '{path}': {ex.Message}");
            }
        }

        private static void ReplaceFileAtomic(string sourcePath, string destinationPath)
        {
            if (string.IsNullOrEmpty(sourcePath))
            {
                throw new ArgumentNullException(nameof(sourcePath));
            }

            if (string.IsNullOrEmpty(destinationPath))
            {
                throw new ArgumentNullException(nameof(destinationPath));
            }

            try
            {
                if (File.Exists(destinationPath))
                {
                    File.Replace(sourcePath, destinationPath, null);
                }
                else
                {
                    File.Move(sourcePath, destinationPath);
                }
            }
            catch (PlatformNotSupportedException)
            {
                TryDeleteFile(destinationPath);
                File.Move(sourcePath, destinationPath);
            }
            catch (IOException)
            {
                TryDeleteFile(destinationPath);
                File.Move(sourcePath, destinationPath);
            }
        }

        private async Task SaveMetadataAsync()
        {
            if (_metadata == null) { return; }

            await _metadataWriteLock.WaitAsync().ConfigureAwait(false);
            string tempPath = null;
            try
            {
                _metadata.LastModifiedTime = DateTime.UtcNow;
                if (string.IsNullOrEmpty(_metadata.WorkingDirectory))
                {
                    _metadata.WorkingDirectory = _chunkDirectory;
                }

                var metadataDirectory = Path.GetDirectoryName(_metadataFilePath);
                if (!string.IsNullOrEmpty(metadataDirectory))
                {
                    Directory.CreateDirectory(metadataDirectory);
                }

                var json = JsonUtility.ToJson(_metadata, true);
                tempPath = _metadataFilePath + ".tmp";
                await File.WriteAllTextAsync(tempPath, json).ConfigureAwait(false);
                ReplaceFileAtomic(tempPath, _metadataFilePath);
            }
            finally
            {
                if (!string.IsNullOrEmpty(tempPath) && File.Exists(tempPath))
                {
                    TryDeleteFile(tempPath);
                }

                _metadataWriteLock.Release();
            }
        }

        private async Task LoadMetadataAsync()
        {
            var json = await File.ReadAllTextAsync(_metadataFilePath).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new InvalidOperationException("Download metadata file is empty.");
            }

            _metadata = JsonUtility.FromJson<DownloadMetadata>(json);
            if (_metadata == null || _metadata.Chunks == null || _metadata.Chunks.Count == 0)
            {
                throw new InvalidOperationException("Download metadata is corrupted or missing chunk information.");
            }

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
                SetConcurrency(1);
            }

            ReconcileChunksWithDisk();
        }

        private void ReconcileChunksWithDisk()
        {
            if (_metadata == null || _metadata.Chunks == null)
            {
                return;
            }

            foreach (var chunk in _metadata.Chunks)
            {
                if (string.IsNullOrEmpty(chunk.TempFileName))
                {
                    chunk.TempFileName = $"chunk_{chunk.Index:D4}.part";
                }

                var chunkPath = GetChunkFilePath(chunk);
                long lengthOnDisk = 0;

                if (File.Exists(chunkPath))
                {
                    try
                    {
                        var info = new FileInfo(chunkPath);
                        lengthOnDisk = info.Length;
                    }
                    catch (Exception ex)
                    {
                        SherpaLog.Warning($"[SherpaFileDownloader] Unable to inspect chunk file '{chunkPath}': {ex.Message}");
                    }
                }

                var expected = chunk.ExpectedLength;
                if (lengthOnDisk > expected)
                {
                    try
                    {
                        using var stream = new FileStream(chunkPath, FileMode.Open, FileAccess.Write, FileShare.None);
                        stream.SetLength(expected);
                        lengthOnDisk = expected;
                    }
                    catch (Exception ex)
                    {
                        SherpaLog.Warning($"[SherpaFileDownloader] Failed to trim chunk file '{chunkPath}' to expected length: {ex.Message}");
                        lengthOnDisk = expected;
                    }
                }

                chunk.Downloaded = Math.Min(lengthOnDisk, expected);
                chunk.IsCompleted = chunk.Downloaded >= expected;

                if (lengthOnDisk == 0 && !File.Exists(chunkPath))
                {
                    chunk.IsCompleted = false;
                }
            }

            CalculateDownloadedBytes();
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

            SetConcurrency(1);
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

        private static void HandleGlobalApplicationQuitting()
        {
            List<SherpaFileDownloader> aliveInstances;
            lock (InstancesLock)
            {
                aliveInstances = new List<SherpaFileDownloader>(Instances.Count);
                var dead = new List<WeakReference<SherpaFileDownloader>>();
                foreach (var weak in Instances)
                {
                    if (weak.TryGetTarget(out var downloader))
                    {
                        aliveInstances.Add(downloader);
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

            foreach (var downloader in aliveInstances)
            {
                try
                {
                    downloader.OnApplicationQuitting();
                }
                catch (Exception ex)
                {
                    SherpaLog.Exception(ex);
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

        private void OnApplicationQuitting()
        {
            if (_isDisposed || _shutdownRequested)
            {
                return;
            }

            _shutdownRequested = true;
            try
            {
                RequestPause();
            }
            catch (Exception ex)
            {
                SherpaLog.Exception(ex);
            }

            Cancel();

            try
            {
                Dispose();
            }
            catch (Exception ex)
            {
                SherpaLog.Exception(ex);
            }
        }

        private void SetConcurrency(int concurrency, bool resetCounters = true)
        {
            lock (_concurrencyLock)
            {
                _currentConcurrency = Mathf.Clamp(concurrency, 1, _maxConcurrentChunks);
                if (resetCounters)
                {
                    _consecutiveSuccessfulChunks = 0;
                    _consecutiveTransientFailures = 0;
                }
            }
        }

        private int GetAllowedConcurrency()
        {
            if (_metadata == null || !_metadata.SupportsRangeRequests)
            {
                return 1;
            }

            lock (_concurrencyLock)
            {
                return Mathf.Clamp(_currentConcurrency, 1, _maxConcurrentChunks);
            }
        }

        private int ComputeAdaptiveConcurrencyHint()
        {
            var cpuCount = Mathf.Max(1, SystemInfo.processorCount);
            var memoryMb = SystemInfo.systemMemorySize;
            var isMobile = Application.isMobilePlatform;
            var isEditor = Application.isEditor;

            int concurrency;
            if (isMobile)
            {
                concurrency = Mathf.Clamp(Mathf.CeilToInt(cpuCount / 2f), 1, Mathf.Min(3, _maxConcurrentChunks));
            }
            else
            {
                concurrency = Mathf.Clamp(cpuCount - 1, 1, _maxConcurrentChunks);
                if (isEditor || Application.isBatchMode)
                {
                    concurrency = Mathf.Clamp(cpuCount, 1, _maxConcurrentChunks);
                }
            }

            if (memoryMb > 0)
            {
                if (memoryMb < 1024)
                {
                    concurrency = 1;
                }
                else if (memoryMb < 2048)
                {
                    concurrency = Mathf.Min(concurrency, 2);
                }
            }

            return Mathf.Clamp(concurrency, 1, _maxConcurrentChunks);
        }

        private void RegisterChunkSuccess()
        {
            if (_metadata != null && _metadata.SupportsRangeRequests)
            {
                lock (_concurrencyLock)
                {
                    _consecutiveSuccessfulChunks++;
                    _consecutiveTransientFailures = 0;

                    if (_currentConcurrency < _maxConcurrentChunks)
                    {
                        var growthThreshold = Mathf.Clamp(_currentConcurrency * 2, 3, 10);
                        if (_consecutiveSuccessfulChunks >= growthThreshold)
                        {
                            _currentConcurrency = Mathf.Clamp(_currentConcurrency + 1, 1, _maxConcurrentChunks);
                            _consecutiveSuccessfulChunks = 0;
                        }
                    }
                }
            }

            RecordSuccessfulChunk();
        }

        private void RegisterTransientFailure(ChunkDownloadException exception = null)
        {
            if (_metadata == null || !_metadata.SupportsRangeRequests)
            {
                return;
            }

            lock (_concurrencyLock)
            {
                _consecutiveTransientFailures++;
                _consecutiveSuccessfulChunks = 0;

                var shouldReduce = _consecutiveTransientFailures >= 2;
                if (exception != null && (exception.IsTimeout || exception.ResponseCode == 429 || exception.ResponseCode >= 500))
                {
                    shouldReduce = true;
                }

                if (shouldReduce && _currentConcurrency > 1)
                {
                    _currentConcurrency = Mathf.Max(1, _currentConcurrency - 1);
                    _consecutiveTransientFailures = 0;
                }
            }
        }

        private async Task WaitForNetworkHealthAsync(CancellationToken token)
        {
            while (true)
            {
                DateTime backoffUntil;
                lock (_networkResilienceLock)
                {
                    backoffUntil = _networkBackoffUntilUtc;
                }

                var delay = backoffUntil - DateTime.UtcNow;
                if (delay <= TimeSpan.Zero)
                {
                    return;
                }

                var slice = delay > NetworkBackoffMaxWaitSlice ? NetworkBackoffMaxWaitSlice : delay;
                if (slice < NetworkBackoffMinWaitSlice)
                {
                    slice = delay;
                }

                try
                {
                    await Task.Delay(slice, token).ConfigureAwait(false);
                }
                catch (TaskCanceledException)
                {
                    token.ThrowIfCancellationRequested();
                }
            }
        }

        private void RecordNetworkFailure(Exception exception, ChunkDownloadException chunkException, TimeSpan retryDelay)
        {
            if (chunkException != null)
            {
                if (chunkException.IsTimeout)
                {
                    IncreaseTimeoutAfterFailure(chunkException);
                    ForceSequentialMode("Download timed out.");
                    var backoff = TimeSpan.FromSeconds(Math.Max(5, Math.Min(20, retryDelay.TotalSeconds * 1.5f)));
                    ScheduleNetworkBackoff(backoff, "timeout");
                    return;
                }

                if (chunkException.Result == UnityWebRequest.Result.ConnectionError || chunkException.ResponseCode == 0)
                {
                    IncreaseTimeoutAfterFailure(chunkException);
                    ForceSequentialMode("Connection dropped.");
                    var backoff = TimeSpan.FromSeconds(Math.Max(3, Math.Min(15, retryDelay.TotalSeconds + 1)));
                    ScheduleNetworkBackoff(backoff, "connection error");
                    return;
                }

                if (chunkException.ResponseCode == 429)
                {
                    var backoff = TimeSpan.FromSeconds(Math.Max(30, Math.Min(120, retryDelay.TotalSeconds * 2)));
                    ScheduleNetworkBackoff(backoff, "rate limited (429)");
                    return;
                }

                if (chunkException.ResponseCode >= 500)
                {
                    var backoff = TimeSpan.FromSeconds(Math.Max(5, Math.Min(30, retryDelay.TotalSeconds + 3)));
                    ScheduleNetworkBackoff(backoff, $"server error {chunkException.ResponseCode}");
                    return;
                }
            }

            if (exception is IOException)
            {
                var backoff = TimeSpan.FromSeconds(Math.Max(2, Math.Min(10, retryDelay.TotalSeconds)));
                ScheduleNetworkBackoff(backoff, "I/O error");
            }
        }

        private void ForceSequentialMode(string reason)
        {
            if (_metadata == null || !_metadata.SupportsRangeRequests)
            {
                return;
            }

            var switched = false;
            lock (_concurrencyLock)
            {
                if (_currentConcurrency > 1)
                {
                    _currentConcurrency = 1;
                    _consecutiveSuccessfulChunks = 0;
                    _consecutiveTransientFailures = 0;
                    switched = true;
                }
            }

            if (switched)
            {
                SherpaLog.Warning($"[SherpaFileDownloader] {reason} Switching to sequential chunk downloads until network stability improves.");
            }
        }

        private void IncreaseTimeoutAfterFailure(ChunkDownloadException exception)
        {
            lock (_networkResilienceLock)
            {
                var current = _currentTimeoutSeconds <= 0 ? _timeoutSeconds : _currentTimeoutSeconds;
                var increment = exception != null && exception.IsTimeout
                    ? Mathf.Max(15, current / 2)
                    : Mathf.Max(10, current / 4);
                var newTimeout = Mathf.Clamp(current + increment, _timeoutSeconds, _maxTimeoutSeconds);
                if (newTimeout > current)
                {
                    _currentTimeoutSeconds = newTimeout;
                    SherpaLog.Warning($"[SherpaFileDownloader] Increasing request timeout to {_currentTimeoutSeconds}s due to network instability.");
                }
            }
        }

        private void RecordSuccessfulChunk()
        {
            lock (_networkResilienceLock)
            {
                if (_currentTimeoutSeconds > _timeoutSeconds)
                {
                    var decrease = Mathf.Max(5, _currentTimeoutSeconds / 10);
                    _currentTimeoutSeconds = Mathf.Max(_timeoutSeconds, _currentTimeoutSeconds - decrease);
                }

                if (_networkBackoffUntilUtc > DateTime.UtcNow)
                {
                    _networkBackoffUntilUtc = DateTime.UtcNow;
                }
            }
        }

        private void ScheduleNetworkBackoff(TimeSpan backoff, string reason)
        {
            if (backoff <= TimeSpan.Zero)
            {
                return;
            }

            var candidateUntil = DateTime.UtcNow.Add(backoff);
            DateTime previousUntil;
            var updated = false;
            lock (_networkResilienceLock)
            {
                previousUntil = _networkBackoffUntilUtc;
                if (candidateUntil > _networkBackoffUntilUtc)
                {
                    _networkBackoffUntilUtc = candidateUntil;
                    updated = true;
                }
            }

            if (updated && (candidateUntil - previousUntil) > TimeSpan.FromSeconds(0.5))
            {
                SherpaLog.Warning($"[SherpaFileDownloader] Network instability detected ({reason}). Backing off for {Math.Min(backoff.TotalSeconds, 120):0.##}s.");
            }
        }

        private int GetCurrentTimeoutSeconds()
        {
            var current = Volatile.Read(ref _currentTimeoutSeconds);
            if (current <= 0)
            {
                current = _timeoutSeconds;
            }

            var max = _maxTimeoutSeconds > 0 ? _maxTimeoutSeconds : int.MaxValue;
            return Mathf.Clamp(current, 1, max);
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

        private TimeSpan GetBackoffDelay(int attempt, Exception exception = null)
        {
            var exponent = Math.Pow(2, Math.Min(attempt, 5));
            var baseSeconds = _baseRetryDelay.TotalSeconds * exponent;

            if (exception is ChunkDownloadException chunkException)
            {
                if (chunkException.IsTimeout)
                {
                    baseSeconds = Math.Max(baseSeconds, 5);
                }
                else if (chunkException.ResponseCode == 429)
                {
                    baseSeconds = Math.Max(baseSeconds, 10);
                }
                else if (chunkException.ResponseCode >= 500)
                {
                    baseSeconds = Math.Max(baseSeconds, 6);
                }
            }

            var jitter = 0.65 + GetRetryJitter() * 0.7;
            var seconds = Math.Min(60, baseSeconds * jitter);
            return TimeSpan.FromSeconds(seconds);
        }

        private double GetRetryJitter()
        {
            lock (RetryRandomLock)
            {
                return RetryRandom.NextDouble();
            }
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
                SherpaLog.Warning($"Downloader received invalid URL: {url}");
                return;
            }

            if (!uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                SherpaLog.Warning($"URL '{url}' is not HTTPS. Ensure ATS exceptions are configured if targeting iOS.");
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
            _wasCancelled = true;
            _manualCancellationSource.Cancel();
            _pauseSignal.Set();
        }

        public void Dispose()
        {
            if (_isDisposed) { return; }
            _isDisposed = true;

            _manualCancellationSource.Cancel();
            _pauseSignal.Set();

            _manualCancellationSource.Dispose();
            _pauseCancellationSource.Dispose();

            lock (InstancesLock)
            {
                Instances.RemoveWhere(weak => !weak.TryGetTarget(out var target) || target == this);
            }
        }
    }
}
