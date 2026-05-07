/// Copyright (c)  2026  Xiaomi Corporation (authors: Fangjun Kuang)

using System.Runtime.InteropServices;

namespace Eitan.SherpaONNXUnity.Runtime.Native
{
    [StructLayout(LayoutKind.Sequential)]
    public struct OfflineSourceSeparationSpleeterModelConfig
    {
        public OfflineSourceSeparationSpleeterModelConfig(bool initialize = true)
        {
            Vocals = "";
            Accompaniment = "";
        }

        [MarshalAs(UnmanagedType.LPStr)]
        public string Vocals;

        [MarshalAs(UnmanagedType.LPStr)]
        public string Accompaniment;
    }
}
