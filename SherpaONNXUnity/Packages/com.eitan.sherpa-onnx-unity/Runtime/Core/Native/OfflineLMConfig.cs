/// Copyright (c)  2023  Xiaomi Corporation (authors: Fangjun Kuang)
/// Copyright (c)  2023 by manyeyes
/// Copyright (c)  2024.5 by 东风破

using System.Runtime.InteropServices;

namespace Eitan.SherpaONNXUnity.Runtime.Native
{

    [StructLayout(LayoutKind.Sequential)]
    public struct OfflineLMConfig
    {
        public OfflineLMConfig(bool initialize = true)
        {
            Model = "";
            Scale = 0.5F;
        }
        [MarshalAs(UnmanagedType.LPStr)]
        public string Model;

        public float Scale;
    }

}