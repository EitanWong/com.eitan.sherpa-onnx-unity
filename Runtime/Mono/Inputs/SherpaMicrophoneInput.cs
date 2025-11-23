// Runtime: Packages/com.eitan.sherpa-onnx-unity/Runtime/Mono/Inputs/SherpaMicrophoneInput.cs

namespace Eitan.Sherpa.Onnx.Unity.Mono.Inputs
{
    using System;
    using System.Collections.Generic;
    using UnityEngine;
    using UnityEngine.Events;

    /// <summary>
    /// Standalone microphone capture component that emits mono PCM chunks at a fixed cadence.
    /// Other sherpa components can subscribe to <see cref="ChunkReady"/> without duplicating capture logic.
    /// </summary>
    [AddComponentMenu("SherpaONNX/Audio Inputs/Microphone Capture")]
    [DisallowMultipleComponent]
    public sealed class SherpaMicrophoneInput : SherpaAudioInputSource
    {
        [Header("Capture Settings")]
        [SerializeField]
        [Tooltip("Automatically start recording when this component becomes enabled in play mode.")]
        private bool autoStartOnEnable = true;

        [SerializeField]
        [Tooltip("Preferred microphone device. Leave empty to use the first detected input.")]
        private string preferredDevice = string.Empty;

        [SerializeField]
        [Tooltip("Sample rate requested from Unity's Microphone API.")]
        private int requestedSampleRate = 16000;

        [SerializeField]
        [Tooltip("Length of each chunk emitted to listeners (seconds).")]
        [Range(0.05f, 0.5f)]
        private float chunkDurationSeconds = 0.2f;

        [SerializeField]
        [Tooltip("Internal circular buffer length used by Unity's Microphone API (seconds).")]
        [Range(1, 30)]
        private int bufferLengthSeconds = 5;

        [SerializeField]
        [Tooltip("Downmix multi-channel microphone input into a mono signal before emitting chunks.")]
        private bool downmixToMono = true;

        [Header("Events")]
        [SerializeField]
        private ChunkReadyUnityEvent onChunkReady = new ChunkReadyUnityEvent();

        [SerializeField]
        private UnityEvent<bool> onRecordingStateChanged = new UnityEvent<bool>();

        public override event Action<float[], int> ChunkReady;

        public override int OutputSampleRate => requestedSampleRate;

        public override bool IsCapturing =>
            !string.IsNullOrEmpty(activeDeviceName) &&
            Microphone.IsRecording(activeDeviceName);

        private AudioClip microphoneClip;
        private string activeDeviceName;
        private int microphoneChannels = 1;
        private int microphoneSampleFrames;
        private float[] microphoneRingBuffer;
        private int chunkFrameCount;
        private int lastReadSampleFrame;
        private bool microphoneWarmupPending;
        private readonly List<string> deviceCache = new List<string>();

        [Serializable]
        public sealed class ChunkReadyUnityEvent : UnityEvent<float[], int>
        {
        }

        private void Awake()
        {
            ConfigureChunkSettings();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            ConfigureChunkSettings();
        }
#endif

        private void OnEnable()
        {
            if (Application.isPlaying && autoStartOnEnable)
            {
                TryStartCapture();
            }
        }

        private void Update()
        {
            if (Application.isPlaying)
            {
                PollMicrophone();
            }
        }

        private void OnDisable()
        {
            StopCapture();
        }

        public override bool TryStartCapture()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[SherpaMicrophoneInput] Capture is only supported in Play Mode.");
                return false;
            }

            if (IsCapturing)
            {
                return true;
            }

            var device = ResolveMicrophoneDevice(preferredDevice);
            if (string.IsNullOrEmpty(device))
            {
                Debug.LogWarning("[SherpaMicrophoneInput] No microphone devices detected.");
                return false;
            }

            ConfigureChunkSettings();

            int bufferLength = Mathf.Clamp(bufferLengthSeconds, 1, 30);
            microphoneClip = Microphone.Start(device, true, bufferLength, requestedSampleRate);
            if (microphoneClip == null)
            {
                Debug.LogError($"[SherpaMicrophoneInput] Failed to start microphone '{device}'.");
                return false;
            }

            activeDeviceName = device;
            microphoneChannels = Mathf.Max(1, microphoneClip.channels);
            microphoneSampleFrames = microphoneClip.samples;
            microphoneRingBuffer = new float[microphoneSampleFrames * microphoneChannels];
            lastReadSampleFrame = 0;
            microphoneWarmupPending = true;

