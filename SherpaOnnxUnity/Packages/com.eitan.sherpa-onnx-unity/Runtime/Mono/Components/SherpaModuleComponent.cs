// Runtime: Packages/com.eitan.sherpa-onnx-unity/Runtime/Mono/Components/SherpaModuleComponent.cs

namespace Eitan.Sherpa.Onnx.Unity.Mono.Components
{
    using System;
    using Eitan.SherpaOnnxUnity.Runtime;
    using UnityEngine;
    using UnityEngine.Events;

    /// <summary>
    /// Shared infrastructure for MonoBehaviours that host a sherpa-onnx module.
    /// Handles lifecycle, feedback routing, and basic validation.
    /// </summary>
    /// <typeparam name="TModule">Concrete sherpa module type.</typeparam>
    public abstract class SherpaModuleComponent<TModule> : MonoBehaviour, ISherpaFeedbackHandler
        where TModule : SherpaOnnxModule
    {
        [Header("Model")]
        [SerializeField]
        [Tooltip("Model identifier registered in SherpaOnnxModelRegistry.")]
        private string modelId = string.Empty;

        [SerializeField]
        [Tooltip("Sample rate forwarded to the underlying module.")]
        private int sampleRate = 16000;

        [SerializeField]
        [Tooltip("Automatically instantiate the module during Awake when the scene starts.")]
        private bool loadOnAwake = true;

        [SerializeField]
        [Tooltip("Dispose the module when this component is destroyed.")]
        private bool disposeOnDestroy = true;

        [SerializeField]
        [Tooltip("Echo feedback messages to the Unity console for easier debugging.")]
        private bool logFeedbackToConsole = true;

        [Header("Events")]
        [SerializeField]
        private UnityEvent<bool> onInitializationStateChanged = new UnityEvent<bool>();

        [SerializeField]
        private FeedbackMessageEvent onFeedbackMessage = new FeedbackMessageEvent();

        /// <summary>
        /// UnityEvent wrapper that exposes textual feedback.
        /// </summary>
        [Serializable]
        public sealed class FeedbackMessageEvent : UnityEvent<string>
        {
        }

        private TModule module;
        private SherpaOnnxFeedbackReporter reporter;
        private bool isReady;

        /// <summary>
        /// Gets the instantiated module or null when not loaded.
        /// </summary>
        protected TModule Module => module;

        /// <summary>
        /// Gets the reporter used to receive load/prepare feedback.
        /// </summary>
        protected SherpaOnnxFeedbackReporter Reporter => reporter;

        /// <summary>
        /// Gets or sets the model identifier.
        /// </summary>
        public string ModelId
        {
            get => modelId;
            set => modelId = value;
        }

        /// <summary>
        /// Gets the requested sample rate used during module creation.
        /// </summary>
        protected int SampleRate => sampleRate;

        /// <summary>
        /// Allows derived classes to override the serialized sample rate value (e.g., to set -1 for TTS).
        /// </summary>
        protected void SetSampleRateForInspector(int newValue)
        {
            sampleRate = newValue;
        }

        /// <summary>
        /// Indicates whether the module reports being initialized successfully.
        /// </summary>
        public bool IsInitialized => module != null && module.Initialized;

        protected virtual void Awake()
        {
            if (Application.isPlaying && loadOnAwake)
            {
                TryLoadModule();
            }
        }

        protected virtual void OnDestroy()
        {
            if (disposeOnDestroy)
            {
                DisposeModule();
            }
        }

        /// <summary>
        /// Ensures the module exists and is ready to process work.
        /// </summary>
        protected bool EnsureModuleReady(out TModule loadedModule)
        {
            loadedModule = module;
            if (module == null)
            {
                Debug.LogWarning($"[{GetType().Name}] Module not loaded. Call TryLoadModule first.");
                return false;
            }

            if (!module.Initialized)
            {
                Debug.LogWarning($"[{GetType().Name}] Module is still initializing.");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Instantiates the module if not already created.
        /// </summary>
        public bool TryLoadModule()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning($"[{GetType().Name}] Modules should be loaded only in play mode.");
            }

            if (module != null)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(modelId))
            {
                Debug.LogError($"[{GetType().Name}] Model ID cannot be empty.");
                return false;
            }

            reporter = new SherpaOnnxFeedbackReporter(null, this);
            module = CreateModule(modelId.Trim(), sampleRate, reporter);
            if (module == null)
            {
                Debug.LogError($"[{GetType().Name}] Failed to create module for model '{modelId}'.");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Releases the module instance.
        /// </summary>
        public void DisposeModule()
        {
            if (module != null)
            {
                module.Dispose();
                module = null;
            }

            reporter = null;
            UpdateReadyState(false);
        }

        /// <summary>
        /// Derived classes must instantiate the concrete module here.
        /// </summary>
        protected abstract TModule CreateModule(string resolvedModelId, int resolvedSampleRate, SherpaOnnxFeedbackReporter resolvedReporter);

        #region Feedback Handling

        void ISherpaFeedbackHandler.OnFeedback(PrepareFeedback feedback) => HandleFeedback(feedback, LogType.Log);
        void ISherpaFeedbackHandler.OnFeedback(DownloadFeedback feedback) => HandleFeedback(feedback, LogType.Log);
        void ISherpaFeedbackHandler.OnFeedback(DecompressFeedback feedback) => HandleFeedback(feedback, LogType.Log);
        void ISherpaFeedbackHandler.OnFeedback(VerifyFeedback feedback) => HandleFeedback(feedback, LogType.Log);
        void ISherpaFeedbackHandler.OnFeedback(LoadFeedback feedback) => HandleFeedback(feedback, LogType.Log);
        void ISherpaFeedbackHandler.OnFeedback(CancelFeedback feedback)
        {
            HandleFeedback(feedback, LogType.Warning);
            UpdateReadyState(false);
        }

        void ISherpaFeedbackHandler.OnFeedback(SuccessFeedback feedback)
        {
            HandleFeedback(feedback, LogType.Log);
            UpdateReadyState(true);
        }

        void ISherpaFeedbackHandler.OnFeedback(FailedFeedback feedback)
        {
            HandleFeedback(feedback, LogType.Error);
            UpdateReadyState(false);
        }

        void ISherpaFeedbackHandler.OnFeedback(CleanFeedback feedback) => HandleFeedback(feedback, LogType.Log);

        private void HandleFeedback(SherpaFeedback feedback, LogType logType)
        {
            if (feedback == null)
            {
                return;
            }

            var message = BuildFeedbackMessage(feedback);

            if (logFeedbackToConsole)
            {
                switch (logType)
                {
                    case LogType.Error:
                        Debug.LogError(message);
                        break;
                    case LogType.Warning:
                        Debug.LogWarning(message);
                        break;
                    default:
                        Debug.Log(message);
                        break;
                }
            }

            onFeedbackMessage?.Invoke(message);
        }

        private static string BuildFeedbackMessage(SherpaFeedback feedback)
        {
            var model = feedback.Metadata?.modelId ?? "unknown-model";
            return $"[{feedback.GetType().Name}] {model}: {feedback.Message}";
        }

        private void UpdateReadyState(bool ready)
        {
            if (isReady == ready)
            {
                return;
            }

            isReady = ready;
            onInitializationStateChanged?.Invoke(ready);
        }

        #endregion
    }
}

