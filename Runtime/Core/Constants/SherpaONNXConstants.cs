using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Eitan.SherpaONNXUnity.Runtime.Utilities;
using UnityEngine.Networking;

namespace Eitan.SherpaONNXUnity.Runtime.Constants
{
    public partial class SherpaONNXConstants
    {
        private static readonly SherpaONNXModuleType[] ALL_MANIFEST_MODULE_TYPES = new[]
        {
            SherpaONNXModuleType.SpeechRecognition,
            SherpaONNXModuleType.VoiceActivityDetection,
            SherpaONNXModuleType.SpeechSynthesis,
            SherpaONNXModuleType.KeywordSpotting,
            SherpaONNXModuleType.SpeechEnhancement,
            SherpaONNXModuleType.SpokenLanguageIdentification,
            SherpaONNXModuleType.AddPunctuation,
            SherpaONNXModuleType.AudioTagging,
            SherpaONNXModuleType.SpeakerIdentification,
            SherpaONNXModuleType.SourceSeparation,
            SherpaONNXModuleType.SpeakerDiarization, // There is no checksum.txt file, so the model cannot be obtained
        };

        internal static IEnumerable<SherpaONNXModuleType> EnumerateManifestModuleTypes()
        {
            return ALL_MANIFEST_MODULE_TYPES;
        }

        private const int DefaultChecksumCacheTtlSeconds = 3600;

        private static bool ShouldFetchLatestManifest() =>
            SherpaONNXEnvironment.GetBool(SherpaONNXEnvironment.BuiltinKeys.FetchLatestManifest, @default: true);

        private static int GetChecksumCacheTtlSeconds()
        {
            try
            {
                return SherpaONNXEnvironment.GetInt(
                    SherpaONNXEnvironment.BuiltinKeys.ChecksumCacheTtlSeconds,
                    DefaultChecksumCacheTtlSeconds);
            }
            catch
            {
                return DefaultChecksumCacheTtlSeconds;
            }
        }

        private static bool TryReadChecksumCache(string tag, bool allowExpired, out string content)
        {
            content = string.Empty;
            var ttlSeconds = GetChecksumCacheTtlSeconds();
            if (ttlSeconds == 0)
            {
                return false; // caching disabled
            }

            var cachePath = BuildChecksumCachePath(tag);
            if (string.IsNullOrEmpty(cachePath) || !File.Exists(cachePath))
            {
                return false;
            }

            if (!allowExpired && ttlSeconds > 0)
            {
                var maxAge = TimeSpan.FromSeconds(ttlSeconds);
                var age = DateTime.UtcNow - File.GetLastWriteTimeUtc(cachePath);
                if (age > maxAge)
                {
                    return false;
                }
            }

            try
            {
                content = File.ReadAllText(cachePath);
                return !string.IsNullOrWhiteSpace(content);
            }
            catch (Exception ex)
            {
                SherpaLog.Warning($"SherpaONNX checksum cache read failed ({cachePath}): {ex.Message}");
                return false;
            }
        }

