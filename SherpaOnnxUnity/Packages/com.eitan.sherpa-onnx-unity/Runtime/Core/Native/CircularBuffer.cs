/// Copyright (c)  2024  Xiaomi Corporation (authors: Fangjun Kuang)

using System;
using System.Runtime.InteropServices;

namespace Eitan.SherpaONNXUnity.Runtime.Native
{
    public class CircularBuffer : IDisposable
    {
        public CircularBuffer(int capacity)
        {
            IntPtr h = SherpaONNXCreateCircularBuffer(capacity);
            _handle = new HandleRef(this, h);
        }

        public void Push(float[] data)
        {
            SherpaONNXCircularBufferPush(_handle.Handle, data, data.Length);
        }

        public float[] Get(int startIndex, int n)
        {
            IntPtr p = SherpaONNXCircularBufferGet(_handle.Handle, startIndex, n);

            float[] ans = new float[n];
            Marshal.Copy(p, ans, 0, n);

            SherpaONNXCircularBufferFree(p);

            return ans;
        }

        public void Pop(int n)
        {
            SherpaONNXCircularBufferPop(_handle.Handle, n);
        }

        public int Size
        {
            get
            {
                return SherpaONNXCircularBufferSize(_handle.Handle);
            }
        }

        public int Head
        {
            get
            {
                return SherpaONNXCircularBufferHead(_handle.Handle);
            }
        }

        public void Reset()
        {
            SherpaONNXCircularBufferReset(_handle.Handle);
        }

        public void Dispose()
        {
            Cleanup();
            // Prevent the object from being placed on the
            // finalization queue
            System.GC.SuppressFinalize(this);
        }

        ~CircularBuffer()
        {
            Cleanup();
        }

        private void Cleanup()
        {
            SherpaONNXDestroyCircularBuffer(_handle.Handle);

            // Don't permit the handle to be used again.
            _handle = new HandleRef(this, IntPtr.Zero);
        }

        private HandleRef _handle;

        [DllImport(Dll.Filename)]
        private static extern IntPtr SherpaONNXCreateCircularBuffer(int capacity);

        [DllImport(Dll.Filename)]
        private static extern void SherpaONNXDestroyCircularBuffer(IntPtr handle);

        [DllImport(Dll.Filename)]
        private static extern void SherpaONNXCircularBufferPush(IntPtr handle, float[] p, int n);

        [DllImport(Dll.Filename)]
        private static extern IntPtr SherpaONNXCircularBufferGet(IntPtr handle, int startIndex, int n);

        [DllImport(Dll.Filename)]
        private static extern void SherpaONNXCircularBufferFree(IntPtr p);

        [DllImport(Dll.Filename)]
        private static extern void SherpaONNXCircularBufferPop(IntPtr handle, int n);

        [DllImport(Dll.Filename)]
        private static extern int SherpaONNXCircularBufferSize(IntPtr handle);

        [DllImport(Dll.Filename)]
        private static extern int SherpaONNXCircularBufferHead(IntPtr handle);

        [DllImport(Dll.Filename)]
        private static extern void SherpaONNXCircularBufferReset(IntPtr handle);
    }
}
