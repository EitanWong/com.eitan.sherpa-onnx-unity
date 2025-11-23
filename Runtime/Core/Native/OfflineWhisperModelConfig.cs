/// Copyright (c)  2024.5 by 东风破

using System.Runtime.InteropServices;

namespace Eitan.SherpaONNXUnity.Runtime.Native
{
    [StructLayout(LayoutKind.Sequential)]
    public struct OfflineWhisperModelConfig
    {
        public OfflineWhisperModelConfig(bool initializeDefaults = true)
        {
            this = default;

            if (!initializeDefaults)
            {
                return;
            }

            Encoder = "";
            Decoder = "";
            Language = "";
            Task = "transcribe";
            TailPaddings = -1;
        }
        [MarshalAs(UnmanagedType.LPStr)]
        public string Encoder;

        [MarshalAs(UnmanagedType.LPStr)]
        public string Decoder;

        [MarshalAs(UnmanagedType.LPStr)]
        public string Language;

        [MarshalAs(UnmanagedType.LPStr)]
        public string Task;

        public int TailPaddings;
    }

}
