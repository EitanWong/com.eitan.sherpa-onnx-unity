/// Copyright (c)  2025  Xiaomi Corporation (authors: Fangjun Kuang)

using System.Runtime.InteropServices;

namespace Eitan.SherpaONNXUnity.Runtime.Native
{
    [StructLayout(LayoutKind.Sequential)]
    public struct HomophoneReplacerConfig
    {
        public HomophoneReplacerConfig(bool initializeDefaults = true)
        {
            this = default;

            if (!initializeDefaults)
            {
                return;
            }

            DictDir = "";
            Lexicon = "";
            RuleFsts = "";
        }

        [MarshalAs(UnmanagedType.LPStr)]
        public string DictDir;

        [MarshalAs(UnmanagedType.LPStr)]
        public string Lexicon;

        [MarshalAs(UnmanagedType.LPStr)]
        public string RuleFsts;
    }
}
