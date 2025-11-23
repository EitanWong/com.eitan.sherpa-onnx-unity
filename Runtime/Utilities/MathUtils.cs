namespace Eitan.SherpaONNXUnity.Runtime.Utilities
{
    // 建議將此函式放在一個靜態的工具類別中
    public static class MathUtils
    {
        /// <summary>
        /// Calculates the next power of two for a given integer.
        /// </summary>
        public static int NextPowerOfTwo(int n)
        {
            if (n <= 0) { return 1; }
            n--;
            n |= n >> 1;
            n |= n >> 2;
            n |= n >> 4;
            n |= n >> 8;
            n |= n >> 16;
            return n + 1;
        }
    }
}
