/// Copyright (c)  2025  Xiaomi Corporation (authors: Fangjun Kuang)

using System.Runtime.InteropServices;

namespace Eitan.SherpaONNXUnity.Runtime.Native
{
    [StructLayout(LayoutKind.Sequential)]
    public struct OfflineSpeechDenoiserConfig
    {
        public OfflineSpeechDenoiserConfig(bool initialize = true)
        {
            Model = new OfflineSpeechDenoiserModelConfig();
        }
        public OfflineSpeechDenoiserModelConfig Model;
    }
}
