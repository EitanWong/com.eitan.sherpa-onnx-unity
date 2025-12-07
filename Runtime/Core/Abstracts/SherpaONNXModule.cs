// SherpaONNXModule.cs (Optimized)

namespace Eitan.SherpaONNXUnity.Runtime
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Reflection;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;
    using Eitan.SherpaONNXUnity.Runtime.Utilities;

    public abstract class SherpaONNXModule : IDisposable
    {
        protected abstract SherpaONNXModuleType ModuleType { get; }

        private static readonly List<WeakReference<SherpaONNXModule>> s_LiveModules = new List<WeakReference<SherpaONNXModule>>();
        private static readonly object s_LiveModulesLock = new object();
        private static bool s_AppQuitHooked;
        private static readonly ConcurrentDictionary<Type, FieldInfo> s_HandleFieldCache = new ConcurrentDictionary<Type, FieldInfo>();
        private static readonly ConcurrentDictionary<Type, byte> s_HandleResolutionWarnings = new ConcurrentDictionary<Type, byte>();
        private static readonly string[] s_HandleFieldCandidates = new[] { "_handle", "handle", "Handle", "_Handle" };

        private readonly SynchronizationContext _rootThreadContext;
        private readonly object _lockObject = new object();
        private readonly object _initLock = new object();
        private readonly string _modelId;
        private readonly int _sampleRate;
        private readonly SherpaONNXFeedbackReporter _externalReporter;
        private readonly bool _isMobilePlatform;
        private readonly DateTime _createdUtc = DateTime.UtcNow;
        private int _disposeState;
        private int _onDestroyInvoked;

        private Task _initializationTask;
        private CancellationTokenSource _initCts;
        private bool _initializationStarted;
        private Exception _initializationException;
        private DateTime _lastStatusUtc;

        protected readonly TaskRunner runner;

        // --- 统一的、线程安全的销毁标志 ---
        protected bool IsDisposed { get; private set; }

        public bool Initialized { get; private set; }
        public bool InitializationStarted => _initializationStarted;
        public string ModelId => _modelId;
        public Exception InitializationException => _initializationException;
        public DateTime LastStatusUtc => _lastStatusUtc;

        public InitializationStatus GetInitializationStatus()
        {
            return new InitializationStatus(Initialized, InitializationStarted, _initializationException, _lastStatusUtc == default ? _createdUtc : _lastStatusUtc);
        }

        /// <summary>
        /// Allows callers to await the initialization task or trigger initialization later.
        /// </summary>
        public Task InitializationTask => _initializationTask ?? Task.CompletedTask;

        protected void TraceLifecycle(string message, [CallerMemberName] string caller = null)
        {
            SherpaLog.Trace(
                $"[{ModuleType}] {caller}: {message} (modelId={_modelId}, thread={Thread.CurrentThread.ManagedThreadId})",
                category: "Lifecycle",
                includeStackTrace: true);
        }

        public SherpaONNXModule(string modelID, int sampleRate = 16000, SherpaONNXFeedbackReporter reporter = null, bool startImmediately = true, int maxConcurrentTasks = 0)
        {
            _rootThreadContext = SynchronizationContext.Current;
            // Pre-warm Unity download infrastructure on the main thread so background tasks can safely use UnityWebRequest.
            SherpaUtils.Prepare.EnsureUnityThreadInfrastructure();
            runner = new TaskRunner(maxConcurrentTasks);

            _modelId = modelID ?? throw new ArgumentNullException(nameof(modelID));
            _sampleRate = sampleRate;
            _externalReporter = reporter;
            _isMobilePlatform = UnityEngine.Application.isMobilePlatform;
            _lastStatusUtc = DateTime.UtcNow;

            RegisterInstance(this);

            if (startImmediately)
            {
                StartInitialization();
            }
        }

        /// <summary>
        /// Manually start initialization (useful when caller needs to configure module before load).
        /// Safe to call multiple times; only the first call executes.
        /// </summary>
        public Task StartInitialization(CancellationToken cancellationToken = default)
        {
            if (IsDisposed || Volatile.Read(ref _disposeState) != 0)
            {
                throw new ObjectDisposedException(GetType().Name);
            }

            lock (_initLock)
            {
                if (_initializationTask != null)
                {
                    return _initializationTask;
                }

                if (_initializationStarted)
                {
                    return _initializationTask ?? Task.CompletedTask;
                }

                _initializationStarted = true;
                _lastStatusUtc = DateTime.UtcNow;
                _initializationException = null;

                TraceLifecycle("Initialization requested");
                _initCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

                // Keep initialization on the captured Unity context so UnityWebRequest/StreamingAssets
                // work the same as the previous version. Offloading to a thread pool (ExecutionPolicy.Auto)
                // breaks Unity-only APIs that expect the main thread (manifest fetch, downloader).
                _initializationTask = runner.RunAsync(async (ct) =>
                {
                    await InitializeInternalAsync(ct).ConfigureAwait(false);
                }, cancellationToken: _initCts.Token, policy: ExecutionPolicy.Never);

                _ = _initializationTask.ContinueWith(_ =>
                {
                    _initCts?.Dispose();
                    _initCts = null;
                }, TaskScheduler.Default);
            }

            return _initializationTask ?? Task.CompletedTask;
        }

        private async Task InitializeInternalAsync(CancellationToken ct)
        {
            // 在启动时检查是否已经被销毁 (例如，对象创建后立即被销毁)
            if (IsDisposed)
            {
                return;
            }
            ct.ThrowIfCancellationRequested();
            _lastStatusUtc = DateTime.UtcNow;
            TraceLifecycle("InitializeInternalAsync enter");
            var reporterAdapter = new SherpaONNXFeedbackReporter(feedbackArgs =>
            {
                // 使用 _isDisposed 标志位进行更可靠的检查
                if (IsDisposed || runner.IsDisposed) { return; }

                ExecuteOnMainThread(state =>
                {
                    if (IsDisposed || runner.IsDisposed) { return; }
                    try
                    {
                        _externalReporter?.Report((IFeedback)state);
                    }
                    catch (Exception e)
                    {
                        SherpaLog.Exception(e, category: "Feedback");
                    }
                }, feedbackArgs);
            });

            var metadata = await SherpaONNXModelRegistry.Instance.GetMetadataAsync(_modelId, ct).ConfigureAwait(false);

            try
            {
                TraceLifecycle($"Preparing model '{_modelId}'");
                var prepareResult = await SherpaUtils.Prepare.PrepareAndLoadModelAsync(metadata, reporterAdapter, ct).ConfigureAwait(false);

                if (prepareResult)
                {

                    reporterAdapter?.Report(new PrepareFeedback(metadata, message: $"{ModuleType} model:{_modelId} ready to init"));

                    TraceLifecycle("Running module-specific initialization");
                    Initialized = await Initialization(metadata, _sampleRate, _isMobilePlatform, reporterAdapter, ct).ConfigureAwait(false);
                    _lastStatusUtc = DateTime.UtcNow;

                    // 初始化成功后再次检查，防止在初始化过程中被销毁
                    if (ct.IsCancellationRequested || IsDisposed)
                    {
                        var cancelled = new OperationCanceledException("Initialization was cancelled or disposed.", ct);
                        reporterAdapter?.Report(new CancelFeedback(metadata, message: cancelled.Message));
                        _initializationException = cancelled;
                        throw cancelled;
                    }
                    if (Initialized)
                    {
                        TraceLifecycle("Initialization succeeded");
                        reporterAdapter?.Report(new SuccessFeedback(metadata, message: $"{ModuleType} model:{_modelId} init success"));
                        _initializationException = null;
                    }
                    else
                    {
                        var initFailed = new InvalidOperationException($"{ModuleType} model:{_modelId} init failed");
                        _initializationException = initFailed;
                        TraceLifecycle($"Initialization failed: {initFailed.Message}");
                        reporterAdapter?.Report(new FailedFeedback(metadata, message: initFailed.Message));
                        throw initFailed;
                    }

                }
                else
                {
                    var prepareFailed = new InvalidOperationException($"Model {metadata.modelId} initialization failed\nplease download from url:{metadata.downloadUrl}\nthen uncompress it to {SherpaUtils.Model.GetModuleTypeByModelId(metadata.modelId)} manually.");
                    _initializationException = prepareFailed;
                    TraceLifecycle($"Prepare phase failed: {prepareFailed.Message}");
                    throw prepareFailed;
                }

            }
            catch (OperationCanceledException oce)
            {
                _initializationException = oce;
                _lastStatusUtc = DateTime.UtcNow;
                TraceLifecycle($"Initialization cancelled: {oce.Message}");
                reporterAdapter?.Report(new CancelFeedback(metadata, message: oce.Message));
                throw;
            }
            catch (Exception ex)
            {
                try
                {
                    SherpaLog.Error(
                        $"[{GetType().Name}] Initialization failed for model '{_modelId}'. Thread={Thread.CurrentThread.ManagedThreadId} IsMainThread={(SynchronizationContext.Current?.GetType().Name ?? "<null>")}",
                        ex,
                        category: "Lifecycle",
                        includeStackTrace: true);
                }
                catch
                {
                    // ignore logging failures
                }
                _initializationException = ex;
                _lastStatusUtc = DateTime.UtcNow;
                TraceLifecycle($"Initialization exception: {ex.GetType().Name}: {ex.Message}");
                reporterAdapter?.Report(new FailedFeedback(metadata, message: ex.Message, exception: ex));
                TryCleanupCorruptedModel(metadata, ex);
                throw;
            }
        }
        private static readonly string[] s_CorruptionMarkers = new[]
        {
            "protobuf",
            "onnx",
            "invalid argument",
            "invalidargument",
            "corrupt",
            "corrupted",
            "checksum",
            "hash mismatch",
            "parse",
            "parsing",
        };

        /// <summary>
        /// Attempts to delete model artifacts when initialization fails due to likely corruption,
        /// so the next prepare can re-download clean data. Avoids running on unrelated errors.
        /// </summary>
        private void TryCleanupCorruptedModel(SherpaONNXModelMetadata metadata, Exception ex)
        {
            if (metadata == null || ex == null)
            {
                return;
            }

            var message = ex.ToString();
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            var lower = message.ToLowerInvariant();
            var isCorruption = false;
            foreach (var marker in s_CorruptionMarkers)
            {
                if (lower.Contains(marker))
                {
                    isCorruption = true;
                    break;
                }
            }

            if (!isCorruption)
            {
                return;
            }

            try
            {
                var downloadPath = SherpaUtils.Prepare.ResolveDownloadFilePath(metadata, out _, out _, out _, out _);
                var modelRoot = Utilities.SherpaPathResolver.GetModelRootPath(metadata.modelId);
                SherpaFileUtils.DeleteModelArtifacts(modelRoot, downloadPath);
                SherpaLog.Warning($"[{ModuleType}] Detected likely corrupted model for {metadata.modelId}; cleaned local artifacts to force re-download.");
            }
            catch (Exception cleanupEx)
            {
                SherpaLog.Warning($"[{ModuleType}] Failed to clean corrupted model artifacts: {cleanupEx.Message}");
            }
        }

        // --- 实现完整的、标准的 IDisposable 模式 ---
        public void Dispose()
        {
            // 这是供外部调用的标准 Dispose 方法
            Dispose(true);
            // 请求垃圾回收器不要调用终结器（析构函数），因为我们已经手动清理了
            GC.SuppressFinalize(this);
        }

        // 析构函数，作为最后的安全网。它会在对象被GC回收时调用
        ~SherpaONNXModule()
        {
            Dispose(false);
        }

        protected bool IsSuccessInitializad(object target, string handleFieldName = "_handle")
        {
            if (target == null)
            {
                return false;
            }

            var targetType = target.GetType();
            var fieldInfo = s_HandleFieldCache.GetOrAdd(targetType, type => ResolveHandleField(type, handleFieldName));

            if (fieldInfo == null)
            {
                // Avoid throwing in production; log once for diagnostics and consider the object valid.
                if (s_HandleResolutionWarnings.TryAdd(targetType, 0))
                {
                    try
                    {
                        SherpaLog.Warning($"[{ModuleType}] Unable to locate native handle field on {targetType.FullName}. Assuming valid to avoid crashes.", category: "Handle");
                    }
                    catch
                    {
                        // Swallow logging failures (e.g., outside Unity context).
                    }
                }
                return true;
            }

            try
            {
                var fieldValue = fieldInfo.GetValue(target);
                switch (fieldValue)
                {
                    case System.Runtime.InteropServices.HandleRef handleRef:
                        return handleRef.Handle != IntPtr.Zero;
                    case IntPtr ptr:
                        return ptr != IntPtr.Zero;
                    default:
                        return fieldValue != null;
                }
            }
            catch (Exception ex)
            {
                if (s_HandleResolutionWarnings.TryAdd(targetType, 0))
                {
                    try
                    {
                        SherpaLog.Warning($"[{ModuleType}] Failed to inspect native handle for {targetType.FullName}: {ex.Message}", ex, category: "Handle");
                    }
                    catch
                    {
                        // Ignore logging failures.
                    }
                }
                return true;
            }
        }

        protected void ExecuteOnMainThread(SendOrPostCallback callback, object args = null)
        {
            if (_rootThreadContext != null)
            {

                _rootThreadContext.Post(callback, args);
            }
            else
            {
                SherpaLog.Error("The main thread context not exist, can't execute callback on main thread", category: "Threading");
            }
        }
        protected SendOrPostCallback CreateCallback<T>(Action<T> handler)
        {
            return state =>
            {
#if UNITY_2020_3_OR_NEWER
                // 新版 Unity：支持模式匹配
                if (state is T value)
                {
                    handler?.Invoke(value);
                }
#else
        // 兼容旧版：先判断类型，再强制转换
        if (state is T)
        {
            var value = (T)state;
            handler?.Invoke(value);
        }
#endif
            };
        }
        protected void SafeExecute(Action action)
        {
            lock (_lockObject)
            {
                action?.Invoke();
            }
        }

        protected Action<string> CreateFallbackReporter(SherpaONNXModelMetadata metadata, SherpaONNXFeedbackReporter reporter)
        {
            return message =>
            {
                if (string.IsNullOrWhiteSpace(message))
                {
                    return;
                }

                var formattedMessage = $"[{ModuleType}] {message}";
                SherpaLog.Warning(formattedMessage, category: "Feedback");
                reporter?.Report(new LoadFeedback(metadata, message: formattedMessage));
            };
        }

        private void Dispose(bool disposing)
        {
            if (Interlocked.Exchange(ref _disposeState, 1) != 0)
            {
                return;
            }

            IsDisposed = true;
            TraceLifecycle("Dispose invoked");
            UnregisterInstance(this);

            var initTask = _initializationTask;

            try { _initCts?.Cancel(); } catch { }
            try { runner?.CancelAll(); } catch { }

            // Run cleanup asynchronously to avoid blocking the calling thread (especially the editor main thread).
            _ = Task.Run(async () =>
            {
                TryWaitInitialization(initTask, timeoutMs: 750);

                try
                {
                    if (runner != null)
                    {
                        await runner.WaitForAllAsync(1500).ConfigureAwait(false);
                    }
                }
                catch
                {
                    // Swallow cancellations/timeouts on shutdown.
                }

                // 调用子类的清理方法，释放非托管资源 (Native)
                if (initTask == null || initTask.IsCompleted)
                {
                    InvokeOnDestroySafe();
                }
                else
                {
                    // Ensure OnDestroy is invoked once initialization finishes/cancels.
                    _ = initTask.ContinueWith(_ => InvokeOnDestroySafe(), TaskScheduler.Default);
                }

                if (disposing)
                {
                    try { runner?.Dispose(); } catch { }
                    try { _initCts?.Dispose(); } catch { }
                    _initCts = null;
                }
            });
        }

        private static void RegisterInstance(SherpaONNXModule instance)
        {
            lock (s_LiveModulesLock)
            {
                s_LiveModules.Add(new WeakReference<SherpaONNXModule>(instance));
                if (!s_AppQuitHooked && IsUnityContextSafe())
                {
                    try
                    {
                        UnityEngine.Application.quitting += HandleApplicationQuitting;
                        s_AppQuitHooked = true;
                    }
                    catch
                    {
                        // Ignore if not in Unity context.
                    }
                }
            }
        }

        private static void UnregisterInstance(SherpaONNXModule instance)
        {
            lock (s_LiveModulesLock)
            {
                s_LiveModules.RemoveAll(weak =>
                {
                    if (!weak.TryGetTarget(out var target))
                    {
                        return true;
                    }

                    return ReferenceEquals(target, instance);
                });

                if (s_LiveModules.Count == 0 && s_AppQuitHooked && IsUnityContextSafe())
                {
                    try
                    {
                        UnityEngine.Application.quitting -= HandleApplicationQuitting;
                    }
                    catch
                    {
                        // Ignore unsubscription failures.
                    }
                    s_AppQuitHooked = false;
                }
            }
        }

        private static void HandleApplicationQuitting()
        {
            List<WeakReference<SherpaONNXModule>> snapshot;
            lock (s_LiveModulesLock)
            {
                snapshot = new List<WeakReference<SherpaONNXModule>>(s_LiveModules);
            }

            foreach (var weak in snapshot)
            {
                if (weak.TryGetTarget(out var module))
                {
                    try
                    {
                        module.Dispose();
                    }
                    catch (Exception ex)
                    {
                        try
                        {
                            SherpaLog.Exception(ex, category: "Lifecycle");
                        }
                        catch
                        {
                            // Swallow logging errors on shutdown.
                        }
                    }
                }
            }
        }

        private static bool IsUnityContextSafe()
        {
            try
            {
                return UnityEngine.Application.isPlaying;
            }
            catch
            {
                return false;
            }
        }

        public readonly struct InitializationStatus
        {
            public InitializationStatus(bool initialized, bool initializationStarted, Exception initializationException, DateTime lastStatusUtc)
            {
                Initialized = initialized;
                InitializationStarted = initializationStarted;
                InitializationException = initializationException;
                LastStatusUtc = lastStatusUtc;
            }

            public bool Initialized { get; }
            public bool InitializationStarted { get; }
            public Exception InitializationException { get; }
            public DateTime LastStatusUtc { get; }
            public bool HasError => InitializationException != null;
        }

        public readonly struct ModuleDiagnostics
        {
            public ModuleDiagnostics(
                string modelId,
                SherpaONNXModuleType moduleType,
                bool initialized,
                bool disposed,
                bool initializationStarted,
                bool runnerDisposed,
                int activeTasks,
                Exception initializationException,
                DateTime lastStatusUtc,
                Utilities.TaskRunnerMetrics runnerMetrics)
            {
                ModelId = modelId;
                ModuleType = moduleType;
                Initialized = initialized;
                Disposed = disposed;
                InitializationStarted = initializationStarted;
                RunnerDisposed = runnerDisposed;
                ActiveTasks = activeTasks;
                InitializationException = initializationException;
                LastStatusUtc = lastStatusUtc;
                RunnerMetrics = runnerMetrics;
            }

            public string ModelId { get; }
            public SherpaONNXModuleType ModuleType { get; }
            public bool Initialized { get; }
            public bool Disposed { get; }
            public bool InitializationStarted { get; }
            public bool RunnerDisposed { get; }
            public int ActiveTasks { get; }
            public Exception InitializationException { get; }
            public DateTime LastStatusUtc { get; }
            public Utilities.TaskRunnerMetrics RunnerMetrics { get; }
            public bool HasError => InitializationException != null;
        }

        public ModuleDiagnostics GetDiagnostics()
        {
            return new ModuleDiagnostics(
                ModelId,
                ModuleType,
                Initialized,
                IsDisposed,
                InitializationStarted,
                runner?.IsDisposed ?? true,
                runner?.ActiveTaskCount ?? 0,
                _initializationException,
                _lastStatusUtc == default ? _createdUtc : _lastStatusUtc,
                runner?.GetMetrics() ?? default);
        }

        public static List<ModuleDiagnostics> GetLiveModuleDiagnostics()
        {
            var results = new List<ModuleDiagnostics>();
            lock (s_LiveModulesLock)
            {
                for (int i = s_LiveModules.Count - 1; i >= 0; i--)
                {
                    var weak = s_LiveModules[i];
                    if (!weak.TryGetTarget(out var module))
                    {
                        s_LiveModules.RemoveAt(i);
                        continue;
                    }

                    results.Add(module.GetDiagnostics());
                }
            }

            return results;
        }

        /// <summary>
        /// 子类必须实现的初始化逻辑。
        /// </summary>
        protected abstract Task<bool> Initialization(SherpaONNXModelMetadata metadata, int sampleRate, bool isMobilePlatform, SherpaONNXFeedbackReporter reporter, CancellationToken ct);

        /// <summary>
        /// 子类必须实现的资源清理逻辑。
        /// </summary>
        protected abstract void OnDestroy();

        private static FieldInfo ResolveHandleField(Type targetType, string preferredFieldName)
        {
            const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance;

            if (!string.IsNullOrEmpty(preferredFieldName))
            {
                var preferred = targetType.GetField(preferredFieldName, flags);
                if (preferred != null)
                {
                    return preferred;
                }
            }

            foreach (var candidate in s_HandleFieldCandidates)
            {
                var field = targetType.GetField(candidate, flags);
                if (field != null)
                {
                    return field;
                }
            }

            return null;
        }

        private static void TryWaitInitialization(Task initTask, int timeoutMs = 3000)
        {
            if (initTask == null || initTask.IsCompleted)
            {
                return;
            }

            try
            {
                Task.WhenAny(initTask, Task.Delay(timeoutMs)).GetAwaiter().GetResult();
            }
            catch
            {
                // Ignore wait failures.
            }
        }

        private void InvokeOnDestroySafe()
        {
            if (Interlocked.Exchange(ref _onDestroyInvoked, 1) != 0)
            {
                return;
            }

            try
            {
                if (_rootThreadContext != null)
                {
                    _rootThreadContext.Post(_ => SafeOnDestroy(), null);
                    return;
                }
            }
            catch
            {
                // fall through
            }

            SafeOnDestroy();
        }

        private void SafeOnDestroy()
        {
            try
            {
                OnDestroy();
            }
            catch (Exception ex)
            {
                try { SherpaLog.Exception(ex, category: "Dispose"); } catch { }
            }
        }
    }
}
