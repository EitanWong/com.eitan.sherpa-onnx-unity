/// Copyright (c)  2024  Xiaomi Corporation
using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Eitan.SherpaONNXUnity.Runtime.Native
{
    // IntPtr is actually a `const float*` from C++
    public delegate int OfflineSpeakerDiarizationProgressCallback(int numProcessedChunks, int numTotalChunks, IntPtr arg);

    public class OfflineSpeakerDiarization : IDisposable
    {
        public OfflineSpeakerDiarization(OfflineSpeakerDiarizationConfig config)
        {
            IntPtr h = SherpaONNXCreateOfflineSpeakerDiarization(ref config);
            _handle = new HandleRef(this, h);
        }

        public void SetConfig(OfflineSpeakerDiarizationConfig config)
        {
            SherpaONNXOfflineSpeakerDiarizationSetConfig(_handle.Handle, ref config);
        }

        public OfflineSpeakerDiarizationSegment[] Process(float[] samples)
        {
            IntPtr result = SherpaONNXOfflineSpeakerDiarizationProcess(_handle.Handle, samples, samples.Length);
            return ProcessImpl(result);
        }

        public OfflineSpeakerDiarizationSegment[] ProcessWithCallback(float[] samples, OfflineSpeakerDiarizationProgressCallback callback, IntPtr arg)
        {
            IntPtr result = SherpaONNXOfflineSpeakerDiarizationProcessWithCallback(_handle.Handle, samples, samples.Length, callback, arg);
            return ProcessImpl(result);
        }

        private OfflineSpeakerDiarizationSegment[] ProcessImpl(IntPtr result)
        {
            if (result == IntPtr.Zero)
            {
                return new OfflineSpeakerDiarizationSegment[] { };
            }

            int numSegments = SherpaONNXOfflineSpeakerDiarizationResultGetNumSegments(result);
            IntPtr p = SherpaONNXOfflineSpeakerDiarizationResultSortByStartTime(result);

            OfflineSpeakerDiarizationSegment[] ans = new OfflineSpeakerDiarizationSegment[numSegments];
            unsafe
            {
                int size = sizeof(float) * 2 + sizeof(int);
                for (int i = 0; i != numSegments; ++i)
                {
                    IntPtr t = new IntPtr((byte*)p + i * size);
                    ans[i] = new OfflineSpeakerDiarizationSegment(t);

                    // The following IntPtr.Add() does not support net20
                    // ans[i] = new OfflineSpeakerDiarizationSegment(IntPtr.Add(p, i));
                }
            }


            SherpaONNXOfflineSpeakerDiarizationDestroySegment(p);
            SherpaONNXOfflineSpeakerDiarizationDestroyResult(result);

            return ans;

        }

        public void Dispose()
        {
            Cleanup();
            // Prevent the object from being placed on the
            // finalization queue
            System.GC.SuppressFinalize(this);
        }

        ~OfflineSpeakerDiarization()
        {
            Cleanup();
        }

        private void Cleanup()
        {
            SherpaONNXDestroyOfflineSpeakerDiarization(_handle.Handle);

            // Don't permit the handle to be used again.
            _handle = new HandleRef(this, IntPtr.Zero);
        }

        private HandleRef _handle;

        public int SampleRate
        {
            get
            {
                return SherpaONNXOfflineSpeakerDiarizationGetSampleRate(_handle.Handle);
            }
        }

        [DllImport(Dll.Filename)]
        private static extern IntPtr SherpaONNXCreateOfflineSpeakerDiarization(ref OfflineSpeakerDiarizationConfig config);

        [DllImport(Dll.Filename)]
        private static extern void SherpaONNXDestroyOfflineSpeakerDiarization(IntPtr handle);

        [DllImport(Dll.Filename)]
        private static extern int SherpaONNXOfflineSpeakerDiarizationGetSampleRate(IntPtr handle);

        [DllImport(Dll.Filename)]
        private static extern int SherpaONNXOfflineSpeakerDiarizationResultGetNumSegments(IntPtr handle);

        [DllImport(Dll.Filename)]
        private static extern IntPtr SherpaONNXOfflineSpeakerDiarizationProcess(IntPtr handle, float[] samples, int n);

        [DllImport(Dll.Filename, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr SherpaONNXOfflineSpeakerDiarizationProcessWithCallback(IntPtr handle, float[] samples, int n, OfflineSpeakerDiarizationProgressCallback callback, IntPtr arg);

        [DllImport(Dll.Filename)]
        private static extern void SherpaONNXOfflineSpeakerDiarizationDestroyResult(IntPtr handle);

        [DllImport(Dll.Filename)]
        private static extern IntPtr SherpaONNXOfflineSpeakerDiarizationResultSortByStartTime(IntPtr handle);

        [DllImport(Dll.Filename)]
        private static extern void SherpaONNXOfflineSpeakerDiarizationDestroySegment(IntPtr handle);

        [DllImport(Dll.Filename)]
        private static extern void SherpaONNXOfflineSpeakerDiarizationSetConfig(IntPtr handle, ref OfflineSpeakerDiarizationConfig config);
    }
}

