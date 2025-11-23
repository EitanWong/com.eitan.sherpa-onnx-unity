/// Copyright (c)  2025  Xiaomi Corporation (authors: Fangjun Kuang)

using System.Runtime.InteropServices;

namespace Eitan.SherpaONNXUnity.Runtime.Native
{
    [StructLayout(LayoutKind.Sequential)]
    public struct AudioTaggingConfig
    {
        public AudioTaggingConfig(bool initializeDefaults = true)
        {
            this = default;

            if (!initializeDefaults)
            {
                return;
            }

            Model = new AudioTaggingModelConfig();

            Labels = "";
            TopK = 5;
        }

        public AudioTaggingModelConfig Model;

        [MarshalAs(UnmanagedType.LPStr)]
        public string Labels;

        public int TopK;
    }
}
