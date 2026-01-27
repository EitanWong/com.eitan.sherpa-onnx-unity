/// Copyright (c)  2025  Xiaomi Corporation (authors: Fangjun Kuang)

using System.Runtime.InteropServices;

namespace Eitan.SherpaONNXUnity.Runtime.Native
{


    [StructLayout(LayoutKind.Sequential)]
    public struct OfflineFunAsrNanoModelConfig
    {
        public OfflineFunAsrNanoModelConfig(bool initializeDefaults = true)
        {

            this = default;

            if (!initializeDefaults)
            {
                return;
            }
            EncoderAdaptor = "";
            LLM = "";
            Embedding = "";
            Tokenizer = "";
            SystemPrompt = "You are a helpful assistant.";
            UserPrompt = "语音转写：";
            MaxNewTokens = 512;
            Temperature = 1e-6F;
            TopP = 0.8F;
            Seed = 42;
        }

        [MarshalAs(UnmanagedType.LPStr)]
        public string EncoderAdaptor;

        [MarshalAs(UnmanagedType.LPStr)]
        public string LLM;

        [MarshalAs(UnmanagedType.LPStr)]
        public string Embedding;

        [MarshalAs(UnmanagedType.LPStr)]
        public string Tokenizer;

        [MarshalAs(UnmanagedType.LPStr)]
        public string SystemPrompt;

        [MarshalAs(UnmanagedType.LPStr)]
        public string UserPrompt;

        public int MaxNewTokens;
        public float Temperature;
        public float TopP;
        public int Seed;
    }
}
