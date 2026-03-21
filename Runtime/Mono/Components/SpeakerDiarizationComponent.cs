// Runtime: Packages/com.eitan.sherpa-onnx-unity/Runtime/Mono/Components/SpeakerDiarizationComponent.cs

namespace Eitan.Sherpa.Onnx.Unity.Mono.Components
{
    using System;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Eitan.SherpaONNXUnity.Runtime;
    using Eitan.SherpaONNXUnity.Runtime.Modules;
    using UnityEngine;
    using UnityEngine.Events;

    /// <summary>
    /// MonoBehaviour wrapper for offline speaker diarization using a segmentation model plus an embedding model.
    /// Suitable for one-shot AudioClip analysis or manual PCM buffers.
    /// </summary>
    [AddComponentMenu("SherpaONNX/Speaker/Speaker Diarization")]
    [DisallowMultipleComponent]
    public sealed class SpeakerDiarizationComponent : SherpaModuleComponent<SpeakerDiarization>
    {
        [Header("Embedding Model")]
        [SerializeField]
        [Tooltip("Secondary model identifier used for speaker embedding extraction.")]
        private string embeddingModelId = string.Empty;

        [Header("Lifecycle")]
        [SerializeField]
        [Tooltip("Start module initialization immediately when constructed. Disable to configure first, then call StartModuleInitializationAsync manually.")]
        private bool startModuleImmediately = true;

        [Header("Diarization Options")]
        [SerializeField]
        [Tooltip("Minimum duration for speech regions to remain active.")]
        private float minDurationOn = 0.2f;

        [SerializeField]
        [Tooltip("Minimum duration for silence regions before splitting speech.")]
        private float minDurationOff = 0.25f;

        [SerializeField]
        [Tooltip("Fixed number of speakers to cluster. Set to -1 for automatic clustering.")]
        private int numClusters = -1;

        [SerializeField]
        [Tooltip("Similarity threshold used by the fast clustering stage.")]
        [Range(0.01f, 1f)]
        private float clusteringThreshold = 0.45f;

        [Header("Clip Input")]
        [SerializeField]
        [Tooltip("Optional AudioClip to diarize automatically after initialization succeeds.")]
        private AudioClip clipToDiarize;

        [SerializeField]
        [Tooltip("When enabled, the assigned clip is diarized automatically after the module becomes ready.")]
        private bool diarizeAssignedClipOnReady;

        [Header("Events")]
        [SerializeField]
        private UnityEvent<SpeakerDiarization.DiarizationSegment[]> onSegmentsReady = new UnityEvent<SpeakerDiarization.DiarizationSegment[]>();

        [SerializeField]
        private UnityEvent<string> onDiarizationLogReady = new UnityEvent<string>();

        [SerializeField]
        private UnityEvent<string> onDiarizationFailed = new UnityEvent<string>();

        public UnityEvent<SpeakerDiarization.DiarizationSegment[]> SegmentsReadyEvent => onSegmentsReady;

        public UnityEvent<string> DiarizationLogReadyEvent => onDiarizationLogReady;

        public UnityEvent<string> DiarizationFailedEvent => onDiarizationFailed;

        public string EmbeddingModelId
        {
            get => embeddingModelId;
            set => embeddingModelId = value;
        }

        protected override SpeakerDiarization CreateModule(string resolvedModelId, int resolvedSampleRate, SherpaONNXFeedbackReporter resolvedReporter)
        {
            if (string.IsNullOrWhiteSpace(embeddingModelId))
            {
                throw new ArgumentException("Embedding Model ID cannot be empty.", nameof(embeddingModelId));
            }

            return new SpeakerDiarization(
                resolvedModelId,
                embeddingModelId.Trim(),
                resolvedReporter,
                startImmediately: startModuleImmediately,
                options: BuildOptions());
        }

        protected override void OnModuleInitializationStateChanged(bool ready)
        {
            base.OnModuleInitializationStateChanged(ready);

            if (!ready || !Application.isPlaying || !diarizeAssignedClipOnReady || clipToDiarize == null)
            {
                return;
            }

            _ = DiarizeClipAsync(clipToDiarize);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            minDurationOn = SanitizePositive(minDurationOn, 0.2f);
            minDurationOff = SanitizePositive(minDurationOff, 0.25f);
            clusteringThreshold = Mathf.Clamp(clusteringThreshold, 0.01f, 1f);
            if (numClusters == 0)
            {
                numClusters = -1;
            }

            Module?.UpdateOptions(BuildOptions());
        }
#endif

