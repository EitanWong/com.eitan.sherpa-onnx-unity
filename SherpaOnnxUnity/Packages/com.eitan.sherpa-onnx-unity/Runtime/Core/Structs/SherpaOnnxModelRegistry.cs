using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Eitan.SherpaOnnxUnity.Runtime.Utilities;
using UnityEngine;

namespace Eitan.SherpaOnnxUnity.Runtime
{
    public class SherpaOnnxModelRegistry
    {
        private static readonly SherpaOnnxModelRegistry _instance = new SherpaOnnxModelRegistry();
        public static SherpaOnnxModelRegistry Instance => _instance;

        private readonly Dictionary<string, SherpaOnnxModelMetadata> _modelData = new Dictionary<string, SherpaOnnxModelMetadata>();
        private readonly HashSet<string> _resolvedModelIds = new HashSet<string>();

        private SherpaOnnxModelManifest _manifest;

        public bool IsInitialized { get; private set; }
        public bool IsInitializing { get; private set; }
        private readonly object _initLock = new object();
        private Task _initTask;
        private CancellationTokenSource _initCts;
        private int _initGeneration = 0;

        public event Action Initialized;

        private SherpaOnnxModelRegistry() { }


        /// <summary>
        /// Clear the loaded manifest and internal caches, marking the registry as uninitialized.
        /// Safe to call from Editor (main thread). Any in-flight initialization will be ignored.
        /// </summary>
        public void Uninitialize()
        {
            lock (_initLock)
            {
                // Bump generation to invalidate any older init completions
                _initCts?.Cancel();
                _initCts?.Dispose();
                _initCts = null;
                _initGeneration++;
                _manifest = null;
                _modelData.Clear();
                _resolvedModelIds.Clear();
                IsInitialized = false;
                IsInitializing = false;
                _initTask = null;
            }
        }

        /// <summary>
        /// Initialize the registry from the default manifest once, asynchronously.
        /// Safe to call multiple times; concurrent callers await the same task.
        /// </summary>
        public Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            Task initTask;
            CancellationTokenSource currentCts;

            lock (_initLock)
            {
                if (IsInitialized)
                {
                    return Task.CompletedTask;
                }

                if (_initTask == null || _initTask.IsFaulted || _initTask.IsCanceled)
                {
                    _initCts?.Dispose();
                    _initCts = new CancellationTokenSource();
                    currentCts = _initCts;
                    IsInitializing = true;
                    int gen = ++_initGeneration; // capture new generation for this init
                    _initTask = InitializeInternalAsync(gen, _initCts.Token);
                }
                else
                {
                    currentCts = _initCts;
                }

                initTask = _initTask;
            }

            CancellationTokenRegistration registration = default;
            try
            {
                if (cancellationToken.CanBeCanceled && currentCts != null)
                {
                    registration = cancellationToken.Register(() =>
                    {
                        if (!currentCts.IsCancellationRequested)
                        {
                            currentCts.Cancel();
                        }
                    }, useSynchronizationContext: false);
                }

                if (!cancellationToken.CanBeCanceled)
                {
                    return initTask;
                }

                return WaitForInitTaskAsync(initTask, cancellationToken);
            }
            finally
            {
                registration.Dispose();
            }
        }

        private static async Task WaitForInitTaskAsync(Task initTask, CancellationToken cancellationToken)
        {
            if (initTask == null)
            {
                return;
            }

            if (initTask.IsCompleted)
            {
                await initTask.ConfigureAwait(true);
                return;
            }

            var completionTcs = new TaskCompletionSource<bool>();

            using (cancellationToken.Register(() => completionTcs.TrySetCanceled(), useSynchronizationContext: false))
            {
                var completed = await Task.WhenAny(initTask, completionTcs.Task).ConfigureAwait(true);
                if (completed != initTask)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }
            }

