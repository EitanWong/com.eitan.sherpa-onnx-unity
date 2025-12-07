using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Eitan.SherpaONNXUnity.Runtime.Utilities;
using UnityEngine;

namespace Eitan.SherpaONNXUnity.Runtime
{
    public class SherpaONNXModelRegistry
    {
        private static readonly SherpaONNXModelRegistry _instance = new SherpaONNXModelRegistry();
        public static SherpaONNXModelRegistry Instance => _instance;

        private readonly Dictionary<string, SherpaONNXModelMetadata> _modelData = new Dictionary<string, SherpaONNXModelMetadata>();
        private readonly HashSet<string> _resolvedModelIds = new HashSet<string>();
        private readonly SemaphoreSlim _manifestUpdateSemaphore = new SemaphoreSlim(1, 1);

        private SherpaONNXModelManifest _manifest;

        public bool IsInitialized { get; private set; }
        public bool IsInitializing { get; private set; }
        private readonly object _initLock = new object();

        public event Action Initialized;

        private SherpaONNXModelRegistry() { }

        /// <summary>
        /// Synchronously ensure the registry is initialized. Since initialization
        /// is now trivial (allocating an empty manifest), we avoid async/state machine overhead.
        /// Thread-safe and idempotent.
        /// </summary>
        public void EnsureInitialized()
        {
            if (IsInitialized)
            {
                return;
            }


            lock (_initLock)
            {
                if (IsInitialized)
                {
                    return;
                }


                IsInitializing = true;

                // Minimal init: create an empty manifest and reset caches.
                _manifest = new SherpaONNXModelManifest();
                _resolvedModelIds.Clear();

                // Populate dictionary from (empty) manifest to keep behavior consistent.
                PopulateDictionaryFromManifest(_manifest, clearExisting: true);

                IsInitialized = true;
                IsInitializing = false;
            }

            // Fire callback outside the lock
            try
            {
                Initialized?.Invoke();
            }
            catch (Exception cbEx)
            {
                SherpaLog.Warning($"Initialized callback error: {cbEx.Message}");
            }
        }


        /// <summary>
        /// Clear the loaded manifest and internal caches, marking the registry as uninitialized.
        /// Safe to call from Editor (main thread). Any in-flight initialization will be ignored.
        /// </summary>
        public void Uninitialize()
        {
            lock (_initLock)
            {

                _manifest = null;
                _modelData.Clear();
                _resolvedModelIds.Clear();
                IsInitialized = false;
                IsInitializing = false;
            }
        }

        /// <summary>
        /// Initialize the registry from the default manifest once, asynchronously.
        /// Safe to call multiple times; concurrent callers await the same task.
        /// </summary>
        public Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromCanceled(cancellationToken);
            }

