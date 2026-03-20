using System.Linq;
using Eitan.SherpaONNXUnity.Runtime.Utilities;

namespace Eitan.SherpaONNXUnity.Runtime
{

    internal static class SherpaMetadataExtensions
    {

        // Global blacklist: names or extensions (starting with '.') to ignore entirely
        private static readonly System.Collections.Generic.HashSet<string> s_FileBlacklist =
            new System.Collections.Generic.HashSet<string>(System.StringComparer.OrdinalIgnoreCase)
            {
                // filenames
                "MODEL_CARD", "README", "LICENSE",
                // extensions (start with '.')
                ".ds_store", ".md",
                // audio
                ".wav", ".mp3", ".flac", ".ogg", ".opus", ".m4a", ".aac", ".wma", ".aiff", ".aif", ".alac", ".caf",
                // images
                ".png", ".jpg", ".jpeg", ".webp", ".bmp", ".gif", ".svg",
            };

        // Prefix blacklist: if the file/folder name (without extension) starts with any of these, ignore it.
        private static readonly string[] s_FileNamePrefixBlacklist = new[]
        {
            // common noise docs / legal / meta
            "LICENSE", "LICENCE", "LICENSES", "COPYING", "COPYRIGHT", "NOTICE",
            "README", "CHANGELOG", "CHANGES", "HISTORY", "NEWS", "SECURITY",
            "CONTRIBUTING", "CODE_OF_CONDUCT", "CODEOWNERS", "AUTHORS", "THANKS",
            "ACKNOWLEDGEMENTS", "ACKS", "CREDITS",
        };

        // Global priority table: higher number = higher priority
        // Keys can be exact filenames (e.g., "en_GB-alan-low.onnx") or extensions (e.g., ".onnx")
        private static readonly System.Collections.Generic.Dictionary<string, int> s_PriorityTable =
            new System.Collections.Generic.Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase)
            {
                // Extensions first
                [".onnx"] = 100,
                [".ort"] = 100,
                [".pt"] = 60,
                [".bin"] = 60,
                [".tflite"] = 60,
                [".json"] = 50,
                [".yaml"] = 50,
                [".yml"] = 50,
                [".txt"] = 40,
                [".fst"] = 40,
                [".far"] = 40,
                // Example of specific important filenames (customize as needed)
                ["tokens.txt"] = 45,
                // ["en_GB-alan-low.onnx"] = 200, // uncomment to hard-prefer a specific file
            };

        // Compiled regex for splitting filenames into tokens for exact-word checks
        private static readonly System.Text.RegularExpressions.Regex s_FileNameSplitRegex =
            new System.Text.RegularExpressions.Regex(@"[^a-zA-Z0-9]+", System.Text.RegularExpressions.RegexOptions.Compiled);

        private static string[] SplitIntoWordsForMatch(string fileName)
        {
            return s_FileNameSplitRegex
                .Split(fileName ?? string.Empty)
                .Where(w => w.Length != 0)
                .ToArray();
        }

        // Compute priority for a given entry by filename first, then extension; default boosts any extension over none
        private static int GetPriorityForEntry(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return 0;
            }


            if (s_PriorityTable.TryGetValue(name, out var byName))
            {
                return byName;
            }


            var ext = System.IO.Path.GetExtension(name);
            if (!string.IsNullOrEmpty(ext) && s_PriorityTable.TryGetValue(ext, out var byExt))
            {

                return byExt;
            }

            // default: any extension slightly preferred over no-extension

