/// Copyright (c)  2024.5 by 东风破

using System.Runtime.InteropServices;

namespace Eitan.SherpaONNXUnity.Runtime.Native
{
    [StructLayout(LayoutKind.Sequential)]
    public struct OfflineTtsVitsModelConfig
    {
        public OfflineTtsVitsModelConfig(bool initializeDefaults = true)
        {
            this = default;

            if (!initializeDefaults)
            {
                return;
            }

            Model = "";
            Lexicon = "";
            Tokens = "";
            DataDir = "";

            NoiseScale = 0.667F;
            NoiseScaleW = 0.8F;
            LengthScale = 1.0F;

            DictDir = "";
        }
        [MarshalAs(UnmanagedType.LPStr)]
        public string Model;

        [MarshalAs(UnmanagedType.LPStr)]
        public string Lexicon;

        [MarshalAs(UnmanagedType.LPStr)]
        public string Tokens;

        [MarshalAs(UnmanagedType.LPStr)]
        public string DataDir;

        public float NoiseScale;
        public float NoiseScaleW;
        public float LengthScale;

        [MarshalAs(UnmanagedType.LPStr)]
        public string DictDir;
    }
}
