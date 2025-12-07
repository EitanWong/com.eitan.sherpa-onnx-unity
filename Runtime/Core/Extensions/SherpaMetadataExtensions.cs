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

        internal static string GetModelFilePath(this SherpaONNXModelMetadata metadata, string modelFile)
        {
            if (string.IsNullOrEmpty(modelFile))
            {

                throw new System.Exception("modelFile can't be Null or Empty");
            }
            var modelFolderPath = SherpaPathResolver.GetModelRootPath(metadata.modelId);
            if (string.IsNullOrEmpty(modelFolderPath))
            {
                throw new System.Exception("model Folder can't found");
            }

            return System.IO.Path.Combine(modelFolderPath, modelFile);

        }

        internal static string[] ListModelFiles(this SherpaONNXModelMetadata metadata, bool fileNameOnly = false)
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

            var modelFolderPath = SherpaPathResolver.GetModelRootPath(metadata.modelId);
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

                var filePaths = System.IO.Directory.GetFileSystemEntries(modelFolderPath);
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

            // Enumerate actual file names on disk (filename only for matching)
            var fileNames = metadata.ListModelFiles(fileNameOnly: true);
            if (fileNames == null || fileNames.Length == 0)
            {
                return null;
            }

            // Collect candidates with scores
            var candidates = new System.Collections.Generic.List<(string Name, int Score, int ExactWordMatches, int Priority, int NameLength)>(fileNames.Length);

            foreach (var name in fileNames)
            {
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
                    candidates.Add((name, matchedKeywords.Count, exactWordMatches, prio, name.Length));
                }
            }

            if (candidates.Count == 0)
            {
                return null;
            }

            // Order by: (1) matched keywords DESC, (2) priority DESC,
            //           (3) exact word matches DESC, (4) filename length ASC, (5) name ordinal-insensitive ASC
            var ordered = candidates
                .OrderByDescending(c => c.Score)
                .ThenByDescending(c => c.Priority)
                .ThenByDescending(c => c.ExactWordMatches)
                .ThenBy(c => c.NameLength)
                .ThenBy(c => c.Name, System.StringComparer.OrdinalIgnoreCase)
                .Select(c => metadata.GetModelFilePath(c.Name))
                .ToArray();

            return ordered;
        }

        internal static string[] GetModelFilesByExtensionName(this SherpaONNXModelMetadata metadata, params string[] extensions)
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
            var fileNamesOnDisk = metadata.ListModelFiles(fileNameOnly: true);
            if (fileNamesOnDisk == null || fileNamesOnDisk.Length == 0)
            {
                return System.Array.Empty<string>();
            }

            var results = fileNamesOnDisk
                .Where(name => validExtensions.Contains(System.IO.Path.GetExtension(name)))
                .Select(name => metadata.GetModelFilePath(name))
                .ToArray();

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
