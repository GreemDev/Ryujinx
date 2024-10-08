using Ryujinx.Common.Memory;
using System.Runtime.InteropServices;

namespace Ryujinx.Audio.Renderer.Parameter.Effect
{
    /// <summary>
    /// Effect result state for <seealso cref="Common.EffectType.Compressor"/>.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct CompressorStatistics
    {
        /// <summary>
        /// Maximum input mean value since last reset.
        /// </summary>
        public float MaximumMean;

        /// <summary>
        /// Minimum output gain since last reset.
        /// </summary>
        public float MinimumGain;

        /// <summary>
        /// Last processed input sample, per channel.
        /// </summary>
        public Array6<float> LastSamples;

        /// <summary>
        /// Reset the statistics.
        /// </summary>
        /// <param name="channelCount">Number of channels to reset.</param>
        public void Reset(ushort channelCount)
        {
            MaximumMean = 0.0f;
            MinimumGain = 1.0f;
            LastSamples.AsSpan()[..channelCount].Clear();
        }
    }
}
