using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;

namespace Eitan.SherpaONNXUnity.Runtime.Utilities
{
    /// <summary>
    /// Controls how TaskRunner decides to offload work to a background thread.
    /// </summary>
    public enum ExecutionPolicy
    {
        /// <summary>
        /// Automatically offload when invoked from the main thread; otherwise execute inline.
        /// </summary>
        Auto,
        /// <summary>
        /// Always offload to a background thread.
        /// </summary>
        Always,
        /// <summary>
        /// Never offload automatically; execute inline (current behavior).
        /// </summary>
        Never
    }

    /// <summary>
    /// Thread-safe task runner with automatic cleanup and Unity integration.
    /// Provides controlled task execution with proper resource management.
    /// </summary>
    public sealed class TaskRunner : IDisposable
    {
        private readonly SynchronizationContext _mainContext;
        private readonly ConcurrentDictionary<Task, byte> _activeTasks = new();
        private readonly SemaphoreSlim _concurrencyLimiter;
        private readonly Timer _cleanupTimer;
        private readonly int _mainThreadId;
        private int _totalTasksStarted;
        private int _completedTasks;
        private long _totalDurationTicks;
        private double _lastDurationMs;
        private static int s_profilingEnabled;

        private CancellationTokenSource _globalCts = new();
        private volatile bool _disposed;

        // Configuration
        private readonly int _maxConcurrentTasks;
        private const int CleanupIntervalMs = 10000; // 10 seconds

        public TaskRunner(int maxConcurrentTasks = 0)
        {
            _maxConcurrentTasks = maxConcurrentTasks > 0
                ? maxConcurrentTasks
                : ComputeAdaptiveConcurrency();
            _mainContext = SynchronizationContext.Current ?? new SynchronizationContext();
            _mainThreadId = Thread.CurrentThread.ManagedThreadId;
            _concurrencyLimiter = new SemaphoreSlim(_maxConcurrentTasks, _maxConcurrentTasks);

            // Periodic cleanup of completed tasks
            _cleanupTimer = new Timer(CleanupCompletedTasks, null,
                TimeSpan.FromMilliseconds(CleanupIntervalMs),
                TimeSpan.FromMilliseconds(CleanupIntervalMs));
        }

        /// <summary>
        /// Gets the number of currently active tasks
        /// </summary>
        public int ActiveTaskCount => _activeTasks.Count;

        /// <summary>
        /// Gets the configured concurrency limit for this runner.
        /// </summary>
        public int MaxConcurrentTasks => _maxConcurrentTasks;

        /// <summary>
        /// Returns a snapshot of current task metrics for diagnostics/profiling.
        /// </summary>
        public TaskRunnerMetrics GetMetrics()
        {
            if (!ProfilingEnabled)
            {
                return default;
            }

            var completed = Volatile.Read(ref _completedTasks);
            var totalStarted = Volatile.Read(ref _totalTasksStarted);
            var durationTicks = Interlocked.Read(ref _totalDurationTicks);
            var avgMs = completed > 0
                ? TimeSpan.FromTicks(durationTicks / Math.Max(1, completed)).TotalMilliseconds
                : 0d;

            return new TaskRunnerMetrics(
                ActiveTaskCount,
                totalStarted,
                completed,
                avgMs,
                Volatile.Read(ref _lastDurationMs));
        }

        /// <summary>
        /// Gets whether the runner has been disposed
        /// </summary>
        public bool IsDisposed => _disposed;

        /// <summary>
        /// Runs an async operation with proper resource management and error handling
        /// </summary>
        public async Task<T> RunAsync<T>(Func<CancellationToken, Task<T>> asyncFunc,
                                       Action<Exception> onComplete = null,
                                       CancellationToken cancellationToken = default,
                                       ExecutionPolicy policy = ExecutionPolicy.Auto)
        {
            ThrowIfDisposed();
            ValidateInput(asyncFunc, nameof(asyncFunc));

            await _concurrencyLimiter.WaitAsync(cancellationToken).ConfigureAwait(false);

            using var linkedCts = CreateLinkedTokenSource(cancellationToken);

            // Decide whether to offload to a background thread
            Task<T> task;
            var profiling = ProfilingEnabled;
            if (profiling)
            {
                Interlocked.Increment(ref _totalTasksStarted);
            }
            if (ShouldOffload(policy))
            {
                task = Task.Run(() => ExecuteWithCleanup(asyncFunc, onComplete, linkedCts.Token), linkedCts.Token);
            }
            else
            {
                task = ExecuteWithCleanup(asyncFunc, onComplete, linkedCts.Token);
            }

            TrackTask(task);
            return await task.ConfigureAwait(false);
        }

        /// <summary>
        /// Runs an async operation without return value
        /// </summary>
        public Task RunAsync(Func<CancellationToken, Task> asyncAction,
                           Action<Exception> onComplete = null,
                           CancellationToken cancellationToken = default,
                           ExecutionPolicy policy = ExecutionPolicy.Auto)
        {
            return RunAsync(async ct => { await asyncAction(ct).ConfigureAwait(false); return true; }, onComplete, cancellationToken, policy);
        }

