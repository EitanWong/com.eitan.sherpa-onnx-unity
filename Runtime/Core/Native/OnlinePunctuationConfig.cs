/// Copyright (c)  2026  Xiaomi Corporation (authors: Fangjun Kuang)

using System.Runtime.InteropServices;

namespace Eitan.SherpaONNXUnity.Runtime.Native
{
    [StructLayout(LayoutKind.Sequential)]
    public struct OnlinePunctuationConfig
    {
        public OnlinePunctuationConfig(bool initialize = true)
        {
            Model = new OnlinePunctuationModelConfig();
        }

        public OnlinePunctuationModelConfig Model;
    }
}
