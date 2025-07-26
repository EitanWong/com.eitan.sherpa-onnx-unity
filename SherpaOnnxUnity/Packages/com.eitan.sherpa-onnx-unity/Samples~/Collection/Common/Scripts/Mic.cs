using System;
using System.Collections.Generic;
using UnityEngine;

namespace Eitan.SherpaOnnxUnity.Samples {
    /// <summary>
    /// High-performance, zero-GC microphone recording system
    /// </summary>
    public class Mic : MonoBehaviour {
        /// <summary>
        /// Circular buffer for efficient audio data management
        /// </summary>
        private class CircularBuffer {
            private float[] buffer;
            private int head;
            private int tail;
            private int count;
            private readonly int capacity;

            public CircularBuffer(int capacity) {
                this.capacity = capacity;
                buffer = new float[capacity];
                head = 0;
                tail = 0;
                count = 0;
            }

            public int Count => count;
            public int Capacity => capacity;

            public void Write(float[] data, int offset, int length) {
                for (int i = 0; i < length; i++) {
                    buffer[tail] = data[offset + i];
                    tail = (tail + 1) % capacity;
                    
                    if (count < capacity) {
                        count++;
                    } else {
                        head = (head + 1) % capacity;
                    }
                }
            }

            public int Read(float[] output, int outputOffset, int length) {
                int actualLength = Math.Min(length, count);
                
                for (int i = 0; i < actualLength; i++) {
                    output[outputOffset + i] = buffer[head];
                    head = (head + 1) % capacity;
                    count--;
                }
                
                return actualLength;
            }

            public void Clear() {
                head = 0;
                tail = 0;
                count = 0;
            }
        }

        /// <summary>
        /// Device data for efficient processing
        /// </summary>
        private class DeviceData {
            public AudioClip clip;
            public int prevPosition;
            public CircularBuffer pcmBuffer;
            public float[] tempBuffer;
            public float[] frameBuffer;
            public int frameLength;
            public bool isActive;

            public DeviceData(int samplingFrequency, int frameDurationMS, int channelCount) {
                frameLength = samplingFrequency * frameDurationMS * channelCount / 1000;
                pcmBuffer = new CircularBuffer(samplingFrequency * 2); // 2 seconds buffer
                tempBuffer = new float[samplingFrequency / 10]; // 100ms temp buffer
                frameBuffer = new float[frameLength];
                prevPosition = 0;
                isActive = false;
            }

            public void UpdateFrameLength(int samplingFrequency, int frameDurationMS, int channelCount) {
                int newFrameLength = samplingFrequency * frameDurationMS * channelCount / 1000;
                if (newFrameLength != frameLength) {
                    frameLength = newFrameLength;
                    if (frameBuffer.Length < frameLength) {
                        frameBuffer = new float[frameLength];
                    }
                }
            }

            public void Clear() {
                if (clip != null) {
                    Destroy(clip);
                    clip = null;
                }
                pcmBuffer?.Clear();
                prevPosition = 0;
                isActive = false;
            }
        }

        /// <summary>
        /// Provides information and APIs for a single recording device.
        /// </summary>
        public class Device {
            public const int DEFAULT_FRAME_DURATION_MS = 20;
            public const int DEFAULT_SAMPLING_FREQUENCY = 48000;

            public event Action OnStartRecording;
            public event Action<int, int, float[]> OnFrameCollected;
            public event Action OnStopRecording;

            public string Name { get; private set; }
            public int MaxFrequency { get; private set; }
            public int MinFrequency { get; private set; }
            public bool SupportsAnyFrequency => MaxFrequency == 0 && MinFrequency == 0;

            private float volumeMultiplier = 1f;
            public float VolumeMultiplier {
                get => volumeMultiplier;
                set => volumeMultiplier = value;
            }

            private int samplingFrequency;
            public int SamplingFrequency {
                get => samplingFrequency;
                private set => samplingFrequency = value;
            }

            private int frameDurationMS;
            public int FrameDurationMS {
                get => frameDurationMS;
                private set {
                    if (value <= 0)
                    {
                        throw new Exception("FrameDurationMS cannot be zero or negative");
                    }
                    frameDurationMS = value;
                }
            }

