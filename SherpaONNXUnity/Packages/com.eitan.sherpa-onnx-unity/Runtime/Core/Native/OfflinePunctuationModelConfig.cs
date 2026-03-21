/// Copyright (c)  2024  Xiaomi Corporation (authors: Fangjun Kuang)

using System.Runtime.InteropServices;

namespace Eitan.SherpaONNXUnity.Runtime.Native
{
    [StructLayout(LayoutKind.Sequential)]
    public struct OfflinePunctuationModelConfig
    {
        public OfflinePunctuationModelConfig(bool initialize = true)
        {
            CtTransformer = "";
            NumThreads = 1;
            Debug = 0;
            Provider = "cpu";
        }

        [MarshalAs(UnmanagedType.LPStr)]
        public string CtTransformer;

        public int NumThreads;

        public int Debug;

        [MarshalAs(UnmanagedType.LPStr)]
        public string Provider;
    }
}
