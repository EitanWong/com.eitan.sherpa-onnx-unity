/// Copyright (c)  2024  Xiaomi Corporation

using System.Runtime.InteropServices;

namespace Eitan.SherpaONNXUnity.Runtime.Native
{

    [StructLayout(LayoutKind.Sequential)]
    public struct OfflineSpeakerDiarizationConfig
    {
        public OfflineSpeakerDiarizationConfig(bool initializeDefaults = true)
        {
            this = default;

            if (!initializeDefaults)
            {
                return;
            }

            Segmentation = new OfflineSpeakerSegmentationModelConfig();
            Embedding = new SpeakerEmbeddingExtractorConfig();
            Clustering = new FastClusteringConfig();

            MinDurationOn = 0.3F;
            MinDurationOff = 0.5F;
        }

        public OfflineSpeakerSegmentationModelConfig Segmentation;
        public SpeakerEmbeddingExtractorConfig Embedding;
        public FastClusteringConfig Clustering;

        public float MinDurationOn;
        public float MinDurationOff;
    }
}



