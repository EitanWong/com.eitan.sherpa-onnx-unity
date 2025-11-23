/// Copyright (c)  2024.5 by 东风破

using System.Runtime.InteropServices;

namespace Eitan.SherpaONNXUnity.Runtime.Native
{
    public struct SpokenLanguageIdentificationConfig
    {
        public SpokenLanguageIdentificationConfig(bool initializeDefaults = true)
        {
            this = default;

            if (!initializeDefaults)
            {
                return;
            }

            Whisper = new SpokenLanguageIdentificationWhisperConfig();
            NumThreads = 1;
            Debug = 0;
            Provider = "cpu";
        }
        public SpokenLanguageIdentificationWhisperConfig Whisper;

        public int NumThreads;
        public int Debug;

        [MarshalAs(UnmanagedType.LPStr)]
        public string Provider;
    }

}