            public int FrameLength => SamplingFrequency / 1000 * FrameDurationMS * ChannelCount;
            public int ChannelCount => GetChannelCount(this);

            internal Device(string name, int minFrequency, int maxFrequency) {
                Name = name;
                MinFrequency = minFrequency;
                MaxFrequency = maxFrequency;
            }

            public void StartRecording(int frameDurationMS = DEFAULT_FRAME_DURATION_MS) {
                StartRecording(SupportsAnyFrequency ? DEFAULT_SAMPLING_FREQUENCY : MaxFrequency, frameDurationMS);
            }

            public void StartRecording(int samplingFrequency, int frameDurationMS = DEFAULT_FRAME_DURATION_MS) {
                if (SamplingFrequency == samplingFrequency && FrameDurationMS == frameDurationMS && IsRecording)
                    return;

                Mic.StopRecording(this);
                SamplingFrequency = samplingFrequency;
                FrameDurationMS = frameDurationMS;

                Mic.StartRecording(this);
                if (IsRecording)
                    OnStartRecording?.Invoke();
            }

            public void StopRecording() {
                if (!IsRecording) return;

                Mic.StopRecording(this);
                if (!IsRecording)
                    OnStopRecording?.Invoke();
            }

            public bool IsRecording => Mic.IsRecording(this);

            internal void BroadcastFrame(int channelCount, float[] pcm, int length) {
                if (volumeMultiplier != 1f) {
                    for (int i = 0; i < length; i++)
                        pcm[i] *= volumeMultiplier;
                }
                OnFrameCollected?.Invoke(SamplingFrequency, channelCount, pcm);
            }
        }

        // ================================================

        private const string TAG = "Mic";
        private static readonly Dictionary<string, Device> deviceMap = new Dictionary<string, Device>(8);
        private static readonly Dictionary<Device, DeviceData> deviceDataMap = new Dictionary<Device, DeviceData>(8);
        private static readonly List<Device> deviceList = new List<Device>(8);
        private static readonly List<string> tempDeviceNames = new List<string>(8);

        public static List<Device> AvailableDevices {
            get {
                if (instance == null)
                    Init();

                // Get current device names
                tempDeviceNames.Clear();
                var deviceNames = Microphone.devices;
                for (int i = 0; i < deviceNames.Length; i++) {
                    tempDeviceNames.Add(deviceNames[i]);
                }

                // Add new devices
                for (int i = 0; i < tempDeviceNames.Count; i++) {
                    var deviceName = tempDeviceNames[i];
                    if (!deviceMap.ContainsKey(deviceName)) {
                        Microphone.GetDeviceCaps(deviceName, out int minFreq, out int maxFreq);
                        var device = new Device(deviceName, minFreq, maxFreq);
                        deviceMap.Add(deviceName, device);
                    }
                }

                // Remove disconnected devices
                deviceList.Clear();
                foreach (var kvp in deviceMap) {
                    if (!tempDeviceNames.Contains(kvp.Key)) {
                        deviceList.Add(kvp.Value);
                    }
                }

                for (int i = 0; i < deviceList.Count; i++) {
                    var device = deviceList[i];
                    StopRecording(device);
                    deviceMap.Remove(device.Name);
                }

                // Return current devices
                deviceList.Clear();
                foreach (var kvp in deviceMap) {
                    deviceList.Add(kvp.Value);
                }

                return deviceList;
            }
        }

        [Obsolete("Mic is a MonoBehaviour singleton that is created on its own upon usage.", true)]
        public Mic() { }

        private static Mic instance;

        public static void Init() {
            if (instance != null) {
                Debug.unityLogger.Log(LogType.Warning, TAG, "UniMic.Mic is already initialized.");
                return;
            }

            var go = new GameObject("UniMic.Mic");
            go.hideFlags = HideFlags.DontSave;
            DontDestroyOnLoad(go);
            instance = go.AddComponent<Mic>();
            Debug.unityLogger.Log(LogType.Log, TAG, "UniMic.Mic initialized.");
        }

        private static void StartRecording(Device device) {
            StopRecording(device);

            var newClip = Microphone.Start(device.Name, true, 1, device.SamplingFrequency);
            if (newClip != null) {
                var deviceData = new DeviceData(device.SamplingFrequency, device.FrameDurationMS, newClip.channels);
                deviceData.clip = newClip;
                deviceData.isActive = true;
                deviceDataMap[device] = deviceData;
            }
        }