            await initTask.ConfigureAwait(true);
        }

        private async Task InitializeInternalAsync(int generation, CancellationToken cancellationToken)
        {
            if (IsInitialized)
            {
                IsInitializing = false;
                return;
            }

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                // Build manifest without blocking the main thread.
                _manifest = await Constants.SherpaOnnxConstants.GetDefaultManifestAsync().ConfigureAwait(true);
                cancellationToken.ThrowIfCancellationRequested();
                _resolvedModelIds.Clear();
                PopulateDictionaryFromManifest(_manifest);

                // If a reset occurred during initialization, ignore this result
                lock (_initLock)
                {
                    if (generation != _initGeneration)
                    {
                        // Stale init; do not touch state
                        return;
                    }
                }

                // Only set IsInitialized after we have fully populated dictionaries.
                IsInitialized = true;
                try
                {
                    Initialized?.Invoke();
                }
                catch (Exception cbEx)
                {
                    UnityEngine.Debug.LogWarning($"Initialized callback error: {cbEx.Message}");
                }
            }
            catch (OperationCanceledException)
            {
                IsInitialized = false;
                throw;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"Failed to initialize model registry: {ex.GetType().Name}: {ex.Message}.");
                IsInitialized = false;
                throw;
            }
            finally
            {
                IsInitializing = false;
                lock (_initLock)
                {
                    if (generation == _initGeneration)
                    {
                        _initTask = null;
                        _initCts?.Dispose();
                        _initCts = null;
                    }
                }
            }
        }

        private void PopulateDictionaryFromManifest(SherpaOnnxModelManifest manifest)
        {
            _modelData.Clear();
            if (manifest?.models == null || manifest.models.Count == 0)
            {
                return;
            }

            foreach (var metadata in manifest.models)
            {
                if (string.IsNullOrWhiteSpace(metadata.modelId))
                {
                    Debug.LogWarning("Encountered a model entry with an empty modelId. Entry skipped.");
                    continue;
                }

                if (!_modelData.ContainsKey(metadata.modelId))
                {
                    _modelData.Add(metadata.modelId, metadata);
                }
                else
                {
                    Debug.LogWarning($"Duplicate modelId in manifest: '{metadata.modelId}'. Entry skipped.");
                }
            }
        }

        //         private async Task<string> ReadManifestFileAsync()
        //         {

        //             string directoryPath = Path.Combine(Application.streamingAssetsPath, SherpaOnnxConstants.RootDirectoryName);
        //             string manifestPath = Path.Combine(directoryPath, SherpaOnnxConstants.ManifestFileName);

        // #if (!UNITY_ANDROID && !UNITY_IOS && !UNITY_WEBGL)
        //             if (!File.Exists(manifestPath))
        //             {
        //                 string defaultJson = SherpaOnnxConstants.GetDefaultManifestContent();
        //                 if (!Directory.Exists(directoryPath))
        //                 {
        //                     Directory.CreateDirectory(directoryPath);
        //                 }
        //                 await File.WriteAllTextAsync(manifestPath, defaultJson);
        //             }

        //             if (File.Exists(manifestPath))
        //             {
        //                 return await File.ReadAllTextAsync(manifestPath);
        //             }
        //             return null;
        // #else
        //             using (UnityWebRequest www = UnityWebRequest.Get(manifestPath))
        //             {
        //                 var operation = www.SendWebRequest();
        //                 while (!operation.isDone)
        //                 {
        //                     await Task.Yield();
        //                 }

        //                 return www.result == UnityWebRequest.Result.Success ? www.downloadHandler.text : null;
        //             }

        // #endif
        //         }

        /// <summary>
        /// Get metadata for a specific modelId. Resolves model file names to absolute paths on first access.
        /// </summary>
        private SherpaOnnxModelMetadata GetMetadata(string modelId)
        {
            if (!IsInitialized)
            {
                UnityEngine.Debug.LogWarning("SherpaOnnxModelRegistry is not initialized yet. Call and await InitializeAsync() before accessing metadata.");
                return null;
            }

            if (_modelData.TryGetValue(modelId, out var metadata))
            {
                // Resolve model file names to absolute paths only once per modelId
                if (!_resolvedModelIds.Contains(modelId))
                {
                    // for (int i = 0; i < metadata.modelFileNames.Length; i++)
                    // {
                    //     metadata.modelFileNames[i] = SherpaPathResolver.GetModelFilePath(modelId, metadata.modelFileNames[i]);
                    // }
                    _resolvedModelIds.Add(modelId);
                }

                return metadata;
            }

            Debug.LogError($"Metadata for modelId '{modelId}' not found in the manifest.");
            return null;
        }

        /// <summary>
        /// Async version of GetMetadata; awaits initialization if needed.
        /// </summary>
        public async Task<SherpaOnnxModelMetadata> GetMetadataAsync(string modelId, CancellationToken cancellationToken = default)
        {
            await InitializeAsync(cancellationToken).ConfigureAwait(true);
            cancellationToken.ThrowIfCancellationRequested();
            return GetMetadata(modelId);
        }


        /// <summary>
        /// Try to get the manifest without waiting. Returns true if initialized and manifest is not null.
        /// </summary>
        public bool TryGetManifest(out SherpaOnnxModelManifest manifest)
        {
            manifest = _manifest;
            return IsInitialized && manifest != null;
        }

        /// <summary>
        /// Await until the registry has finished initialization and then return the manifest.
        /// Does not block the main thread.
        /// </summary>
        public async Task<SherpaOnnxModelManifest> WaitForManifestAsync(CancellationToken cancellationToken = default)
        {
            await InitializeAsync(cancellationToken).ConfigureAwait(true);
            cancellationToken.ThrowIfCancellationRequested();
            return _manifest;
        }

        /// <summary>
        /// Get the loaded manifest. Triggers lazy initialization if necessary.
        /// </summary>
        // public SherpaOnnxModelManifest GetManifest()
        // {
        //     if (!IsInitialized)
        //     {
        //         UnityEngine.Debug.LogWarning("SherpaOnnxModelRegistry is not initialized yet. Call and await InitializeAsync() before accessing the manifest.");
        //         return null;
        //     }
        //     return _manifest;
        // }

        /// <summary>
        /// Async version of GetManifest; awaits initialization if needed.
        /// </summary>
        public async Task<SherpaOnnxModelManifest> GetManifestAsync(CancellationToken cancellationToken = default)
        {
            await InitializeAsync(cancellationToken).ConfigureAwait(true);
            cancellationToken.ThrowIfCancellationRequested();
            return _manifest;
        }
    }
}
