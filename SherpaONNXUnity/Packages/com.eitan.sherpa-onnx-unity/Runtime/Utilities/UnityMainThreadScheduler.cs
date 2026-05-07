using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Eitan.SherpaONNXUnity.Runtime.Utilities
{
    internal static class UnityMainThreadScheduler
    {
        private static readonly object InitLock = new object();
        private static readonly ConcurrentQueue<Action> PendingActions = new ConcurrentQueue<Action>();
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
                while (PendingActions.TryDequeue(out _))
                {
                }
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void InitializeOnLoad()
        {
            EnsureInitialized();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InitializeBeforeSceneLoad()
        {
            EnsureInitialized();
        }

        public static bool IsMainThread => _initialized && Thread.CurrentThread.ManagedThreadId == _mainThreadId;

        public static void EnsureInitialized()
        {
            if (_initialized)
            {
                return;
            }

            lock (InitLock)
            {
                if (_initialized)
                {
                    return;
                }

                var currentContext = SynchronizationContext.Current;
                if (!IsUnitySynchronizationContext(currentContext))
                {
                    return;
                }

                _context = currentContext;
                _mainThreadId = Thread.CurrentThread.ManagedThreadId;
                _initialized = true;
                FlushPendingActions();
            }
        }

        public static void Post(Action action)
        {
            if (action == null)
            {
                return;
            }

            EnsureInitialized();

            if (!_initialized)
            {
                PendingActions.Enqueue(action);
                return;
            }

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

            if (!_initialized)
            {
                throw new InvalidOperationException("Unity main thread scheduler has not been initialized yet.");
            }

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

            if (!_initialized)
            {
                throw new InvalidOperationException("Unity main thread scheduler has not been initialized yet.");
            }

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

            if (!_initialized)
            {
                throw new InvalidOperationException("Unity main thread scheduler has not been initialized yet.");
            }

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

            if (!_initialized)
            {
                throw new InvalidOperationException("Unity main thread scheduler has not been initialized yet.");
            }

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
            if (operation == null)
            {
                throw new ArgumentNullException(nameof(operation));
            }

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

        private static bool IsUnitySynchronizationContext(SynchronizationContext context)
        {
            if (context == null)
            {
                return false;
            }

            var typeName = context.GetType().Name;
            return string.Equals(typeName, "UnitySynchronizationContext", StringComparison.Ordinal) ||
                   string.Equals(typeName, "UnitySynchronizationContext", StringComparison.OrdinalIgnoreCase);
        }

        private static void FlushPendingActions()
        {
            while (PendingActions.TryDequeue(out var action))
            {
                try
                {
                    action?.Invoke();
                }
                catch (Exception ex)
                {
                    SherpaLog.Exception(ex);
                }
            }
        }
    }
}
