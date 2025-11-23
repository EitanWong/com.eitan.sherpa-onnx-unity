/// Copyright (c)  2024  Xiaomi Corporation (authors: Fangjun Kuang)

using System.Runtime.InteropServices;

namespace Eitan.SherpaONNXUnity.Runtime.Native
{
    [StructLayout(LayoutKind.Sequential)]
    public struct VadModelConfig
    {
        public VadModelConfig(bool initializeDefaults = true)
        {
            this = default;

            if (!initializeDefaults)
            {
                return;
            }

            SileroVad = new SileroVadModelConfig();
            SampleRate = 16000;
            NumThreads = 1;
            Provider = "cpu";
            Debug = 0;
            TenVad = new TenVadModelConfig();
        }

        public SileroVadModelConfig SileroVad;

        public int SampleRate;

        public int NumThreads;

        [MarshalAs(UnmanagedType.LPStr)]
        public string Provider;

        public int Debug;

        public TenVadModelConfig TenVad;
    }
}

