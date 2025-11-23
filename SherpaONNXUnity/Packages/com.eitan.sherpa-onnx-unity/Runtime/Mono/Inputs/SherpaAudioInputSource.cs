// Runtime helper: Packages/com.eitan.sherpa-onnx-unity/Runtime/Mono/Inputs/SherpaAudioInputSource.cs

namespace Eitan.Sherpa.Onnx.Unity.Mono.Inputs
{
    using System;
    using UnityEngine;

    /// <summary>
    /// Base abstraction for components that expose a stream of audio chunks.
    /// Derived classes decide how audio is captured (microphone, clip playback, etc.).
    /// </summary>
    public abstract class SherpaAudioInputSource : MonoBehaviour
    {
        /// <summary>
        /// Raised whenever a new chunk of PCM samples (mono) is available.
        /// The int argument specifies the sample rate of the chunk.
        /// </summary>
        public abstract event Action<float[], int> ChunkReady;

        /// <summary>
        /// The sample rate expected for emitted chunks.
        /// </summary>
        public abstract int OutputSampleRate { get; }

        /// <summary>
        /// Indicates whether the source is actively capturing audio.
        /// </summary>
        public abstract bool IsCapturing { get; }

        /// <summary>
        /// Attempts to start audio capture/streaming.
        /// </summary>
        public abstract bool TryStartCapture();

        /// <summary>
        /// Stops the audio capture/streaming session.
        /// </summary>
        public abstract void StopCapture();
    }
}
