/// Copyright (c)  2026  Xiaomi Corporation (authors: Fangjun Kuang)

using System.Runtime.InteropServices;

namespace Eitan.SherpaONNXUnity.Runtime.Native
{
    [StructLayout(LayoutKind.Sequential)]
    public struct OfflineSourceSeparationConfig
    {
        public OfflineSourceSeparationConfig(bool initialize = true)
        {
            Model = new OfflineSourceSeparationModelConfig();
        }

        public OfflineSourceSeparationModelConfig Model;
    }
}