        public Task StartModuleInitializationAsync(CancellationToken cancellationToken = default)
        {
            if (Module == null && !TryLoadModule())
            {
                RaiseError("Failed to load speaker diarization module.");
                return Task.CompletedTask;
            }

            return Module?.StartInitialization(cancellationToken) ?? Task.CompletedTask;
        }

        public Task<SpeakerDiarization.DiarizationSegment[]> DiarizeAssignedClipAsync(CancellationToken cancellationToken = default)
        {
            return DiarizeClipAsync(clipToDiarize, cancellationToken);
        }

        public async Task<SpeakerDiarization.DiarizationSegment[]> DiarizeClipAsync(AudioClip clip, CancellationToken cancellationToken = default)
        {
            if (clip == null)
            {
                const string message = "Missing AudioClip reference for speaker diarization.";
                DispatchToUnity(() => onDiarizationFailed?.Invoke(message));
                RaiseError(message);
                return Array.Empty<SpeakerDiarization.DiarizationSegment>();
            }

            if (!EnsureModuleReady(out var module))
            {
                return Array.Empty<SpeakerDiarization.DiarizationSegment>();
            }

            try
            {
                var segments = await module.DiarizeAsync(clip, cancellationToken).ConfigureAwait(false);
                PublishSegments(segments);
                return segments ?? Array.Empty<SpeakerDiarization.DiarizationSegment>();
            }
            catch (OperationCanceledException)
            {
                return Array.Empty<SpeakerDiarization.DiarizationSegment>();
            }
            catch (Exception ex)
            {
                HandleFailure(ex.Message);
                return Array.Empty<SpeakerDiarization.DiarizationSegment>();
            }
        }

        public async Task<SpeakerDiarization.DiarizationSegment[]> DiarizeSamplesAsync(float[] samples, int sampleRate, CancellationToken cancellationToken = default)
        {
            if (samples == null || samples.Length == 0)
            {
                return Array.Empty<SpeakerDiarization.DiarizationSegment>();
            }

            if (!EnsureModuleReady(out var module))
            {
                return Array.Empty<SpeakerDiarization.DiarizationSegment>();
            }

            var clone = new float[samples.Length];
            Array.Copy(samples, clone, samples.Length);

            try
            {
                var segments = await module.DiarizeAsync(clone, sampleRate, cancellationToken).ConfigureAwait(false);
                PublishSegments(segments);
                return segments ?? Array.Empty<SpeakerDiarization.DiarizationSegment>();
            }
            catch (OperationCanceledException)
            {
                return Array.Empty<SpeakerDiarization.DiarizationSegment>();
            }
            catch (Exception ex)
            {
                HandleFailure(ex.Message);
                return Array.Empty<SpeakerDiarization.DiarizationSegment>();
            }
        }

        public static string FormatSegments(SpeakerDiarization.DiarizationSegment[] segments)
        {
            if (segments == null || segments.Length == 0)
            {
                return string.Empty;
            }

            var builder = new StringBuilder(segments.Length * 32);
            for (int i = 0; i < segments.Length; i++)
            {
                var segment = segments[i];
                if (builder.Length > 0)
                {
                    builder.AppendLine();
                }

                builder.Append("Speaker ")
                    .Append(segment.Speaker)
                    .Append(": ")
                    .Append(segment.Start.ToString("F2"))
                    .Append("s - ")
                    .Append(segment.End.ToString("F2"))
                    .Append("s");
            }

            return builder.ToString();
        }

        private void PublishSegments(SpeakerDiarization.DiarizationSegment[] segments)
        {
            var safeSegments = segments ?? Array.Empty<SpeakerDiarization.DiarizationSegment>();
            var formattedLog = FormatSegments(safeSegments);

            DispatchToUnity(() =>
            {
                onSegmentsReady?.Invoke(safeSegments);
                onDiarizationLogReady?.Invoke(formattedLog);
            });
        }

        private void HandleFailure(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                message = "Speaker diarization failed.";
            }

            SherpaLog.Error($"[SpeakerDiarizationComponent] {message}");
            DispatchToUnity(() => onDiarizationFailed?.Invoke(message));
            RaiseError(message);
        }

        private SpeakerDiarization.Options BuildOptions()
        {
            return new SpeakerDiarization.Options
            {
                MinDurationOn = minDurationOn,
                MinDurationOff = minDurationOff,
                NumClusters = numClusters,
                ClusteringThreshold = clusteringThreshold
            };
        }

        private static float SanitizePositive(float value, float fallback)
        {
            return value > 0f && !float.IsNaN(value) && !float.IsInfinity(value) ? value : fallback;
        }
    }
}
