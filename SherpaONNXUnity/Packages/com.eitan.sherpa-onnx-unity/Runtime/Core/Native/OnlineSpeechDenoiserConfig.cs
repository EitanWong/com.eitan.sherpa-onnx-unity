/// Copyright (c)  2026  Xiaomi Corporation (authors: Fangjun Kuang)

namespace Eitan.SherpaONNXUnity.Runtime.Native
{
    public struct OnlineSpeechDenoiserConfig
    {
        public OnlineSpeechDenoiserConfig(bool initialize = true)
        {
            Model = new OfflineSpeechDenoiserModelConfig();
        }

        public OfflineSpeechDenoiserModelConfig Model;
    }
}
