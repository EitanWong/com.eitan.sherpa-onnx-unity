/// Copyright (c)  2025  Xiaomi Corporation (authors: Fangjun Kuang)

using System.Runtime.InteropServices;

namespace Eitan.SherpaONNXUnity.Runtime.Native
{

    [StructLayout(LayoutKind.Sequential)]
    public struct OfflineZipformerCtcModelConfig
    {
        public OfflineZipformerCtcModelConfig(bool initialize = true)
        {
            Model = "";
        }
        [MarshalAs(UnmanagedType.LPStr)]
        public string Model;
    }
}
