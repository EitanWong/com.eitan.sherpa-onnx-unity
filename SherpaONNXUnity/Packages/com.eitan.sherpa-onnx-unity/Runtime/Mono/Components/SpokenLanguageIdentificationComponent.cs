// Runtime: Packages/com.eitan.sherpa-onnx-unity/Runtime/Mono/Components/SpokenLanguageIdentificationComponent.cs

namespace Eitan.Sherpa.Onnx.Unity.Mono.Components
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Eitan.SherpaONNXUnity.Runtime;
    using Eitan.SherpaONNXUnity.Runtime.Modules;

    using UnityEngine;
    using UnityEngine.Events;

    /// <summary>
    /// High-level wrapper over <see cref="SpokenLanguageIdentification"/> for AudioClips or raw samples.
    /// </summary>
    [AddComponentMenu("SherpaONNX/Language/Spoken Language Identification")]
    [DisallowMultipleComponent]
    public sealed class SpokenLanguageIdentificationComponent : SherpaModuleComponent<SpokenLanguageIdentification>
    {
        [Header("Clip Input")]
        [SerializeField]
        private AudioClip clip;

        [SerializeField]
        [Tooltip("When enabled, the assigned AudioClip is identified automatically on Start.")]
        private bool identifyOnStart;

        [Header("Events")]
        [SerializeField]
        private UnityEvent<string> onLanguageIdentified = new UnityEvent<string>();

        [SerializeField]
        private UnityEvent<string> onIdentificationFailed = new UnityEvent<string>();

        /// <summary>
        /// Event invoked once the component identifies a language.
        /// </summary>
        public UnityEvent<string> LanguageIdentifiedEvent => onLanguageIdentified;

        /// <summary>
        /// Event invoked whenever identification fails.
        /// </summary>
        public UnityEvent<string> IdentificationFailedEvent => onIdentificationFailed;

        protected override SpokenLanguageIdentification CreateModule(string resolvedModelId, int resolvedSampleRate, SherpaONNXFeedbackReporter resolvedReporter)
        {
            return new SpokenLanguageIdentification(resolvedModelId, resolvedSampleRate, resolvedReporter);
        }

        private void Start()
        {
            if (identifyOnStart && clip != null)
            {
                _ = IdentifyClipAsync(clip);
            }
        }

        public void IdentifyAssignedClip()
        {
            if (clip != null)
            {
                _ = IdentifyClipAsync(clip);
            }
        }

        public async Task<string> IdentifyClipAsync(AudioClip audioClip, CancellationToken cancellationToken = default)
        {
            if (audioClip == null)
            {
                throw new ArgumentNullException(nameof(audioClip));
            }

            if (!EnsureModuleReady(out var module))
            {
                return string.Empty;
            }

            try
            {
                var result = await module.IdentifyAsync(audioClip, cancellationToken).ConfigureAwait(true);
                if (!string.IsNullOrWhiteSpace(result))
                {
                    onLanguageIdentified?.Invoke(result);
                    return result;
                }

                return string.Empty;
            }
            catch (OperationCanceledException)
            {
                return string.Empty;
            }
            catch (Exception ex)
            {
                SherpaLog.Error($"[SpokenLanguageIdentificationComponent] Identification failed: {ex.Message}");
                onIdentificationFailed?.Invoke(ex.Message);
                RaiseError(ex.Message);
                return string.Empty;
            }
        }

        public async Task<string> IdentifySamplesAsync(float[] samples, int sampleRate, CancellationToken cancellationToken = default)
        {
            if (samples == null || samples.Length == 0)
            {
                return string.Empty;
            }

            if (!EnsureModuleReady(out var module))
            {
                return string.Empty;
            }

            try
            {
                var clone = new float[samples.Length];
                Array.Copy(samples, clone, samples.Length);
                var result = await module.IdentifyAsync(clone, sampleRate, cancellationToken).ConfigureAwait(true);
                if (!string.IsNullOrWhiteSpace(result))
                {
                    onLanguageIdentified?.Invoke(result);
                    return result;
                }

                return string.Empty;
            }
            catch (OperationCanceledException)
            {
                return string.Empty;
            }
            catch (Exception ex)
            {
                SherpaLog.Error($"[SpokenLanguageIdentificationComponent] Identification failed: {ex.Message}");
                onIdentificationFailed?.Invoke(ex.Message);
                return string.Empty;
            }
        }
    }
}
