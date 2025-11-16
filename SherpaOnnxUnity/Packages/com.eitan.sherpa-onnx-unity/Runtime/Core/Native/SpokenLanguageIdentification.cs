/// Copyright (c)  2024.5 by 东风破
using System;
using System.Runtime.InteropServices;

namespace Eitan.SherpaONNXUnity.Runtime.Native
{
    public class SpokenLanguageIdentification : IDisposable
    {
        public SpokenLanguageIdentification(SpokenLanguageIdentificationConfig config)
        {
            IntPtr h = SherpaONNXCreateSpokenLanguageIdentification(ref config);
            _handle = new HandleRef(this, h);
        }

        public OfflineStream CreateStream()
        {
            IntPtr p = SherpaONNXSpokenLanguageIdentificationCreateOfflineStream(_handle.Handle);
            return new OfflineStream(p);
        }

        public SpokenLanguageIdentificationResult Compute(OfflineStream stream)
        {
            IntPtr h = SherpaONNXSpokenLanguageIdentificationCompute(_handle.Handle, stream.Handle);
            SpokenLanguageIdentificationResult result = new SpokenLanguageIdentificationResult(h);
            SherpaONNXDestroySpokenLanguageIdentificationResult(h);
            return result;
        }

        public void Dispose()
        {
            Cleanup();
            // Prevent the object from being placed on the
            // finalization queue
            System.GC.SuppressFinalize(this);
        }

        ~SpokenLanguageIdentification()
        {
            Cleanup();
        }

        private void Cleanup()
        {
            SherpaONNXDestroySpokenLanguageIdentification(_handle.Handle);

            // Don't permit the handle to be used again.
            _handle = new HandleRef(this, IntPtr.Zero);
        }

        private HandleRef _handle;

        [DllImport(Dll.Filename)]
        private static extern IntPtr SherpaONNXCreateSpokenLanguageIdentification(ref SpokenLanguageIdentificationConfig config);

        [DllImport(Dll.Filename)]
        private static extern void SherpaONNXDestroySpokenLanguageIdentification(IntPtr handle);

        [DllImport(Dll.Filename)]
        private static extern IntPtr SherpaONNXSpokenLanguageIdentificationCreateOfflineStream(IntPtr handle);

        [DllImport(Dll.Filename)]
        private static extern IntPtr SherpaONNXSpokenLanguageIdentificationCompute(IntPtr handle, IntPtr stream);

        [DllImport(Dll.Filename)]
        private static extern void SherpaONNXDestroySpokenLanguageIdentificationResult(IntPtr handle);
    }
}
