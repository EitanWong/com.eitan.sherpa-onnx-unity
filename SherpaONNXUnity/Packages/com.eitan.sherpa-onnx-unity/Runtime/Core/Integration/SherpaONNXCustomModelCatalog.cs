using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using Eitan.SherpaONNXUnity.Runtime.Utilities;

namespace Eitan.SherpaONNXUnity.Runtime
{
    internal static class SherpaONNXCustomModelCatalog
    {
        internal static List<SherpaONNXModelMetadata> GetLocalModels()
        {
            var snapshot = SherpaONNXRuntimeResourceProvider.GetCustomModelCatalogSnapshot();
            if (snapshot.LocalModels.Count == 0)
            {
                return new List<SherpaONNXModelMetadata>();
            }

            var models = new List<SherpaONNXModelMetadata>(snapshot.LocalModels.Count);
            foreach (var metadata in snapshot.LocalModels)
            {
                models.Add(CloneMetadata(metadata));
            }

            return models;
        }

        internal static List<string> GetRemoteManifestUrls()
        {
            var snapshot = SherpaONNXRuntimeResourceProvider.GetCustomModelCatalogSnapshot();
            return snapshot.RemoteManifestUrls.Count == 0
                ? new List<string>()
                : new List<string>(snapshot.RemoteManifestUrls);
        }

        internal static async Task<List<SherpaONNXModelMetadata>> FetchRemoteModelsAsync(
            IEnumerable<string> manifestUrls,
            CancellationToken cancellationToken)
        {
            var results = new List<SherpaONNXModelMetadata>();
            if (manifestUrls == null)
            {
                return results;
            }

            foreach (var url in manifestUrls)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (string.IsNullOrWhiteSpace(url))
                {
                    continue;
                }

                var trimmedUrl = url.Trim();
                var (ok, content) = await TryHttpGetTextAsync(trimmedUrl, 20000, cancellationToken).ConfigureAwait(true);
                if (!ok || string.IsNullOrWhiteSpace(content))
                {
                    SherpaLog.Warning($"Custom manifest fetch failed: {trimmedUrl}", category: "Catalog");
                    continue;
                }

                var manifest = TryParseManifest(content);
                if (manifest?.models == null || manifest.models.Count == 0)
                {
                    SherpaLog.Warning($"Custom manifest returned no models: {trimmedUrl}", category: "Catalog");
                    continue;
                }

                foreach (var metadata in manifest.models)
                {
                    if (metadata == null || string.IsNullOrWhiteSpace(metadata.modelId))
                    {
                        continue;
                    }

                    results.Add(NormalizeMetadata(metadata));
                }
            }

            return results;
        }

        private static SherpaONNXModelManifest TryParseManifest(string json)
        {
            try
            {
                return JsonUtility.FromJson<SherpaONNXModelManifest>(json);
            }
            catch (Exception ex)
            {
                SherpaLog.Warning($"Custom manifest parse failed: {ex.GetType().Name}: {ex.Message}", category: "Catalog");
                return null;
            }
        }

        private static SherpaONNXModelMetadata NormalizeMetadata(SherpaONNXModelMetadata metadata)
        {
            if (metadata == null)
            {
                return null;
            }

            metadata = CloneMetadata(metadata);

            metadata.modelId = metadata.modelId?.Trim();
            metadata.moduleTypeHint = metadata.moduleTypeHint?.Trim();
            metadata.downloadUrl = metadata.downloadUrl?.Trim();
            metadata.downloadFileHash = metadata.downloadFileHash?.Trim();
            metadata.modelTypeHint = metadata.modelTypeHint?.Trim();
            NormalizeBindings(metadata.fileBindings);
            return metadata;
        }

        private static SherpaONNXModelMetadata CloneMetadata(SherpaONNXModelMetadata metadata)
        {
            var clone = new SherpaONNXModelMetadata
            {
                modelId = metadata.modelId,
                moduleType = metadata.moduleType,
                moduleTypeHint = metadata.moduleTypeHint,
                downloadUrl = metadata.downloadUrl,
                downloadFileHash = metadata.downloadFileHash,
                modelTypeHint = metadata.modelTypeHint,
                numberOfSpeakers = metadata.numberOfSpeakers,
                sampleRate = metadata.sampleRate,
                fileBindings = new List<SherpaONNXModelFileBinding>()
            };

            if (metadata.fileBindings != null)
            {
                foreach (var binding in metadata.fileBindings)
                {
                    clone.fileBindings.Add(binding == null
                        ? null
                        : new SherpaONNXModelFileBinding
                        {
                            key = binding.key,
                            path = binding.path
                        });
                }
            }

            return clone;
        }

        private static void NormalizeBindings(List<SherpaONNXModelFileBinding> bindings)
        {
            if (bindings == null || bindings.Count == 0)
            {
                return;
            }

            for (int i = bindings.Count - 1; i >= 0; i--)
            {
                var binding = bindings[i];
                if (binding == null)
                {
                    bindings.RemoveAt(i);
                    continue;
                }

                binding.path = binding.path?.Trim();

                if (binding.key == SherpaONNXModelFileKey.None || string.IsNullOrWhiteSpace(binding.path))
                {
                    bindings.RemoveAt(i);
                }
            }
        }

        private static async Task<(bool ok, string text)> TryHttpGetTextAsync(
            string url,
            int timeoutMs,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return (false, string.Empty);
            }

            try
            {
                return await UnityMainThreadScheduler.Run(async () =>
                {
                    using (var uwr = UnityWebRequest.Get(url))
                    {
                        uwr.downloadHandler = new DownloadHandlerBuffer();
                        var op = uwr.SendWebRequest();

                        using (var timeoutCts = new CancellationTokenSource(timeoutMs))
                        using (var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token))
                        {
                            await UnityMainThreadScheduler.AwaitAsyncOperation(op, linkedCts.Token).ConfigureAwait(true);

                            if (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
                            {
                                SherpaLog.Warning($"Custom manifest fetch timed out: {url}", category: "Catalog");
                                return (false, string.Empty);
                            }
                        }

#if UNITY_2020_1_OR_NEWER
                        if (uwr.result != UnityWebRequest.Result.Success)
#else
                        if (uwr.isNetworkError || uwr.isHttpError)
#endif
                        {
                            SherpaLog.Warning($"Custom manifest HTTP error: {uwr.error} ({url})", category: "Catalog");
                            return (false, string.Empty);
                        }

                        return (true, uwr.downloadHandler.text ?? string.Empty);
                    }
                }).ConfigureAwait(true);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                SherpaLog.Warning($"Custom manifest fetch exception: {ex.GetType().Name}: {ex.Message}", category: "Catalog");
                return (false, string.Empty);
            }
        }
    }
}
