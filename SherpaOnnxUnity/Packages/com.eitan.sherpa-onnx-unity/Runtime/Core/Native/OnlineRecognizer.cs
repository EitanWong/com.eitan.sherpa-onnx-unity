/// Copyright (c)  2023  Xiaomi Corporation (authors: Fangjun Kuang)
/// Copyright (c)  2023 by manyeyes
/// Copyright (c)  2024.5 by 东风破
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Eitan.SherpaONNXUnity.Runtime.Native
{
    // please see
    // https://www.mono-project.com/docs/advanced/pinvoke/#gc-safe-pinvoke-code
    // https://www.mono-project.com/docs/advanced/pinvoke/#properly-disposing-of-resources
    public class OnlineRecognizer : IDisposable
    {
        public OnlineRecognizer(OnlineRecognizerConfig config)
        {
            IntPtr h = SherpaONNXCreateOnlineRecognizer(ref config);
            _handle = new HandleRef(this, h);
        }

        public OnlineStream CreateStream()
        {
            IntPtr p = SherpaONNXCreateOnlineStream(_handle.Handle);
            return new OnlineStream(p);
        }

        /// Return true if the passed stream is ready for decoding.
        public bool IsReady(OnlineStream stream)
        {
            return IsReady(_handle.Handle, stream.Handle) != 0;
        }

        /// Return true if an endpoint is detected for this stream.
        /// You probably need to invoke Reset(stream) when this method returns
        /// true.
        public bool IsEndpoint(OnlineStream stream)
        {
            return SherpaONNXOnlineStreamIsEndpoint(_handle.Handle, stream.Handle) != 0;
        }

        /// You have to ensure that IsReady(stream) returns true before
        /// you call this method
        public void Decode(OnlineStream stream)
        {
            Decode(_handle.Handle, stream.Handle);
        }

        // The caller should ensure all passed streams are ready for decoding.
        public void Decode(IEnumerable<OnlineStream> streams)
        {
            // TargetFramework=net20 does not support System.Linq
            // IntPtr[] ptrs = streams.Select(s => s.Handle).ToArray();
            List<IntPtr> list = new List<IntPtr>();
            foreach (OnlineStream s in streams)
            {
                list.Add(s.Handle);
            }

            IntPtr[] ptrs = list.ToArray();
            Decode(_handle.Handle, ptrs, ptrs.Length);
        }

        public OnlineRecognizerResult GetResult(OnlineStream stream)
        {
            IntPtr h = GetResult(_handle.Handle, stream.Handle);
            OnlineRecognizerResult result = new OnlineRecognizerResult(h);
            DestroyResult(h);
            return result;
        }

        /// When this method returns, IsEndpoint(stream) will return false.
        public void Reset(OnlineStream stream)
        {
            SherpaONNXOnlineStreamReset(_handle.Handle, stream.Handle);
        }

        public void Dispose()
        {
            Cleanup();
            // Prevent the object from being placed on the
            // finalization queue
            System.GC.SuppressFinalize(this);
        }

        ~OnlineRecognizer()
        {
            Cleanup();
        }

        private void Cleanup()
        {
            SherpaONNXDestroyOnlineRecognizer(_handle.Handle);

            // Don't permit the handle to be used again.
            _handle = new HandleRef(this, IntPtr.Zero);
        }

        private HandleRef _handle;

        [DllImport(Dll.Filename)]
        private static extern IntPtr SherpaONNXCreateOnlineRecognizer(ref OnlineRecognizerConfig config);

        [DllImport(Dll.Filename)]
        private static extern void SherpaONNXDestroyOnlineRecognizer(IntPtr handle);

        [DllImport(Dll.Filename)]
        private static extern IntPtr SherpaONNXCreateOnlineStream(IntPtr handle);

        [DllImport(Dll.Filename, EntryPoint = "SherpaONNXIsOnlineStreamReady")]
        private static extern int IsReady(IntPtr handle, IntPtr stream);

        [DllImport(Dll.Filename, EntryPoint = "SherpaONNXDecodeOnlineStream")]
        private static extern void Decode(IntPtr handle, IntPtr stream);

        [DllImport(Dll.Filename, EntryPoint = "SherpaONNXDecodeMultipleOnlineStreams")]
        private static extern void Decode(IntPtr handle, IntPtr[] streams, int n);

        [DllImport(Dll.Filename, EntryPoint = "SherpaONNXGetOnlineStreamResult")]
        private static extern IntPtr GetResult(IntPtr handle, IntPtr stream);

        [DllImport(Dll.Filename, EntryPoint = "SherpaONNXDestroyOnlineRecognizerResult")]
        private static extern void DestroyResult(IntPtr result);

        [DllImport(Dll.Filename)]
        private static extern void SherpaONNXOnlineStreamReset(IntPtr handle, IntPtr stream);

        [DllImport(Dll.Filename)]
        private static extern int SherpaONNXOnlineStreamIsEndpoint(IntPtr handle, IntPtr stream);
    }
}
