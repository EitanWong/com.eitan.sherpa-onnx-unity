/// Copyright (c)  2025  Xiaomi Corporation (authors: Fangjun Kuang)

using System.Runtime.InteropServices;

namespace Eitan.SherpaONNXUnity.Runtime.Native
{
    [StructLayout(LayoutKind.Sequential)]
    public struct OfflineTtsKittenModelConfig
    {
        public OfflineTtsKittenModelConfig(bool initializeDefaults = true)
        {
            this = default;

            if (!initializeDefaults)
            {
                return;
            }

            Model = "";
            Voices = "";
            Tokens = "";
            DataDir = "";

            LengthScale = 1.0F;
        }
        [MarshalAs(UnmanagedType.LPStr)]
        public string Model;

        [MarshalAs(UnmanagedType.LPStr)]
        public string Voices;

        [MarshalAs(UnmanagedType.LPStr)]
        public string Tokens;

        [MarshalAs(UnmanagedType.LPStr)]
        public string DataDir;

        public float LengthScale;
    }
}
