using System.Linq;
using System.Collections.Generic;
using Eitan.SherpaONNXUnity.Runtime.Utilities.Pinyin.Exception;
using Eitan.SherpaONNXUnity.Runtime.Utilities.Pinyin.Data;

namespace Eitan.SherpaONNXUnity.Runtime.Utilities.Pinyin
{
    /// <summary>
    /// 处理姓氏专用
    /// </summary>
    public class Name4Net
    {
        protected Name4Net() { }

        #region // 获取拼音
        /// <summary>
        /// 获取姓的拼音，如果是复姓则由空格分隔
        /// </summary>
        /// <param name="firstName">要查询拼音的姓</param>
        /// <param name="format">输出拼音格式化参数</param>
        /// <returns>返回姓的拼音，若未找到姓，则返回null</returns>
        /// <exception cref="UnsupportedUnicodeException">当要获取拼音的字符不是汉字时抛出此异常</exception>
        public static string GetPinyin(string firstName, PinyinFormat format = PinyinFormat.None)
        {
            if (!firstName.All(PinyinUtil.IsHanzi))
            {
                // 不是汉字
                throw new UnsupportedUnicodeException("不支持的字符: " + firstName);
            }
            var pinyin = NameDB.Instance.GetPinyin(firstName);
            if (format == PinyinFormat.None)
            {
                return pinyin;
            }

            return string.Join(" ", pinyin.Split(' ').Select(item => PinyinUtil.Format(item, format)));
        }

        /// <summary>
        /// 获取姓的首字母，如果是复姓则由空格分隔首字母
        /// </summary>
        /// <param name="firstName">要查询拼音的姓</param>
        /// <returns>返回姓的拼音首字母，若未找到姓，则返回null</returns>
        /// <exception cref="UnsupportedUnicodeException">当要获取拼音的字符不是汉字时抛出此异常</exception>
        public static string GetFirstLetter(string firstName)
        {
            var pinyin = GetPinyin(firstName);
            if (pinyin == null)
            {
                return null;
            }
            return string.Join(" ", pinyin.Split(' ').Select(py => py[0]));
        }
        #endregion

        /// <summary>
        /// 根据拼音查询匹配的姓
        /// </summary>
        /// <param name="pinyin">当 matchAll 为 true 时，会自动处理 v->u: 以及 lyu->lu:</param>
        /// <param name="matchAll">是否全部匹配，为true时，匹配整个拼音，否则匹配开头字符，此参数用于告知传入的拼音是完整拼音还是仅仅是声母</param>
        /// <returns>匹配的姓数组</returns>
        public static string[] GetHanzi(string pinyin, bool matchAll)
        {
            return NameDB.Instance.GetHanzi(pinyin.ToLower(), matchAll).ToArray();
        }
        /// <summary>
        /// 更新姓氏数据库
        /// </summary>
        /// <param name="data">Key为姓，Value为读音(复姓拼音作为数组的项)</param>
        /// <param name="replace">是否替换已经存在的项，默认为 false</param>
        public static void UpdateMap(Dictionary<string, string[]> data, bool replace = false)
        {
            NameDB.Instance.Update(data, replace);
        }
    }
}
