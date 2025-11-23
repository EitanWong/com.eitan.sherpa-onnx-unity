/// Copyright (c)  2025  Xiaomi Corporation (authors: Fangjun Kuang)

using System.Runtime.InteropServices;

namespace Eitan.SherpaONNXUnity.Runtime.Native
{
    [StructLayout(LayoutKind.Sequential)]
    public struct OfflineSpeechDenoiserModelConfig
    {
        public OfflineSpeechDenoiserModelConfig(bool initializeDefaults = true)
        {
            this = default;

            if (!initializeDefaults)
            {
                return;
            }

            Gtcrn = new OfflineSpeechDenoiserGtcrnModelConfig();
            NumThreads = 1;
            Debug = 0;
            Provider = "cpu";
        }

        public OfflineSpeechDenoiserGtcrnModelConfig Gtcrn;

        public int NumThreads;

        public int Debug;

        [MarshalAs(UnmanagedType.LPStr)]
        public string Provider;
    }
}