        /// <summary>
        /// Runs a synchronous operation on a background thread
        /// </summary>
        public Task RunAsync(Action<CancellationToken> action,
                           Action<Exception> onComplete = null,
                           CancellationToken cancellationToken = default,
                           ExecutionPolicy policy = ExecutionPolicy.Auto)
        {
            ValidateInput(action, nameof(action));
            // This overload already offloads using Task.Run, so don't offload again.
            return RunAsync(ct => Task.Run(() => action(ct), ct), onComplete, cancellationToken, ExecutionPolicy.Never);
        }

        /// <summary>
        /// Starts a recurring loop with the specified interval
        /// </summary>
        public Task LoopAsync(Func<CancellationToken, Task> asyncAction,
                                 TimeSpan interval,
                                 Action<Exception> onIteration = null,
                                 CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            ValidateInput(asyncAction, nameof(asyncAction));
            ValidateInterval(interval);

            return RunAsync(ct => ExecuteLoop(asyncAction, interval, onIteration, ct), null, cancellationToken);
        }

        /// <summary>
        /// Starts a recurring loop with synchronous action
        /// </summary>
        public Task LoopAsync(Action<CancellationToken> action,
                                 TimeSpan interval,
                                 Action<Exception> onIteration = null,
                                 CancellationToken cancellationToken = default)
        {
            ValidateInput(action, nameof(action));
            return LoopAsync(ct => Task.Run(() => action(ct), ct), interval, onIteration, cancellationToken);
        }

        /// <summary>
        /// Cancels all currently running tasks
        /// </summary>
        public void CancelAll()
        {
            if (!_disposed)
            {
                _globalCts?.Cancel();
            }
        }

        /// <summary>
        /// Waits for all active tasks to complete with timeout
        /// </summary>
        public async Task<bool> WaitForAllAsync(int timeoutMs = 5000)
        {
            if (_activeTasks.IsEmpty)
            { return true; }

            var activeTasks = _activeTasks.Keys.ToArray();
            if (activeTasks.Length == 0)
            { return true; }

            try
            {
                var whenAllTask = Task.WhenAll(activeTasks);
                var timeoutTask = Task.Delay(timeoutMs);

                var completedTask = await Task.WhenAny(whenAllTask, timeoutTask);

                if (completedTask == timeoutTask)
                {
                    return false; // Timeout occurred
                }

                await whenAllTask; // Propagate any exceptions
                return true;
            }
            catch
            {
                return false; // Other errors
            }
        }

        private async Task<T> ExecuteWithCleanup<T>(Func<CancellationToken, Task<T>> asyncFunc,
                                                  Action<Exception> onComplete,
                                                  CancellationToken cancellationToken)
        {
            Exception capturedException = null;
            T result = default(T);
            var profiling = ProfilingEnabled;
            Stopwatch sw = null;
            if (profiling)
            {
                sw = Stopwatch.StartNew();
            }

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                result = await asyncFunc(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException ex)
            {
                capturedException = ex;
                // Cancellation is a control flow event; do not log as error
                throw;
            }
            catch (Exception ex)
            {
                capturedException = ex;
                // Ensure background exceptions are surfaced
                ReportException(ex);
                throw;
            }
            finally
            {
                if (profiling && sw != null)
                {
                    sw.Stop();
                    var elapsedMs = sw.Elapsed.TotalMilliseconds;
                    Interlocked.Add(ref _totalDurationTicks, sw.ElapsedTicks);
                    Interlocked.Exchange(ref _lastDurationMs, elapsedMs);
                    Interlocked.Increment(ref _completedTasks);
                }
                SafeReleaseSemaphore();

                if (onComplete != null)
                {
                    PostCallback(() => onComplete(capturedException));
                }
            }

            return result;
        }