            onRecordingStateChanged?.Invoke(true);
            return true;
        }

        public override void StopCapture()
        {
            if (!string.IsNullOrEmpty(activeDeviceName))
            {
                Microphone.End(activeDeviceName);
            }

            microphoneClip = null;
            activeDeviceName = null;
            microphoneRingBuffer = null;
            microphoneSampleFrames = 0;
            lastReadSampleFrame = 0;
            microphoneWarmupPending = false;

            onRecordingStateChanged?.Invoke(false);
        }

        /// <summary>
        /// Returns the currently detected microphone devices.
        /// </summary>
        public IReadOnlyList<string> GetAvailableDevices()
        {
            deviceCache.Clear();
            if (Microphone.devices != null)
            {
                deviceCache.AddRange(Microphone.devices);
            }
            return deviceCache;
        }

        /// <summary>
        /// Changes the preferred device name. Takes effect on the next capture start.
        /// </summary>
        public void SetPreferredDevice(string deviceName)
        {
            preferredDevice = deviceName ?? string.Empty;
        }

        private void ConfigureChunkSettings()
        {
            requestedSampleRate = Mathf.Max(8000, requestedSampleRate);
            chunkFrameCount = Mathf.Max(
                128,
                Mathf.RoundToInt(requestedSampleRate * Mathf.Clamp(chunkDurationSeconds, 0.05f, 0.5f)));
        }

        private void PollMicrophone()
        {
            if (microphoneClip == null || string.IsNullOrEmpty(activeDeviceName))
            {
                return;
            }

            if (!Microphone.IsRecording(activeDeviceName))
            {
                return;
            }

            var currentPosition = Microphone.GetPosition(activeDeviceName);
            if (currentPosition <= 0 && microphoneWarmupPending)
            {
                return;
            }
            microphoneWarmupPending = false;

            if (currentPosition < 0 || microphoneSampleFrames <= 0)
            {
                return;
            }

            var framesAvailable = currentPosition - lastReadSampleFrame;
            if (framesAvailable < 0)
            {
                framesAvailable += microphoneSampleFrames;
            }

            if (framesAvailable < chunkFrameCount)
            {
                return;
            }

            microphoneClip.GetData(microphoneRingBuffer, 0);

            while (framesAvailable >= chunkFrameCount)
            {
                var payload = ExtractChunk(lastReadSampleFrame);
                lastReadSampleFrame = (lastReadSampleFrame + chunkFrameCount) % microphoneSampleFrames;
                framesAvailable -= chunkFrameCount;
                EmitChunk(payload);
            }
        }

        private float[] ExtractChunk(int startFrame)
        {
            var chunk = new float[chunkFrameCount];
            if (microphoneRingBuffer == null)
            {
                return chunk;
            }

            if (!downmixToMono || microphoneChannels <= 1)
            {
                for (int i = 0; i < chunk.Length; i++)
                {
                    int frameIndex = (startFrame + i) % microphoneSampleFrames;
                    int rawIndex = frameIndex * microphoneChannels;
                    chunk[i] = microphoneRingBuffer[rawIndex];
                }
                return chunk;
            }

            for (int i = 0; i < chunk.Length; i++)
            {
                int frameIndex = (startFrame + i) % microphoneSampleFrames;
                int rawIndex = frameIndex * microphoneChannels;
                float sum = 0f;
                for (int ch = 0; ch < microphoneChannels; ch++)
                {
                    sum += microphoneRingBuffer[rawIndex + ch];
                }
                chunk[i] = sum / microphoneChannels;
            }

            return chunk;
        }

        private void EmitChunk(float[] samples)
        {
            if (samples == null || samples.Length == 0)
            {
                return;
            }

            onChunkReady?.Invoke(samples, requestedSampleRate);
            ChunkReady?.Invoke(samples, requestedSampleRate);
        }

        private static string ResolveMicrophoneDevice(string preference)
        {
            var devices = Microphone.devices;
            if (devices == null || devices.Length == 0)
            {
                return string.Empty;
            }

            if (!string.IsNullOrEmpty(preference))
            {
                foreach (var candidate in devices)
                {
                    if (string.Equals(candidate, preference, StringComparison.OrdinalIgnoreCase))
                    {
                        return candidate;
                    }
                }
            }

            return devices[0];
        }
    }
}
