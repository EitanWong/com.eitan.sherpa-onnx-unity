// Runtime: Packages/com.eitan.sherpa-onnx-unity/Runtime/Mono/Components/KeywordSpottingComponent.cs

namespace Eitan.Sherpa.Onnx.Unity.Mono.Components
{
    using System;
    using System.Collections.Generic;
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

        [Header("Events")]
        [SerializeField]
        private UnityEvent<string> onKeywordDetected = new UnityEvent<string>();

        /// <summary>
        /// Gives runtime access to keyword detection events for quick UI wiring.
        /// </summary>
        public UnityEvent<string> KeywordDetectedEvent => onKeywordDetected;

        protected override KeywordSpotting CreateModule(string resolvedModelId, int resolvedSampleRate, SherpaONNXFeedbackReporter resolvedReporter)
        {
            var payload = customKeywords != null && customKeywords.Count > 0
                ? customKeywords.ToArray()
                : null;

            var module = new KeywordSpotting(resolvedModelId, resolvedSampleRate, keywordsScore, keywordsThreshold, payload, resolvedReporter);
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
            if (!EnsureModuleReady(out var module))
            {
                return;
            }

            if (sampleRate != SampleRate)
            {
                Debug.LogWarning($"[KeywordSpottingComponent] Sample rate mismatch. Input={sampleRate}Hz Component={SampleRate}Hz.");
            }

            module.StreamDetect(samples);
        }

        public void FeedSamples(float[] samples, int sampleRate)
        {
            if (!CanProcessChunk(samples, sampleRate))
            {
                return;
            }

            OnAudioChunkReceived(samples, sampleRate);
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