            EnsureInitialized();
            return Task.CompletedTask;
        }


        private void PopulateDictionaryFromManifest(SherpaONNXModelManifest manifest, bool clearExisting)
        {
            if (clearExisting)
            {
                _modelData.Clear();
            }

            if (manifest?.models == null || manifest.models.Count == 0)
            {
                return;
            }

            foreach (var metadata in manifest.models)
            {
                if (metadata == null)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(metadata.modelId))
                {
                    SherpaLog.Warning("Encountered a model entry with an empty modelId. Entry skipped.");
                    continue;
                }

                _modelData[metadata.modelId] = metadata;
            }
        }

        private bool IsModuleLoaded(SherpaONNXModuleType moduleType)
        {
            if (_manifest?.models == null)
            {

                return false;
            }


            return _manifest.models.Any(m => m != null && m.moduleType == moduleType);
        }

        private bool IsManifestFullyLoaded()
        {
            if (_manifest?.models == null)
            {

                return false;
            }


            var present = new HashSet<SherpaONNXModuleType>(
                _manifest.models.Where(m => m != null).Select(m => m.moduleType)
            );
            var required = Constants.SherpaONNXConstants.EnumerateManifestModuleTypes()
                .Where(t => t != SherpaONNXModuleType.Undefined);
            return required.All(t => present.Contains(t));
        }

        private async Task EnsureModuleDataAsync(SherpaONNXModuleType moduleType, CancellationToken cancellationToken)
        {
            if (moduleType == SherpaONNXModuleType.Undefined)
            {
                await EnsureAllModulesLoadedAsync(cancellationToken).ConfigureAwait(true);
                return;
            }

            if (IsModuleLoaded(moduleType))
            {
                return;
            }

            await _manifestUpdateSemaphore.WaitAsync(cancellationToken).ConfigureAwait(true);
            try
            {
                if (IsModuleLoaded(moduleType))
                {
                    return;
                }

                await Constants.SherpaONNXConstants.PopulateManifestAsync(_manifest, new[] { moduleType }, cancellationToken).ConfigureAwait(true);
                PopulateDictionaryFromManifest(_manifest, clearExisting: false);
                // MarkModuleLoaded(moduleType);
            }
            finally
            {
                _manifestUpdateSemaphore.Release();
            }
        }

        private async Task EnsureAllModulesLoadedAsync(CancellationToken cancellationToken)
        {
            var pending = Constants.SherpaONNXConstants.EnumerateManifestModuleTypes()
                .Where(t => t != SherpaONNXModuleType.Undefined && !IsModuleLoaded(t))
                .ToArray();

            if (pending.Length == 0)
            {
                return;
            }

            await _manifestUpdateSemaphore.WaitAsync(cancellationToken).ConfigureAwait(true);
            try
            {
                pending = Constants.SherpaONNXConstants.EnumerateManifestModuleTypes()
                    .Where(t => t != SherpaONNXModuleType.Undefined && !IsModuleLoaded(t))
                    .ToArray();

                if (pending.Length == 0)
                {
                    return;
                }

                await Constants.SherpaONNXConstants.PopulateManifestAsync(_manifest, pending, cancellationToken).ConfigureAwait(true);
                PopulateDictionaryFromManifest(_manifest, clearExisting: false);
            }
            finally
            {
                _manifestUpdateSemaphore.Release();
            }
        }

        /// <summary>
        /// Get metadata for a specific modelId. Resolves model file names to absolute paths on first access.
        /// </summary>
        private SherpaONNXModelMetadata GetMetadata(string modelId)
        {
            if (!IsInitialized)
            {
                SherpaLog.Warning("SherpaONNXModelRegistry is not initialized yet. Call and await InitializeAsync() before accessing metadata.");
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

            SherpaLog.Error($"Metadata for modelId '{modelId}' not found in the manifest.");
            return null;
        }

        /// <summary>
        /// Async version of GetMetadata; awaits initialization if needed.
        /// </summary>
        public async Task<SherpaONNXModelMetadata> GetMetadataAsync(string modelId, CancellationToken cancellationToken = default)
        {
            await InitializeAsync(cancellationToken).ConfigureAwait(true);
            cancellationToken.ThrowIfCancellationRequested();
            var moduleType = SherpaUtils.Model.GetModuleTypeByModelId(modelId);
            await EnsureModuleDataAsync(moduleType, cancellationToken).ConfigureAwait(true);
            cancellationToken.ThrowIfCancellationRequested();
            return GetMetadata(modelId);
        }


        /// <summary>
        /// Try to get the manifest without waiting. Returns true if initialized and manifest is not null.
        /// </summary>
        public bool TryGetManifest(out SherpaONNXModelManifest manifest)
        {
            manifest = _manifest;
            return IsInitialized && manifest != null && IsManifestFullyLoaded();
        }

        /// <summary>
        /// Await until the registry has finished initialization and then return the manifest.
        /// Does not block the main thread.
        /// </summary>
        public async Task<SherpaONNXModelManifest> WaitForManifestAsync(CancellationToken cancellationToken = default)
        {
            await InitializeAsync(cancellationToken).ConfigureAwait(true);
            cancellationToken.ThrowIfCancellationRequested();
            await EnsureAllModulesLoadedAsync(cancellationToken).ConfigureAwait(true);
            cancellationToken.ThrowIfCancellationRequested();
            return _manifest;
        }

        /// <summary>
        /// Async version of GetManifest; awaits initialization if needed.
        /// </summary>
        public async Task<SherpaONNXModelManifest> GetManifestAsync(CancellationToken cancellationToken = default)
        {
            await InitializeAsync(cancellationToken).ConfigureAwait(true);
            cancellationToken.ThrowIfCancellationRequested();
            await EnsureAllModulesLoadedAsync(cancellationToken).ConfigureAwait(true);
            cancellationToken.ThrowIfCancellationRequested();
            return _manifest;
        }

        public async Task<SherpaONNXModelManifest> GetManifestAsync(
            SherpaONNXModuleType moduleType,
            CancellationToken cancellationToken = default)
        {
            await InitializeAsync(cancellationToken).ConfigureAwait(true);
            cancellationToken.ThrowIfCancellationRequested();

            // If the caller truly wants "all", they should call the parameterless overload.
            // For Undefined, just return whatever we currently have without forcing a full load.
            if (moduleType == SherpaONNXModuleType.Undefined)
            {
                return _manifest ?? new SherpaONNXModelManifest();
            }

            // Ensure only the requested module type is present in the cached manifest.
            await EnsureModuleDataAsync(moduleType, cancellationToken).ConfigureAwait(true);
            cancellationToken.ThrowIfCancellationRequested();

            // Return a filtered snapshot so the caller sees just this module's entries,
            // while the internal _manifest remains the shared cache.
            var result = new SherpaONNXModelManifest();
            if (_manifest?.models != null)
            {
                result.models.AddRange(_manifest.models.Where(m => m != null && m.moduleType == moduleType));
            }
            return result;
        }


    }
}
