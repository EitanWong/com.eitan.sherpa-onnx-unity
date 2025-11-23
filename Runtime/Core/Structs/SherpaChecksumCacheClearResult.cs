using System;
using System.Collections.Generic;

namespace Eitan.SherpaONNXUnity.Runtime
{
    /// <summary>
    /// Result data for checksum cache deletion attempts.
    /// </summary>
    public readonly struct SherpaChecksumCacheClearResult
    {
        public SherpaChecksumCacheClearResult(
            string cacheDirectory,
            bool directoryFound,
            int deletedFiles,
            int failedFiles,
            IReadOnlyList<string> errors)
        {
            CacheDirectory = cacheDirectory ?? string.Empty;
            DirectoryFound = directoryFound;
            DeletedFiles = Math.Max(0, deletedFiles);
            FailedFiles = Math.Max(0, failedFiles);
            Errors = errors ?? Array.Empty<string>();
        }

        /// <summary>Absolute directory that was inspected.</summary>
        public string CacheDirectory { get; }

        /// <summary>True if the cache directory existed on disk.</summary>
        public bool DirectoryFound { get; }

        /// <summary>Number of checksum files deleted successfully.</summary>
        public int DeletedFiles { get; }

        /// <summary>Number of checksum files that failed to delete.</summary>
        public int FailedFiles { get; }

        /// <summary>Detailed error messages for failures, if any.</summary>
        public IReadOnlyList<string> Errors { get; }

        /// <summary>True when at least one checksum file was removed.</summary>
        public bool AnyDeleted => DeletedFiles > 0;

        /// <summary>True when any failure occurred.</summary>
        public bool HasErrors => FailedFiles > 0 || (Errors?.Count ?? 0) > 0;
    }
}
