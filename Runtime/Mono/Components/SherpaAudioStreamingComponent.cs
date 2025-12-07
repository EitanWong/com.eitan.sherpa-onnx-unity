// Runtime helper: Packages/com.eitan.sherpa-onnx-unity/Runtime/Mono/Components/SherpaAudioStreamingComponent.cs

namespace Eitan.Sherpa.Onnx.Unity.Mono.Components
{
    using System;
    using System.Threading.Tasks;
    using Eitan.Sherpa.Onnx.Unity.Mono.Inputs;
    using Eitan.SherpaONNXUnity.Runtime;
    using UnityEngine;

    /// <summary>
    /// Base MonoBehaviour for sherpa modules that continuously consume audio from a <see cref="SherpaAudioInputSource"/>.
    /// Handles binding/unbinding sources, automatically managing capture state, and guarding chunk delivery until the
    /// underlying module finished initializing.
    /// </summary>
    /// <typeparam name="TModule">Concrete sherpa module type.</typeparam>
    public abstract class SherpaAudioStreamingComponent<TModule> : SherpaModuleComponent<TModule>
        where TModule : SherpaONNXModule
    {
        [Header("Audio Input")]
        [SerializeField]
        [Tooltip("Audio source that produces PCM chunks (e.g., SherpaMicrophoneInput).")]
        private SherpaAudioInputSource audioInput;

        [SerializeField]
        [Tooltip("Bind the referenced audio input automatically on enable.")]
        private bool autoBindInput = true;

        [SerializeField]
        [Tooltip("Start or stop the audio capture automatically based on module readiness.")]
        private bool startCaptureWhenReady = true;

        [SerializeField]
        [Tooltip("Process incoming chunks on a background worker instead of the main thread.")]
        private bool processChunksInBackground;

        [SerializeField]
        [Tooltip("Log when chunks are dropped because the module is not initialized yet.")]
        private bool warnOnDroppedWhileUnready = true;

        private SherpaAudioInputSource boundInput;
        private bool captureStarted;
        private int droppedWhileUnready;
        private float lastDropLogTime;

        /// <summary>
        /// Gets the currently bound input (null when unbound).
        /// </summary>
        protected SherpaAudioInputSource BoundInput => boundInput;

        /// <summary>
        /// Override to false when a derived type wants to process audio even before the module finished loading.
        /// </summary>
        protected virtual bool RequiresReadyModuleForCapture => true;

        /// <summary>
        /// Called by Unity when the component becomes enabled.
        /// </summary>
        protected virtual void OnEnable()
        {
            if (Application.isPlaying && autoBindInput)
            {
                BindInput(audioInput);
            }

            droppedWhileUnready = 0;
            lastDropLogTime = 0f;
        }

        /// <summary>
        /// Called by Unity when the component becomes disabled.
        /// </summary>
        protected virtual void OnDisable()
        {
            UnbindInput(boundInput);
        }

        /// <summary>
        /// Binds a new audio input source at runtime.
        /// </summary>
        public void BindInput(SherpaAudioInputSource source)
        {
            audioInput = source;
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
            OnInputBound(source);

            if (ShouldStartCaptureImmediately())
            {
                StartCapture();
            }
        }

        /// <summary>
        /// Unbinds a previously registered audio input source.
        /// </summary>
        public void UnbindInput(SherpaAudioInputSource source)
        {
            if (source == null)
            {
                return;
            }

            source.ChunkReady -= HandleChunkFromInput;

            if (captureStarted && source == boundInput)
            {
                source.StopCapture();
                captureStarted = false;
                OnCaptureStateChanged(false);
            }

            if (boundInput == source)
            {
                OnInputUnbound(source);
                boundInput = null;
            }
        }

        /// <summary>
        /// Determines whether the current chunk can be processed by the module.
        /// Exposed so manual feeds can reuse the same validation logic as automatic input feeds.
        /// </summary>
        protected bool CanProcessChunk(float[] samples, int sampleRate)
        {
            if (samples == null || samples.Length == 0 || sampleRate <= 0)
            {
                return false;
            }

            if (RequiresReadyModuleForCapture && !IsInitialized)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Invoked when audio capture starts or stops because of automatic management.
        /// </summary>
        protected virtual void OnCaptureStateChanged(bool capturing)
        {
        }

        /// <summary>
        /// Invoked whenever <see cref="BindInput"/> successfully attaches the input.
        /// </summary>
        protected virtual void OnInputBound(SherpaAudioInputSource source)
        {
        }

        /// <summary>
        /// Invoked whenever the current input is detached.
        /// </summary>
        protected virtual void OnInputUnbound(SherpaAudioInputSource source)
        {
        }

        /// <summary>
        /// Called for every chunk emitted by the audio input after validation passes.
        /// </summary>
        protected abstract void OnAudioChunkReceived(float[] samples, int sampleRate);

        protected override void OnModuleInitializationStateChanged(bool ready)
        {
            base.OnModuleInitializationStateChanged(ready);

            if (!startCaptureWhenReady || !Application.isPlaying || boundInput == null)
            {
                return;
            }

            if (ready)
            {
                StartCapture();
            }
            else
            {
                StopCapture();
            }
        }

        private bool ShouldStartCaptureImmediately()
        {
            if (!Application.isPlaying || boundInput == null || !startCaptureWhenReady)
            {
                return false;
            }

            return !RequiresReadyModuleForCapture || IsInitialized;
        }

        private void StartCapture()
        {
            if (boundInput == null)
            {
                return;
            }

            if (!boundInput.IsCapturing)
            {
                boundInput.TryStartCapture();
            }

            if (boundInput.IsCapturing)
            {
                if (!captureStarted)
                {
                    captureStarted = true;
                    OnCaptureStateChanged(true);
                }
            }
        }

        private void StopCapture()
        {
            if (boundInput == null)
            {
                captureStarted = false;
                return;
            }

            boundInput.StopCapture();
            if (captureStarted)
            {
                captureStarted = false;
                OnCaptureStateChanged(false);
            }
        }

        private void HandleChunkFromInput(float[] samples, int sampleRate)
        {
            if (!CanProcessChunk(samples, sampleRate))
            {
                MaybeLogDroppedChunk();
                return;
            }

            if (processChunksInBackground)
            {
                var clone = new float[samples.Length];
                Array.Copy(samples, clone, samples.Length);
                _ = Task.Run(() =>
                {
                    try
                    {
                        OnAudioChunkReceived(clone, sampleRate);
                    }
                    catch (Exception ex)
                    {
                        SherpaLog.Error($"[{GetType().Name}] Failed to process audio chunk on background thread: {ex.Message}");
                    }
                });
                return;
            }

            OnAudioChunkReceived(samples, sampleRate);
        }

        private void MaybeLogDroppedChunk()
        {
            if (!warnOnDroppedWhileUnready || !RequiresReadyModuleForCapture)
            {
                return;
            }

            if (IsInitialized)
            {
                return;
            }

            droppedWhileUnready++;
            var now = Time.realtimeSinceStartup;
            if (now - lastDropLogTime < 1f)
            {
                return;
            }

            lastDropLogTime = now;
            SherpaLog.Warning($"[{GetType().Name}] Dropped {droppedWhileUnready} chunks while waiting for module initialization. Consider enabling startCaptureWhenReady or increasing initialization speed.");
            droppedWhileUnready = 0;
        }
    }
}
