namespace Eitan.SherpaONNXUnity.Runtime.Core.Utilities.Pinyin.Exception
{
    /// <summary>
    /// 转换拼音的字符非汉字字符
    /// </summary>
    public class UnsupportedUnicodeException : PinyinException
    {
        public UnsupportedUnicodeException(string message) : base(message)
        {

        }
    }
}
