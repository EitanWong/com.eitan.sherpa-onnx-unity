using System;
using System.Collections.Generic;
using UnityEngine;

namespace Eitan.SherpaONNXUnity.Runtime
{
    public enum SherpaONNXCustomCatalogEntryType
    {
        Model,
        RemoteManifest
    }

    [Serializable]
    public sealed class SherpaONNXCustomCatalogEntry
    {
        public bool enabled = true;
        public SherpaONNXCustomCatalogEntryType entryType = SherpaONNXCustomCatalogEntryType.Model;
        public string name = string.Empty;

        public string modelId = string.Empty;
        public SherpaONNXModuleType moduleType = SherpaONNXModuleType.Undefined;
        public string moduleTypeHint = string.Empty;
        public string downloadUrl = string.Empty;
        public string downloadFileHash = string.Empty;
        public int numberOfSpeakers;
        public int sampleRate = 16000;
        public string modelTypeHint = string.Empty;
        public List<SherpaONNXModelFileBinding> fileBindings = new List<SherpaONNXModelFileBinding>();

        public string remoteManifestUrl = string.Empty;

        public bool IsModel => entryType == SherpaONNXCustomCatalogEntryType.Model;
        public bool IsRemoteManifest => entryType == SherpaONNXCustomCatalogEntryType.RemoteManifest;

        public SherpaONNXModelMetadata ToMetadata()
        {
            if (!IsModel)
            {
                return null;
            }

            return new SherpaONNXModelMetadata
            {
                modelId = modelId?.Trim(),
                moduleType = moduleType,
                moduleTypeHint = moduleTypeHint?.Trim(),
                downloadUrl = downloadUrl?.Trim(),
                downloadFileHash = downloadFileHash?.Trim(),
                numberOfSpeakers = numberOfSpeakers,
                sampleRate = sampleRate,
                modelTypeHint = modelTypeHint?.Trim(),
                fileBindings = fileBindings ?? new List<SherpaONNXModelFileBinding>()
            };
        }
    }

    /// <summary>
    /// ScriptableObject that stores custom model entries and remote manifest URLs.
    /// Stored under Resources so it ships with builds and can be accessed at runtime.
    /// </summary>
    public sealed class SherpaONNXCustomModelSettings : ScriptableObject
    {
        public const string ResourceName = "SherpaONNXCustomModels";
        public const string AssetPath = "Assets/Resources/" + ResourceName + ".asset";
        public const string EntriesPropertyName = nameof(_entries);

        [SerializeField] private List<SherpaONNXCustomCatalogEntry> _entries = new List<SherpaONNXCustomCatalogEntry>();
        [SerializeField, HideInInspector] private List<SherpaONNXCustomModelEntry> _models = new List<SherpaONNXCustomModelEntry>();
        [SerializeField, HideInInspector] private List<SherpaONNXRemoteManifestEntry> _remoteManifests = new List<SherpaONNXRemoteManifestEntry>();

        public IReadOnlyList<SherpaONNXCustomCatalogEntry> Entries => _entries;

        internal IEnumerable<SherpaONNXModelMetadata> EnumerateEnabledModels()
        {
            EnsureMigrated();
            if (_entries == null || _entries.Count == 0)
            {
                yield break;
            }

            for (int i = 0; i < _entries.Count; i++)
            {
                var entry = _entries[i];
                if (entry == null || !entry.enabled || !entry.IsModel)
                {
                    continue;
                }

                var metadata = entry.ToMetadata();
                if (metadata == null || string.IsNullOrWhiteSpace(metadata.modelId))
                {
                    continue;
                }

                yield return metadata;
            }
        }

        internal IEnumerable<string> EnumerateEnabledRemoteManifestUrls()
        {
            EnsureMigrated();
            if (_entries == null || _entries.Count == 0)
            {
                yield break;
            }

            for (int i = 0; i < _entries.Count; i++)
            {
                var entry = _entries[i];
                if (entry == null || !entry.enabled || !entry.IsRemoteManifest)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(entry.remoteManifestUrl))
                {
                    continue;
                }

                yield return entry.remoteManifestUrl.Trim();
            }
        }

        private void EnsureMigrated()
        {
            if (_entries != null && _entries.Count > 0)
            {
                return;
            }

            bool hasLegacy = (_models != null && _models.Count > 0) || (_remoteManifests != null && _remoteManifests.Count > 0);
            if (!hasLegacy)
            {
                return;
            }

            _entries = _entries ?? new List<SherpaONNXCustomCatalogEntry>();

            if (_models != null)
            {
                for (int i = 0; i < _models.Count; i++)
                {
                    var legacy = _models[i];
                    if (legacy == null)
                    {
                        continue;
                    }

                    _entries.Add(new SherpaONNXCustomCatalogEntry
                    {
                        enabled = legacy.enabled,
                        entryType = SherpaONNXCustomCatalogEntryType.Model,
                        name = legacy.modelId,
                        modelId = legacy.modelId,
                        moduleType = legacy.moduleType,
                        downloadUrl = legacy.downloadUrl,
                        downloadFileHash = legacy.downloadFileHash,
                        numberOfSpeakers = legacy.numberOfSpeakers,
                        sampleRate = legacy.sampleRate,
                        modelTypeHint = legacy.modelTypeHint
                    });
                }
            }

            if (_remoteManifests != null)
            {
                for (int i = 0; i < _remoteManifests.Count; i++)
                {
                    var legacy = _remoteManifests[i];
                    if (legacy == null)
                    {
                        continue;
                    }

                    _entries.Add(new SherpaONNXCustomCatalogEntry
                    {
                        enabled = legacy.enabled,
                        entryType = SherpaONNXCustomCatalogEntryType.RemoteManifest,
                        name = legacy.name,
                        remoteManifestUrl = legacy.url
                    });
                }
            }

            _models.Clear();
            _remoteManifests.Clear();
        }

        internal static SherpaONNXCustomModelSettings LoadFromResources()
        {
            var direct = Resources.Load<SherpaONNXCustomModelSettings>(ResourceName);
            if (direct != null)
            {
                return direct;
            }

            var discovered = Resources.LoadAll<SherpaONNXCustomModelSettings>(string.Empty);
            if (discovered == null || discovered.Length == 0)
            {
                return null;
            }

            var valid = new List<SherpaONNXCustomModelSettings>(discovered.Length);
            foreach (var candidate in discovered)
            {
                if (candidate != null)
                {
                    valid.Add(candidate);
                }
            }

            if (valid.Count == 0)
            {
                return null;
            }

            if (valid.Count > 1)
            {
                SherpaLog.Error($"Multiple {nameof(SherpaONNXCustomModelSettings)} assets detected under Resources. Please keep only one asset to avoid ambiguity.", category: "Settings");
            }

            valid.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            return valid[0];
        }
    }

    [Serializable]
    internal sealed class SherpaONNXCustomModelEntry
    {
        public bool enabled = true;
        public string modelId = string.Empty;
        public SherpaONNXModuleType moduleType = SherpaONNXModuleType.Undefined;
        public string downloadUrl = string.Empty;
        public string downloadFileHash = string.Empty;
        public int numberOfSpeakers;
        public int sampleRate = 16000;
        public string modelTypeHint = string.Empty;
    }

    [Serializable]
    internal sealed class SherpaONNXRemoteManifestEntry
    {
        public bool enabled = true;
        public string name = string.Empty;
        public string url = string.Empty;
    }
}
