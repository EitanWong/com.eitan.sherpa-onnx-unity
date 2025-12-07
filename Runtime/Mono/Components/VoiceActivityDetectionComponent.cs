// Runtime: Packages/com.eitan.sherpa-onnx-unity/Runtime/Mono/Components/VoiceActivityDetectionComponent.cs

namespace Eitan.Sherpa.Onnx.Unity.Mono.Components
{
    using System;
    using System.Threading.Tasks;
    using Eitan.Sherpa.Onnx.Unity.Mono.Inputs;
    using Eitan.SherpaONNXUnity.Runtime;
    using Eitan.SherpaONNXUnity.Runtime.Modules;

    using UnityEngine;
    using UnityEngine.Events;

    /// <summary>
    /// Wraps <see cref="VoiceActivityDetection"/> and forwards audio from an input source.
    /// Emits detected speech segments that can be fed into offline recognizers or custom logic.
    /// </summary>
    [AddComponentMenu("SherpaONNX/Voice Processing/Voice Activity Detector")]
    [DisallowMultipleComponent]
    public sealed class VoiceActivityDetectionComponent : SherpaAudioStreamingComponent<VoiceActivityDetection>
    {
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

        [Header("Streaming")]
        [SerializeField]
        [Tooltip("Maximum buffered audio (seconds) before oldest samples are dropped.")]
        [Min(1)]
        private int maxBufferedSeconds = 5;

        [SerializeField]
        [Tooltip("Drop incoming audio when the detector is overloaded to avoid unbounded growth.")]
        private bool dropIfLagging = true;

        [Header("Events")]
        [SerializeField]
        private SpeechSegmentUnityEvent onSpeechSegment = new SpeechSegmentUnityEvent();

        [SerializeField]
        private UnityEvent<bool> onSpeakingStateChanged = new UnityEvent<bool>();

        public event Action<float[], int> SpeechSegmentReady;
        public event Action<bool> SpeakingStateChanged;

        private bool warnedSampleRateMismatch;

        [Serializable]
        public sealed class SpeechSegmentUnityEvent : UnityEvent<float[], int>
        {
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            warnedSampleRateMismatch = false;
        }

        protected override VoiceActivityDetection CreateModule(string resolvedModelId, int resolvedSampleRate, SherpaONNXFeedbackReporter resolvedReporter)
        {
            var module = new VoiceActivityDetection(resolvedModelId, resolvedSampleRate, resolvedReporter, maxBufferedSeconds, dropIfLagging)
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
            DetachModuleCallbacks();
            base.OnDestroy();
        }

        public void FeedSamples(float[] samples)
        {
            if (!CanProcessChunk(samples, SampleRate))
            {
                return;
            }

            OnAudioChunkReceived(samples, SampleRate);
        }

        public Task FlushAsync()
        {
            if (!EnsureModuleReady(out var module))
            {
                return Task.CompletedTask;
            }

            return module.FlushAsync();
        }

        protected override void OnAudioChunkReceived(float[] samples, int sampleRate)
        {
            if (!EnsureModuleReady(out var module))
            {
                return;
            }

            if (sampleRate != SampleRate && !warnedSampleRateMismatch)
            {
                warnedSampleRateMismatch = true;
                SherpaLog.Warning($"[VoiceActivityDetectionComponent] Sample rate mismatch. Input={sampleRate}Hz Component={SampleRate}Hz. Consider aligning values to avoid drift.");
            }

            try
            {
                module.StreamDetect(samples);
            }
            catch (Exception ex)
            {
                SherpaLog.Error($"[VoiceActivityDetectionComponent] StreamDetect failed: {ex.Message}");
                RaiseError(ex.Message);
            }
        }

        protected override void OnInputBound(SherpaAudioInputSource source)
        {
            warnedSampleRateMismatch = false;
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
