/// Copyright (c)  2024.5 by 东风破
using System;
using System.Runtime.InteropServices;

namespace Eitan.SherpaONNXUnity.Runtime.Native
{
    public class SpeakerEmbeddingExtractor : IDisposable
    {
        public SpeakerEmbeddingExtractor(SpeakerEmbeddingExtractorConfig config)
        {
            IntPtr h = SherpaONNXCreateSpeakerEmbeddingExtractor(ref config);
            _handle = new HandleRef(this, h);
        }

        public OnlineStream CreateStream()
        {
            IntPtr p = SherpaONNXSpeakerEmbeddingExtractorCreateStream(_handle.Handle);
            return new OnlineStream(p);
        }

        public bool IsReady(OnlineStream stream)
        {
            return SherpaONNXSpeakerEmbeddingExtractorIsReady(_handle.Handle, stream.Handle) != 0;
        }

        public float[] Compute(OnlineStream stream)
        {
            IntPtr p = SherpaONNXSpeakerEmbeddingExtractorComputeEmbedding(_handle.Handle, stream.Handle);

            int dim = Dim;
            float[] ans = new float[dim];
            Marshal.Copy(p, ans, 0, dim);

            SherpaONNXSpeakerEmbeddingExtractorDestroyEmbedding(p);

            return ans;
        }

        public int Dim
        {
            get
            {
                return SherpaONNXSpeakerEmbeddingExtractorDim(_handle.Handle);
            }
        }

        public void Dispose()
        {
            Cleanup();
            // Prevent the object from being placed on the
            // finalization queue
            System.GC.SuppressFinalize(this);
        }

        ~SpeakerEmbeddingExtractor()
        {
            Cleanup();
        }

        private void Cleanup()
        {
            SherpaONNXDestroySpeakerEmbeddingExtractor(_handle.Handle);

            // Don't permit the handle to be used again.
            _handle = new HandleRef(this, IntPtr.Zero);
        }

        private HandleRef _handle;

        [DllImport(Dll.Filename)]
        private static extern IntPtr SherpaONNXCreateSpeakerEmbeddingExtractor(ref SpeakerEmbeddingExtractorConfig config);

        [DllImport(Dll.Filename)]
        private static extern void SherpaONNXDestroySpeakerEmbeddingExtractor(IntPtr handle);

        [DllImport(Dll.Filename)]
        private static extern int SherpaONNXSpeakerEmbeddingExtractorDim(IntPtr handle);

        [DllImport(Dll.Filename)]
        private static extern IntPtr SherpaONNXSpeakerEmbeddingExtractorCreateStream(IntPtr handle);

        [DllImport(Dll.Filename)]
        private static extern int SherpaONNXSpeakerEmbeddingExtractorIsReady(IntPtr handle, IntPtr stream);

        [DllImport(Dll.Filename)]
        private static extern IntPtr SherpaONNXSpeakerEmbeddingExtractorComputeEmbedding(IntPtr handle, IntPtr stream);

        [DllImport(Dll.Filename)]
        private static extern void SherpaONNXSpeakerEmbeddingExtractorDestroyEmbedding(IntPtr p);
    }

}
