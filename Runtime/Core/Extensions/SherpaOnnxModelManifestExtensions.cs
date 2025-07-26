
using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace Eitan.SherpaOnnxUnity.Runtime
{
    public static class SherpaOnnxModelManifestExtensions
    {
       /// <summary>
        /// 根据外部提供的筛选表达式来过滤 manifest 中的模型。
        /// </summary>
        /// <param name="manifest">要操作的 manifest 对象。</param>
        /// <param name="predicate">一个函数，用于测试每个模型是否满足条件。
        /// 它接收一个 SherpaOnnxModelMetadata 对象，并返回 true (包含) 或 false (排除)。</param>
        /// <returns>一个包含所有满足条件的模型的数组。</returns>
        public static SherpaOnnxModelMetadata[] Filter(
            this SherpaOnnxModelManifest manifest,
            Func<SherpaOnnxModelMetadata, bool> predicate)
        {
            // 安全检查：确保 manifest、models 数组和 predicate 都不为 null
            if (manifest?.models == null || predicate == null)
            {
                return Array.Empty<SherpaOnnxModelMetadata>();
            }
            // 使用 LINQ 的 Where 方法，它会根据 predicate 表达式来过滤集合
            return manifest.models.Where(predicate).ToArray();
        }
    }
}