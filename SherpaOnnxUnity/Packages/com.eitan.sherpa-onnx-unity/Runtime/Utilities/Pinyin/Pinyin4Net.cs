using System.Collections.Generic;
using System.Linq;
using System.Text;
using System;
using Eitan.SherpaONNXUnity.Runtime.Core.Utilities.Pinyin.Exception;
using Eitan.SherpaONNXUnity.Runtime.Core.Utilities.Pinyin.Data;

namespace Eitan.SherpaONNXUnity.Runtime.Core.Utilities.Pinyin
{
    /// <summary>
    /// 拼音与汉字相互转换入口
    /// </summary>
    public class Pinyin4Net
    {
        protected Pinyin4Net() { }

        #region 获取单字拼音
        /// <summary>
        /// 获取汉字的拼音数组
        /// </summary>
        /// <param name="hanzi">要查询拼音的汉字字符</param>
        /// <param name="format">设置输出拼音的格式</param>
        /// <returns>汉字的拼音数组，若未找到汉字拼音，则返回空数组</returns>
        /// <exception cref="UnsupportedUnicodeException">当要获取拼音的字符不是汉字时抛出此异常</exception>
        public static string[] GetPinyin(char hanzi, PinyinFormat format = PinyinFormat.None)
        {
            if (!PinyinUtil.IsHanzi(hanzi))
            {
                // 不是汉字
                throw new UnsupportedUnicodeException("不支持的字符: " + hanzi);
            }
            var pinyin = PinyinDB.Instance.GetPinyin(hanzi);
            if (format == PinyinFormat.None)
            {
                return pinyin;
            }
            return pinyin.Select(item => PinyinUtil.Format(item, format)).ToArray();
        }

        /// <summary>
        /// 获取格式化后的唯一拼音(单音字)或者第一个拼音(多音字)
        /// </summary>
        /// <param name="hanzi">要查询拼音的汉字字符</param>
        /// <param name="format">拼音输出格式化参数</param>
        /// <see cref="PinyinFormat"/>
        /// <seealso cref="PinyinUtil"/>
        /// <returns>格式化后的唯一拼音(单音字)或者第一个拼音(多音字)</returns>
        /// <exception cref="UnsupportedUnicodeException">当要获取拼音的字符不是汉字时抛出此异常</exception>
        public static string GetFirstPinyin(char hanzi, PinyinFormat format = PinyinFormat.None)
        {
            var pinyin = GetPinyin(hanzi)[0];
            if (format == PinyinFormat.None)
            {
                return pinyin;
            }
            return PinyinUtil.Format(pinyin, format);
        }
        #endregion

        #region 获取多个汉字拼音
        /// <summary>
        /// 获取一个字符串内所有汉字的拼音数组
        /// </summary>
        /// <param name="text">要获取拼音的汉字字符串</param>
        /// <param name="format">拼音输出格式化参数</param>
        /// <returns>返回拼音列表，每个汉字的拼音会作为一个数组存放（无论是单音字还是多音字）</returns>
        /// <see cref="PinyinItem"/>
        public static List<PinyinItem> GetPinyinArray(string text, PinyinFormat format)
        {
            var pinyin = new List<PinyinItem>();
            if (string.IsNullOrEmpty(text))
            {
                return pinyin;
            }
            ;

            foreach (var character in text)
            {
                var item = new PinyinItem(character);
                if (item.IsHanzi)
                {
                    item.AddRange(GetPinyin(character, format));
                }
                pinyin.Add(item);
            }

            return pinyin;
        }

