namespace Eitan.SherpaONNXUnity.Runtime.Utilities
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
    using System.Collections.Generic;

    /// <summary>
    /// Represents the result of a decompression operation with detailed metrics.
    /// </summary>
    public class DecompressionEventArgs : EventArgs
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
        public bool UseSystemTarDecompress = false;
    }

    /// <summary>
    /// A high-performance, parallelized, low-GC utility for decompressing archives using SharpZipLib.
    /// This optimized version focuses on high-throughput I/O, memory efficiency, and throttled progress reporting.
    /// </summary>
    internal static class SherpaDecompressHelper
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
            {

                return new DecompressionEventArgs(false, "Source archive path and destination directory must not be empty.");
            }


            if (!File.Exists(sourceArchivePath))
            {

                return new DecompressionEventArgs(false, $"Source file not found: {sourceArchivePath}");
            }


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
            {
                return ExtractZipAsync(baseStream, destination, options, progress, ct);
            }

            if (lowerCasePath.EndsWith(".tar.gz") || lowerCasePath.EndsWith(".tgz"))
            {
                // Prefer system 'tar' (fast, native). Fallback to SharpZipLib on failure.
                string archivePath = baseStream.Name;
                return ExtractTarGzWithSystemTarFallbackAsync(archivePath, destination, baseStream, options, progress, ct);
            }

            if (lowerCasePath.EndsWith(".tar.bz2") || lowerCasePath.EndsWith(".tbz2") || lowerCasePath.EndsWith(".tb2"))
            {
                // NEW: Prefer using the system 'tar' (fast, native, low-memory). Fall back to SharpZipLib on failure.
                // We have the real path via baseStream.Name.
                string archivePath = baseStream.Name;

                // Try native tar first
                return ExtractTarBz2Async(archivePath, destination, baseStream, options, progress, ct);
            }

            if (lowerCasePath.EndsWith(".tar"))
            {
                return ExtractTarStreamAsync(baseStream, baseStream, destination, options, progress, ct);
            }

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
        /// Handles .tar.gz/.tgz extraction with system 'tar' first. Provides realtime progress.
        /// Falls back to SharpZipLib if native tar is unavailable or fails.
        /// </summary>
        private static async Task<long> ExtractTarGzWithSystemTarFallbackAsync(
            string archivePath, string destination, FileStream baseStream,
            DecompressionOptions options, IProgress<float> progress, CancellationToken ct)
        {
            var result = await ExtractWithSystemTarAsync(
                archivePath: archivePath,
                destination: destination,
                compressionFlag: "z", // gzip
                options: options,
                progress: progress,
                ct: ct);

            if (result.success)
            {
                return result.bytesProcessed;
            }

            // Fallback: managed extraction via SharpZipLib
            var gzipStream = new GZipInputStream(baseStream) { IsStreamOwner = false };
            return await ExtractTarStreamAsync(gzipStream, baseStream, destination, options, progress, ct);
        }

        /// <summary>
        /// Handles .tar.bz2 extraction with system 'tar' first. While using system tar,
        /// we compute exact extracted bytes and report progress in realtime. If system tar
        /// is unavailable or fails, we fall back to SharpZipLib.
        /// </summary>
        private static async Task<long> ExtractTarBz2Async(
            string archivePath, string destination, FileStream baseStream,
            DecompressionOptions options, IProgress<float> progress, CancellationToken ct)
        {
            // First try native tar with progress reporting
            if (options.UseSystemTarDecompress)
            {
                var result = await ExtractWithSystemTarAsync(
                    archivePath: archivePath,
                    destination: destination,
                    compressionFlag: "j",                    // bzip2
                    options: options,
                    progress: progress,
                    ct: ct);

                if (result.success)
                {
                    return result.bytesProcessed;
                }
            }

            // Fall back to managed extraction via SharpZipLib (original behavior)
            var bzip2Stream = new BZip2InputStream(baseStream) { IsStreamOwner = false };
            return await ExtractTarStreamAsync(bzip2Stream, baseStream, destination, options, progress, ct);
        }

        /// <summary>
        /// Extract using system 'tar' while providing precise uncompressed bytes processed and realtime progress.
        /// Strategy:
        /// 1) Build a lightweight TAR index (name -> size) using SharpZipLib when EnableAccurateProgress = true.
        ///    This reuses our managed parser and matches the managed path's accuracy expectations.
        /// 2) Run system tar with -xv (single verbose) so we receive a filename event as each entry starts extracting.
        /// 3) For each file, poll its on-disk length at short intervals, reporting byte deltas against the known size.
        ///    This yields smooth progress for large files and exact totals on completion.
        /// 4) If any step fails (no tar, bad args, etc.), we return (success: false) to trigger managed fallback.
        /// </summary>
        private static async Task<(bool success, long bytesProcessed)> ExtractWithSystemTarAsync(
            string archivePath,
            string destination,
            string compressionFlag, // e.g. "j" (bzip2), "z" (gzip) – keep generic for future reuse
            DecompressionOptions options,
            IProgress<float> progress,
            CancellationToken ct)
        {
            if (string.IsNullOrEmpty(archivePath) || string.IsNullOrEmpty(destination))
            {

                return (false, 0L);
            }



            Directory.CreateDirectory(destination);

            // Build a name->size map and total size if accurate progress requested.
            Dictionary<string, long> sizeIndex = null;
            long totalUncompressedSize = 0;

            if (options?.EnableAccurateProgress == true)
            {
                try
                {
                    (sizeIndex, totalUncompressedSize) = await BuildTarIndexForBzip2Async(archivePath, ct);
                }
                catch
                {
                    // If index fails, we still can proceed without it (will report only step-wise per-file completion).
                    sizeIndex = null;
                    totalUncompressedSize = 0;
                }
            }

            // Progress reporter selection: if we have total, use Accurate; otherwise, throttle simple pulses based on bytes observed.
            IProgressReporter reporter = null;
            if (totalUncompressedSize > 0)
            {
                reporter = new AccurateProgressReporter(progress, totalUncompressedSize);
            }
            else
            {
                // Fallback: emulate the "simple" reporter behavior using byte deltas with an unknown total.
                // We approximate a denominator using cumulative bytes + a small epsilon to avoid division by zero.
                reporter = new FallbackIndeterminateProgressReporter(progress);
            }
            // Emit an initial progress pulse so UI shows activity immediately.
            progress?.Report(0f);

            // Compose tar arguments. We'll use single -v to get one filename per entry start (less noise than -vv),
            // and rely on our index for sizes.
            string quotedArchive = Quote(archivePath);
            //string quotedDest = Quote(destination);

            // Build candidates for tar extraction without -C; WorkingDirectory is set to destination.
            var candidates = (compressionFlag == "j")
                ? new[]
                  {
                      $"-x{compressionFlag}f {quotedArchive} -v",
                      $"-x -f {quotedArchive} -{compressionFlag} -v",
                      $"-x -f {quotedArchive} --bzip2 -v"
                  }
                : new[]
                  {
                      $"-x{compressionFlag}f {quotedArchive} -v",
                      $"-x -f {quotedArchive} -{compressionFlag} -v"
                  };

            foreach (var args in candidates)
            {
                try
                {
                    using (var process = new Process())
                    {
                        process.StartInfo = new ProcessStartInfo
                        {
                            FileName = "tar",
                            Arguments = args,
                            UseShellExecute = false,
                            RedirectStandardOutput = true, // verbose lines (filenames) usually go to stdout when writing to disk
                            RedirectStandardError = true,
                            CreateNoWindow = true,
                            WorkingDirectory = destination,
                        };

                        var tcsExit = new TaskCompletionSource<int>();
                        long bytesProcessed = 0;

                        // State for current file being extracted
                        string currentTarPath = null; // path inside archive (with '/')
                        string currentAbsPath = null; // absolute path on disk
                        long currentExpectedSize = 0;
                        long currentLastObserved = 0;
                        CancellationTokenSource monitorCts = null;

                        void StopMonitorAndFinalize()
                        {
                            if (monitorCts != null)
                            {
                                try { monitorCts.Cancel(); } catch { }
                                monitorCts.Dispose();
                                monitorCts = null;
                            }

                            if (!string.IsNullOrEmpty(currentAbsPath))
                            {
                                try
                                {
                                    var fi = new FileInfo(currentAbsPath);
                                    if (fi.Exists)
                                    {
                                        long len = fi.Length;
                                        if (len > currentLastObserved)
                                        {
                                            long delta = len - currentLastObserved;
                                            currentLastObserved = len;
                                            bytesProcessed += delta;
                                            reporter.ReportBytesWritten(delta);
                                        }
                                    }
                                }
                                catch { /* ignore final stat failure */ }
                            }

                            // Reset current file state
                            currentTarPath = null;
                            currentAbsPath = null;
                            currentExpectedSize = 0;
                            currentLastObserved = 0;
                        }

                        // Start process and wire events
                        process.EnableRaisingEvents = true;
                        process.Exited += (s, e) =>
                        {
                            try { tcsExit.TrySetResult(process.ExitCode); } catch { }
                        };

                        if (!process.Start())
                        {
                            continue;
                        }

                        // Shared line handler for tar verbose output from either STDOUT or STDERR
                        void HandleTarLine(string raw)
                        {
                            if (raw == null)
                            {
                                return;
                            }
                            // Preserve leading spaces in file names; only strip a trailing CR from Windows pipes.

                            string line = raw.EndsWith("\r", StringComparison.Ordinal) ? raw.Substring(0, raw.Length - 1) : raw;
                            if (line.Length == 0)
                            {
                                return;
                            }

                            // Common verbose formats:
                            //  - GNU/bsdtar: "x path/to/file"
                            //  - Some builds: just "path/to/file"
                            // We only trim the leading "x " token if present; DO NOT Trim() the rest.

                            string name = line.StartsWith("x ", StringComparison.Ordinal) ? line.Substring(2) : line;

                            // Heuristic: discard lines that clearly aren't file paths (contain control chars or look like option echoes)
                            if (name.StartsWith("--", StringComparison.Ordinal) || name.IndexOfAny(new[] { '\t', '\n' }) >= 0)
                            {
                                return;
                            }

                            // Ignore obvious diagnostic lines (stderr noise), keep file names even if they have ':'.
                            if (name.StartsWith("tar: ", StringComparison.OrdinalIgnoreCase) ||
                                name.StartsWith("bsdtar: ", StringComparison.OrdinalIgnoreCase) ||
                                name.StartsWith("tar.exe: ", StringComparison.OrdinalIgnoreCase))
                            {
                                return;
                            }

                            // Directory entries often end with '/' in tar listings; skip them for byte counting.
                            if (name.EndsWith("/", StringComparison.Ordinal))
                            {
                                return;
                            }

                            // Starting a new file: finalize previous and start monitoring this one

                            StopMonitorAndFinalize();

                            currentTarPath = NormalizeTarPath(name);
                            currentAbsPath = GetSafeEntryPath(destination, currentTarPath);

                            if (sizeIndex != null && sizeIndex.TryGetValue(currentTarPath, out var size))
                            {
                                currentExpectedSize = size;
                            }
                            else
                            {
                                currentExpectedSize = 0; // Unknown size; we'll still poll and report deltas we see.
                            }
                            currentLastObserved = 0;

                            // Start a lightweight polling monitor for the file while it is being written
                            monitorCts = new CancellationTokenSource();
                            var linked = CancellationTokenSource.CreateLinkedTokenSource(monitorCts.Token, ct);
                            _ = Task.Run(async () =>
                            {
                                var pollInterval = TimeSpan.FromMilliseconds(100);
                                try
                                {
                                    while (!linked.Token.IsCancellationRequested)
                                    {
                                        long len = 0;
                                        try
                                        {
                                            var fi = new FileInfo(currentAbsPath);
                                            if (fi.Exists)
                                            {
                                                len = fi.Length;
                                            }

                                        }
                                        catch
                                        {
                                            // ignore transient errors (e.g., file not yet opened by tar)
                                        }

                                        if (len > currentLastObserved)
                                        {
                                            long delta = len - currentLastObserved;
                                            currentLastObserved = len;
                                            bytesProcessed += delta;
                                            reporter.ReportBytesWritten(delta);

                                            if (currentExpectedSize > 0 && currentLastObserved >= currentExpectedSize)
                                            {
                                                break;
                                            }
                                        }

                                        await Task.Delay(pollInterval, linked.Token).ConfigureAwait(false);
                                    }
                                }
                                catch { /* ignore */ }
                            });
                        }

                        // Begin consuming BOTH stdout and stderr to capture verbose lines on all platforms
                        process.OutputDataReceived += (s, e) => { if (e.Data != null) { HandleTarLine(e.Data); } };
                        process.ErrorDataReceived += (s, e) => { if (e.Data != null) { HandleTarLine(e.Data); } };

                        process.BeginOutputReadLine();
                        process.BeginErrorReadLine();

                        using (ct.Register(() =>
                        {
                            try { if (!process.HasExited) { process.Kill(); } } catch { }
                        }))
                        {
                            int exitCode = await tcsExit.Task.ConfigureAwait(false);

                            // Ensure all output has been flushed (prevents missing the last filename line on some runtimes)
                            try { process.WaitForExit(); } catch { /* ignore */ }

                            // Finalize any last file after process exits
                            StopMonitorAndFinalize();

                            if (exitCode == 0)
                            {
                                // Ensure we signal completion to the UI
                                if (reporter is AccurateProgressReporter acc && totalUncompressedSize > 0)
                                {
                                    // Force 100%
                                    acc.ReportBytesWritten(0);
                                }
                                else
                                {
                                    (reporter as FallbackIndeterminateProgressReporter)?.ForceComplete();
                                }

                                return (true, bytesProcessed);
                            }
                        }
                    }
                }
                catch
                {
                    // try next candidate
                }
            }

            return (false, 0L);
        }

        /// <summary>
        /// Build a TAR index (name -> size) and total size for .tar.bz2 using SharpZipLib.
        /// This avoids extracting and keeps memory usage low by streaming headers only.
        /// </summary>
        private static Task<(Dictionary<string, long> map, long total)> BuildTarIndexForBzip2Async(string archivePath, CancellationToken ct)
        {
            return Task.Run<(Dictionary<string, long>, long)>(() =>
            {
                var map = new Dictionary<string, long>(StringComparer.Ordinal);
                long total = 0;

                using var fs = new FileStream(archivePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                using var bzip = new BZip2InputStream(fs) { IsStreamOwner = false };
                using var tar = new TarInputStream(bzip, Encoding.UTF8) { IsStreamOwner = false };

                TarEntry entry;
                while ((entry = tar.GetNextEntry()) != null)
                {
                    ct.ThrowIfCancellationRequested();
                    if (entry.IsDirectory)
                    {
                        continue;
                    }


                    var tarPath = NormalizeTarPath(entry.Name);
                    long size = Math.Max(0, entry.Size);
                    map[tarPath] = size;
                    total += size;

                    // Skip entry content efficiently (TarInputStream already advances on next GetNextEntry)
                }

                return (map, total);
            }, ct);
        }

        /// <summary>
        /// Normalize an entry name to canonical TAR form (always '/').
        /// </summary>
        private static string NormalizeTarPath(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return name;
            }


            return name.Replace('\\', '/');
        }

        /// <summary>
        /// Progress reporter used when we don't know total size; it reports a pulsing fraction based on elapsed bytes,
        /// and completes at 100% when forced. Keeps UX consistent without heavy pre-scan.
        /// </summary>
        private sealed class FallbackIndeterminateProgressReporter : IProgressReporter
        {
            private readonly IProgress<float> _progress;
            private readonly Stopwatch _sw = Stopwatch.StartNew();
            private const double IntervalMs = 100;
            private long _totalBytesObserved;

            public FallbackIndeterminateProgressReporter(IProgress<float> progress)
            {
                _progress = progress;
            }

            public void ReportBytesWritten(long bytes)
            {
                if (_progress == null)
                {
                    return;
                }


                Interlocked.Add(ref _totalBytesObserved, bytes);

                if (_sw.Elapsed.TotalMilliseconds >= IntervalMs)
                {
                    // Map to a soft curve approaching 1, without ever reaching it prematurely.
                    // This avoids lying while still giving a sense of motion.
                    var observed = Math.Max(1, Interlocked.Read(ref _totalBytesObserved));
                    float fraction = (float)(1.0 - (1.0 / Math.Log10(observed + 10))); // smooth, increases with bytes
                    fraction = Math.Max(0f, Math.Min(0.99f, fraction));
                    _progress.Report(fraction);
                    _sw.Restart();
                }
            }

            public void ReportPosition(long position) { /* not used */ }

            public void ForceComplete()
            {
                _progress?.Report(1f);
            }
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
                            if (entry.IsDirectory)
                            {
                                continue;
                            }


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
                if (_progress == null || _totalSize <= 0)
                {
                    return;
                }


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
                if (_progress == null || _totalSize <= 0)
                {
                    return;
                }


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



        /// <summary>
        /// Quote a path for the shell if it isn't already quoted.
        /// </summary>
        private static string Quote(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return path;
            }
            // Simple defensive quoting; avoids double-quoting if already wrapped.

            if (path.Length >= 2 && path[0] == '"' && path[path.Length - 1] == '"')
            {
                return path;
            }

            return $"\"{path}\"";
        }
    }
}
