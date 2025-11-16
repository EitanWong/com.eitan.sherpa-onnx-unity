/// Copyright (c)  2024.5 by 东风破

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Eitan.SherpaONNXUnity.Runtime.Native
{
    public class OfflineRecognizer : IDisposable
    {
        public OfflineRecognizer(OfflineRecognizerConfig config)
        {
            IntPtr h = SherpaONNXCreateOfflineRecognizer(ref config);
            _handle = new HandleRef(this, h);
        }

        public void SetConfig(OfflineRecognizerConfig config)
        {
            SherpaONNXOfflineRecognizerSetConfig(_handle.Handle, ref config);
        }

        public OfflineStream CreateStream()
        {
            IntPtr p = SherpaONNXCreateOfflineStream(_handle.Handle);
            return new OfflineStream(p);
        }

        public void Decode(OfflineStream stream)
        {
            Decode(_handle.Handle, stream.Handle);
        }

        // The caller should ensure all passed streams are ready for decoding.
        public void Decode(IEnumerable<OfflineStream> streams)
        {
            // TargetFramework=net20 does not support System.Linq
            // IntPtr[] ptrs = streams.Select(s => s.Handle).ToArray();
            List<IntPtr> list = new List<IntPtr>();
            foreach (OfflineStream s in streams)
            {
                list.Add(s.Handle);
            }
            IntPtr[] ptrs = list.ToArray();
            Decode(_handle.Handle, ptrs, ptrs.Length);
        }

        public void Dispose()
        {
            Cleanup();
            // Prevent the object from being placed on the
            // finalization queue
            System.GC.SuppressFinalize(this);
        }

        ~OfflineRecognizer()
        {
            Cleanup();
        }

        private void Cleanup()
        {
            SherpaONNXDestroyOfflineRecognizer(_handle.Handle);

            // Don't permit the handle to be used again.
            _handle = new HandleRef(this, IntPtr.Zero);
        }

        private HandleRef _handle;

        [DllImport(Dll.Filename)]
        private static extern IntPtr SherpaONNXCreateOfflineRecognizer(ref OfflineRecognizerConfig config);

        [DllImport(Dll.Filename)]
        private static extern void SherpaONNXOfflineRecognizerSetConfig(IntPtr handle, ref OfflineRecognizerConfig config);

        [DllImport(Dll.Filename)]
        private static extern void SherpaONNXDestroyOfflineRecognizer(IntPtr handle);

        [DllImport(Dll.Filename)]
        private static extern IntPtr SherpaONNXCreateOfflineStream(IntPtr handle);

        [DllImport(Dll.Filename, EntryPoint = "SherpaONNXDecodeOfflineStream")]
        private static extern void Decode(IntPtr handle, IntPtr stream);

        [DllImport(Dll.Filename, EntryPoint = "SherpaONNXDecodeMultipleOfflineStreams")]
        private static extern void Decode(IntPtr handle, IntPtr[] streams, int n);
    }

}
