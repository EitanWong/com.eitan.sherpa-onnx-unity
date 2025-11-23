using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Eitan.SherpaONNXUnity.Runtime.Utilities.Pinyin.Exception;
using static Eitan.SherpaONNXUnity.Runtime.Utilities.Pinyin.PinyinFormat;

namespace Eitan.SherpaONNXUnity.Runtime.Utilities.Pinyin
{
    /// <summary>
    /// 拼音工具类
    /// </summary>
    public static class PinyinUtil
    {
        /// <summary>
        /// 声明不需要将首字母大写的拼音
        /// </summary>
        private static readonly string[] IGNORE_LIST = { "a", "e", "o" };

        /// <summary>
        /// 判断字符是否是汉字
        /// </summary>
        /// <param name="ch">要判断的字符</param>
        /// <returns></returns>
        public static bool IsHanzi(char ch)
        {
            return 0x4e00 <= ch && ch <= 0x9fbb;
        }

        /// <summary>
        /// 将字符串格式的 PinyinFormat 转换成 PinyinFormat 对象
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static PinyinFormat ParseFormat(string str)
        {
            if (Enum.TryParse(str, out PinyinFormat format))
            {
                return format;
            }
            return PinyinFormat.None;
        }

        /// <summary>
        /// 扩展 OutputFormat，判断是否包含指定的格式化标识
        /// </summary>
        /// <param name="expected"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static bool Contains(this PinyinFormat value, PinyinFormat expected)
        {
            return (expected & value) == expected;
        }

        /// <summary>
        /// 将拼音格式化成指定的格式
        /// </summary>
        /// <param name="py">待格式化的拼音</param>
        /// <param name="format">格式</param>
        /// <see cref="ToneFormat"/>
        /// <see cref="CaseFormat"/>
        /// <see cref="VCharFormat"/>
        /// <returns></returns>
        public static string Format(string py, PinyinFormat format)
        {
            // "v"或"u:" yu 不能添加声调
            if (format.Contains(WITH_TONE_MARK) && (format.Contains(WITH_V) || format.Contains(WITH_U_AND_COLON) || format.Contains(WITH_YU)))
            {
                throw new PinyinException("\"v\", \"u:\", \"yu\" 不能添加声调");
            }
            var pinyin = py;

            if (format.Contains(WITHOUT_TONE))
            {
                // 不带声调
                var reg = new Regex("[1-5]");
                pinyin = reg.Replace(pinyin, "");
            }
            else if (format.Contains(WITH_TONE_MARK))
            {
                // 带声调标志
                pinyin = pinyin.Replace("u:", "v");
                pinyin = convertToneNumber2ToneMark(pinyin);
            }

            if (format.Contains(WITH_V))
            {
                // 输出v
                pinyin = pinyin.Replace("u:", "v");
            }
            else if (format.Contains(WITH_U_UNICODE))
            {
                // 输出ü
                pinyin = pinyin.Replace("u:", "ü");
            }
            else if (format.Contains(WITH_YU))
            {
                // 输出 yu
                pinyin = pinyin.Replace("u:", "yu");
            }

            if (format.Contains(UPPERCASE))
            {
                // 大写
                pinyin = pinyin.ToUpper();
            }
            else if (format.Contains(LOWERCASE))
            {
                // 小写
                pinyin = pinyin.ToLower();
            }
            else if (format.Contains(CAPITALIZE_FIRST_LETTER))
            {
                // 首字母大写

                // 不处理单拼音 a e o
                if (!IGNORE_LIST.Contains(pinyin.ToLower()))
                {
                    pinyin = pinyin.Substring(0, 1).ToUpper() + (pinyin.Length == 1 ? "" : pinyin.Substring(1));
                }
            }

            return pinyin;
        }


