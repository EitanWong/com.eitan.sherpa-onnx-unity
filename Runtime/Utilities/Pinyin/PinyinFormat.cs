using System;

namespace Eitan.SherpaONNXUnity.Runtime.Utilities.Pinyin
{
    [Flags]
    public enum PinyinFormat
    {
        /// <summary>
        /// 不指定格式
        /// </summary>
        None,
        /// <summary>
        /// 首字母大写，此选项对 a e o i u 几个独音无效
        /// </summary>
        CAPITALIZE_FIRST_LETTER = 1 << 1,
        /// <summary>
        /// 全小写
        /// </summary>
        LOWERCASE = 1 << 2,
        /// <summary>
        /// 全大写
        /// </summary>
        UPPERCASE = 1 << 3,
        /// <summary>
        /// 将 ü 输出为 u:
        /// </summary>
        WITH_U_AND_COLON = 1 << 4,
        /// <summary>
        /// 将 ü 输出为 v
        /// </summary>
        WITH_V = 1 << 5,
        /// <summary>
        /// 将 ü 输出为ü
        /// </summary>
        WITH_U_UNICODE = 1 << 6,
        /// <summary>
        /// 将 ü 输出为 yu
        /// 在兼容英文的环境下， ü 要写作 yu。注意，目前仅存在 lyu 这一个读音有这种情况。
        /// </summary>
        WITH_YU = 1 << 10,
        /// <summary>
        /// 带声调标志
        /// </summary>
        WITH_TONE_MARK = 1 << 7,
        /// <summary>
        /// 不带声调
        /// </summary>
        WITHOUT_TONE = 1 << 8,
        /// <summary>
        /// 带声调数字值
        /// </summary>
        WITH_TONE_NUMBER = 1 << 9,
    }
}
