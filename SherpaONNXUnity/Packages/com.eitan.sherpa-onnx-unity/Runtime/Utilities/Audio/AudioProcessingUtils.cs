namespace Eitan.SherpaONNXUnity.Runtime.Utilities
{
    using System;
    using UnityEngine;

    public enum AudioFadeCurve
    {
        Linear,
        EqualPower,
    }

    /// <summary>
    /// In-place PCM post-processing options for runtime audio produced by sherpa-onnx modules.
    /// </summary>
    public readonly struct AudioProcessingOptions
    {
        public static AudioProcessingOptions None => new AudioProcessingOptions(
            enabled: false,
            fadeInMilliseconds: 0,
            fadeOutMilliseconds: 0);

        public static AudioProcessingOptions SourceSeparationSafeDefault => new AudioProcessingOptions(
            enabled: true,
            fadeInMilliseconds: 4,
            fadeOutMilliseconds: 8,
            removeDcOffset: false,
            clampToUnitRange: true,
            fadeCurve: AudioFadeCurve.EqualPower);

        public AudioProcessingOptions(
            bool enabled = true,
            int fadeInMilliseconds = 0,
            int fadeOutMilliseconds = 0,
            bool removeDcOffset = false,
            bool clampToUnitRange = false,
            AudioFadeCurve fadeCurve = AudioFadeCurve.Linear)
        {
            Enabled = enabled;
            FadeInMilliseconds = Math.Max(0, fadeInMilliseconds);
            FadeOutMilliseconds = Math.Max(0, fadeOutMilliseconds);
            RemoveDcOffset = removeDcOffset;
            ClampToUnitRange = clampToUnitRange;
            FadeCurve = fadeCurve;
        }

        public bool Enabled { get; }
        public int FadeInMilliseconds { get; }
        public int FadeOutMilliseconds { get; }
        public bool RemoveDcOffset { get; }
        public bool ClampToUnitRange { get; }
        public AudioFadeCurve FadeCurve { get; }
        public bool HasWork => Enabled && (FadeInMilliseconds > 0 || FadeOutMilliseconds > 0 || RemoveDcOffset || ClampToUnitRange);
    }

    /// <summary>
    /// High-performance, allocation-free PCM utilities for multi-channel float audio.
    /// </summary>
    public static class AudioProcessingUtils
    {
        public static void ProcessChannelsInPlace(float[][] channels, int sampleRate, in AudioProcessingOptions options)
        {
            if (!options.HasWork || channels == null || channels.Length == 0 || sampleRate <= 0)
            {
                return;
            }

            int frames = GetConsistentFrameCount(channels);
            if (frames <= 0)
            {
                return;
            }

            if (options.RemoveDcOffset)
            {
                RemoveDcOffsetInPlace(channels, frames);
            }

            if (options.FadeInMilliseconds > 0)
            {
                ApplyFadeInInPlace(channels, frames, sampleRate, options.FadeInMilliseconds, options.FadeCurve);
            }

            if (options.FadeOutMilliseconds > 0)
            {
                ApplyFadeOutInPlace(channels, frames, sampleRate, options.FadeOutMilliseconds, options.FadeCurve);
            }

            if (options.ClampToUnitRange)
            {
                ClampToUnitRangeInPlace(channels, frames);
            }
        }

        public static void ProcessInterleavedInPlace(float[] interleavedSamples, int channels, int sampleRate, in AudioProcessingOptions options)
        {
            if (!options.HasWork || interleavedSamples == null || interleavedSamples.Length == 0 || channels <= 0 || sampleRate <= 0)
            {
                return;
            }

            if ((interleavedSamples.Length % channels) != 0)
            {
                return;
            }

            int frames = interleavedSamples.Length / channels;
            if (frames <= 0)
            {
                return;
            }

            if (options.RemoveDcOffset)
            {
                RemoveDcOffsetInterleavedInPlace(interleavedSamples, channels, frames);
            }

            if (options.FadeInMilliseconds > 0)
            {
                ApplyFadeInInterleavedInPlace(interleavedSamples, channels, frames, sampleRate, options.FadeInMilliseconds, options.FadeCurve);
            }

            if (options.FadeOutMilliseconds > 0)
            {
                ApplyFadeOutInterleavedInPlace(interleavedSamples, channels, frames, sampleRate, options.FadeOutMilliseconds, options.FadeCurve);
            }

            if (options.ClampToUnitRange)
            {
                for (int i = 0; i < interleavedSamples.Length; i++)
                {
                    interleavedSamples[i] = Mathf.Clamp(interleavedSamples[i], -1f, 1f);
                }
            }
        }

        private static int GetConsistentFrameCount(float[][] channels)
        {
            int frames = channels[0]?.Length ?? 0;
            if (frames <= 0)
            {
                return 0;
            }

            for (int i = 1; i < channels.Length; i++)
            {
                if (channels[i] == null || channels[i].Length != frames)
                {
                    return 0;
                }
            }

            return frames;
        }

        private static void RemoveDcOffsetInPlace(float[][] channels, int frames)
        {
            for (int channel = 0; channel < channels.Length; channel++)
            {
                var samples = channels[channel];
                float mean = 0f;
                for (int i = 0; i < frames; i++)
                {
                    mean += samples[i];
                }

                mean /= frames;
                if (Mathf.Approximately(mean, 0f))
                {
                    continue;
                }

                for (int i = 0; i < frames; i++)
                {
                    samples[i] -= mean;
                }
            }
        }

        private static void RemoveDcOffsetInterleavedInPlace(float[] interleavedSamples, int channels, int frames)
        {
            for (int channel = 0; channel < channels; channel++)
            {
                float mean = 0f;
                for (int frame = 0; frame < frames; frame++)
                {
                    mean += interleavedSamples[frame * channels + channel];
                }

                mean /= frames;
                if (Mathf.Approximately(mean, 0f))
                {
                    continue;
                }

                for (int frame = 0; frame < frames; frame++)
                {
                    int index = frame * channels + channel;
                    interleavedSamples[index] -= mean;
                }
            }
        }

        private static void ClampToUnitRangeInPlace(float[][] channels, int frames)
        {
            for (int channel = 0; channel < channels.Length; channel++)
            {
                var samples = channels[channel];
                for (int i = 0; i < frames; i++)
                {
                    samples[i] = Mathf.Clamp(samples[i], -1f, 1f);
                }
            }
        }

        private static void ApplyFadeInInPlace(float[][] channels, int frames, int sampleRate, int fadeMilliseconds, AudioFadeCurve curve)
        {
            int fadeFrames = Math.Min(frames, Mathf.CeilToInt(sampleRate * fadeMilliseconds / 1000f));
            if (fadeFrames <= 1)
            {
                return;
            }

            for (int frame = 0; frame < fadeFrames; frame++)
            {
                float gain = EvaluateCurve(frame / (float)(fadeFrames - 1), curve);
                for (int channel = 0; channel < channels.Length; channel++)
                {
                    channels[channel][frame] *= gain;
                }
            }
        }

        private static void ApplyFadeOutInPlace(float[][] channels, int frames, int sampleRate, int fadeMilliseconds, AudioFadeCurve curve)
        {
            int fadeFrames = Math.Min(frames, Mathf.CeilToInt(sampleRate * fadeMilliseconds / 1000f));
            if (fadeFrames <= 1)
            {
                return;
            }

            int startFrame = frames - fadeFrames;
            for (int i = 0; i < fadeFrames; i++)
            {
                float gain = 1f - EvaluateCurve(i / (float)(fadeFrames - 1), curve);
                int frame = startFrame + i;
                for (int channel = 0; channel < channels.Length; channel++)
                {
                    channels[channel][frame] *= gain;
                }
            }
        }

        private static void ApplyFadeInInterleavedInPlace(float[] interleavedSamples, int channels, int frames, int sampleRate, int fadeMilliseconds, AudioFadeCurve curve)
        {
            int fadeFrames = Math.Min(frames, Mathf.CeilToInt(sampleRate * fadeMilliseconds / 1000f));
            if (fadeFrames <= 1)
            {
                return;
            }

            for (int frame = 0; frame < fadeFrames; frame++)
            {
                float gain = EvaluateCurve(frame / (float)(fadeFrames - 1), curve);
                int baseIndex = frame * channels;
                for (int channel = 0; channel < channels; channel++)
                {
                    interleavedSamples[baseIndex + channel] *= gain;
                }
            }
        }

        private static void ApplyFadeOutInterleavedInPlace(float[] interleavedSamples, int channels, int frames, int sampleRate, int fadeMilliseconds, AudioFadeCurve curve)
        {
            int fadeFrames = Math.Min(frames, Mathf.CeilToInt(sampleRate * fadeMilliseconds / 1000f));
            if (fadeFrames <= 1)
            {
                return;
            }

            int startFrame = frames - fadeFrames;
            for (int i = 0; i < fadeFrames; i++)
            {
                float gain = 1f - EvaluateCurve(i / (float)(fadeFrames - 1), curve);
                int baseIndex = (startFrame + i) * channels;
                for (int channel = 0; channel < channels; channel++)
                {
                    interleavedSamples[baseIndex + channel] *= gain;
                }
            }
        }

        private static float EvaluateCurve(float t, AudioFadeCurve curve)
        {
            t = Mathf.Clamp01(t);
            switch (curve)
            {
                case AudioFadeCurve.EqualPower:
                    return Mathf.Sin(t * Mathf.PI * 0.5f);
                case AudioFadeCurve.Linear:
                default:
                    return t;
            }
        }
    }
}