        /// <summary>
        /// 将拼音的声调数字转换成字符
        /// </summary>
        /// <param name="pinyin"></param>
        /// <returns></returns>
        private static string convertToneNumber2ToneMark(string pinyin)
        {
            var lowerCasePinyinStr = pinyin.ToLower();
            var reg = new Regex("[a-z]*[1-5]?");
            if (!reg.IsMatch(lowerCasePinyinStr))
            {
                return lowerCasePinyinStr;
            }

            const char defautlCharValue = '$';
            const int defautlIndexValue = -1;

            var unmarkedVowel = defautlCharValue;
            var indexOfUnmarkedVowel = defautlIndexValue;

            const char charA = 'a';
            const char charE = 'e';
            const string ouStr = "ou";
            const string allUnmarkedVowelStr = "aeiouv";
            const string allMarkedVowelStr = "āáăàaēéĕèeīíĭìiōóŏòoūúŭùuǖǘǚǜü";
            reg = new Regex("[a-z]*[1-5]");
            if (!reg.IsMatch(lowerCasePinyinStr))
            {
                return lowerCasePinyinStr.Replace("v", "ü");
            }

            var tuneNumber = (int)char.GetNumericValue(lowerCasePinyinStr[lowerCasePinyinStr.Length - 1]);

            var indexOfA = lowerCasePinyinStr.IndexOf(charA);
            var indexOfE = lowerCasePinyinStr.IndexOf(charE);
            var ouIndex = lowerCasePinyinStr.IndexOf(ouStr, StringComparison.Ordinal);

            if (-1 != indexOfA)
            {
                indexOfUnmarkedVowel = indexOfA;
                unmarkedVowel = charA;
            }
            else if (-1 != indexOfE)
            {
                indexOfUnmarkedVowel = indexOfE;
                unmarkedVowel = charE;
            }
            else if (-1 != ouIndex)
            {
                indexOfUnmarkedVowel = ouIndex;
                unmarkedVowel = ouStr[0];
            }
            else
            {
                reg = new Regex("[" + allUnmarkedVowelStr + "]");

                for (var i = lowerCasePinyinStr.Length - 1; i >= 0; i--)
                {
                    if (!reg.IsMatch(lowerCasePinyinStr[i].ToString()))
                    {
                        continue;
                    }

                    indexOfUnmarkedVowel = i;
                    unmarkedVowel = lowerCasePinyinStr[i];
                    break;
                }
            }

            if (defautlCharValue == unmarkedVowel || defautlIndexValue == indexOfUnmarkedVowel)
            {
                return lowerCasePinyinStr;
            }

            var rowIndex = allUnmarkedVowelStr.IndexOf(unmarkedVowel);
            var columnIndex = tuneNumber - 1;

            var vowelLocation = rowIndex * 5 + columnIndex;

            var markedVowel = allMarkedVowelStr[vowelLocation];

            var resultBuffer = new StringBuilder();
            // 声母
            resultBuffer.Append(lowerCasePinyinStr.Substring(0, indexOfUnmarkedVowel).Replace("v", "ü"));
            // 有声调的部分
            resultBuffer.Append(markedVowel);
            // 剩下的部分
            resultBuffer.Append(lowerCasePinyinStr.Substring(indexOfUnmarkedVowel + 1).Replace("v", "ü"));

            var result = resultBuffer.ToString();
            // 替换声调数字
            result = new Regex("[0-9]").Replace(result, "");

            return result;
            // only replace v with ü (umlat) character
        }

        /// <summary>
        /// 将首字母搞成大写的
        /// </summary>
        /// <param name="buffer"></param>
        /// <returns></returns>
        public static string CapitalizeFirstLetter(StringBuilder buffer)
        {
            // 遇到空格后，将后面一个非空格的字符设置为大写
            for (var i = 0; i < buffer.Length; i++)
            {
                if (buffer[i] != ' ')
                {
                    continue;
                }

                // 当前是最后一个字符
                if (i == buffer.Length - 1)
                {
                    continue;
                }

                var nextchar = buffer[i + 1];

                // 后一个字符是空格
                if (nextchar == ' ')
                {
                    continue;
                }

                buffer[i + 1] = char.ToUpper(nextchar);
            }

            return buffer.ToString();
        }

        /// <summary>
        /// 扩展大小写格式
        /// </summary>
        /// <param name="format"></param>
        /// <param name="caseSpread"></param>
        /// <param name="firstLetterOnly"></param>
        /// <param name="pinyin"></param>
        /// <returns></returns>
        public static string SpreadCase(PinyinFormat format, bool caseSpread, bool firstLetterOnly, StringBuilder pinyin)
        {
            if (firstLetterOnly || !caseSpread)
            {
                return pinyin.ToString().Trim();
            }

            if (format.Contains(CAPITALIZE_FIRST_LETTER))
            {
                return CapitalizeFirstLetter(pinyin); ;
            }
            if (format.Contains(LOWERCASE))
            {
                return pinyin.ToString().Trim().ToLower();
            }
            if (format.Contains(UPPERCASE))
            {
                return pinyin.ToString().Trim().ToUpper();
            }

            return pinyin.ToString().Trim();
        }
    }
}
