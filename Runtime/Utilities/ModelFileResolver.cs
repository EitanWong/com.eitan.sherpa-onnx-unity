using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Eitan.SherpaONNXUnity.Runtime;

namespace Eitan.SherpaONNXUnity.Runtime.Utilities
{
    public readonly struct ModelFileCriteria
    {
        private readonly Func<SherpaONNXModelMetadata, string[]> _candidateResolver;

        public ModelFileCriteria(Func<SherpaONNXModelMetadata, string[]> candidateResolver, bool expectDirectory = false, long? minSizeBytes = null, int? minEntryCount = null)
        {
            _candidateResolver = candidateResolver;
            ExpectDirectory = expectDirectory;
            MinSizeBytes = minSizeBytes;
            MinEntryCount = minEntryCount;
        }

        public bool ExpectDirectory { get; }
        public long? MinSizeBytes { get; }
        public int? MinEntryCount { get; }

        public string[] ResolveCandidates(SherpaONNXModelMetadata metadata)
        {
            return _candidateResolver?.Invoke(metadata) ?? Array.Empty<string>();
        }

        public static ModelFileCriteria FromKeywords(params string[] keywords)
        {
            return FromKeywords(expectDirectory: false, minSizeBytes: null, minEntryCount: null, keywords: keywords);
        }

        public static ModelFileCriteria FromDirectoryKeywords(params string[] keywords)
        {
            return FromKeywords(expectDirectory: true, minSizeBytes: null, minEntryCount: 1, keywords: keywords);
        }

        public static ModelFileCriteria FromKeywords(bool expectDirectory, long? minSizeBytes, int? minEntryCount, params string[] keywords)
        {
            var sanitizedKeywords = (keywords ?? Array.Empty<string>())
                .Where(k => !string.IsNullOrWhiteSpace(k))
                .ToArray();

            return new ModelFileCriteria(
                metadata =>
                {
                    if (sanitizedKeywords.Length == 0)
                    {
                        return Array.Empty<string>();
                    }

                    return metadata.GetModelFilePathByKeywords(sanitizedKeywords) ?? Array.Empty<string>();
                },
                expectDirectory,
                minSizeBytes,
                minEntryCount);
        }

        public static ModelFileCriteria FromExtensions(params string[] extensions)
        {
            return FromExtensions(expectDirectory: false, minSizeBytes: null, extensions: extensions);
        }

        public static ModelFileCriteria FromExtensions(bool expectDirectory, long? minSizeBytes, params string[] extensions)
        {
            var sanitizedExtensions = (extensions ?? Array.Empty<string>())
                .Where(ext => !string.IsNullOrWhiteSpace(ext))
                .ToArray();

            return new ModelFileCriteria(
                metadata =>
                {
                    if (sanitizedExtensions.Length == 0)
                    {
                        return Array.Empty<string>();
                    }

                    return metadata.GetModelFilesByExtensionName(sanitizedExtensions) ?? Array.Empty<string>();
                },
                expectDirectory,
                minSizeBytes,
                minEntryCount: null);
        }
    }

    public static class ModelFileResolver
    {
        public readonly struct ResolverFailure
        {
            public ResolverFailure(string modelId, string message, DateTime timestamp)
            {
                ModelId = modelId;
                Message = message;
                Timestamp = timestamp;
            }

            public string ModelId { get; }
            public string Message { get; }
            public DateTime Timestamp { get; }
        }

        private const int MaxTrackedFailures = 32;
        private static readonly ConcurrentQueue<ResolverFailure> s_RecentFailures = new ConcurrentQueue<ResolverFailure>();

        private static readonly Dictionary<string, long> s_DefaultMinFileSizes = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase)
        {
            [".onnx"] = 1024,
            [".tflite"] = 1024,
            [".pt"] = 1024,
            [".bin"] = 512,
            [".model"] = 1024,
            [".json"] = 16,
            [".yaml"] = 16,
            [".yml"] = 16,
            [".txt"] = 8,
            [".fst"] = 16,
            [".far"] = 16,
            [".bpe"] = 8,
        };

        public static bool TryResolveFirstValidPath(
            SherpaONNXModelMetadata metadata,
            out string resolvedPath,
            Action<string> onFallback = null,
            bool recordFailures = true,
            params ModelFileCriteria[] criteria)
        {
            resolvedPath = null;

            void Notify(string message)
            {
                if (recordFailures)
                {
                    RecordResolverFailure(metadata?.modelId, message);
                }
                onFallback?.Invoke(message);
            }

            if (metadata == null || string.IsNullOrWhiteSpace(metadata.modelId))
            {
                Notify("Metadata is null or modelId is empty.");
                return false;
            }

            if (criteria == null || criteria.Length == 0)
            {
                Notify("No model file criteria specified.");
                return false;
            }

            foreach (var criterion in criteria)
            {
                var candidates = criterion.ResolveCandidates(metadata);
                if (candidates == null || candidates.Length == 0)
                {
                    continue;
                }

                foreach (var candidate in candidates)
                {
                    if (ValidateCandidate(candidate, criterion.ExpectDirectory, criterion.MinSizeBytes, criterion.MinEntryCount, out var failureReason))
                    {
                        resolvedPath = candidate;
                        ClearFailuresForModel(metadata.modelId);
                        return true;
                    }

                    if (!string.IsNullOrEmpty(candidate))
                    {
                        Notify($"Rejected candidate '{candidate}': {failureReason}");
                    }
                }
            }

            if (resolvedPath == null)
            {
                Notify($"No valid model file found for {metadata.modelId}.");
            }

            return false;
        }

