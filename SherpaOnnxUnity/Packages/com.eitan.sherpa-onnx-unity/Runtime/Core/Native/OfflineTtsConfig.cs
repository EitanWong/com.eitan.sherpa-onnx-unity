/// Copyright (c)  2024.5 by 东风破

using System.Runtime.InteropServices;

namespace Eitan.SherpaONNXUnity.Runtime.Native
{
    [StructLayout(LayoutKind.Sequential)]
    public struct OfflineTtsConfig
    {
        public OfflineTtsConfig(bool initializeDefaults = true)
        {
            this = default;

            if (!initializeDefaults)
            {
                return;
            }

            Model = new OfflineTtsModelConfig();
            RuleFsts = "";
            MaxNumSentences = 1;
            RuleFars = "";
            SilenceScale = 0.2F;
        }
        public OfflineTtsModelConfig Model;

        [MarshalAs(UnmanagedType.LPStr)]
        public string RuleFsts;

        public int MaxNumSentences;

        [MarshalAs(UnmanagedType.LPStr)]
        public string RuleFars;

        public float SilenceScale;
    }
}
