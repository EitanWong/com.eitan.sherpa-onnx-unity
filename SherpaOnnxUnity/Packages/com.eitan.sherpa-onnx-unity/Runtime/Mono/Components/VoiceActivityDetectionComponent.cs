// Runtime: Packages/com.eitan.sherpa-onnx-unity/Runtime/Mono/Components/VoiceActivityDetectionComponent.cs

namespace Eitan.Sherpa.Onnx.Unity.Mono.Components
{
    using System;
    using System.Threading.Tasks;
    using Eitan.Sherpa.Onnx.Unity.Mono.Inputs;
    using Eitan.SherpaOnnxUnity.Runtime;
    using Eitan.SherpaOnnxUnity.Runtime.Core;
    using UnityEngine;
    using UnityEngine.Events;

    /// <summary>
    /// Wraps <see cref="VoiceActivityDetection"/> and forwards audio from an input source.
    /// Emits detected speech segments that can be fed into offline recognizers or custom logic.
    /// </summary>
    [AddComponentMenu("Sherpa ONNX/Voice Processing/Voice Activity Detector")]
    [DisallowMultipleComponent]
    public sealed class VoiceActivityDetectionComponent : SherpaModuleComponent<VoiceActivityDetection>
    {
        [Header("Audio Input")]
        [SerializeField]
        [Tooltip("PCM source that will stream data into the detector (e.g., SherpaMicrophoneInput).")]
        private SherpaAudioInputSource audioInput;

        [SerializeField]
        [Tooltip("Automatically subscribe to the assigned audio input when enabled.")]
        private bool autoBindInput = true;

        [Header("Detector Settings")]
        [SerializeField]
        [Tooltip("Probability threshold applied to the VAD output.")]
        [Range(0f, 1f)]
        private float threshold = 0.5f;

        [SerializeField]
        [Tooltip("Minimum silence duration (seconds) before a segment is closed.")]
        [Min(0f)]
        private float minSilenceDuration = 0.3f;

        [SerializeField]
        [Tooltip("Minimum duration (seconds) before a detected segment is emitted.")]
        [Min(0f)]
        private float minSpeechDuration = 0.1f;

        [SerializeField]
        [Tooltip("Maximum duration (seconds) before a running segment is forced to close.")]
        [Min(0.5f)]
        private float maxSpeechDuration = 30f;

        [SerializeField]
        [Tooltip("Amount of leading audio (seconds) to keep before speech onset.")]
        [Min(0f)]
        private float leadingPaddingDuration = 0.2f;

        [Header("Events")]
        [SerializeField]
        private SpeechSegmentUnityEvent onSpeechSegment = new SpeechSegmentUnityEvent();

        [SerializeField]
        private UnityEvent<bool> onSpeakingStateChanged = new UnityEvent<bool>();

        public event Action<float[], int> SpeechSegmentReady;
        public event Action<bool> SpeakingStateChanged;

        private SherpaAudioInputSource boundInput;
        private bool warnedSampleRateMismatch;

        [Serializable]
        public sealed class SpeechSegmentUnityEvent : UnityEvent<float[], int>
        {
        }

        private void OnEnable()
        {
            if (Application.isPlaying && autoBindInput)
            {
                BindInput(audioInput);
            }
            warnedSampleRateMismatch = false;
        }

        private void OnDisable()
        {
            UnbindInput(boundInput);
        }

        protected override VoiceActivityDetection CreateModule(string resolvedModelId, int resolvedSampleRate, SherpaOnnxFeedbackReporter resolvedReporter)
        {
            var module = new VoiceActivityDetection(resolvedModelId, resolvedSampleRate, resolvedReporter)
            {
                Threshold = threshold,
                MinSilenceDuration = minSilenceDuration,
                MinSpeechDuration = minSpeechDuration,
                MaxSpeechDuration = maxSpeechDuration,
                LeadingPaddingDuration = leadingPaddingDuration
            };

            module.OnSpeechSegmentDetected += HandleSegmentDetected;
            module.OnSpeakingStateChanged += HandleSpeakingStateChanged;
            return module;
        }

        protected override void OnDestroy()
        {
            UnbindInput(boundInput);
            DetachModuleCallbacks();
            base.OnDestroy();
        }

        public void BindInput(SherpaAudioInputSource source)
        {
            if (boundInput == source)
            {
                return;
            }

            UnbindInput(boundInput);
            if (source == null)
            {
                return;
            }

            source.ChunkReady += HandleChunkFromInput;
            boundInput = source;
            warnedSampleRateMismatch = false;

            if (Application.isPlaying && !source.IsCapturing)
            {
                source.TryStartCapture();
            }
        }

        public void UnbindInput(SherpaAudioInputSource source)
        {
            if (source == null)
            {
                return;
            }

            source.ChunkReady -= HandleChunkFromInput;

            if (boundInput == source)
            {
                boundInput = null;
            }
        }

        public void FeedSamples(float[] samples)
        {
            HandleChunkFromInput(samples, SampleRate);
        }

        public Task FlushAsync()
        {
            if (!EnsureModuleReady(out var module))
            {
                return Task.CompletedTask;
            }

            return module.FlushAsync();
        }

        private void HandleChunkFromInput(float[] samples, int sampleRate)
        {
            if (samples == null || samples.Length == 0)
            {
                return;
            }

            if (!EnsureModuleReady(out var module))
            {
                return;
            }

            if (sampleRate != SampleRate && !warnedSampleRateMismatch)
            {
                warnedSampleRateMismatch = true;
                Debug.LogWarning($"[VoiceActivityDetectionComponent] Sample rate mismatch. Input={sampleRate}Hz Component={SampleRate}Hz. Consider aligning values to avoid drift.");
            }

            module.StreamDetect(samples);
        }

        private void HandleSegmentDetected(float[] samples)
        {
            if (samples == null || samples.Length == 0)
            {
                return;
            }

            var clone = new float[samples.Length];
            Array.Copy(samples, clone, samples.Length);

            onSpeechSegment?.Invoke(clone, SampleRate);
            SpeechSegmentReady?.Invoke(clone, SampleRate);
        }

        private void HandleSpeakingStateChanged(bool speaking)
        {
            onSpeakingStateChanged?.Invoke(speaking);
            SpeakingStateChanged?.Invoke(speaking);
        }

        private void DetachModuleCallbacks()
        {
            if (Module == null)
            {
                return;
            }

            Module.OnSpeechSegmentDetected -= HandleSegmentDetected;
            Module.OnSpeakingStateChanged -= HandleSpeakingStateChanged;
        }
    }
}
