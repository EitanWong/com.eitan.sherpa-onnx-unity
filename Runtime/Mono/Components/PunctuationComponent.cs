// Runtime: Packages/com.eitan.sherpa-onnx-unity/Runtime/Mono/Components/PunctuationComponent.cs

namespace Eitan.Sherpa.Onnx.Unity.Mono.Components
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Eitan.SherpaONNXUnity.Runtime;
    using Eitan.SherpaONNXUnity.Runtime.Core;
    using UnityEngine;
    using UnityEngine.Events;

    /// <summary>
    /// Simple text post-processor that adds punctuation via the sherpa-onnx OfflinePunctuation model.
    /// </summary>
    [AddComponentMenu("SherpaONNX/Text Processing/Punctuation")]
    [DisallowMultipleComponent]
    public sealed class PunctuationComponent : SherpaModuleComponent<Punctuation>
    {
        [Header("Preview")]
        [SerializeField]
        [TextArea(3, 6)]
        private string previewText = "ni hao wo shi sherpa";

        [Header("Events")]
        [SerializeField]
        private UnityEvent<string> onPunctuationReady = new UnityEvent<string>();

        [SerializeField]
        private UnityEvent<string> onPunctuationFailed = new UnityEvent<string>();

        /// <summary>
        /// Allows runtime listeners to observe punctuation results.
        /// </summary>
        public UnityEvent<string> PunctuationReadyEvent => onPunctuationReady;

        /// <summary>
        /// Allows runtime listeners to observe punctuation errors.
        /// </summary>
        public UnityEvent<string> PunctuationFailedEvent => onPunctuationFailed;

        protected override Punctuation CreateModule(string resolvedModelId, int resolvedSampleRate, SherpaONNXFeedbackReporter resolvedReporter)
        {
            return new Punctuation(resolvedModelId, resolvedSampleRate, resolvedReporter);
        }

        /// <summary>
        /// Convenience button for the inspector to process the preview text.
        /// </summary>
        public void RunPreview()
        {
            _ = AddPunctuationAsync(previewText);
        }

        public async Task<string> AddPunctuationAsync(string text, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            if (!EnsureModuleReady(out var module))
            {
                return string.Empty;
            }

            try
            {
                var result = await module.AddPunctuationAsync(text.Trim(), cancellationToken).ConfigureAwait(true);
                onPunctuationReady?.Invoke(result);
                return result ?? string.Empty;
            }
            catch (OperationCanceledException)
            {
                return string.Empty;
            }
            catch (Exception ex)
            {
                SherpaLog.Error($"[PunctuationComponent] Failed to add punctuation: {ex.Message}");
                onPunctuationFailed?.Invoke(ex.Message);
                RaiseError(ex.Message);
                return string.Empty;
            }
        }
    }
}
