using System.Linq;
using Eitan.SherpaOnnxUnity.Runtime.Utilities;

namespace Eitan.SherpaOnnxUnity.Runtime
{

    public static class SherpaMetadataExtensions
    {

        public static string GetModelFilePath(this SherpaOnnxModelMetadata metadata, string modelFile)
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

        /// <summary>
        /// Finds a model file path from the metadata by searching for a filename that contains a set of keywords.
        /// </summary>
        /// <param name="modelID">The identifier of the model.</param>
        /// <param name="keywords">The keywords that the filename must contain.</param>
        /// <returns>
        /// A task that represents the asynchronous operation. The task result contains the path of the first matching file.
        /// It returns null if the metadata for the model can't be found, if there are no model files, or if no file matches all the specified keywords.
        /// </returns>
        public static string GetModelFilePathByKeywords(this SherpaOnnxModelMetadata metadata, params string[] keywords)
        {
            if (metadata?.modelFileNames == null || !metadata.modelFileNames.Any())
            {
                UnityEngine.Debug.LogError("Model metadata filenames are empty. Please check the manifest file.");
                return null;
            }

            // 预处理：过滤空关键字并转换为小写
            var validKeywords = keywords
                .Where(k => !string.IsNullOrWhiteSpace(k))
                .Select(k => k.ToLowerInvariant())
                .ToArray();

            if (validKeywords.Length == 0)
            {
                return null;
            }

            // 分割文件名为单词列表（按非字母数字字符分割）
            string[] SplitIntoWords(string fileName)
            {
                return System.Text.RegularExpressions.Regex
                    .Split(fileName, @"[^a-zA-Z0-9]+")
                    .Where(w => !string.IsNullOrEmpty(w))
                    .ToArray();
            }

            // 存储所有匹配文件及其匹配度
            var matchedFiles = new System.Collections.Generic.List<(string FileName, int KeywordCount)>();

            foreach (var file in metadata.modelFileNames)
            {
                var words = SplitIntoWords(file);
                var lowerWords = words.Select(w => w.ToLowerInvariant()).ToArray();

                // 计算匹配的关键字数量（不要求全部匹配）
                int matchCount = validKeywords.Count(kw => lowerWords.Contains(kw));

                if (matchCount > 0)
                {
                    matchedFiles.Add((file, matchCount));
                }
            }

            // 没有匹配文件
            if (matchedFiles.Count == 0)
            {
                return null;
            }

            // 优先选择：1. 匹配关键字最多的文件 2. 文件路径长度最短的（提高稳定性）
            var bestMatch = matchedFiles
                .OrderByDescending(x => x.KeywordCount) // 匹配关键字数量降序
                .ThenBy(x => x.FileName.Length)         // 文件名长度升序
                .First();

            return metadata.GetModelFilePath(bestMatch.FileName);
        }

        public static bool IsOnlineModel(this SherpaOnnxModelMetadata metadata)
        {
           return SherpaUtils.Model.IsOnlineModel(metadata.modelId);
        }
    }
}