        public static string ResolveRequiredFile(
            SherpaONNXModelMetadata metadata,
            string description,
            Action<string> onFallback = null,
            params ModelFileCriteria[] criteria)
        {
            if (TryResolveFirstValidPath(metadata, out var path, onFallback, recordFailures: true, criteria))
            {
                return path;
            }

            throw new InvalidOperationException($"Unable to locate {description} for model '{metadata?.modelId}'.");
        }

        public static string ResolveRequiredByKeywords(
            SherpaONNXModelMetadata metadata,
            string description,
            Action<string> onFallback = null,
            params string[] keywords)
        {
            return ResolveRequiredFile(metadata, description, onFallback, ModelFileCriteria.FromKeywords(keywords));
        }

        public static string ResolveOptionalFile(
            SherpaONNXModelMetadata metadata,
            Action<string> onFallback = null,
            params ModelFileCriteria[] criteria)
        {
            if (metadata == null || string.IsNullOrWhiteSpace(metadata.modelId))
            {
                return null;
            }

            if (criteria == null || criteria.Length == 0)
            {
                return null;
            }

            Action<string> fallbackAction = null;
            if (onFallback != null)
            {
                var suppressMessage = $"No valid model file found for {metadata.modelId}.";
                fallbackAction = message =>
                {
                    if (!string.Equals(message, suppressMessage, StringComparison.OrdinalIgnoreCase))
                    {
                        onFallback(message);
                    }
                };
            }

            return TryResolveFirstValidPath(metadata, out var path, fallbackAction ?? onFallback, recordFailures: false, criteria) ? path : null;
        }

        public static string ResolveOptionalByKeywords(
            SherpaONNXModelMetadata metadata,
            Action<string> onFallback = null,
            params string[] keywords)
        {
            return ResolveOptionalFile(metadata, onFallback, ModelFileCriteria.FromKeywords(keywords));
        }

        public static string ResolveOptionalDirectoryByKeywords(
            SherpaONNXModelMetadata metadata,
            Action<string> onFallback = null,
            params string[] keywords)
        {
            return ResolveOptionalFile(metadata, onFallback, ModelFileCriteria.FromDirectoryKeywords(keywords), ModelFileCriteria.FromKeywords(keywords));
        }

        public static string[] FilterValidFiles(IEnumerable<string> paths, Action<string> onRejected = null)
        {
            if (paths == null)
            {
                return Array.Empty<string>();
            }

            var results = new List<string>();

            foreach (var path in paths)
            {
                if (ValidateCandidate(path, expectDirectory: false, minSizeBytes: null, minEntryCount: null, out var failureReason))
                {
                    results.Add(path);
                }
                else if (!string.IsNullOrEmpty(path))
                {
                    onRejected?.Invoke($"Rejected file '{path}': {failureReason}");
                }
            }

            return results.ToArray();
        }

        private static bool ValidateCandidate(string path, bool expectDirectory, long? minSizeBytes, int? minEntryCount, out string failureReason)
        {
            failureReason = string.Empty;

            if (string.IsNullOrWhiteSpace(path))
            {
                failureReason = "Path is null or empty.";
                return false;
            }

            if (expectDirectory)
            {
                if (!Directory.Exists(path))
                {
                    failureReason = "Directory does not exist.";
                    return false;
                }

                var minimumEntries = Math.Max(1, minEntryCount ?? 1);
                var hasEntries = Directory.EnumerateFileSystemEntries(path).Take(minimumEntries).Count() >= minimumEntries;
                if (!hasEntries)
                {
                    failureReason = "Directory is empty.";
                    return false;
                }

                return true;
            }

            if (!File.Exists(path))
            {
                failureReason = "File does not exist.";
                return false;
            }

            try
            {
                var fileInfo = new FileInfo(path);
                var requiredSize = minSizeBytes ?? GetDefaultMinimumSize(fileInfo.Extension);
                if (fileInfo.Length < requiredSize)
                {
                    failureReason = $"File size {fileInfo.Length} bytes is below minimum threshold ({requiredSize} bytes).";
                    return false;
                }
            }
            catch (Exception ex)
            {
                failureReason = $"Failed to inspect file: {ex.Message}";
                return false;
            }

            return true;
        }

        private static long GetDefaultMinimumSize(string extension)
        {
            if (string.IsNullOrWhiteSpace(extension))
            {
                return 1;
            }

            if (s_DefaultMinFileSizes.TryGetValue(extension, out var threshold))
            {
                return threshold;
            }

            return 1;
        }

        public static IReadOnlyList<ResolverFailure> GetRecentFailures()
        {
            return s_RecentFailures.ToArray();
        }

        private static void RecordResolverFailure(string modelId, string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            s_RecentFailures.Enqueue(new ResolverFailure(modelId, message, DateTime.UtcNow));

            while (s_RecentFailures.Count > MaxTrackedFailures && s_RecentFailures.TryDequeue(out _))
            {
                // Trim oldest entries.
            }
        }

        private static void ClearFailuresForModel(string modelId)
        {
            if (string.IsNullOrWhiteSpace(modelId) || s_RecentFailures.IsEmpty)
            {
                return;
            }

            var survivors = new List<ResolverFailure>(s_RecentFailures.Count);

            while (s_RecentFailures.TryDequeue(out var failure))
            {
                if (!string.Equals(failure.ModelId, modelId, StringComparison.OrdinalIgnoreCase))
                {
                    survivors.Add(failure);
                }
            }

            for (int i = 0; i < survivors.Count; i++)
            {
                s_RecentFailures.Enqueue(survivors[i]);
            }
        }
    }
}