        private static bool IsRecording(Device device) {
            return Microphone.IsRecording(device.Name);
        }

        private static int GetChannelCount(Device device) {
            if (!deviceDataMap.TryGetValue(device, out var deviceData) || deviceData.clip == null)
                return 0;
            return deviceData.clip.channels;
        }

        private static void StopRecording(Device device) {
            if (device == null)
                return;

            Microphone.End(device.Name);
            
            if (deviceDataMap.TryGetValue(device, out var deviceData)) {
                deviceData.Clear();
                deviceDataMap.Remove(device);
            }
        }

        // Pre-allocated variables for Update loop (zero GC)
        private int pos;
        private bool didLoop;
        private DeviceData deviceData;
        private AudioClip clip;
        private int prevPos;
        private CircularBuffer pcmBuffer;
        private int frameLen;
        private int samplesToRead;
        private int samplesRead;
        private float[] tempBuffer;
        private float[] frameBuffer;

        private void Update() {
            // Process each active device
            foreach (var kvp in deviceDataMap) {
                var device = kvp.Key;
                deviceData = kvp.Value;

                if (!deviceData.isActive || deviceData.clip == null || !device.IsRecording)
                    continue;

                clip = deviceData.clip;
                prevPos = deviceData.prevPosition;
                pcmBuffer = deviceData.pcmBuffer;
                tempBuffer = deviceData.tempBuffer;

                // Get current microphone position
                pos = Microphone.GetPosition(device.Name);
                if (pos == prevPos)
                    continue;

                // Handle microphone position wrap-around
                didLoop = pos < prevPos;
                
                if (!didLoop) {
                    // Normal case: read from prevPos to pos
                    samplesToRead = pos - prevPos;
                    if (samplesToRead > tempBuffer.Length) {
                        // Resize temp buffer if needed (rare case)
                        tempBuffer = new float[samplesToRead];
                        deviceData.tempBuffer = tempBuffer;
                    }
                    
                    clip.GetData(tempBuffer, prevPos);
                    pcmBuffer.Write(tempBuffer, 0, samplesToRead);
                } else {
                    // Wrap-around case: read from prevPos to end, then from 0 to pos
                    int samplesFromPrevToEnd = clip.samples - prevPos;
                    int samplesFromStartToPos = pos;
                    
                    // Read from prevPos to end
                    if (samplesFromPrevToEnd > 0) {
                        if (samplesFromPrevToEnd > tempBuffer.Length) {
                            tempBuffer = new float[Math.Max(samplesFromPrevToEnd, samplesFromStartToPos)];
                            deviceData.tempBuffer = tempBuffer;
                        }
                        clip.GetData(tempBuffer, prevPos);
                        pcmBuffer.Write(tempBuffer, 0, samplesFromPrevToEnd);
                    }
                    
                    // Read from start to pos
                    if (samplesFromStartToPos > 0) {
                        if (samplesFromStartToPos > tempBuffer.Length) {
                            tempBuffer = new float[samplesFromStartToPos];
                            deviceData.tempBuffer = tempBuffer;
                        }
                        clip.GetData(tempBuffer, 0);
                        pcmBuffer.Write(tempBuffer, 0, samplesFromStartToPos);
                    }
                }

                // Update frame length if needed
                deviceData.UpdateFrameLength(device.SamplingFrequency, device.FrameDurationMS, clip.channels);
                frameLen = deviceData.frameLength;
                frameBuffer = deviceData.frameBuffer;

                // Extract complete frames
                while (pcmBuffer.Count >= frameLen) {
                    samplesRead = pcmBuffer.Read(frameBuffer, 0, frameLen);
                    if (samplesRead == frameLen) {
                        device.BroadcastFrame(clip.channels, frameBuffer, frameLen);
                    }
                }

                // Update previous position
                deviceData.prevPosition = pos;
            }
        }

        private void OnDestroy() {
            // Clean up all devices
            foreach (var kvp in deviceDataMap) {
                kvp.Value.Clear();
            }
            deviceDataMap.Clear();
        }
    }
}