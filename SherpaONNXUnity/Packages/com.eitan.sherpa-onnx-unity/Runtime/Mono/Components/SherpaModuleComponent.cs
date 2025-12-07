// Runtime: Packages/com.eitan.sherpa-onnx-unity/Runtime/Mono/Components/SherpaModuleComponent.cs

namespace Eitan.Sherpa.Onnx.Unity.Mono.Components
{
    using System;
    using System.Threading;
    using Eitan.SherpaONNXUnity.Runtime;
    using Eitan.SherpaONNXUnity.Runtime.Utilities;
    using UnityEngine;
    using UnityEngine.Events;

    /// <summary>
    /// Shared infrastructure for MonoBehaviours that host a sherpa-onnx module.
    /// Handles lifecycle, feedback routing, and basic validation.
    /// </summary>
    /// <typeparam name="TModule">Concrete sherpa module type.</typeparam>
    public abstract class SherpaModuleComponent<TModule> : MonoBehaviour, ISherpaFeedbackHandler
        where TModule : SherpaONNXModule
    {
        [Header("Model")]
        [SerializeField]
        [Tooltip("Model identifier registered in SherpaONNXModelRegistry.")]
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

        [Header("Errors")]
        [SerializeField]
        [Tooltip("Raised whenever the component encounters an error condition.")]
        private UnityEvent<string> onError = new UnityEvent<string>();

        /// <summary>
        /// Exposes the initialization UnityEvent so callers can register listeners in code.
        /// </summary>
        public UnityEvent<bool> InitializationStateChangedEvent => onInitializationStateChanged;

        /// <summary>
        /// Fires whenever any feedback is received. Use this for structured progress handling.
        /// </summary>
        public event Action<SherpaFeedback> FeedbackReceived;

        /// <summary>
        /// Exposes feedback messages so UI scripts can surface loader state easily.
        /// </summary>
        public FeedbackMessageEvent FeedbackMessages => onFeedbackMessage;

        /// <summary>
        /// Exposes error notifications for consumers.
        /// </summary>
        public UnityEvent<string> ErrorEvent => onError;

        /// <summary>
        /// UnityEvent wrapper that exposes textual feedback.
        /// </summary>
        [Serializable]
        public sealed class FeedbackMessageEvent : UnityEvent<string>
        {
        }

        private readonly object moduleGate = new object();

        private TModule module;
        private SherpaONNXFeedbackReporter reporter;
        private bool isReady;
        private bool loadInProgress;
        private bool disposeInProgress;

        private SynchronizationContext unityContext;
        private int unityThreadId;

        /// <summary>
        /// Gets the instantiated module or null when not loaded.
        /// </summary>
        protected TModule Module => module;

        /// <summary>
        /// Gets the reporter used to receive load/prepare feedback.
        /// </summary>
        protected SherpaONNXFeedbackReporter Reporter => reporter;

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
            // Capture Unity SystemInfo on the main thread to avoid background-thread access errors.
            ThreadingUtils.PrimeUnityInfo();

            unityContext = SynchronizationContext.Current ?? new SynchronizationContext();
            unityThreadId = Thread.CurrentThread.ManagedThreadId;

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
                SherpaLog.Warning($"[{GetType().Name}] Module not loaded. Call TryLoadModule first.");
                return false;
            }

            if (!module.Initialized)
            {
                SherpaLog.Warning($"[{GetType().Name}] Module is still initializing.");
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
                SherpaLog.Warning($"[{GetType().Name}] Modules should be loaded only in play mode.");
            }

            lock (moduleGate)
            {
                if (loadInProgress)
                {
                    SherpaLog.Warning($"[{GetType().Name}] Module load already in progress.");
                    RaiseError($"{GetType().Name} is already loading a module.");
                    return false;
                }

                if (module != null)
                {
                    SherpaLog.Warning($"[{GetType().Name}] Module already loaded; ignoring duplicate request.");
                    return false;
                }

                loadInProgress = true;
            }

            if (string.IsNullOrWhiteSpace(modelId))
            {
                SherpaLog.Error($"[{GetType().Name}] Model ID cannot be empty.");
                RaiseError("Model ID cannot be empty.");
                MarkLoadComplete();
                return false;
            }

            reporter = new SherpaONNXFeedbackReporter(null, this);

            try
            {
                module = CreateModule(modelId.Trim(), sampleRate, reporter);
            }
            catch (Exception ex)
            {
                module = null;
                reporter = null;
                SherpaLog.Error($"[{GetType().Name}] Failed to create module for model '{modelId}': {ex.Message}");
                RaiseError($"Failed to create module for model '{modelId}': {ex.Message}");
                MarkLoadComplete();
                return false;
            }

            if (module == null)
            {
                SherpaLog.Error($"[{GetType().Name}] Failed to create module for model '{modelId}'.");
                RaiseError($"Failed to create module for model '{modelId}'.");
                MarkLoadComplete();
                return false;
            }

            MarkLoadComplete();
            return true;
        }

        /// <summary>
        /// Releases the module instance.
        /// </summary>
        public void DisposeModule()
        {
            lock (moduleGate)
            {
                if (disposeInProgress)
                {
                    return;
                }
                disposeInProgress = true;
            }

            try
            {
                if (module != null)
                {
                    module.Dispose();
                    module = null;
                }
            }
            finally
            {
                reporter = null;
                UpdateReadyState(false);
                lock (moduleGate)
                {
                    disposeInProgress = false;
                    loadInProgress = false;
                }
            }
        }

        /// <summary>
        /// Derived classes must instantiate the concrete module here.
        /// </summary>
        protected abstract TModule CreateModule(string resolvedModelId, int resolvedSampleRate, SherpaONNXFeedbackReporter resolvedReporter);

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
                        SherpaLog.Error(message);
                        break;
                    case LogType.Warning:
                        SherpaLog.Warning(message);
                        break;
                    default:
                        SherpaLog.Info(message);
                        break;
                }
            }

            DispatchToUnity(() => onFeedbackMessage?.Invoke(message));
            DispatchToUnity(() => FeedbackReceived?.Invoke(feedback));
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
            DispatchToUnity(() => onInitializationStateChanged?.Invoke(ready));
            DispatchToUnity(() => OnModuleInitializationStateChanged(ready));
        }

        /// <summary>
        /// Invoked whenever the module transitions between ready/not-ready states.
        /// </summary>
        /// <param name="ready">True when the module finished initializing successfully.</param>
        protected virtual void OnModuleInitializationStateChanged(bool ready)
        {
        }

        /// <summary>
        /// Raises an error message to listeners in a consistent way.
        /// </summary>
        /// <param name="message">Human-readable error details.</param>
        protected void RaiseError(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            DispatchToUnity(() => onError?.Invoke(message));
        }

        /// <summary>
        /// Executes an action on the Unity synchronization context when available.
        /// Ensures UnityEvents fire on the main thread even when awaited tasks resume on worker threads.
        /// </summary>
        protected void DispatchToUnity(Action action)
        {
            if (action == null)
            {
                return;
            }

            if (unityContext == null)
            {
                action();
                return;
            }

            if (Thread.CurrentThread.ManagedThreadId == unityThreadId)
            {
                action();
                return;
            }

            unityContext.Post(_ => action(), null);
        }

        private void MarkLoadComplete()
        {
            lock (moduleGate)
            {
                loadInProgress = false;
            }
        }

        #endregion
    }
}
