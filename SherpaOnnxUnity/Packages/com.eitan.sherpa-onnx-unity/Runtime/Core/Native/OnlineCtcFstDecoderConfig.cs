/// Copyright (c)  2023  Xiaomi Corporation (authors: Fangjun Kuang)
/// Copyright (c)  2023 by manyeyes
/// Copyright (c)  2024.5 by 东风破

using System.Runtime.InteropServices;

namespace Eitan.SherpaONNXUnity.Runtime.Native
{
    [StructLayout(LayoutKind.Sequential)]
    public struct OnlineCtcFstDecoderConfig
    {
        public OnlineCtcFstDecoderConfig(bool initializeDefaults = true)
        {
            this = default;

            if (!initializeDefaults)
            {
                return;
            }

            Graph = "";
            MaxActive = 3000;
        }

        [MarshalAs(UnmanagedType.LPStr)]
        public string Graph;

        public int MaxActive;
    }

}