            return string.IsNullOrEmpty(ext) ? 0 : 10;
        }

        private static string ResolveBindingPath(SherpaONNXModelMetadata metadata, string rawPath)
        {
            if (metadata == null || string.IsNullOrWhiteSpace(rawPath))
            {
                return string.Empty;
            }

            if (System.IO.Path.IsPathRooted(rawPath))
            {
                return rawPath;
            }

            var modelFolderPath = GetModelFolderPath(metadata);
            return System.IO.Path.Combine(modelFolderPath, rawPath);
        }

        private static SherpaONNXModuleType GetEffectiveModuleType(SherpaONNXModelMetadata metadata)
        {
            if (metadata == null)
            {
                return SherpaONNXModuleType.Undefined;
            }

            if (metadata.moduleType != SherpaONNXModuleType.Undefined)
            {
                return metadata.moduleType;
            }

            if (!string.IsNullOrWhiteSpace(metadata.moduleTypeHint) &&
                System.Enum.TryParse(metadata.moduleTypeHint.Trim(), true, out SherpaONNXModuleType hinted) &&
                hinted != SherpaONNXModuleType.Undefined)
            {
                return hinted;
            }

            if (SherpaUtils.Model.ResolveSpeakerDiarizationModelType(metadata) != SpeakerDiarizationModelType.None)
            {
                return SherpaONNXModuleType.SpeakerDiarization;
            }

            return SherpaUtils.Model.GetModuleTypeByModelId(metadata.modelId);
        }

        private static string GetModelFolderPath(SherpaONNXModelMetadata metadata)
        {
            if (metadata == null || string.IsNullOrWhiteSpace(metadata.modelId))
            {
                return string.Empty;
            }

            var moduleType = GetEffectiveModuleType(metadata);
            if (moduleType == SherpaONNXModuleType.Undefined)
            {
                return SherpaPathResolver.GetModelRootPath(metadata.modelId);
            }

            return System.IO.Path.Combine(SherpaPathResolver.GetModuleRootPath(moduleType), metadata.modelId);
        }

        private static readonly System.Collections.Generic.Dictionary<SherpaONNXModelFileKey, string[]> s_BindingKeywords =
            new System.Collections.Generic.Dictionary<SherpaONNXModelFileKey, string[]>
            {
                { SherpaONNXModelFileKey.Model, new [] { "model", ".onnx", ".ort", ".tflite", ".pt", ".bin", ".model" } },
                { SherpaONNXModelFileKey.Encoder, new [] { "encoder", "encode" } },
                { SherpaONNXModelFileKey.Decoder, new [] { "decoder", "merged_decoder", "decoder_model_merged", "merged-decoder" } },
                { SherpaONNXModelFileKey.Joiner, new [] { "joiner" } },
                { SherpaONNXModelFileKey.Tokens, new [] { "tokens", "tokens.txt" } },
                { SherpaONNXModelFileKey.Lexicon, new [] { "lexicon" } },
                { SherpaONNXModelFileKey.DictDir, new [] { "dict" } },
                { SherpaONNXModelFileKey.DataDir, new [] { "espeak-ng-data", "data" } },
                { SherpaONNXModelFileKey.Vocoder, new [] { "vocoder", "vocos", "vocos_24khz" } },
                { SherpaONNXModelFileKey.AcousticModel, new [] { "acoustic", "matcha" } },
                { SherpaONNXModelFileKey.FlowMatchingModel, new [] { "flow", "flow-matching", "flow_matching", "fm", "fm_decoder" } },
                { SherpaONNXModelFileKey.TextModel, new [] { "text", "language", "text_encoder" } },
                { SherpaONNXModelFileKey.Preprocessor, new [] { "preprocessor", "preprocess" } },
                { SherpaONNXModelFileKey.CachedDecoder, new [] { "cached", "cached-decoder", "cached_decode" } },
                { SherpaONNXModelFileKey.UncachedDecoder, new [] { "uncached", "uncached-decoder", "uncached_decode" } },
                { SherpaONNXModelFileKey.Embedding, new [] { "embedding" } },
                { SherpaONNXModelFileKey.Tokenizer, new [] { "tokenizer", "tokenizer.json", "tokenizer.model", "bpe", "spm", "qwen3-0.6b" } },
                { SherpaONNXModelFileKey.Llm, new [] { "llm" } },
                { SherpaONNXModelFileKey.EncoderAdaptor, new [] { "encoder-adaptor", "encoder_adaptor", "adaptor", "adapter" } },
                { SherpaONNXModelFileKey.Labels, new [] { "labels", "class_labels_indices" } },
                { SherpaONNXModelFileKey.Keywords, new [] { "keywords", "keywords.txt" } },
                { SherpaONNXModelFileKey.Hotwords, new [] { "hotwords" } },
                { SherpaONNXModelFileKey.Voices, new [] { "voices" } },
                { SherpaONNXModelFileKey.RuleFsts, new [] { "rule", "fst" } },
                { SherpaONNXModelFileKey.RuleFars, new [] { "rule", "far" } },
                { SherpaONNXModelFileKey.Pinyin, new [] { "pinyin" } },
                { SherpaONNXModelFileKey.Fst, new [] { "fst" } },
                { SherpaONNXModelFileKey.Far, new [] { "far" } },
                { SherpaONNXModelFileKey.SileroVad, new [] { "silero", "silero-vad", "silero_vad" } },
                { SherpaONNXModelFileKey.TenVad, new [] { "ten", "ten-vad", "ten_vad" } },
                { SherpaONNXModelFileKey.Tdnn, new [] { "tdnn" } },
                { SherpaONNXModelFileKey.Gtcrn, new [] { "gtcrn" } },
                { SherpaONNXModelFileKey.Ced, new [] { "ced" } },
                { SherpaONNXModelFileKey.Zipformer, new [] { "zipformer" } },
            };

        private static bool KeywordsMatchBinding(string keyword, SherpaONNXModelFileKey bindingKey)
        {
            if (bindingKey == SherpaONNXModelFileKey.None || string.IsNullOrWhiteSpace(keyword))
            {
                return false;
            }

            var trimmed = keyword.Trim().ToLowerInvariant();
            if (string.Equals(trimmed, bindingKey.ToString(), System.StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (s_BindingKeywords.TryGetValue(bindingKey, out var synonyms))
            {
                for (int i = 0; i < synonyms.Length; i++)
                {
                    var synonym = synonyms[i];
                    if (string.IsNullOrWhiteSpace(synonym))
                    {
                        continue;
                    }

                    var norm = synonym.ToLowerInvariant();
                    if (trimmed == norm || trimmed.Contains(norm) || norm.Contains(trimmed))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static string[] GetBoundPathsByKeywords(SherpaONNXModelMetadata metadata, string[] keywords)
        {
            if (metadata?.fileBindings == null || metadata.fileBindings.Count == 0 || keywords == null || keywords.Length == 0)
            {
                return System.Array.Empty<string>();
            }

            var results = new System.Collections.Generic.List<string>();
            for (int i = 0; i < metadata.fileBindings.Count; i++)
            {
                var binding = metadata.fileBindings[i];
                if (binding == null || binding.key == SherpaONNXModelFileKey.None || string.IsNullOrWhiteSpace(binding.path))
                {
                    continue;
                }

                for (int k = 0; k < keywords.Length; k++)
                {
                    var keyword = keywords[k];
                    if (string.IsNullOrWhiteSpace(keyword))
                    {
                        continue;
                    }

                    if (KeywordsMatchBinding(keyword, binding.key))
                    {
                        var resolved = ResolveBindingPath(metadata, binding.path);
                        if (!string.IsNullOrWhiteSpace(resolved) && !ContainsPath(results, resolved))
                        {
                            results.Add(resolved);
                        }
                        break;
                    }
                }
            }

            return results.ToArray();
        }

        internal static string[] GetModelFilePathsByBindingKeys(this SherpaONNXModelMetadata metadata, params SherpaONNXModelFileKey[] keys)
        {
            if (metadata?.fileBindings == null || metadata.fileBindings.Count == 0 || keys == null || keys.Length == 0)
            {
                return System.Array.Empty<string>();
            }

            var sanitizedKeys = new System.Collections.Generic.HashSet<SherpaONNXModelFileKey>(keys.Where(key => key != SherpaONNXModelFileKey.None));
            if (sanitizedKeys.Count == 0)
            {
                return System.Array.Empty<string>();
            }

            var results = new System.Collections.Generic.List<string>();
            for (int k = 0; k < keys.Length; k++)
            {
                var requestedKey = keys[k];
                if (requestedKey == SherpaONNXModelFileKey.None || !sanitizedKeys.Contains(requestedKey))
                {
                    continue;
                }

                for (int i = 0; i < metadata.fileBindings.Count; i++)
                {
                    var binding = metadata.fileBindings[i];
                    if (binding == null || binding.key != requestedKey || string.IsNullOrWhiteSpace(binding.path))
                    {
                        continue;
                    }

                    var resolved = ResolveBindingPath(metadata, binding.path);
                    if (!string.IsNullOrWhiteSpace(resolved) && !ContainsPath(results, resolved))
                    {
                        results.Add(resolved);
                    }
                }
            }

            return results.ToArray();
        }

        private static bool ContainsPath(System.Collections.Generic.List<string> list, string value)
        {
            if (list == null || list.Count == 0 || string.IsNullOrEmpty(value))
            {
                return false;
            }

            for (int i = 0; i < list.Count; i++)
            {
                if (string.Equals(list[i], value, System.StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        internal static string GetModelFilePath(this SherpaONNXModelMetadata metadata, string modelFile)
        {
            if (string.IsNullOrEmpty(modelFile))
            {

                throw new System.Exception("modelFile can't be Null or Empty");
            }
            var modelFolderPath = GetModelFolderPath(metadata);
            if (string.IsNullOrEmpty(modelFolderPath))
            {
                throw new System.Exception("model Folder can't found");
            }

            return System.IO.Path.Combine(modelFolderPath, modelFile);

        }

        internal static string[] ListModelFiles(this SherpaONNXModelMetadata metadata, bool fileNameOnly = false)
        {
            return ListModelFiles(metadata, fileNameOnly, recursive: false);
        }

        internal static string[] ListModelFiles(this SherpaONNXModelMetadata metadata, bool fileNameOnly, bool recursive)
        {
            // Validate inputs
            if (metadata == null)
            {
                SherpaLog.Error("Metadata is null.");
                return System.Array.Empty<string>();
            }

            if (string.IsNullOrWhiteSpace(metadata.modelId))
            {
                SherpaLog.Error("Model ID is empty. Please check the manifest file.");
                return System.Array.Empty<string>();
            }

            var modelFolderPath = GetModelFolderPath(metadata);
            if (string.IsNullOrWhiteSpace(modelFolderPath))
            {
                SherpaLog.Error($"Model root path not found for modelId: {metadata.modelId}");
                return System.Array.Empty<string>();
            }

            try
            {
                if (!System.IO.Directory.Exists(modelFolderPath))
                {
                    SherpaLog.Error($"Model folder does not exist: {modelFolderPath}");
                    return System.Array.Empty<string>();
                }

                var filePaths = System.IO.Directory.GetFileSystemEntries(
                    modelFolderPath,
                    "*",
                    recursive ? System.IO.SearchOption.AllDirectories : System.IO.SearchOption.TopDirectoryOnly);
                if (filePaths == null || filePaths.Length == 0)
                {
                    return System.Array.Empty<string>();
                }

                // Exclude Unity .meta sidecar files and global blacklist
                filePaths = filePaths
                    .Where(p => !p.EndsWith(".meta", System.StringComparison.OrdinalIgnoreCase))
                    .Where(p =>
                    {
                        var name = System.IO.Path.GetFileName(p);
                        var ext = System.IO.Path.GetExtension(name);
                        if (s_FileBlacklist.Contains(name))
                        {
                            return false;
                        }


                        if (!string.IsNullOrEmpty(ext) && s_FileBlacklist.Contains(ext))
                        {
                            return false;
                        }

                        // Prefix-based blacklist (e.g., LICENSE*, README*, NOTICE*, etc.)

                        var stem = System.IO.Path.GetFileNameWithoutExtension(name);
                        for (int i = 0; i < s_FileNamePrefixBlacklist.Length; i++)
                        {
                            var prefix = s_FileNamePrefixBlacklist[i];
                            if (stem.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase))
                            {

                                return false;
                            }

                        }

                        return true;
                    })
                    .ToArray();

                if (filePaths.Length == 0)
                {
                    return System.Array.Empty<string>();
                }

                // Deterministic order improves reproducibility and test stability
                System.Array.Sort(filePaths, System.StringComparer.OrdinalIgnoreCase);

                if (fileNameOnly)
                {
                    for (int i = 0; i < filePaths.Length; i++)
                    {
                        filePaths[i] = System.IO.Path.GetFileName(filePaths[i]);
                    }
                }

                return filePaths;
            }
            catch (System.Exception ex)
            {
                SherpaLog.Error($"Failed to list model files under '{modelFolderPath}': {ex}");
                return System.Array.Empty<string>();
            }
        }

        /// <summary>
        /// Finds model file paths by searching actual files on disk whose filenames contain all or some of the provided keywords.
        /// Uses <see cref="ListModelFiles"/> to enumerate files and returns full paths sorted by match quality.
        /// </summary>
        /// <param name="metadata">Model metadata containing the modelId used to resolve the model folder.</param>
        /// <param name="keywords">Keywords to match against filenames (case-insensitive). Empty or whitespace keywords are ignored.</param>
        /// <returns>
        /// An array of matching file paths ordered by: (1) number of matched keywords (descending), (2) priority (descending),
        /// (3) number of exact word matches (descending), then (4) filename length (ascending).
        /// Returns <c>null</c> if there are no keywords or no files match.
        /// </returns>
        internal static string[] GetModelFilePathByKeywords(this SherpaONNXModelMetadata metadata, params string[] keywords)
        {
            return GetModelFilePathByKeywords(metadata, true, keywords);
        }

        internal static string[] GetModelFilePathByKeywords(this SherpaONNXModelMetadata metadata, bool recursive, params string[] keywords)
        {
            if (string.IsNullOrEmpty(metadata.modelId))
            {
                SherpaLog.Error("Model ID is empty. Please check the manifest file.");
                return null;
            }

            // Normalize: filter blanks, lowercase, and de-duplicate keywords
            var validKeywords = (keywords ?? System.Array.Empty<string>())
                .Where(k => !string.IsNullOrWhiteSpace(k))
                .Select(k => k.ToLowerInvariant())
                .Distinct()
                .ToArray();

            if (validKeywords.Length == 0)
            {
                return null;
            }

            var boundCandidates = GetBoundPathsByKeywords(metadata, validKeywords);

            // Enumerate actual file names on disk (filename only for matching)
            var entries = metadata.ListModelFiles(fileNameOnly: false, recursive: recursive);
            if (entries == null || entries.Length == 0)
            {
                return boundCandidates.Length > 0 ? boundCandidates : null;
            }

            // Collect candidates with scores
            var candidates = new System.Collections.Generic.List<(string Path, string Name, int Score, int ExactWordMatches, int Priority, int NameLength)>(entries.Length);

            foreach (var entryPath in entries)
            {
                var name = System.IO.Path.GetFileName(entryPath);
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                var lowerName = name.ToLowerInvariant();
                var words = SplitIntoWordsForMatch(name);
                var wordSet = new System.Collections.Generic.HashSet<string>(words.Select(w => w.ToLowerInvariant()));

                var matchedKeywords = new System.Collections.Generic.HashSet<string>();
                int exactWordMatches = 0;

                foreach (var kw in validKeywords)
                {
                    if (kw.Length == 0)
                    {
                        continue;
                    }

                    // Normalize for exact word check: ".onnx" -> "onnx" so that extensions count as words too.
                    var kwWord = kw.StartsWith(".") ? kw.Substring(1) : kw;

                    // 1) Whole-word exact match (case-insensitive)
                    if (kwWord.Length > 0 && wordSet.Contains(kwWord))
                    {
                        if (matchedKeywords.Add(kw))
                        {
                            exactWordMatches++;
                        }
                    }

                    // 2) Substring match (covers tokens like ".onnx", "tokens.txt", etc.)
                    if (!matchedKeywords.Contains(kw) && lowerName.Contains(kw))
                    {
                        matchedKeywords.Add(kw);
                    }
                }

                if (matchedKeywords.Count > 0)
                {
                    var prio = GetPriorityForEntry(name);
                    candidates.Add((entryPath, name, matchedKeywords.Count, exactWordMatches, prio, name.Length));
                }
            }

            if (candidates.Count == 0)
            {
                return boundCandidates.Length > 0 ? boundCandidates : null;
            }

            // Order by: (1) matched keywords DESC, (2) priority DESC,
            //           (3) exact word matches DESC, (4) filename length ASC, (5) name ordinal-insensitive ASC
            var ordered = candidates
                .OrderByDescending(c => c.Score)
                .ThenByDescending(c => c.Priority)
                .ThenByDescending(c => c.ExactWordMatches)
                .ThenBy(c => c.NameLength)
                .ThenBy(c => c.Name, System.StringComparer.OrdinalIgnoreCase)
                .Select(c => c.Path)
                .ToArray();

            return boundCandidates.Length > 0 ? boundCandidates : ordered;
        }

        internal static string[] GetModelFilesByExtensionName(this SherpaONNXModelMetadata metadata, params string[] extensions)
        {
            return GetModelFilesByExtensionName(metadata, true, extensions);
        }

        internal static string[] GetModelFilesByExtensionName(this SherpaONNXModelMetadata metadata, bool recursive, params string[] extensions)
        {
            if (metadata == null || string.IsNullOrWhiteSpace(metadata.modelId))
            {
                SherpaLog.Error("Model ID is empty. Please check the manifest file.");
                return System.Array.Empty<string>();
            }

            // Normalize and validate extensions
            var validExtensions = new System.Collections.Generic.HashSet<string>(
                (extensions ?? System.Array.Empty<string>())
                    .Where(ext => !string.IsNullOrWhiteSpace(ext))
                    .Select(ext => ext.StartsWith(".") ? ext : "." + ext),
                System.StringComparer.OrdinalIgnoreCase
            );

            if (validExtensions.Count == 0)
            {
                return System.Array.Empty<string>();
            }

            // List actual files on disk and filter by extension
            var filePathsOnDisk = metadata.ListModelFiles(fileNameOnly: false, recursive: recursive);
            if (filePathsOnDisk == null || filePathsOnDisk.Length == 0)
            {
                return System.Array.Empty<string>();
            }

            var results = filePathsOnDisk
                .Where(path => validExtensions.Contains(System.IO.Path.GetExtension(path)))
                .ToArray();

            if (metadata.fileBindings != null && metadata.fileBindings.Count > 0)
            {
                var boundMatches = new System.Collections.Generic.List<string>();
                for (int i = 0; i < metadata.fileBindings.Count; i++)
                {
                    var binding = metadata.fileBindings[i];
                    if (binding == null || string.IsNullOrWhiteSpace(binding.path))
                    {
                        continue;
                    }

                    var resolved = ResolveBindingPath(metadata, binding.path);
                    if (string.IsNullOrWhiteSpace(resolved))
                    {
                        continue;
                    }

                    var ext = System.IO.Path.GetExtension(resolved);
                    if (validExtensions.Contains(ext))
                    {
                        boundMatches.Add(resolved);
                    }
                }

                if (boundMatches.Count > 0)
                {
                    results = boundMatches.ToArray();
                }
            }

            // Deterministic ordering
            System.Array.Sort(results, System.StringComparer.OrdinalIgnoreCase);
            return results;
        }



        internal static bool IsOnlineModel(this SherpaONNXModelMetadata metadata)
        {
            return SherpaUtils.Model.IsOnlineModel(metadata.modelId);
        }
    }
}