        private static void WriteChecksumCache(string tag, string content)
        {
            var ttlSeconds = GetChecksumCacheTtlSeconds();
            if (ttlSeconds == 0)
            {
                return;
            }

            var cachePath = BuildChecksumCachePath(tag);
            if (string.IsNullOrEmpty(cachePath))
            {
                return;
            }

            try
            {
                var directory = Path.GetDirectoryName(cachePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                File.WriteAllText(cachePath, content);
            }
            catch (Exception ex)
            {
                SherpaLog.Warning($"SherpaONNX checksum cache write failed ({cachePath}): {ex.Message}");
            }
        }

        private static string BuildChecksumCachePath(string tag)
        {
            try
            {
                var cacheDirectory = ResolveChecksumCacheDirectory();
                if (string.IsNullOrEmpty(cacheDirectory))
                {
                    return string.Empty;
                }

                var safeFileName = SanitizeFileName(string.IsNullOrWhiteSpace(tag) ? "default" : tag);
                return Path.Combine(cacheDirectory, $"{safeFileName}-checksum.txt");
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string ResolveChecksumCacheDirectory()
        {
            try
            {
                var root = ResolveChecksumCacheRoot();
                if (string.IsNullOrWhiteSpace(root))
                {
                    return string.Empty;
                }

                return Path.Combine(root, "manifest-cache");
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string ResolveChecksumCacheRoot()
        {
            var customRoot = SherpaONNXEnvironment.Get(SherpaONNXEnvironment.BuiltinKeys.ChecksumCacheDirectory, string.Empty);
            return string.IsNullOrWhiteSpace(customRoot) ? ResolveDefaultCacheRoot() : customRoot.Trim();
        }

        public static SherpaChecksumCacheClearResult ClearChecksumCache()
        {
            var cacheDirectory = ResolveChecksumCacheDirectory();
            if (string.IsNullOrWhiteSpace(cacheDirectory))
            {
                return new SherpaChecksumCacheClearResult(
                    cacheDirectory,
                    directoryFound: false,
                    deletedFiles: 0,
                    failedFiles: 0,
                    errors: new[] { "Checksum cache directory could not be resolved." });
            }

            if (!Directory.Exists(cacheDirectory))
            {
                return new SherpaChecksumCacheClearResult(
                    cacheDirectory,
                    directoryFound: false,
                    deletedFiles: 0,
                    failedFiles: 0,
                    errors: Array.Empty<string>());
            }

            var deleted = 0;
            var failed = 0;
            var errors = new List<string>();
            IEnumerable<string> filesToDelete;

            try
            {
                filesToDelete = Directory.EnumerateFiles(cacheDirectory, "*-checksum.txt", SearchOption.AllDirectories);
            }
            catch (Exception ex)
            {
                errors.Add(ex.Message);
                SherpaLog.Warning($"SherpaONNX checksum cache enumeration failed ({cacheDirectory}): {ex.Message}");
                return new SherpaChecksumCacheClearResult(
                    cacheDirectory,
                    directoryFound: true,
                    deletedFiles: 0,
                    failedFiles: 0,
                    errors: errors);
            }

            foreach (var file in filesToDelete)
            {
                try
                {
                    if (File.Exists(file))
                    {
                        File.Delete(file);
                        deleted++;
                    }
                }
                catch (Exception ex)
                {
                    failed++;
                    errors.Add($"{file}: {ex.Message}");
                    SherpaLog.Warning($"SherpaONNX checksum cache delete failed ({file}): {ex.Message}");
                }
            }

            TryDeleteEmptyDirectories(cacheDirectory, errors);

            return new SherpaChecksumCacheClearResult(
                cacheDirectory,
                directoryFound: true,
                deletedFiles: deleted,
                failedFiles: failed,
                errors: errors);
        }

        private static void TryDeleteEmptyDirectories(string directory, List<string> errors)
        {
            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
            {
                return;
            }

            try
            {
                var subDirectories = Directory.GetDirectories(directory);
                foreach (var sub in subDirectories)
                {
                    TryDeleteEmptyDirectories(sub, errors);
                }

                if (!Directory.EnumerateFileSystemEntries(directory).Any())
                {
                    Directory.Delete(directory);
                }
            }
            catch (Exception ex)
            {
                errors?.Add($"{directory}: {ex.Message}");
                SherpaLog.Warning($"SherpaONNX checksum cache cleanup failed ({directory}): {ex.Message}");
            }
        }

        private static string ResolveDefaultCacheRoot()
        {
            try
            {
                var unityTemp = UnityEngine.Application.temporaryCachePath;
                if (!string.IsNullOrEmpty(unityTemp))
                {
                    return Path.Combine(unityTemp, "SherpaONNXUnity");
                }
            }
            catch
            {
                // ignored, fall back to system temp
            }

            try
            {
                var systemTemp = Path.GetTempPath();
                if (!string.IsNullOrEmpty(systemTemp))
                {
                    return Path.Combine(systemTemp, "SherpaONNXUnity");
                }
            }
            catch
            {
                // ignored, as a last resort fall through to current directory
            }

            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory ?? ".", "SherpaONNXUnity");
        }

        private static string SanitizeFileName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return "default";
            }

            var invalidChars = Path.GetInvalidFileNameChars();
            var buffer = new char[name.Length];
            var length = 0;
            for (int i = 0; i < name.Length; i++)
            {
                var ch = name[i];
                buffer[length++] = Array.IndexOf(invalidChars, ch) >= 0 ? '_' : ch;
            }

            return new string(buffer, 0, length);
        }

        // Read-only initialization blacklist (unified): supports Exact, Prefix, Suffix, Contains, and Regex.
        private enum InitFileNameMatchKind { Exact, Prefix, Suffix, Contains, Regex }

        private sealed class InitFileNameBlacklistRule
        {
            public readonly InitFileNameMatchKind Kind;
            public readonly string Pattern; // used for non-regex kinds
            public readonly Regex Regex;     // used for Regex kind

            public InitFileNameBlacklistRule(InitFileNameMatchKind kind, string pattern)
            {
                Kind = kind;
                Pattern = pattern ?? string.Empty;
                if (kind == InitFileNameMatchKind.Regex && !string.IsNullOrEmpty(pattern))
                {
                    // Avoid RegexOptions.Compiled for maximum IL2CPP portability
                    Regex = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
                }
            }
        }

        // Extend this list to add more filters.
        private static readonly InitFileNameBlacklistRule[] INIT_FILENAME_BLACKLIST = new[]
        {
            // Exact file names
            new InitFileNameBlacklistRule(InitFileNameMatchKind.Exact, "hotwords.txt"),

            // Common non-model assets by suffix
            new InitFileNameBlacklistRule(InitFileNameMatchKind.Suffix, ".zip"),
            new InitFileNameBlacklistRule(InitFileNameMatchKind.Suffix, ".wav"),
            new InitFileNameBlacklistRule(InitFileNameMatchKind.Suffix, ".mp3"),

            new InitFileNameBlacklistRule(InitFileNameMatchKind.Contains, "espeak-ng-data"),
            new InitFileNameBlacklistRule(InitFileNameMatchKind.Contains, "librknnrt-android"),

            // Examples (disabled by default):
            // new InitFileNameBlacklistRule(InitFileNameMatchKind.Contains, "readme"),
            // new InitFileNameBlacklistRule(InitFileNameMatchKind.Prefix, "LICENSE"),
            // new InitFileNameBlacklistRule(InitFileNameMatchKind.Regex, @"^.*\.(sha256|sig|md)$"),
        };

        private static bool IsInitBlacklisted(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                return false;
            }


            for (int i = 0; i < INIT_FILENAME_BLACKLIST.Length; i++)
            {
                var r = INIT_FILENAME_BLACKLIST[i];
                switch (r.Kind)
                {
                    case InitFileNameMatchKind.Exact:
                        if (string.Equals(fileName, r.Pattern, StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }


                        break;
                    case InitFileNameMatchKind.Prefix:
                        if (fileName.StartsWith(r.Pattern, StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }


                        break;
                    case InitFileNameMatchKind.Suffix:
                        if (fileName.EndsWith(r.Pattern, StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }


                        break;
                    case InitFileNameMatchKind.Contains:
                        if (fileName.IndexOf(r.Pattern, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            return true;
                        }


                        break;
                    case InitFileNameMatchKind.Regex:
                        if (r.Regex != null && r.Regex.IsMatch(fileName))
                        {
                            return true;
                        }


                        break;
                }
            }
            return false;
        }

        private static string GetReleaseTagByModuleType(SherpaONNXModuleType moduleType)
        {

            var tagName = string.Empty;
            switch (moduleType)
            {
                case SherpaONNXModuleType.SpeechRecognition:
                    tagName = "asr-models";
                    break;
                case SherpaONNXModuleType.VoiceActivityDetection:
                    tagName = "asr-models"; // i know it's weird but it's work.
                    break;
                case SherpaONNXModuleType.SpeechSynthesis:
                    tagName = "tts-models";
                    break;
                case SherpaONNXModuleType.KeywordSpotting:
                    tagName = "kws-models";
                    break;
                case SherpaONNXModuleType.SpeechEnhancement:
                    tagName = "speech-enhancement-models";
                    break;
                case SherpaONNXModuleType.SpokenLanguageIdentification:
                    tagName = "asr-models"; // use whisper model so it's should be asr-models
                    break;
                case SherpaONNXModuleType.AddPunctuation:
                    tagName = "punctuation-models";
                    break;
                case SherpaONNXModuleType.AudioTagging:
                    tagName = "audio-tagging-models";
                    break;
                case SherpaONNXModuleType.SpeakerDiarization:
                    tagName = "speaker-segmentation-models";
                    break;
                case SherpaONNXModuleType.SpeakerIdentification:
                    tagName = "speaker-recongition-models";
                    break;
                case SherpaONNXModuleType.SourceSeparation:
                    tagName = "source-separation-models";
                    break;
            }
            return tagName;
        }

        // Applies GitHub proxy idempotently and only for direct github.com URLs.
        private static string ApplyGithubProxyIfAny(string rawUrl)
        {
            if (string.IsNullOrWhiteSpace(rawUrl))
            {
                return rawUrl;
            }

            string proxy = null;
            try
            {
                if (SherpaONNXEnvironment.Contains(SherpaONNXEnvironment.BuiltinKeys.GithubProxy))
                {
                    proxy = SherpaONNXEnvironment.Get(SherpaONNXEnvironment.BuiltinKeys.GithubProxy)?.Trim();
                }
            }
            catch (Exception ex)
            {
                SherpaLog.Warning($"ApplyGithubProxyIfAny failed: {ex.GetType().Name}: {ex.Message}");
                return rawUrl;
            }

            if (string.IsNullOrWhiteSpace(proxy))
            {
                return rawUrl;
            }

            proxy = NormalizeProxy(proxy);
            var proxyNoSlash = proxy.TrimEnd('/');

            // Start with the incoming URL
            var url = rawUrl.Trim();

            // Unwrap any number of the same proxy prefix (with or without a trailing slash).
            // e.g., https://gh-proxy.com/https://gh-proxy.com/https://github.com/... -> https://github.com/...
            while (url.StartsWith(proxy, StringComparison.OrdinalIgnoreCase) ||
                   url.StartsWith(proxyNoSlash + "/", StringComparison.OrdinalIgnoreCase))
            {
                url = url.Substring(proxy.Length).TrimStart('/');
            }

            // Only apply the proxy for direct GitHub URLs; otherwise return as-is.
            if (url.StartsWith("https://github.com/", StringComparison.OrdinalIgnoreCase) ||
                url.StartsWith("http://github.com/", StringComparison.OrdinalIgnoreCase))
            {
                return proxy + url;
            }

            // If it's already proxied by some other gateway (not equal to ours), don't re-wrap it.
            return url;
        }

        private static string NormalizeProxy(string proxy)
        {
            if (!proxy.EndsWith("/", StringComparison.Ordinal))
            {
                proxy += "/";
            }
            return proxy;
        }

        /// <summary>
        /// Attempts to populate the downloadFileHash for a model using checksum.txt (cache first, optional fetch).
        /// Returns true if a hash was found and assigned.
        /// </summary>
        public static bool TryPopulateDownloadHash(SherpaONNXModelMetadata metadata)
        {
            if (metadata == null || string.IsNullOrWhiteSpace(metadata.modelId))
            {
                return false;
            }

            var moduleType = metadata.moduleType;
            var tag = GetReleaseTagByModuleType(moduleType);
            if (string.IsNullOrWhiteSpace(tag))
            {
                return false;
            }

            // Helper to search for the hash in a checksum.txt content
            bool TryResolveFromContent(string content)
            {
                var parsed = ParseChecksumContent(content, moduleType, tag);
                if (parsed == null || parsed.Length == 0)
                {
                    return false;
                }

                foreach (var entry in parsed)
                {
                    if (entry != null && string.Equals(entry.modelId, metadata.modelId, StringComparison.OrdinalIgnoreCase)
                        && !string.IsNullOrWhiteSpace(entry.downloadFileHash))
                    {
                        metadata.downloadFileHash = entry.downloadFileHash;
                        return true;
                    }
                }

                return false;
            }

            // 1) Try cache
            if (TryReadChecksumCache(tag, allowExpired: true, out var cachedContent) &&
                !string.IsNullOrWhiteSpace(cachedContent) &&
                TryResolveFromContent(cachedContent))
            {
                return true;
            }

            // 2) Optionally fetch if allowed
            if (!ShouldFetchLatestManifest())
            {
                return false;
            }

            var rawUrl = $"https://github.com/k2-fsa/sherpa-onnx/releases/download/{tag}/checksum.txt";
            var url = ApplyGithubProxyIfAny(rawUrl);
            try
            {
                var (ok, content) = TryHttpGetTextAsync(url, 20000).GetAwaiter().GetResult();
                if (!ok || string.IsNullOrWhiteSpace(content))
                {
                    // Fallback to direct URL if proxied failed
                    if (!string.Equals(url, rawUrl, StringComparison.OrdinalIgnoreCase))
                    {
                        (ok, content) = TryHttpGetTextAsync(rawUrl, 20000).GetAwaiter().GetResult();
                    }
                }

                if (ok && !string.IsNullOrWhiteSpace(content))
                {
                    WriteChecksumCache(tag, content);
                    return TryResolveFromContent(content);
                }
            }
            catch
            {
                // Ignore network errors; will proceed without hash.
            }

            return false;
        }

        private static bool TryHttpGetTextWithProxyFallback(string rawUrl, out string text, int timeoutMs = 20000)
        {
            text = string.Empty;
            // 1) Try with proxy (if any)
            var proxied = ApplyGithubProxyIfAny(rawUrl);
            if (TryHttpGetText(proxied, out text, timeoutMs))
            {
                return true;
            }

            // 2) If we changed the URL via proxy, also try direct as a fallback

            if (!string.Equals(proxied, rawUrl, StringComparison.OrdinalIgnoreCase))
            {

                return TryHttpGetText(rawUrl, out text, timeoutMs);
            }


            return false;
        }

        private static bool TryHttpGetText(string url, out string text, int timeoutMs = 20000)
        {
            text = string.Empty;
            try
            {
                using (var uwr = UnityWebRequest.Get(url))
                {
                    uwr.downloadHandler = new DownloadHandlerBuffer();
                    var op = uwr.SendWebRequest();
                    var start = DateTime.UtcNow;
                    while (!op.isDone)
                    {
                        if ((DateTime.UtcNow - start).TotalMilliseconds > timeoutMs)
                        {
                            uwr.Abort();
                            SherpaLog.Warning($"TryHttpGetText timeout: {url}");
                            return false;
                        }
                        Thread.Sleep(10);
                    }
#if UNITY_2020_1_OR_NEWER
                    if (uwr.result != UnityWebRequest.Result.Success)
#else
                    if (uwr.isNetworkError || uwr.isHttpError)
#endif
                    {
                        SherpaLog.Warning($"TryHttpGetText HTTP error: {uwr.error} ({url})");
                        return false;
                    }
                    text = uwr.downloadHandler.text ?? string.Empty;
                    return true;
                }
            }
            catch (Exception ex)
            {
                SherpaLog.Warning($"TryHttpGetText exception: {ex.GetType().Name}: {ex.Message}");
                return false;
            }
        }

        private static async Task<(bool ok, string text)> TryHttpGetTextAsync(string url, int timeoutMs = 20000)
        {
            try
            {
                using (var uwr = UnityWebRequest.Get(url))
                {
                    uwr.downloadHandler = new DownloadHandlerBuffer();
                    var op = uwr.SendWebRequest();

                    var tcs = new TaskCompletionSource<bool>();
                    using (var cts = new CancellationTokenSource(timeoutMs))
                    {
                        op.completed += _ => tcs.TrySetResult(true);
                        using (cts.Token.Register(() => tcs.TrySetCanceled(), useSynchronizationContext: true))
                        {
                            try
                            {
                                await tcs.Task.ConfigureAwait(true);
                            }
                            catch (TaskCanceledException)
                            {
                                uwr.Abort();
                                SherpaLog.Warning($"TryHttpGetTextAsync timeout: {url}");
                                return (false, string.Empty);
                            }
                        }
                    }
#if UNITY_2020_1_OR_NEWER
                    if (uwr.result != UnityWebRequest.Result.Success)
#else
                    if (uwr.isNetworkError || uwr.isHttpError)
#endif
                    {
                        SherpaLog.Warning($"TryHttpGetTextAsync HTTP error: {uwr.error} ({url})");
                        return (false, string.Empty);
                    }
                    return (true, uwr.downloadHandler.text ?? string.Empty);
                }
            }
            catch (Exception ex)
            {
                SherpaLog.Warning($"TryHttpGetTextAsync exception: {ex.GetType().Name}: {ex.Message}");
                return (false, string.Empty);
            }
        }

        public static async Task<SherpaONNXModelManifest> GetDefaultManifestAsync(
            IEnumerable<SherpaONNXModuleType> moduleTypes = null,
            CancellationToken cancellationToken = default)
        {
            var manifest = new SherpaONNXModelManifest();
            await PopulateManifestAsync(manifest, moduleTypes, cancellationToken).ConfigureAwait(true);
            return manifest;
        }

        public const string RootDirectoryName = "sherpa-onnx";
        // public const string ManifestFileName = "manifest.json";

        public const string ModelRootDirectoryName = "models";

        // public const string githubProxyUrl = "https://gh-proxy.com/";

        private static string GetModelDownloadUrl(string modelId)
        {
            var sherpaModelType = SherpaUtils.Model.GetModuleTypeByModelId(modelId);
            var tag = GetReleaseTagByModuleType(sherpaModelType);
            var ext = sherpaModelType == SherpaONNXModuleType.VoiceActivityDetection ? ".onnx" : ".tar.bz2";
            var rawUrl = $"https://github.com/k2-fsa/sherpa-onnx/releases/download/{tag}/{modelId}{ext}";
            // Store canonical (raw) GitHub URL in metadata; proxy is applied at request time.
            return rawUrl;
        }

        private static void AddToManifest(SherpaONNXModelManifest manifest, SherpaONNXModelMetadata[] modelMetadataList, SherpaONNXModuleType moduleType)
        {
            foreach (var modelConfig in modelMetadataList)
            {
                if (modelConfig == null)
                {
                    continue;
                }

                if (string.IsNullOrEmpty(modelConfig.downloadUrl))
                {
                    modelConfig.downloadUrl = GetModelDownloadUrl(modelConfig.modelId);
                }

                // Assign the target module type for this insertion
                modelConfig.moduleType = moduleType;

                // Prevent duplicates only within the same module type.
                // This allows the same modelId (e.g., Whisper) to exist under
                // both SpeechRecognition and SpokenLanguageIdentification.
                bool exists = manifest.models.Exists(m =>
                    string.Equals(m.modelId, modelConfig.modelId, StringComparison.OrdinalIgnoreCase)
                    && m.moduleType == moduleType);

                if (!exists)
                {
                    manifest.models.Add(modelConfig);
                }
            }
        }

        public static async Task PopulateManifestAsync(
            SherpaONNXModelManifest manifest,
            IEnumerable<SherpaONNXModuleType> moduleTypes,
            CancellationToken cancellationToken = default)
        {
            if (manifest == null)
            {
                throw new ArgumentNullException(nameof(manifest));
            }

            cancellationToken.ThrowIfCancellationRequested();

            var requestedTypes = NormalizeModuleTypes(moduleTypes);
            if (requestedTypes.Length == 0)
            {
                return;
            }

            var missingTypes = requestedTypes
                .Where(t => !ManifestContainsModule(manifest, t))
                .ToArray();

            if (missingTypes.Length == 0)
            {
                return;
            }

            var fetchTasks = new Dictionary<SherpaONNXModuleType, Task<SherpaONNXModelMetadata[]>>(missingTypes.Length);
            foreach (var moduleType in missingTypes)
            {
                cancellationToken.ThrowIfCancellationRequested();
                fetchTasks[moduleType] = FetchModelsAsync(moduleType);
            }

            await Task.WhenAll(fetchTasks.Values).ConfigureAwait(true);

            foreach (var moduleType in missingTypes)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var fetched = await fetchTasks[moduleType].ConfigureAwait(true);
                if (fetched != null && fetched.Length > 0)
                {
                    AddToManifest(manifest, fetched, moduleType);
                    continue;
                }

                var fallback = GetFallbackModels(moduleType);
                if (fallback.Length > 0)
                {
                    AddToManifest(manifest, fallback, moduleType);
                }
            }
        }

        private static bool ManifestContainsModule(SherpaONNXModelManifest manifest, SherpaONNXModuleType moduleType)
        {
            if (manifest == null || manifest.models == null || manifest.models.Count == 0)
            {
                return false;
            }

            return manifest.models.Exists(m => m != null && m.moduleType == moduleType);
        }

        private static SherpaONNXModuleType[] NormalizeModuleTypes(IEnumerable<SherpaONNXModuleType> moduleTypes)
        {
            if (moduleTypes == null)
            {
                return ALL_MANIFEST_MODULE_TYPES;
            }

            return moduleTypes
                .Where(t => t != SherpaONNXModuleType.Undefined)
                .Distinct()
                .ToArray();
        }

        private static SherpaONNXModelMetadata[] CloneMetadataArray(SherpaONNXModelMetadata[] source)
        {
            if (source == null || source.Length == 0)
            {
                return Array.Empty<SherpaONNXModelMetadata>();
            }

            var list = new List<SherpaONNXModelMetadata>(source.Length);
            for (int i = 0; i < source.Length; i++)
            {
                var item = source[i];
                if (item == null)
                {
                    continue;
                }

                list.Add(new SherpaONNXModelMetadata
                {
                    modelId = item.modelId,
                    moduleType = item.moduleType,
                    downloadUrl = item.downloadUrl,
                    downloadFileHash = item.downloadFileHash,
                    numberOfSpeakers = item.numberOfSpeakers,
                    sampleRate = item.sampleRate,
                });
            }

            return list.ToArray();
        }

        private static SherpaONNXModelMetadata[] GetFallbackModels(SherpaONNXModuleType moduleType)
        {
            switch (moduleType)
            {
                case SherpaONNXModuleType.SpeechRecognition:
                    return CloneMetadataArray(Models.ASR_MODELS_METADATA_TABLES);
                case SherpaONNXModuleType.VoiceActivityDetection:
                    return CloneMetadataArray(Models.VAD_MODELS_METADATA_TABLES);
                case SherpaONNXModuleType.SpeechSynthesis:
                    return CloneMetadataArray(Models.TTS_MODELS_METADATA_TABLES);
                case SherpaONNXModuleType.KeywordSpotting:
                    return CloneMetadataArray(Models.KWS_MODELS_METADATA_TABLES);
                case SherpaONNXModuleType.SpeechEnhancement:
                    return CloneMetadataArray(Models.SPEECH_ENHANCEMENT_MODELS_METADATA_TABLES);
                case SherpaONNXModuleType.SpokenLanguageIdentification:
                    return CloneMetadataArray(Models.SPOKEN_LANGUAGEIDENTIFICATION_MODELS_METADATA_TABLES);
                case SherpaONNXModuleType.AddPunctuation:
                    return CloneMetadataArray(Models.PUNCTUATION_MODELS_METADATA_TABLES);
                case SherpaONNXModuleType.AudioTagging:
                    return CloneMetadataArray(Models.AUDIO_TAGGING_MODELS_METADATA_TABLES);
                case SherpaONNXModuleType.SpeakerIdentification:
                    return CloneMetadataArray(Models.SPEAKER_IDENTIFICATION_MODELS_METADATA_TABLES);
                case SherpaONNXModuleType.SpeakerDiarization:
                    return CloneMetadataArray(Models.SPEAKER_DIARIZATION_MODELS_METADATA_TABLES);
                case SherpaONNXModuleType.SourceSeparation:
                    return CloneMetadataArray(Models.SOURCE_SEPARATION_MODELS_METADATA_TABLES);
                default:
                    return Array.Empty<SherpaONNXModelMetadata>();
            }
        }

        private static async Task<SherpaONNXModelMetadata[]> FetchModelsAsync(SherpaONNXModuleType moduleType)
        {
            var tag = GetReleaseTagByModuleType(moduleType);
            if (string.IsNullOrWhiteSpace(tag))
            {
                return Array.Empty<SherpaONNXModelMetadata>();
            }
            if (moduleType == SherpaONNXModuleType.SpeakerDiarization)
            {
                // There is no checksum.txt file in https://github.com/k2-fsa/sherpa-onnx/releases/tag/speaker-segmentation-models, so the model cannot be obtained
                return CloneMetadataArray(Models.SPEAKER_DIARIZATION_MODELS_METADATA_TABLES);
            }

            var fetchAllowed = ShouldFetchLatestManifest();
            if (TryReadChecksumCache(tag, allowExpired: !fetchAllowed, out var cachedContent) &&
                !string.IsNullOrWhiteSpace(cachedContent))
            {
                return ParseChecksumContent(cachedContent, moduleType, tag);
            }

            if (!fetchAllowed)
            {
                SherpaLog.Info($"FetchModelsAsync({moduleType}) skipped network fetch because {SherpaONNXEnvironment.BuiltinKeys.FetchLatestManifest}=false and no cache was present.");
                return Array.Empty<SherpaONNXModelMetadata>();
            }

            var rawUrl = $"https://github.com/k2-fsa/sherpa-onnx/releases/download/{tag}/checksum.txt";
            var url = ApplyGithubProxyIfAny(rawUrl);
            try
            {
                var (ok, content) = await TryHttpGetTextAsync(url, 20000).ConfigureAwait(true);
                if (!ok || string.IsNullOrWhiteSpace(content))
                {
                    // Fallback to direct (non-proxied) if the proxied attempt failed.
                    if (!string.Equals(url, rawUrl, StringComparison.OrdinalIgnoreCase))
                    {
                        (ok, content) = await TryHttpGetTextAsync(rawUrl, 20000).ConfigureAwait(true);
                    }
                }

                if (!ok || string.IsNullOrWhiteSpace(content))
                {
                    return Array.Empty<SherpaONNXModelMetadata>();
                }

                WriteChecksumCache(tag, content);
                return ParseChecksumContent(content, moduleType, tag);
            }
            catch (Exception ex)
            {
                SherpaLog.Warning($"FetchModelsAsync({moduleType}) failed: {ex.GetType().Name}: {ex.Message}");
                return Array.Empty<SherpaONNXModelMetadata>();
            }
        }

        private static SherpaONNXModelMetadata[] ParseChecksumContent(string content, SherpaONNXModuleType moduleType, string tag)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return Array.Empty<SherpaONNXModelMetadata>();
            }

            var list = new List<SherpaONNXModelMetadata>();
            var lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var rkRegex = new Regex(@"rk\d{4}", RegexOptions.IgnoreCase);

            bool isOnnxOnly = moduleType == SherpaONNXModuleType.VoiceActivityDetection
                           || moduleType == SherpaONNXModuleType.SpeechEnhancement;
            string wantedExt = isOnnxOnly ? ".onnx" : ".tar.bz2";
            bool isSlidModel = moduleType == SherpaONNXModuleType.SpokenLanguageIdentification;

            foreach (var raw in lines)
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#"))
                {
                    continue;
                }

                var parts = line.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2)
                {
                    continue;
                }

                var fileName = parts[0].Trim();
                var hash = parts[1].Trim();

                // Apply read-only initialization blacklist (names and suffixes)
                if (IsInitBlacklisted(fileName))
                {
                    continue;
                }

                if (!fileName.EndsWith(wantedExt, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (rkRegex.IsMatch(fileName))
                {
                    continue;
                }

                if (isSlidModel && fileName.IndexOf("whisper", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                string modelId;
                if (isOnnxOnly)
                {
                    modelId = fileName.EndsWith(".onnx", StringComparison.OrdinalIgnoreCase)
                        ? fileName.Substring(0, fileName.Length - ".onnx".Length)
                        : fileName;
                }
                else
                {
                    modelId = fileName.Substring(0, fileName.Length - ".tar.bz2".Length);
                }

                var downloadUrl = ApplyGithubProxyIfAny(
                    $"https://github.com/k2-fsa/sherpa-onnx/releases/download/{tag}/{(isOnnxOnly ? modelId + ".onnx" : modelId + ".tar.bz2")}"
                );

                var meta = new SherpaONNXModelMetadata
                {
                    modelId = modelId,
                    downloadFileHash = hash,
                    downloadUrl = downloadUrl
                };

                list.Add(meta);
            }

            return list.ToArray();
        }

    }


}
