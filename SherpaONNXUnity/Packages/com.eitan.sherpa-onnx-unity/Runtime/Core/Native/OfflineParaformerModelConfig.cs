/// Copyright (c)  2024.5 by 东风破

using System.Runtime.InteropServices;

namespace Eitan.SherpaONNXUnity.Runtime.Native
{
    [StructLayout(LayoutKind.Sequential)]
    public struct OfflineParaformerModelConfig
    {
        public OfflineParaformerModelConfig(bool initializeDefaults = true)
        {
            this = default;

            if (!initializeDefaults)
            {
                return;
            }

            Model = "";
        }
        [MarshalAs(UnmanagedType.LPStr)]
        public string Model;
    }

}
