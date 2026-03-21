/// Copyright (c)  2024  Xiaomi Corporation

using System.Runtime.InteropServices;

namespace Eitan.SherpaONNXUnity.Runtime.Native
{

    [StructLayout(LayoutKind.Sequential)]
    public struct FastClusteringConfig
    {
        public FastClusteringConfig(bool initialize = true)
        {
            NumClusters = -1;
            Threshold = 0.5F;
        }

        public int NumClusters;
        public float Threshold;
    }
}
