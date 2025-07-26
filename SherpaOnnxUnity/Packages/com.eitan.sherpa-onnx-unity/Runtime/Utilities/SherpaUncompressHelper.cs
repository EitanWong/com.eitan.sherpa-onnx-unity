namespace Eitan.SherpaOnnxUnity.Runtime.Utilities
{
    using ICSharpCode.SharpZipLib.BZip2;
    using ICSharpCode.SharpZipLib.GZip;
    using ICSharpCode.SharpZipLib.Tar;
    using ICSharpCode.SharpZipLib.Zip;
    using System;
    using System.Buffers;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Security;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Represents the result of a decompression operation with detailed metrics.
    /// </summary>
    public class DecompressionEventArgs:EventArgs
    {
        public bool Success { get; }
        public string ErrorMessage { get; }
        public long BytesProcessed { get; }
        public TimeSpan ElapsedTime { get; }
        public float Progress { get; }


        public DecompressionEventArgs(bool success, string errorMessage = null, long bytesProcessed = 0, float progress = 0, TimeSpan elapsedTime = default)
        {
            Success = success;
            ErrorMessage = errorMessage;
            BytesProcessed = bytesProcessed;
            Progress = progress;
            ElapsedTime = elapsedTime;
        }
    }

    /// <summary>
    /// Enhanced decompression options for fine-tuning performance.
    /// </summary>
    public class DecompressionOptions
    {
        /// <summary>
        /// Buffer size for I/O operations. Default is 1MB, which is a good balance for modern SSDs.
        /// </summary>
        public int BufferSize { get; set; } = 1_048_576; // 1MB

        /// <summary>
        /// Enables parallel extraction for ZIP archives. Not applicable to TAR archives.
        /// </summary>
        public bool UseParallelExtraction { get; set; } = true;

        /// <summary>
        /// Maximum number of concurrent threads for parallel ZIP extraction.
        /// </summary>
        public int MaxDegreeOfParallelism { get; set; } = Environment.ProcessorCount;

        /// <summary>
        /// Pre-allocates file space to potentially reduce fragmentation and improve write speed.
        /// </summary>
        public bool PreAllocateFiles { get; set; } = false;

        /// <summary>
        /// Enables pre-scanning of TAR archives to calculate total uncompressed size for accurate progress reporting.
        /// This adds overhead but provides linear progress updates.
        /// </summary>
        public bool EnableAccurateProgress { get; set; } = false;
    }

    /// <summary>
    /// A high-performance, parallelized, low-GC utility for decompressing archives using SharpZipLib.
    /// This optimized version focuses on high-throughput I/O, memory efficiency, and throttled progress reporting.
    /// </summary>
    internal static class SherpaUncompressHelper
    {
        private static readonly DecompressionOptions DefaultOptions = new DecompressionOptions();

        /// <summary>
        /// Asynchronously decompresses a source archive file to a destination directory.
        /// </summary>
        public static Task<DecompressionEventArgs> DecompressAsync(
            string sourceArchivePath,
            string destinationDirectory,
            IProgress<DecompressionEventArgs> progress = null,
            CancellationToken cancellationToken = default)
        {
            return DecompressAsync(sourceArchivePath, destinationDirectory, DefaultOptions, progress, cancellationToken);
        }

        /// <summary>
        /// Asynchronously decompresses a source archive file with custom options.
        /// </summary>
        public static async Task<DecompressionEventArgs> DecompressAsync(
            string sourceArchivePath,
            string destinationDirectory,
            DecompressionOptions options,
            IProgress<DecompressionEventArgs> progress = null,
            CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();
            long bytesProcessed = 0;

            if (string.IsNullOrEmpty(sourceArchivePath) || string.IsNullOrEmpty(destinationDirectory))
                return new DecompressionEventArgs(false, "Source archive path and destination directory must not be empty.");
            if (!File.Exists(sourceArchivePath))
                return new DecompressionEventArgs(false, $"Source file not found: {sourceArchivePath}");

            options ??= DefaultOptions;
            DecompressionEventArgs args;
            try
            {
                Directory.CreateDirectory(destinationDirectory);

                using var fileStream = new FileStream(sourceArchivePath, FileMode.Open, FileAccess.Read, FileShare.Read, options.BufferSize, FileOptions.Asynchronous);
                
                Progress<float> progressAdapter = new Progress<float>(_progressValue =>
                { 
                    progress?.Report(new DecompressionEventArgs(true, null, bytesProcessed, progress: _progressValue, elapsedTime: stopwatch.Elapsed));
                });


                bytesProcessed = await ExtractAsync(fileStream, destinationDirectory, sourceArchivePath.ToLowerInvariant(), options, progressAdapter, cancellationToken);

                stopwatch.Stop();
                args = new DecompressionEventArgs(true, null, bytesProcessed, progress: 1, elapsedTime: stopwatch.Elapsed);
                progress?.Report(args);
                return args;
            }
            catch (OperationCanceledException)
            {
                stopwatch.Stop();
                args = new DecompressionEventArgs(false, "Decompression was cancelled.", bytesProcessed, elapsedTime: stopwatch.Elapsed);
                return args;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                args = new DecompressionEventArgs(false, $"An error occurred: {ex.Message}", bytesProcessed, elapsedTime: stopwatch.Elapsed);
                return args;
            }
        }

        private static Task<long> ExtractAsync(FileStream baseStream, string destination, string lowerCasePath,
                                                     DecompressionOptions options, IProgress<float> progress, CancellationToken ct)
        {
            if (lowerCasePath.EndsWith(".zip"))
                return ExtractZipAsync(baseStream, destination, options, progress, ct);

            if (lowerCasePath.EndsWith(".tar.gz") || lowerCasePath.EndsWith(".tgz"))
            {
                // FIXED: Remove using statement to prevent premature disposal
                var gzipStream = new GZipInputStream(baseStream) { IsStreamOwner = false };
                return ExtractTarStreamAsync(gzipStream, baseStream, destination, options, progress, ct);
            }

            if (lowerCasePath.EndsWith(".tar.bz2") || lowerCasePath.EndsWith(".tbz2") || lowerCasePath.EndsWith(".tb2"))
            {
                // FIXED: Remove using statement to prevent premature disposal
                var bzip2Stream = new BZip2InputStream(baseStream) { IsStreamOwner = false };
                return ExtractTarStreamAsync(bzip2Stream, baseStream, destination, options, progress, ct);
            }

            if (lowerCasePath.EndsWith(".tar"))
                return ExtractTarStreamAsync(baseStream, baseStream, destination, options, progress, ct);
            
            if (lowerCasePath.EndsWith(".gz"))
            {
                // FIXED: Remove using statement to prevent premature disposal
                var gzipStream = new GZipInputStream(baseStream) { IsStreamOwner = false };
                var newPath = Path.Combine(destination, Path.GetFileNameWithoutExtension(lowerCasePath));
                return WriteStreamToFileAsync(gzipStream, newPath, baseStream, options, progress, ct);
            }

            if (lowerCasePath.EndsWith(".bz2"))
            {
                // FIXED: Remove using statement to prevent premature disposal
                var bzip2Stream = new BZip2InputStream(baseStream) { IsStreamOwner = false };
                var newPath = Path.Combine(destination, Path.GetFileNameWithoutExtension(lowerCasePath));
                return WriteStreamToFileAsync(bzip2Stream, newPath, baseStream, options, progress, ct);
            }

            throw new NotSupportedException($"Unsupported archive format: {Path.GetFileName(lowerCasePath)}");
        }

        /// <summary>
        /// Extracts a TAR stream. This method is optimized for performance by handling synchronous decompression
        /// on a background thread while using asynchronous file writes.
        /// </summary>
        private static Task<long> ExtractTarStreamAsync(Stream compressionStream, FileStream baseStream, string destination,
                                                              DecompressionOptions options, IProgress<float> progress, CancellationToken ct)
        {
            return Task.Run(async () =>
            {
                long totalBytesProcessed = 0;
                var buffer = ArrayPool<byte>.Shared.Rent(options.BufferSize);
                
                try
                {
                    // FIXED: Manage compression stream lifecycle here and implement accurate progress for TAR
                    using (compressionStream) // Properly dispose the compression stream
                    using (var tarInputStream = new TarInputStream(compressionStream, Encoding.UTF8) { IsStreamOwner = false })
                    {
                        IProgressReporter progressReporter;
                        
                        // FIXED: Implement accurate progress reporting for TAR archives
                        if (options.EnableAccurateProgress && compressionStream != baseStream)
                        {
                            // Pre-scan to calculate total uncompressed size for accurate progress
                            var totalUncompressedSize = await CalculateTotalUncompressedSizeAsync(compressionStream, ct);
                            progressReporter = new AccurateProgressReporter(progress, totalUncompressedSize);
                        }
                        else
                        {
                            // Use baseStream position for progress (less accurate but no overhead)
                            progressReporter = new SimpleProgressReporter(progress, baseStream.Length);
                        }

                        TarEntry entry;
                        while ((entry = tarInputStream.GetNextEntry()) != null)
                        {
                            ct.ThrowIfCancellationRequested();
                            if (entry.IsDirectory) continue;

                            var entryPath = GetSafeEntryPath(destination, entry.Name);
                            Directory.CreateDirectory(Path.GetDirectoryName(entryPath));

                            using (var fileStreamOut = new FileStream(entryPath, FileMode.Create, FileAccess.Write, FileShare.None, options.BufferSize, FileOptions.Asynchronous))
                            {
                                if (options.PreAllocateFiles && entry.Size > 0)
                                {
                                    fileStreamOut.SetLength(entry.Size);
                                }

                                int bytesRead;
                                long entryBytesProcessed = 0;
                                
                                // FIXED: Use synchronous read for CPU-bound decompression, async write for I/O
                                while ((bytesRead = tarInputStream.Read(buffer, 0, buffer.Length)) > 0)
                                {
                                    ct.ThrowIfCancellationRequested();
                                    await fileStreamOut.WriteAsync(buffer, 0, bytesRead, ct);
                                    entryBytesProcessed += bytesRead;
                                    
                                    // Report progress based on the type of reporter
                                    if (progressReporter is AccurateProgressReporter accurateReporter)
                                    {
                                        accurateReporter.ReportBytesWritten(bytesRead);
                                    }
                                    else if (progressReporter is SimpleProgressReporter simpleReporter)
                                    {
                                        simpleReporter.ReportPosition(baseStream.Position);
                                    }
                                }
                                
                                totalBytesProcessed += entryBytesProcessed;
                            }
                        }
                    }
                    
                    return totalBytesProcessed;
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                }
            }, ct);
        }

        /// <summary>
        /// Pre-scans a TAR archive to calculate the total uncompressed size for accurate progress reporting.
        /// </summary>
        private static async Task<long> CalculateTotalUncompressedSizeAsync(Stream compressionStream, CancellationToken ct)
        {
            return await Task.Run(() =>
            {
                long totalSize = 0;
                
                // Create a new stream from the same source for pre-scanning
                var originalPosition = compressionStream.Position;
                compressionStream.Position = 0;
                
                try
                {
                    using var tarInputStream = new TarInputStream(compressionStream, Encoding.UTF8) { IsStreamOwner = false };
                    TarEntry entry;
                    while ((entry = tarInputStream.GetNextEntry()) != null)
                    {
                        ct.ThrowIfCancellationRequested();
                        if (!entry.IsDirectory && entry.Size > 0)
                        {
                            totalSize += entry.Size;
                        }
                    }
                }
                finally
                {
                    // Reset position for actual extraction
                    compressionStream.Position = originalPosition;
                }
                
                return totalSize;
            }, ct);
        }

        private static Task<long> ExtractZipAsync(FileStream baseStream, string destination,
                                                        DecompressionOptions options, IProgress<float> progress, CancellationToken ct)
        {
            return Task.Run(() =>
            {
                using var zipFile = new ZipFile(baseStream) { IsStreamOwner = false };
                var fileEntries = zipFile.Cast<ZipEntry>().Where(e => e.IsFile).ToList();
                long totalUncompressedSize = fileEntries.Sum(e => e.Size);
                long totalBytesWritten = 0;
                var progressReporter = new AccurateProgressReporter(progress, totalUncompressedSize);

                Action<long> reportProgress = (bytes) =>
                {
                    long currentTotal = Interlocked.Add(ref totalBytesWritten, bytes);
                    progressReporter.ReportBytesWritten(0); // Just trigger update with current total
                    progressReporter.SetCurrentTotal(currentTotal);
                };

                if (!options.UseParallelExtraction || options.MaxDegreeOfParallelism <= 1)
                {
                    foreach (var entry in fileEntries)
                    {
                        ct.ThrowIfCancellationRequested();
                        ExtractSingleZipEntry(zipFile, entry, destination, options, reportProgress, ct);
                    }
                }
                else
                {
                    var parallelOptions = new ParallelOptions
                    {
                        CancellationToken = ct,
                        MaxDegreeOfParallelism = options.MaxDegreeOfParallelism
                    };
                    Parallel.ForEach(fileEntries, parallelOptions, entry =>
                    {
                        ExtractSingleZipEntry(zipFile, entry, destination, options, reportProgress, ct);
                    });
                }
                return totalBytesWritten;
            }, ct);
        }
        
        private static void ExtractSingleZipEntry(ZipFile zipFile, ZipEntry entry, string destination, DecompressionOptions options, Action<long> progressCallback, CancellationToken ct)
        {
            var buffer = ArrayPool<byte>.Shared.Rent(options.BufferSize);
            try
            {
                var entryPath = GetSafeEntryPath(destination, entry.Name);
                Directory.CreateDirectory(Path.GetDirectoryName(entryPath));

                Stream inputStream;
                lock (zipFile)
                {
                    inputStream = zipFile.GetInputStream(entry);
                }

                using (inputStream)
                using (var fileStreamOut = new FileStream(entryPath, FileMode.Create, FileAccess.Write, FileShare.None, options.BufferSize, FileOptions.Asynchronous))
                {
                    if (options.PreAllocateFiles && entry.Size >= 0)
                    {
                        fileStreamOut.SetLength(entry.Size);
                    }

                    int bytesRead;
                    while ((bytesRead = inputStream.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        ct.ThrowIfCancellationRequested();
                        fileStreamOut.Write(buffer, 0, bytesRead);
                        progressCallback?.Invoke(bytesRead);
                    }
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        private static Task<long> WriteStreamToFileAsync(Stream source, string filePath, FileStream baseStream,
                                                               DecompressionOptions options, IProgress<float> progress, CancellationToken ct)
        {
            return Task.Run(async () =>
            {
                long totalBytesWritten = 0;
                var buffer = ArrayPool<byte>.Shared.Rent(options.BufferSize);
                var progressReporter = new SimpleProgressReporter(progress, baseStream.Length);
                
                try
                {
                    // FIXED: Manage source stream lifecycle properly
                    using (source)
                    using (var fileStreamOut = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, options.BufferSize, FileOptions.Asynchronous))
                    {
                        int bytesRead;
                        // FIXED: Use synchronous read for CPU-bound decompression
                        while ((bytesRead = source.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            ct.ThrowIfCancellationRequested();
                            await fileStreamOut.WriteAsync(buffer, 0, bytesRead, ct);
                            totalBytesWritten += bytesRead;
                            progressReporter.ReportPosition(baseStream.Position);
                        }
                    }
                    
                    return totalBytesWritten;
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                }
            }, ct);
        }

        private static string GetSafeEntryPath(string destinationDirectory, string entryName)
        {
            var normalizedEntryName = entryName.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
            var entryPath = Path.Combine(destinationDirectory, normalizedEntryName);
            var fullEntryPath = Path.GetFullPath(entryPath);
            var fullDestinationPath = Path.GetFullPath(destinationDirectory);

            if (!fullEntryPath.StartsWith(fullDestinationPath, StringComparison.OrdinalIgnoreCase))
            {
                throw new SecurityException($"Entry '{entryName}' attempts to extract outside the destination directory.");
            }
            return fullEntryPath;
        }

        /// <summary>
        /// Interface for progress reporters to support different progress calculation strategies.
        /// </summary>
        private interface IProgressReporter
        {
            void ReportBytesWritten(long bytes);
            void ReportPosition(long position);
        }

        /// <summary>
        /// Progress reporter that provides accurate progress based on bytes written vs total uncompressed size.
        /// </summary>
        private class AccurateProgressReporter : IProgressReporter
        {
            private readonly IProgress<float> _progress;
            private readonly long _totalSize;
            private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
            private long _currentTotal = 0;
            private static readonly TimeSpan ReportInterval = TimeSpan.FromMilliseconds(100);

            public AccurateProgressReporter(IProgress<float> progress, long totalSize)
            {
                _progress = progress;
                _totalSize = totalSize;
            }

            public void ReportBytesWritten(long bytes)
            {
                if (_progress == null || _totalSize <= 0) return;
                
                Interlocked.Add(ref _currentTotal, bytes);
                
                if (_stopwatch.Elapsed > ReportInterval)
                {
                    var current = Interlocked.Read(ref _currentTotal);
                    _progress.Report(Math.Min(1.0f, (float)current / _totalSize));
                    _stopwatch.Restart();
                }
            }

            public void ReportPosition(long position)
            {
                // Not used for accurate progress reporting
            }

            public void SetCurrentTotal(long total)
            {
                Interlocked.Exchange(ref _currentTotal, total);
            }
        }

        /// <summary>
        /// Progress reporter that uses stream position for progress calculation (less accurate but no overhead).
        /// </summary>
        private class SimpleProgressReporter : IProgressReporter
        {
            private readonly IProgress<float> _progress;
            private readonly long _totalSize;
            private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
            private long _lastReportedPosition = -1;
            private static readonly TimeSpan ReportInterval = TimeSpan.FromMilliseconds(100);

            public SimpleProgressReporter(IProgress<float> progress, long totalSize)
            {
                _progress = progress;
                _totalSize = totalSize;
            }

            public void ReportBytesWritten(long bytes)
            {
                // Not used for simple progress reporting
            }

            public void ReportPosition(long currentPosition)
            {
                if (_progress == null || _totalSize <= 0) return;

                if (_stopwatch.Elapsed > ReportInterval)
                {
                    if (currentPosition > _lastReportedPosition)
                    {
                        _progress.Report((float)currentPosition / _totalSize);
                        _lastReportedPosition = currentPosition;
                        _stopwatch.Restart();
                    }
                }
            }
        }
    }
}