// Runtime: Packages/com.eitan.sherpa-onnx-unity/Runtime/Mono/Components/SourceSeparationComponent.cs

namespace Eitan.Sherpa.Onnx.Unity.Mono.Components
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Eitan.SherpaONNXUnity.Runtime;
    using Eitan.SherpaONNXUnity.Runtime.Modules;
    using Eitan.SherpaONNXUnity.Runtime.Utilities;
    using UnityEngine;
    using UnityEngine.Events;

    /// <summary>
    /// Reusable MonoBehaviour that runs offline source separation on AudioClips or raw PCM buffers.
    /// </summary>
    [AddComponentMenu("SherpaONNX/Audio/Source Separation")]
    [DisallowMultipleComponent]
    public sealed class SourceSeparationComponent : SherpaModuleComponent<SourceSeparation>
    {
        #region Serializable Types
        [Serializable]
        public sealed class SeparatedStemClip
        {
            public string stemName;
            public AudioClip clip;
            public int channels;
            public int sampleRate;
        }

        [Serializable]
        public sealed class SeparatedClipSet
        {
            public string sourceName;
            public SourceSeparationModelType modelType;
            public int sampleRate;
            public SeparatedStemClip[] stems;
        }

        [Serializable]
        public sealed class SeparatedClipSetEvent : UnityEvent<SeparatedClipSet>
        {
        }
        #endregion

        #region Serialized Fields
        [Header("Playback")]
        [SerializeField]
        [Tooltip("Optional AudioSources used to preview separated stems by index.")]
        private AudioSource[] playbackAudioSources;

        [SerializeField]
        [Tooltip("Automatically play separated stems on the mapped playback AudioSources.")]
        private bool autoplay = true;

        [Header("Clip Separation")]
        [SerializeField]
        [Tooltip("Optional AudioClip to separate directly. Falls back to the first playback AudioSource clip when null.")]
        private AudioClip clipReference;

        [SerializeField]
        [Tooltip("Process the referenced clip automatically when the component becomes enabled in play mode.")]
        private bool separateOnEnable;

        [Header("Output Processing")]
        [SerializeField]
        [Tooltip("Apply audio post-processing to separated stems before creating AudioClips.")]
        private bool enableOutputProcessing = true;

        [SerializeField]
        [Tooltip("Fade in duration applied to each separated stem.")]
        [Min(0)]
        private int outputFadeInMilliseconds = 4;

        [SerializeField]
        [Tooltip("Fade out duration applied to each separated stem. Useful for preventing tail clicks/pop noise.")]
        [Min(0)]
        private int outputFadeOutMilliseconds = 8;

        [SerializeField]
        [Tooltip("Remove DC offset from each output channel before creating clips.")]
        private bool removeOutputDcOffset;

        [SerializeField]
        [Tooltip("Clamp processed samples into the [-1, 1] PCM range.")]
        private bool clampOutputToUnitRange = true;

        [SerializeField]
        [Tooltip("Shape used for fade envelopes on separated stems.")]
        private AudioFadeCurve outputFadeCurve = AudioFadeCurve.EqualPower;

        [Header("Events")]
        [SerializeField]
        private SeparatedClipSetEvent onSeparationReady = new SeparatedClipSetEvent();
        #endregion

        #region Fields
        private CancellationTokenSource separationCts;
        #endregion

        #region Properties
        public SeparatedClipSetEvent SeparationReadyEvent => onSeparationReady;
        #endregion

        #region Lifecycle
        protected override SourceSeparation CreateModule(string resolvedModelId, int resolvedSampleRate, SherpaONNXFeedbackReporter resolvedReporter)
        {
            return new SourceSeparation(resolvedModelId, resolvedSampleRate, resolvedReporter);
        }

        private void OnEnable()
        {
            if (separationCts == null || separationCts.IsCancellationRequested)
            {
                separationCts?.Dispose();
                separationCts = new CancellationTokenSource();
            }

            if (Application.isPlaying && separateOnEnable && IsInitialized)
            {
                _ = SeparateAssignedClipAsync();
            }
        }

        private void OnDisable()
        {
            CancelSeparationOperations(false);
        }

        protected override void OnDestroy()
        {
            CancelSeparationOperations(false);
            separationCts?.Dispose();
            base.OnDestroy();
        }

        protected override void OnModuleInitializationStateChanged(bool ready)
        {
            base.OnModuleInitializationStateChanged(ready);

            if (!Application.isPlaying)
            {
                return;
            }

            if (ready && separateOnEnable)
            {
                _ = SeparateAssignedClipAsync();
            }
        }
        #endregion

        #region Public API
        public void SeparateAssignedClip()
        {
            _ = SeparateAssignedClipAsync();
        }

        public Task<SeparatedClipSet> SeparateAssignedClipAsync(CancellationToken cancellationToken = default)
        {
            var clip = clipReference != null ? clipReference : GetPrimaryPlaybackClip();
            if (clip == null)
            {
                ReportError("No clip assigned for source separation. Set Clip Reference or assign a clip to the first Playback Audio Source.");
                return Task.FromResult<SeparatedClipSet>(null);
            }

            return SeparateClipAsync(
                clip,
                applyToPlayback: playbackAudioSources != null && playbackAudioSources.Length > 0,
                cancellationToken: cancellationToken);
        }

        public async Task<SeparatedClipSet> SeparateClipAsync(
            AudioClip clip,
            bool applyToPlayback = true,
            CancellationToken cancellationToken = default)
        {
            if (clip == null)
            {
                ReportError("No AudioClip provided for source separation.");
                return null;
            }

            if (!EnsureModuleReady(out var module))
            {
                return null;
            }

            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                separationCts?.Token ?? CancellationToken.None,
                cancellationToken);

            try
            {
                var result = await module.SeparateAsync(
                    clip,
                    GetOutputProcessingOptions(),
                    linkedCts.Token).ConfigureAwait(false);
                SeparatedClipSet clipSet = null;

                await RunOnUnityThreadAsync(() =>
                {
                    clipSet = CreateClipSet(result, clip.name);
                    if (clipSet != null)
                    {
                        HandleSeparationReady(clipSet, applyToPlayback);
                    }
                }).ConfigureAwait(false);

                return clipSet;
            }
            catch (OperationCanceledException)
            {
                return null;
            }
            catch (Exception ex)
            {
                ReportError(ex.Message);
                return null;
            }
        }

        public async Task<SeparatedClipSet> SeparateSamplesAsync(
            float[] interleavedSamples,
            int numChannels,
            int sampleRate,
            bool applyToPlayback = true,
            CancellationToken cancellationToken = default)
        {
            if (interleavedSamples == null || interleavedSamples.Length == 0)
            {
                ReportError("No samples provided for source separation.");
                return null;
            }

            if (numChannels <= 0)
            {
                ReportError("Channel count must be greater than zero.");
                return null;
            }

            if (!EnsureModuleReady(out var module))
            {
                return null;
            }

            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                separationCts?.Token ?? CancellationToken.None,
                cancellationToken);

            try
            {
                var result = await module.SeparateAsync(
                    interleavedSamples,
                    numChannels,
                    sampleRate,
                    GetOutputProcessingOptions(),
                    linkedCts.Token).ConfigureAwait(false);
                SeparatedClipSet clipSet = null;

                await RunOnUnityThreadAsync(() =>
                {
                    clipSet = CreateClipSet(result, "source_separation_output");
                    if (clipSet != null)
                    {
                        HandleSeparationReady(clipSet, applyToPlayback);
                    }
                }).ConfigureAwait(false);

                return clipSet;
            }
            catch (OperationCanceledException)
            {
                return null;
            }
            catch (Exception ex)
            {
                ReportError(ex.Message);
                return null;
            }
        }
        #endregion

        #region Helpers
        private void HandleSeparationReady(SeparatedClipSet clipSet, bool applyToPlayback)
        {
            onSeparationReady?.Invoke(clipSet);

            if (!applyToPlayback || playbackAudioSources == null || playbackAudioSources.Length == 0 || clipSet?.stems == null)
            {
                return;
            }

            var limit = Math.Min(playbackAudioSources.Length, clipSet.stems.Length);
            for (int i = 0; i < limit; i++)
            {
                var audioSource = playbackAudioSources[i];
                var stemClip = clipSet.stems[i]?.clip;
                if (audioSource == null || stemClip == null)
                {
                    continue;
                }

                audioSource.Stop();
                audioSource.clip = stemClip;
                if (autoplay)
                {
                    audioSource.Play();
                }
            }
        }

        private SeparatedClipSet CreateClipSet(SourceSeparation.Result result, string sourceName)
        {
            if (result == null || result.Stems == null || result.Stems.Length == 0)
            {
                return null;
            }

            var stems = new SeparatedStemClip[result.Stems.Length];
            for (int i = 0; i < result.Stems.Length; i++)
            {
                var stem = result.Stems[i];
                var clip = CreateClipFromStem(sourceName, stem, i, result.SampleRate);
                stems[i] = new SeparatedStemClip
                {
                    stemName = stem.Name,
                    clip = clip,
                    channels = stem.NumChannels,
                    sampleRate = result.SampleRate,
                };
            }

            return new SeparatedClipSet
            {
                sourceName = sourceName ?? string.Empty,
                modelType = result.ModelType,
                sampleRate = result.SampleRate,
                stems = stems,
            };
        }

        private static AudioClip CreateClipFromStem(string sourceName, SourceSeparation.Stem stem, int index, int sampleRate)
        {
            var interleaved = stem.ToInterleaved();
            if (interleaved == null || interleaved.Length == 0)
            {
                return null;
            }

            var channels = Math.Max(1, stem.NumChannels);
            var samplesPerChannel = interleaved.Length / channels;
            var clipName = $"{sourceName}_{(string.IsNullOrWhiteSpace(stem.Name) ? $"stem_{index}" : stem.Name)}";
            var clip = AudioClip.Create(clipName, samplesPerChannel, channels, sampleRate, false);
            clip.SetData(interleaved, 0);
            return clip;
        }

        private AudioClip GetPrimaryPlaybackClip()
        {
            if (playbackAudioSources == null || playbackAudioSources.Length == 0)
            {
                return null;
            }

            return playbackAudioSources[0] != null ? playbackAudioSources[0].clip : null;
        }

        private AudioProcessingOptions GetOutputProcessingOptions()
        {
            return new AudioProcessingOptions(
                enabled: enableOutputProcessing,
                fadeInMilliseconds: outputFadeInMilliseconds,
                fadeOutMilliseconds: outputFadeOutMilliseconds,
                removeDcOffset: removeOutputDcOffset,
                clampToUnitRange: clampOutputToUnitRange,
                fadeCurve: outputFadeCurve);
        }

        private void CancelSeparationOperations(bool recreateToken = true)
        {
            if (separationCts != null)
            {
                separationCts.Cancel();
                separationCts.Dispose();
                separationCts = recreateToken ? new CancellationTokenSource() : null;
            }
        }

        private void ReportError(string message)
        {
            SherpaLog.Error($"[SourceSeparationComponent] {message}");
            RaiseError(message);
        }

        private Task RunOnUnityThreadAsync(Action action)
        {
            if (action == null)
            {
                return Task.CompletedTask;
            }

            var tcs = new TaskCompletionSource<bool>();
            DispatchToUnity(() =>
            {
                try
                {
                    action();
                    tcs.TrySetResult(true);
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            });

            return tcs.Task;
        }
        #endregion
    }
}
