// Runtime: Packages/com.eitan.sherpa-onnx-unity/Runtime/Mono/Components/KeywordSpottingComponent.cs

namespace Eitan.Sherpa.Onnx.Unity.Mono.Components
{
    using System;
    using System.Collections.Generic;
    using Eitan.Sherpa.Onnx.Unity.Mono.Inputs;
    using Eitan.SherpaOnnxUnity.Runtime;
    using Eitan.SherpaOnnxUnity.Runtime.Core.Modules;
    using UnityEngine;
    using UnityEngine.Events;

    /// <summary>
    /// Streams audio from an input source into the sherpa-onnx keyword spotter.
    /// </summary>
    [AddComponentMenu("Sherpa ONNX/Keyword Spotting/Keyword Spotter")]
    [DisallowMultipleComponent]
    public sealed class KeywordSpottingComponent : SherpaModuleComponent<KeywordSpotting>
    {
        [Header("Audio Input")]
        [SerializeField]
        private SherpaAudioInputSource audioInput;

        [SerializeField]
        private bool autoBindInput = true;

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

        private SherpaAudioInputSource boundInput;

        protected override KeywordSpotting CreateModule(string resolvedModelId, int resolvedSampleRate, SherpaOnnxFeedbackReporter resolvedReporter)
        {
            var payload = customKeywords != null && customKeywords.Count > 0
                ? customKeywords.ToArray()
                : null;

            var module = new KeywordSpotting(resolvedModelId, resolvedSampleRate, keywordsScore, keywordsThreshold, payload, resolvedReporter);
            module.OnKeywordDetected += HandleKeywordDetected;
            return module;
        }

        private void OnEnable()
        {
            if (Application.isPlaying && autoBindInput)
            {
                BindInput(audioInput);
            }
        }

        private void OnDisable()
        {
            UnbindInput(boundInput);
        }

        protected override void OnDestroy()
        {
            UnbindInput(boundInput);
            if (Module != null)
            {
                Module.OnKeywordDetected -= HandleKeywordDetected;
            }
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

        public void FeedSamples(float[] samples, int sampleRate)
        {
            HandleChunkFromInput(samples, sampleRate);
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

            if (sampleRate != SampleRate)
            {
                Debug.LogWarning($"[KeywordSpottingComponent] Sample rate mismatch. Input={sampleRate}Hz Component={SampleRate}Hz.");
            }

            module.StreamDetect(samples);
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
