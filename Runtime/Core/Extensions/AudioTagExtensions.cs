
namespace Eitan.SherpaONNXUnity.Runtime
{

    using System;
    using System.Linq;
    using System.Text;
    using Eitan.SherpaONNXUnity.Runtime.Modules;


    /// <summary>
    /// Useful extensions for working with <see cref="AudioTagging.AudioTag"/> arrays.
    /// </summary>
    public static class AudioTagExtensions
    {
        /// <summary>
        /// Returns a new array ordered by probability descending.
        /// </summary>
        public static AudioTagging.AudioTag[] OrderByProbabilityDescending(this AudioTagging.AudioTag[] source)
        {
            if (source == null || source.Length == 0)
            {
                return Array.Empty<AudioTagging.AudioTag>();
            }


            return source.OrderByDescending(t => t.Probability).ToArray();
        }

        /// <summary>
        /// Returns the most probable tag, or null if empty.
        /// </summary>
        public static AudioTagging.AudioTag? Best(this AudioTagging.AudioTag[] source)
        {
            if (source == null || source.Length == 0)
            {
                return null;
            }


            var best = source[0];
            for (int i = 1; i < source.Length; i++)
            {

                if (source[i].Probability > best.Probability)
                {
                    best = source[i];
                }
            }


            return best;
        }

        /// <summary>
        /// Returns a new array containing only tags above the provided probability threshold.
        /// </summary>
        public static AudioTagging.AudioTag[] Above(this AudioTagging.AudioTag[] source, float minProbability)
        {
            if (source == null || source.Length == 0)
            {
                return Array.Empty<AudioTagging.AudioTag>();
            }


            return source.Where(t => t.Probability >= minProbability).ToArray();
        }

        /// <summary>
        /// Returns the top-K most probable tags. If k<=0 or k>=Length, returns a clone of the source ordered by probability.
        /// </summary>
        public static AudioTagging.AudioTag[] TopK(this AudioTagging.AudioTag[] source, int k)
        {
            if (source == null || source.Length == 0)
            {
                return Array.Empty<AudioTagging.AudioTag>();
            }


            var ordered = source.OrderByProbabilityDescending();
            if (k <= 0 || k >= ordered.Length)
            {
                return ordered;
            }


            var result = new AudioTagging.AudioTag[k];
            Array.Copy(ordered, result, k);
            return result;
        }

        /// <summary>
        /// Joins tags into a user-friendly string. Format example: "Dog (92.1%), Speech (80.0%)".
        /// </summary>
        public static string ToString(this AudioTagging.AudioTag[] source, string separator = ", ")
        {
            if (source == null || source.Length == 0)
            {
                return string.Empty;
            }


            var sb = new StringBuilder();
            for (int i = 0; i < source.Length; i++)
            {
                if (i > 0)
                {
                    sb.Append(separator);
                }
                sb.Append(source[i].ToString());
            }
            return sb.ToString();
        }
    }

}
