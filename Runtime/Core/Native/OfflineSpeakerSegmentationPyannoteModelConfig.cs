/// Copyright (c)  2024  Xiaomi Corporation

using System.Runtime.InteropServices;

namespace Eitan.SherpaONNXUnity.Runtime.Native
{

    [StructLayout(LayoutKind.Sequential)]
    public struct OfflineSpeakerSegmentationPyannoteModelConfig
    {
        public OfflineSpeakerSegmentationPyannoteModelConfig(bool initialize = true)
        {
            Model = "";
        }

        [MarshalAs(UnmanagedType.LPStr)]
        public string Model;
    }
}

