/// Copyright (c)  2025  Xiaomi Corporation (authors: Fangjun Kuang)

using System.Runtime.InteropServices;

namespace Eitan.SherpaONNXUnity.Runtime.Native
{
    [StructLayout(LayoutKind.Sequential)]
    public struct TenVadModelConfig
    {
        public TenVadModelConfig(bool initializeDefaults = true)
        {
            this = default;

            if (!initializeDefaults)
            {
                return;
            }

            Model = "";
            Threshold = 0.5F;
            MinSilenceDuration = 0.5F;
            MinSpeechDuration = 0.25F;
            WindowSize = 256;
            MaxSpeechDuration = 5.0F;
        }

        [MarshalAs(UnmanagedType.LPStr)]
        public string Model;

        public float Threshold;

        public float MinSilenceDuration;

        public float MinSpeechDuration;

        public int WindowSize;

        public float MaxSpeechDuration;
    }
}
