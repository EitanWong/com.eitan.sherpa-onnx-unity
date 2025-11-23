using System.Collections.Generic;

namespace Eitan.SherpaONNXUnity.Runtime.Utilities.Pinyin
{
    /// <summary>
    /// 调用 GetPinyinArray 时的返回项数据结构，其用于兼容：多音字时返回多个拼音
    /// </summary>
    public class PinyinItem : List<string>
    {
        /// <summary>
        /// 原始字符
        /// </summary>
        public char RawChar { get; set; }
        /// <summary>
        /// 是否是汉字
        /// </summary>
        public bool IsHanzi { get; private set; }

        public PinyinItem() { }
        public PinyinItem(char character)
        {
            RawChar = character;
            IsHanzi = PinyinUtil.IsHanzi(character);
        }

        /// <summary>
        /// 将拼音处理成字符串，如果当前字符为多音字，那么多个拼音使用 , 分隔
        /// </summary>
        public override string ToString()
        {
            if (IsHanzi)
            {
                return $"[{string.Join(",", this)}]";
            }

            return RawChar.ToString();
        }

        public override int GetHashCode()
        {
            return RawChar.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            if (obj == null)
            {
                return false;
            }

            var other = obj as PinyinItem;
            if (other == null)
            {
                return false;
            }

            if (RawChar != other.RawChar)
            {
                return false;
            }

            if (Count != other.Count)
            {
                return false;
            }

            for (int i = 0; i < Count; i++)
            {
                if (this[i] != other[i])
                {
                    return false;
                }
            }

            return true;
        }
    }
}
