using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Eitan.SherpaOnnxUnity.Runtime.Utilities
{
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

        private CancellationTokenSource _globalCts = new();
        private volatile bool _disposed;

        // Configuration
        private readonly int _maxConcurrentTasks;
        private const int DefaultMaxConcurrentTasks = 50;
        private const int CleanupIntervalMs = 30000; // 30 seconds

        public TaskRunner(int maxConcurrentTasks = DefaultMaxConcurrentTasks)
        {
            if (maxConcurrentTasks <= 0)
            {
                throw new ArgumentException("Max concurrent tasks must be positive", nameof(maxConcurrentTasks));
            }

            _maxConcurrentTasks = maxConcurrentTasks;
            _mainContext = SynchronizationContext.Current ?? new SynchronizationContext();
            _concurrencyLimiter = new SemaphoreSlim(maxConcurrentTasks, maxConcurrentTasks);

            // Periodic cleanup of completed tasks
            _cleanupTimer = new Timer(CleanupCompletedTasks, null,
                TimeSpan.FromMilliseconds(CleanupIntervalMs),
                TimeSpan.FromMilliseconds(CleanupIntervalMs));

            // Auto-cleanup on Unity quit
            if (IsUnityContext())
            {
                UnityEngine.Application.quitting += Dispose;
            }
        }

        /// <summary>
        /// Gets the number of currently active tasks
        /// </summary>
        public int ActiveTaskCount => _activeTasks.Count;

        /// <summary>
        /// Gets whether the runner has been disposed
        /// </summary>
        public bool IsDisposed => _disposed;

        /// <summary>
        /// Runs an async operation with proper resource management and error handling
        /// </summary>
        public async Task<T> RunAsync<T>(Func<CancellationToken, Task<T>> asyncFunc,
                                       Action<Exception> onComplete = null,
                                       CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            ValidateInput(asyncFunc, nameof(asyncFunc));

            await _concurrencyLimiter.WaitAsync(cancellationToken);

            using var linkedCts = CreateLinkedTokenSource(cancellationToken);
            var task = ExecuteWithCleanup(asyncFunc, onComplete, linkedCts.Token);

            TrackTask(task);
            return await task;
        }

        /// <summary>
        /// Runs an async operation without return value
        /// </summary>
        public Task RunAsync(Func<CancellationToken, Task> asyncAction,
                           Action<Exception> onComplete = null,
                           CancellationToken cancellationToken = default)
        {
            return RunAsync(async ct => { await asyncAction(ct); return true; }, onComplete, cancellationToken);
        }

        /// <summary>
        /// Runs a synchronous operation on a background thread
        /// </summary>
        public Task RunAsync(Action<CancellationToken> action,
                           Action<Exception> onComplete = null,
                           CancellationToken cancellationToken = default)
        {
            ValidateInput(action, nameof(action));
            return RunAsync(ct => Task.Run(() => action(ct), ct), onComplete, cancellationToken);
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
            if (_disposed || _activeTasks.IsEmpty)
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

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                result = await asyncFunc(cancellationToken);
            }
            catch (OperationCanceledException ex)
            {
                capturedException = ex;
                throw;
            }
            catch (Exception ex)
            {
                capturedException = ex;
                throw;
            }
            finally
            {
                _concurrencyLimiter.Release();

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

        private void TrackTask(Task task)
        {
            _activeTasks.TryAdd(task, 0);

            // Remove task when completed (fire-and-forget)
            _ = task.ContinueWith(t => _activeTasks.TryRemove(t, out _),
                TaskContinuationOptions.ExecuteSynchronously);
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

        public void Dispose()
        {
            if (_disposed) { return; }

            _disposed = true;

            if (IsUnityContext())
            { UnityEngine.Application.quitting -= Dispose; }

            // Cancel all operations
            _globalCts?.Cancel();

            // Cleanup resources
            _cleanupTimer?.Dispose();
            _concurrencyLimiter?.Dispose();

            // Async cleanup without blocking
            _ = Task.Run(async () =>
            {
                try
                {
                    await WaitForAllAsync(2000);
                }
                finally
                {
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
            await Task.WhenAll(tasks);
        }
    }
}