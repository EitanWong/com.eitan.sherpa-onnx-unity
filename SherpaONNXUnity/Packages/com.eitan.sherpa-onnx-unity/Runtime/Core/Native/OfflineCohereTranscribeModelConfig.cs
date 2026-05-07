/// Copyright (c)  2026  Xiaomi Corporation

using System.Runtime.InteropServices;

namespace Eitan.SherpaONNXUnity.Runtime.Native
{
    [StructLayout(LayoutKind.Sequential)]
    public struct OfflineCohereTranscribeModelConfig
    {
        public OfflineCohereTranscribeModelConfig(bool initialize = true)
        {
            Encoder = "";
            Decoder = "";
            Language = "";
            UsePunct = 1;
            UseItn = 1;
        }

        [MarshalAs(UnmanagedType.LPStr)]
        public string Encoder;

        [MarshalAs(UnmanagedType.LPStr)]
        public string Decoder;

        [MarshalAs(UnmanagedType.LPStr)]
        public string Language;

        public int UsePunct;
        public int UseItn;
    }
}