        /// <summary>
        /// 获取一个字符串内所有汉字的拼音（多音字取第一个读音，带格式）
        /// </summary>
        /// <param name="text">要获取拼音的汉字字符串</param>
        /// <param name="format">拼音输出格式化参数</param>
        /// <param name="caseSpread">是否将前面的格式中的大小写扩展到其它非拼音字符，默认为false。firstLetterOnly为false时有效 </param>
        /// <param name="firstLetterOnly">是否只取拼音首字母，为true时，format无效</param>
        /// <param name="multiFirstLetter">firstLetterOnly为true时有效，多音字的多个读音首字母是否全取，如果多音字拼音首字母相同，只保留一个</param>
        /// <returns>firstLetterOnly为true时，只取拼音首字母格式为[L]，后面追加空格；multiFirstLetter为true时，多音字的多个拼音首字母格式为[L, H]，后面追加空格</returns>
        public static string GetPinyin(string text, PinyinFormat format, bool caseSpread, bool firstLetterOnly, bool multiFirstLetter)
        {
            if (string.IsNullOrEmpty(text))
            {
                return "";
            }

            var pinyin = new StringBuilder();
            var firstLetterBuf = new List<string>();

            foreach (var item in text)
            {
                if (!PinyinUtil.IsHanzi(item))
                {
                    pinyin.Append(item);
                    continue;
                }

                if (!firstLetterOnly)
                {
                    pinyin.Append(GetFirstPinyin(item, format) + " ");
                    continue;
                }
                if (!multiFirstLetter)
                {
                    pinyin.AppendFormat("[{0}] ", GetFirstPinyin(item)[0]);
                    continue;
                }

                firstLetterBuf.Clear();

                firstLetterBuf.AddRange(GetPinyin(item)
                    .Select(py => py[0].ToString())
                    // 这句是处理多音字，多音字的拼音可能首字母是一样的，
                    // 如果是一样的，肯定就只返回一次
                    .Distinct());

                pinyin.AppendFormat("[{0}] ", string.Join(",", firstLetterBuf.ToArray()));
            }

            return PinyinUtil.SpreadCase(format, caseSpread, firstLetterOnly, pinyin);
        }

        /// <summary>
        /// 获取一个字符串内所有汉字的拼音（多音字取第一个读音，带格式）
        /// </summary>
        /// <param name="text">要获取拼音的汉字字符串</param>
        /// <param name="format">拼音输出格式化参数</param>
        /// <param name="caseSpread">是否将前面的格式中的大小写扩展到其它非拼音字符，默认为false。</param>
        /// <param name="pinyinHandler">
        /// 拼音处理器，在获取到拼音后通过这个来处理，
        /// 如果传null，则默认取第一个拼音（多音字），
        /// 参数：
        /// 1 string[] 拼音数组
        /// 2 char 当前的汉字
        /// 3 string 要转成拼音的字符串
        /// return 拼音字符串，这个返回值将作为这个汉字的拼音放到结果中
        /// </param>
        public static string GetPinyin(string text, PinyinFormat format, bool caseSpread, Func<string[], char, string, string> pinyinHandler)
        {
            if (string.IsNullOrEmpty(text))
            {
                return "";
            }

            var pinyin = new StringBuilder();

            foreach (var item in text)
            {
                if (!PinyinUtil.IsHanzi(item))
                {
                    pinyin.Append(item);
                    continue;
                }

                var pinyinTemp = PinyinDB.Instance.GetPinyin(item);

                pinyin.Append(pinyinHandler == null ?
                    pinyinTemp[0] :
                    pinyinHandler.Invoke(pinyinTemp, item, text));
            }

            return PinyinUtil.SpreadCase(format, caseSpread, false, pinyin);
        }

        /// <summary>
        /// 获取一个字符串内所有汉字的拼音（多音字取第一个读音，带格式），format中指定的大小写模式不会扩展到非拼音字符
        /// </summary>
        /// <param name="text">要获取拼音的汉字字符串</param>
        /// <param name="format">拼音输出格式化参数</param>
        /// <returns>格式化后的拼音字符串</returns>
        public static string GetPinyin(string text, PinyinFormat format)
        {
            return GetPinyin(text, format, false, false, false);
        }
        #endregion

        /// <summary>
        /// 根据单个拼音查询匹配的汉字
        /// </summary>
        /// <param name="pinyin">要查询汉字的单个拼音</param>
        /// <param name="matchAll">是否全部匹配，为true时，匹配整个拼音，否则匹配开头字符</param>
        /// <returns></returns>
        public static string[] GetHanzi(string pinyin, bool matchAll)
        {
            return PinyinDB.Instance.GetHanzi(pinyin.ToLower(), matchAll)
            .Select(item => item.ToString()).ToArray();
        }

        #region 数据库接口
        /// <summary>
        /// 更新拼音数据库
        /// </summary>
        /// <param name="data">Key为汉字，Value为读音(多个读音的拼音作为数组的项)</param>
        /// <param name="replace">是否替换已经存在的项，默认为 false</param>
        public static void UpdateMap(Dictionary<char, string[]> data, bool replace = false)
        {
            PinyinDB.Instance.Update(data, replace);
        }
        #endregion
    }
}
