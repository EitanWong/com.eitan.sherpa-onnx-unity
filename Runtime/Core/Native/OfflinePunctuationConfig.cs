/// Copyright (c)  2024  Xiaomi Corporation (authors: Fangjun Kuang)

using System.Runtime.InteropServices;

namespace Eitan.SherpaONNXUnity.Runtime.Native
{
    [StructLayout(LayoutKind.Sequential)]
    public struct OfflinePunctuationConfig
    {
        public OfflinePunctuationConfig(bool initialize = true)
        {
            Model = new OfflinePunctuationModelConfig();
        }
        public OfflinePunctuationModelConfig Model;
    }
}
