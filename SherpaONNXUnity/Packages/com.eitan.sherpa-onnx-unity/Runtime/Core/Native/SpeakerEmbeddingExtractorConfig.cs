/// Copyright (c)  2024.5 by 东风破

using System.Runtime.InteropServices;

namespace Eitan.SherpaONNXUnity.Runtime.Native
{
    [StructLayout(LayoutKind.Sequential)]
    public struct SpeakerEmbeddingExtractorConfig
    {
        public SpeakerEmbeddingExtractorConfig(bool initializeDefaults = true)
        {
            this = default;

            if (!initializeDefaults)
            {
                return;
            }

            Model = "";
            NumThreads = 1;
            Debug = 0;
            Provider = "cpu";
        }

        [MarshalAs(UnmanagedType.LPStr)]
        public string Model;

        public int NumThreads;
        public int Debug;

        [MarshalAs(UnmanagedType.LPStr)]
        public string Provider;
    }

}
