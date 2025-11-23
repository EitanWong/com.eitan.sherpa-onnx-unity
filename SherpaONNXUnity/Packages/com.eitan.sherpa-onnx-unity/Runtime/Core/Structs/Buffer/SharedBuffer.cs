namespace Eitan.SherpaONNXUnity.Runtime
{
    using System;
    using System.Buffers;

    /// <summary>
    /// Minimal shared buffer helpers to avoid repeated allocations across modules.
    /// </summary>
    internal static class SharedBuffer
    {
        public static float[] Rent(int minimumLength)
        {
            return ArrayPool<float>.Shared.Rent(minimumLength);
        }

        public static float[] RentAndCopy(ReadOnlySpan<float> source)
        {
            var buffer = ArrayPool<float>.Shared.Rent(source.Length);
            source.CopyTo(buffer.AsSpan(0, source.Length));
            return buffer;
        }

        public static void Return(float[] buffer)
        {
            if (buffer != null)
            {
                ArrayPool<float>.Shared.Return(buffer);
            }
        }
    }
}
