// Runtime: Packages/com.eitan.sherpa-onnx-unity/Runtime/Mono/Components/PunctuationComponent.cs

namespace Eitan.Sherpa.Onnx.Unity.Mono.Components
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Eitan.SherpaOnnxUnity.Runtime;
    using Eitan.SherpaOnnxUnity.Runtime.Core;
    using UnityEngine;
    using UnityEngine.Events;

    /// <summary>
    /// Simple text post-processor that adds punctuation via the sherpa-onnx OfflinePunctuation model.
    /// </summary>
    [AddComponentMenu("Sherpa ONNX/Text Processing/Punctuation")]
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

        protected override Punctuation CreateModule(string resolvedModelId, int resolvedSampleRate, SherpaOnnxFeedbackReporter resolvedReporter)
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
                Debug.LogError($"[PunctuationComponent] Failed to add punctuation: {ex.Message}");
                onPunctuationFailed?.Invoke(ex.Message);
                return string.Empty;
            }
        }
    }
}
