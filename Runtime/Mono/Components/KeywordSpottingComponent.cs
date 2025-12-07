// Runtime: Packages/com.eitan.sherpa-onnx-unity/Runtime/Mono/Components/KeywordSpottingComponent.cs

namespace Eitan.Sherpa.Onnx.Unity.Mono.Components
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Eitan.SherpaONNXUnity.Runtime;
    using Eitan.SherpaONNXUnity.Runtime.Modules;
    using UnityEngine;
    using UnityEngine.Events;

    /// <summary>
    /// Streams audio from an input source into the sherpa-onnx keyword spotter.
    /// </summary>
    [AddComponentMenu("SherpaONNX/Keyword Spotting/Keyword Spotter")]
    [DisallowMultipleComponent]
    public sealed class KeywordSpottingComponent : SherpaAudioStreamingComponent<KeywordSpotting>
    {
        [Header("Keyword Settings")]
        [SerializeField]
        [Tooltip("Score boost applied to registered keywords.")]
        private float keywordsScore = 2.0f;

        [SerializeField]
        [Tooltip("Detection threshold applied to keyword hypotheses.")]
        [Range(0f, 1f)]
        private float keywordsThreshold = 0.25f;

        [SerializeField]
        [Tooltip("Optional custom keywords to register at initialization time.")]
        private List<KeywordSpotting.KeywordRegistration> customKeywords = new List<KeywordSpotting.KeywordRegistration>();

        [Header("Performance")]
        [SerializeField]
        [Tooltip("Maximum samples to queue before dropping audio to keep latency bounded.")]
        private int maxQueuedSamples = 16000;

        [SerializeField]
        [Tooltip("Drop incoming chunks when the detector is behind to avoid runaway latency.")]
        private bool dropIfLagging = true;

        [SerializeField]
        [Tooltip("Start module initialization immediately on construction. Disable to configure first, then call StartModuleInitialization manually.")]
        private bool startModuleImmediately = true;

        [Header("Events")]
        [SerializeField]
        private UnityEvent<string> onKeywordDetected = new UnityEvent<string>();

        /// <summary>
        /// Gives runtime access to keyword detection events for quick UI wiring.
        /// </summary>
        public UnityEvent<string> KeywordDetectedEvent => onKeywordDetected;

        private bool warnedSampleRateMismatch;

        protected override void OnEnable()
        {
            base.OnEnable();
            warnedSampleRateMismatch = false;
        }

        protected override KeywordSpotting CreateModule(string resolvedModelId, int resolvedSampleRate, SherpaONNXFeedbackReporter resolvedReporter)
        {
            var payload = customKeywords != null && customKeywords.Count > 0
                ? customKeywords.ToArray()
                : null;

            var module = new KeywordSpotting(resolvedModelId, resolvedSampleRate, keywordsScore, keywordsThreshold, payload, resolvedReporter, maxQueuedSamples, dropIfLagging, startModuleImmediately);
            module.OnKeywordDetected += HandleKeywordDetected;
            return module;
        }

        protected override void OnDestroy()
        {
            if (Module != null)
            {
                Module.OnKeywordDetected -= HandleKeywordDetected;
            }
            base.OnDestroy();
        }

        protected override void OnAudioChunkReceived(float[] samples, int sampleRate)
        {
            if (Module != null && !Module.InitializationStarted && !startModuleImmediately)
            {
                _ = Module.StartInitialization();
            }

            if (!EnsureModuleReady(out var module))
            {
                return;
            }

            if (sampleRate != SampleRate)
            {
                if (!warnedSampleRateMismatch)
                {
                    warnedSampleRateMismatch = true;
                    SherpaLog.Warning($"[KeywordSpottingComponent] Sample rate mismatch. Input={sampleRate}Hz Component={SampleRate}Hz.");
                    RaiseError($"Keyword spotter input sample rate {sampleRate}Hz does not match configured {SampleRate}Hz.");
                }
            }

            try
            {
                module.StreamDetect(samples);
            }
            catch (Exception ex)
            {
                SherpaLog.Error($"[KeywordSpottingComponent] StreamDetect failed: {ex.Message}");
                RaiseError(ex.Message);
            }
        }

        public void FeedSamples(float[] samples, int sampleRate)
        {
            if (!CanProcessChunk(samples, sampleRate))
            {
                return;
            }

            OnAudioChunkReceived(samples, sampleRate);
        }

        /// <summary>
        /// Starts module initialization when startModuleImmediately is disabled.
        /// </summary>
        public Task StartModuleInitializationAsync(CancellationToken cancellationToken = default)
        {
            if (Module == null && !TryLoadModule())
            {
                return Task.CompletedTask;
            }

            return Module?.StartInitialization(cancellationToken) ?? Task.CompletedTask;
        }

        private void HandleKeywordDetected(string keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword))
            {
                return;
            }

            onKeywordDetected?.Invoke(keyword);
        }
    }
}
