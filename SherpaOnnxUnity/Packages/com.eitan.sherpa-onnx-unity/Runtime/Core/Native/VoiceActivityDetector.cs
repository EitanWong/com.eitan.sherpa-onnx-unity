/// Copyright (c)  2024  Xiaomi Corporation (authors: Fangjun Kuang)
using System;
using System.Runtime.InteropServices;

namespace Eitan.SherpaONNXUnity.Runtime.Native
{
    public class VoiceActivityDetector : IDisposable
    {
        public VoiceActivityDetector(VadModelConfig config, float bufferSizeInSeconds)
        {
            IntPtr h = SherpaONNXCreateVoiceActivityDetector(ref config, bufferSizeInSeconds);
            _handle = new HandleRef(this, h);
        }

        public void AcceptWaveform(float[] samples)
        {
            SherpaONNXVoiceActivityDetectorAcceptWaveform(_handle.Handle, samples, samples.Length);
        }

        public bool IsEmpty()
        {
            return SherpaONNXVoiceActivityDetectorEmpty(_handle.Handle) == 1;
        }

        public bool IsSpeechDetected()
        {
            return SherpaONNXVoiceActivityDetectorDetected(_handle.Handle) == 1;
        }

        public void Pop()
        {
            SherpaONNXVoiceActivityDetectorPop(_handle.Handle);
        }

        public SpeechSegment Front()
        {
            IntPtr p = SherpaONNXVoiceActivityDetectorFront(_handle.Handle);

            SpeechSegment segment = new SpeechSegment(p);

            SherpaONNXDestroySpeechSegment(p);

            return segment;
        }

        public void Clear()
        {
            SherpaONNXVoiceActivityDetectorClear(_handle.Handle);
        }

        public void Reset()
        {
            SherpaONNXVoiceActivityDetectorReset(_handle.Handle);
        }

        public void Flush()
        {
            SherpaONNXVoiceActivityDetectorFlush(_handle.Handle);
        }

        public void Dispose()
        {
            Cleanup();
            // Prevent the object from being placed on the
            // finalization queue
            System.GC.SuppressFinalize(this);
        }

        ~VoiceActivityDetector()
        {
            Cleanup();
        }

        private void Cleanup()
        {
            SherpaONNXDestroyVoiceActivityDetector(_handle.Handle);

            // Don't permit the handle to be used again.
            _handle = new HandleRef(this, IntPtr.Zero);
        }

        private HandleRef _handle;

        [DllImport(Dll.Filename)]
        private static extern IntPtr SherpaONNXCreateVoiceActivityDetector(ref VadModelConfig config, float bufferSizeInSeconds);

        [DllImport(Dll.Filename)]
        private static extern void SherpaONNXDestroyVoiceActivityDetector(IntPtr handle);

        [DllImport(Dll.Filename)]
        private static extern void SherpaONNXVoiceActivityDetectorAcceptWaveform(IntPtr handle, float[] samples, int n);

        [DllImport(Dll.Filename)]
        private static extern int SherpaONNXVoiceActivityDetectorEmpty(IntPtr handle);

        [DllImport(Dll.Filename)]
        private static extern int SherpaONNXVoiceActivityDetectorDetected(IntPtr handle);

        [DllImport(Dll.Filename)]
        private static extern void SherpaONNXVoiceActivityDetectorPop(IntPtr handle);

        [DllImport(Dll.Filename)]
        private static extern void SherpaONNXVoiceActivityDetectorClear(IntPtr handle);

        [DllImport(Dll.Filename)]
        private static extern IntPtr SherpaONNXVoiceActivityDetectorFront(IntPtr handle);

        [DllImport(Dll.Filename)]
        private static extern void SherpaONNXDestroySpeechSegment(IntPtr segment);

        [DllImport(Dll.Filename)]
        private static extern void SherpaONNXVoiceActivityDetectorReset(IntPtr handle);

        [DllImport(Dll.Filename)]
        private static extern void SherpaONNXVoiceActivityDetectorFlush(IntPtr handle);
    }
}
