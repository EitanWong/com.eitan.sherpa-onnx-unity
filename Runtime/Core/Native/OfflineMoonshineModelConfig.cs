/// Copyright (c)  2024  Xiaomi Corporation (authors: Fangjun Kuang)

using System.Runtime.InteropServices;

namespace Eitan.SherpaONNXUnity.Runtime.Native
{
    [StructLayout(LayoutKind.Sequential)]
    public struct OfflineMoonshineModelConfig
    {
        public OfflineMoonshineModelConfig(bool initializeDefaults = true)
        {
            this = default;

            if (!initializeDefaults)
            {
                return;
            }

            Preprocessor = "";
            Encoder = "";
            UncachedDecoder = "";
            CachedDecoder = "";
        }
        [MarshalAs(UnmanagedType.LPStr)]
        public string Preprocessor;

        [MarshalAs(UnmanagedType.LPStr)]
        public string Encoder;

        [MarshalAs(UnmanagedType.LPStr)]
        public string UncachedDecoder;

        [MarshalAs(UnmanagedType.LPStr)]
        public string CachedDecoder;
    }
}
