using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine.Networking;

namespace Eitan.SherpaOnnxUnity.Runtime.Constants
{
    public partial class SherpaOnnxConstants
    {

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

        private static string GetReleaseTagByModuleType(SherpaOnnxModuleType moduleType)
        {
            switch (moduleType)
            {
                case SherpaOnnxModuleType.SpeechRecognition:
                    return "asr-models";
                case SherpaOnnxModuleType.VoiceActivityDetection:
                    return "asr-models"; // VAD lives under ASR releases, but assets are .onnx (no archive)
                case SherpaOnnxModuleType.SpeechSynthesis:
                    return "tts-models";
                case SherpaOnnxModuleType.KeywordSpotting:
                    return "kws-models";
                case SherpaOnnxModuleType.SpeechEnhancement:
                    return "speech-enhancement-models";
                case SherpaOnnxModuleType.SpokenLanguageIdentification:
                    return "asr-models"; // uses whisper models, also under ASR
                case SherpaOnnxModuleType.AddPunctuation:
                    return "punctuation-models";
                default:
                    return "asr-models";
            }
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
                if (SherpaOnnxEnvironment.Contains(SherpaOnnxEnvironment.BuiltinKeys.GithubProxy))
                {
                    proxy = SherpaOnnxEnvironment.Get(SherpaOnnxEnvironment.BuiltinKeys.GithubProxy)?.Trim();
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"ApplyGithubProxyIfAny failed: {ex.GetType().Name}: {ex.Message}");
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
                            UnityEngine.Debug.LogWarning($"TryHttpGetText timeout: {url}");
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
                        UnityEngine.Debug.LogWarning($"TryHttpGetText HTTP error: {uwr.error} ({url})");
                        return false;
                    }
                    text = uwr.downloadHandler.text ?? string.Empty;
                    return true;
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"TryHttpGetText exception: {ex.GetType().Name}: {ex.Message}");
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
                                UnityEngine.Debug.LogWarning($"TryHttpGetTextAsync timeout: {url}");
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
                        UnityEngine.Debug.LogWarning($"TryHttpGetTextAsync HTTP error: {uwr.error} ({url})");
                        return (false, string.Empty);
                    }
                    return (true, uwr.downloadHandler.text ?? string.Empty);
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"TryHttpGetTextAsync exception: {ex.GetType().Name}: {ex.Message}");
                return (false, string.Empty);
            }
        }

        private static async Task<(bool ok, string text)> TryHttpGetTextWithProxyFallbackAsync(string rawUrl, int timeoutMs = 20000)
        {
            var proxied = ApplyGithubProxyIfAny(rawUrl);
            var (ok, text) = await TryHttpGetTextAsync(proxied, timeoutMs).ConfigureAwait(true);
            if (ok)
            {
                return (true, text);
            }


            if (!string.Equals(proxied, rawUrl, StringComparison.OrdinalIgnoreCase))
            {
                return await TryHttpGetTextAsync(rawUrl, timeoutMs).ConfigureAwait(true);
            }
            return (false, string.Empty);
        }

        public static async Task<SherpaOnnxModelManifest> GetDefaultManifestAsync()
        {
            var manifest = new SherpaOnnxModelManifest();

            // 1) Connectivity check (respect Github proxy if provided)
            var canaryRaw = "https://github.com/k2-fsa/sherpa-onnx/releases/download/asr-models/checksum.txt";
            var (networkOk, _) = await TryHttpGetTextWithProxyFallbackAsync(canaryRaw, 10000).ConfigureAwait(true);

            if (networkOk)
            {
                try
                {
                    // 2) Fetch remote manifests concurrently (VAD/SE are .onnx-only; SLID filters whisper)
                    var tAsr = FetchModelsAsync(SherpaOnnxModuleType.SpeechRecognition);
                    var tVad = FetchModelsAsync(SherpaOnnxModuleType.VoiceActivityDetection);
                    var tTts = FetchModelsAsync(SherpaOnnxModuleType.SpeechSynthesis);
                    var tKws = FetchModelsAsync(SherpaOnnxModuleType.KeywordSpotting);
                    var tSe = FetchModelsAsync(SherpaOnnxModuleType.SpeechEnhancement);
                    var tSlid = FetchModelsAsync(SherpaOnnxModuleType.SpokenLanguageIdentification);
                    var tPunc = FetchModelsAsync(SherpaOnnxModuleType.AddPunctuation);

                    await Task.WhenAll(tAsr, tVad, tTts, tKws, tSe, tSlid, tPunc).ConfigureAwait(true);

                    AddToManifest(manifest, await tAsr, SherpaOnnxModuleType.SpeechRecognition);
                    AddToManifest(manifest, await tVad, SherpaOnnxModuleType.VoiceActivityDetection);
                    AddToManifest(manifest, await tTts, SherpaOnnxModuleType.SpeechSynthesis);
                    AddToManifest(manifest, await tKws, SherpaOnnxModuleType.KeywordSpotting);
                    AddToManifest(manifest, await tSe, SherpaOnnxModuleType.SpeechEnhancement);
                    AddToManifest(manifest, await tSlid, SherpaOnnxModuleType.SpokenLanguageIdentification);
                    AddToManifest(manifest, await tPunc, SherpaOnnxModuleType.AddPunctuation);
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogWarning($"FetchModels during GetDefaultManifestAsync failed: {ex.GetType().Name}: {ex.Message}");
                }
            }

            // 3) Offline/failed-network fallback to baked-in tables
            if (manifest.models.Count == 0)
            {
                AddToManifest(manifest, SherpaOnnxConstants.Models.ASR_MODELS_METADATA_TABLES, SherpaOnnxModuleType.SpeechRecognition);
                AddToManifest(manifest, SherpaOnnxConstants.Models.VAD_MODELS_METADATA_TABLES, SherpaOnnxModuleType.VoiceActivityDetection);
                AddToManifest(manifest, SherpaOnnxConstants.Models.TTS_MODELS_METADATA_TABLES, SherpaOnnxModuleType.SpeechSynthesis);
                AddToManifest(manifest, SherpaOnnxConstants.Models.KWS_MODELS_METADATA_TABLES, SherpaOnnxModuleType.KeywordSpotting);
                AddToManifest(manifest, SherpaOnnxConstants.Models.SPEECH_ENHANCEMENT_MODELS_METADATA_TABLES, SherpaOnnxModuleType.SpeechEnhancement);
                AddToManifest(manifest, SherpaOnnxConstants.Models.SPOKEN_LANGUAGEIDENTIFICATION_MODELS_METADATA_TABLES, SherpaOnnxModuleType.SpokenLanguageIdentification);
                AddToManifest(manifest, SherpaOnnxConstants.Models.PUNCTUATION_MODELS_METADATA_TABLES, SherpaOnnxModuleType.AddPunctuation);
            }

            return manifest;
        }

        public const string RootDirectoryName = "sherpa-onnx";
        // public const string ManifestFileName = "manifest.json";

        public const string ModelRootDirectoryName = "models";

        // public const string githubProxyUrl = "https://gh-proxy.com/";

        private static string GetModelDownloadUrl(string modelId)
        {
            var sherpaModelType = Utilities.SherpaUtils.Model.GetModuleTypeByModelId(modelId);
            var typeName = string.Empty;
            switch (sherpaModelType)
            {
                case SherpaOnnxModuleType.SpeechRecognition:
                    typeName = "asr-models";
                    break;
                case SherpaOnnxModuleType.VoiceActivityDetection:
                    typeName = "asr-models"; // i know it's weird but it's work.
                    break;
                case SherpaOnnxModuleType.SpeechSynthesis:
                    typeName = "tts-models";
                    break;
                case SherpaOnnxModuleType.KeywordSpotting:
                    typeName = "kws-models";
                    break;
                case SherpaOnnxModuleType.SpeechEnhancement:
                    typeName = "speech-enhancement-models";
                    break;
                case SherpaOnnxModuleType.SpokenLanguageIdentification:
                    typeName = "asr-models"; // use whisper model so it's should be asr-models
                    break;
                case SherpaOnnxModuleType.AddPunctuation:
                    typeName = "punctuation-models";
                    break;
            }

            var ext = sherpaModelType == SherpaOnnxModuleType.VoiceActivityDetection ? ".onnx" : ".tar.bz2";
            var rawUrl = $"https://github.com/k2-fsa/sherpa-onnx/releases/download/{typeName}/{modelId}{ext}";
            // Store canonical (raw) GitHub URL in metadata; proxy is applied at request time.
            return rawUrl;
        }

        private static void AddToManifest(SherpaOnnxModelManifest manifest, SherpaOnnxModelMetadata[] modelMetadataList, SherpaOnnxModuleType moduleType)
        {
            foreach (var modelConfig in modelMetadataList)
            {
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

        private static async Task<SherpaOnnxModelMetadata[]> FetchModelsAsync(SherpaOnnxModuleType moduleType)
        {
            var tag = GetReleaseTagByModuleType(moduleType);
            if (string.IsNullOrWhiteSpace(tag))
            {
                return Array.Empty<SherpaOnnxModelMetadata>();
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
                    return Array.Empty<SherpaOnnxModelMetadata>();
                }

                var list = new List<SherpaOnnxModelMetadata>();
                var lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                var rkRegex = new Regex(@"rk\d{4}", RegexOptions.IgnoreCase);

                bool isOnnxOnly = moduleType == SherpaOnnxModuleType.VoiceActivityDetection
                               || moduleType == SherpaOnnxModuleType.SpeechEnhancement;
                string wantedExt = isOnnxOnly ? ".onnx" : ".tar.bz2";
                bool isSlidModel = moduleType == SherpaOnnxModuleType.SpokenLanguageIdentification;

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

                    var meta = new SherpaOnnxModelMetadata
                    {
                        modelId = modelId,
                        downloadFileHash = hash,
                        downloadUrl = downloadUrl
                    };

                    list.Add(meta);
                }

                return list.ToArray();
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"FetchModelsAsync({moduleType}) failed: {ex.GetType().Name}: {ex.Message}");
                return Array.Empty<SherpaOnnxModelMetadata>();
            }
        }

    }


}