        private async Task ExecuteLoop(Func<CancellationToken, Task> asyncAction,
                                     TimeSpan interval,
                                     Action<Exception> onIteration,
                                     CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                Exception iterationException = null;

                try
                {
                    await asyncAction(cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    iterationException = ex;
                }

                if (onIteration != null)
                {
                    PostCallback(() => onIteration(iterationException));
                }

                try
                {
                    await Task.Delay(interval, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        private void SafeReleaseSemaphore()
        {
            if (_concurrencyLimiter == null)
            {
                return;
            }

            try
            {
                _concurrencyLimiter.Release();
            }
            catch (ObjectDisposedException)
            {
                // The limiter can be disposed while tasks unwind during shutdown; ignore.
            }
            catch (SemaphoreFullException)
            {
                // Guard against rare double-release during fault paths.
            }
        }

        private void TrackTask(Task task)
        {
            _activeTasks.TryAdd(task, 0);

            // Remove task when completed (fire-and-forget) and report exceptions
            _ = task.ContinueWith(t =>
            {
                _activeTasks.TryRemove(t, out _);
                if (t.IsFaulted && t.Exception != null)
                {
                    var agg = t.Exception.Flatten();
                    foreach (var ex in agg.InnerExceptions)
                    {
                        ReportException(ex);
                    }
                }
            }, TaskContinuationOptions.ExecuteSynchronously);
        }

        private void CleanupCompletedTasks(object state)
        {
            if (_disposed) { return; }

            var completedTasks = _activeTasks.Keys
                .Where(t => t.IsCompleted)
                .ToArray();

            foreach (var task in completedTasks)
            {
                _activeTasks.TryRemove(task, out _);
            }
        }

        private CancellationTokenSource CreateLinkedTokenSource(CancellationToken userToken)
        {
            return userToken == default
                ? CancellationTokenSource.CreateLinkedTokenSource(_globalCts.Token)
                : CancellationTokenSource.CreateLinkedTokenSource(_globalCts.Token, userToken);
        }

        private void PostCallback(Action callback)
        {
            if (callback == null || _disposed) { return; }

            _mainContext.Post(_ =>
            {
                if (!_disposed)
                {
                    try { callback(); }
                    catch { /* Swallow callback exceptions to prevent crashes */ }
                }
            }, null);
        }

        private void ReportException(Exception ex)
        {
            try
            {
                // Prefer logging on main thread when possible for Unity console visibility
                PostCallback(() =>
                {
                    try
                    {
                        SherpaLog.Exception(ex);
                    }
                    catch
                    {
                        try { System.Console.Error.WriteLine(ex.ToString()); } catch { }
                    }
                });
            }
            catch
            {
                try { SherpaLog.Exception(ex); } catch { }
            }
        }

        private bool ShouldOffload(ExecutionPolicy policy)
        {
            switch (policy)
            {
                case ExecutionPolicy.Always:
                    return true;
                case ExecutionPolicy.Never:
                    return false;
                case ExecutionPolicy.Auto:
                default:
                    return IsMainThread();
            }
        }

        private bool IsMainThread()
        {
            return Thread.CurrentThread.ManagedThreadId == _mainThreadId;
        }

        private static bool IsUnityContext()
        {
            try
            {
                return UnityEngine.Application.isPlaying;
            }
            catch
            {
                return false; // Not in Unity context
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            { throw new ObjectDisposedException(nameof(TaskRunner)); }
        }

        private static bool ProfilingEnabled =>
            Interlocked.CompareExchange(ref s_profilingEnabled, 0, 0) != 0;

        public static bool IsProfilingEnabled => ProfilingEnabled;

        public static void SetProfilingEnabled(bool enabled)
        {
            Interlocked.Exchange(ref s_profilingEnabled, enabled ? 1 : 0);
        }

        private static void ValidateInput<T>(T input, string paramName) where T : class
        {
            if (input == null)
            { throw new ArgumentNullException(paramName); }
        }

        private static void ValidateInterval(TimeSpan interval)
        {
            if (interval <= TimeSpan.Zero)
            { throw new ArgumentException("Interval must be positive", nameof(interval)); }
        }

        private static int ComputeAdaptiveConcurrency()
        {
            try
            {
                var adaptive = ThreadingUtils.GetAdaptiveThreadCount(minimum: 1);
                return Math.Clamp(adaptive * 2, 2, 24);
            }
            catch
            {
                return 8;
            }
        }

        public void Dispose()
        {
            if (_disposed) { return; }

            _disposed = true;

            _cleanupTimer?.Dispose();
            _globalCts?.Cancel();

            // Async cleanup without blocking the caller thread
            _ = Task.Run(async () =>
            {
                try
                {
                    await WaitForAllAsync(2000).ConfigureAwait(false);
                }
                finally
                {
                    try { _concurrencyLimiter?.Dispose(); }
                    catch { }
                    _globalCts?.Dispose();
                    _globalCts = null;
                }
            });
        }
    }

    /// <summary>
    /// Extension methods for TaskRunner
    /// </summary>
    public static class TaskRunnerExtensions
    {
        /// <summary>
        /// Runs nested operations using the same TaskRunner instance
        /// </summary>
        public static Task RunNested(this TaskRunner runner,
                                   Func<TaskRunner, CancellationToken, Task> nestedOps,
                                   CancellationToken cancellationToken = default)
        {
            if (runner == null) { throw new ArgumentNullException(nameof(runner)); }
            if (nestedOps == null) { throw new ArgumentNullException(nameof(nestedOps)); }

            return runner.RunAsync(ct => nestedOps(runner, ct), null, cancellationToken);
        }

        /// <summary>
        /// Runs multiple tasks concurrently with the same TaskRunner
        /// </summary>
        public static async Task RunConcurrently(this TaskRunner runner,
                                                params Func<CancellationToken, Task>[] asyncActions)
        {
            if (runner == null) { throw new ArgumentNullException(nameof(runner)); }
            if (asyncActions == null || asyncActions.Length == 0) { return; }

            var tasks = asyncActions.Select(action => runner.RunAsync(action)).ToArray();
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
    }
}
