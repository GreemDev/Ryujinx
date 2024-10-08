using Ryujinx.Audio.Renderer.Server.Effect;
using Ryujinx.Common.Memory;
using System.Runtime.InteropServices;

namespace Ryujinx.Audio.Renderer.Parameter.Effect
{
    /// <summary>
    /// <see cref="IEffectInParameter.SpecificData"/> for <see cref="Common.EffectType.Compressor"/>.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct CompressorParameter
    {
        /// <summary>
        /// The input channel indices that will be used by the <see cref="Dsp.AudioProcessor"/>.
        /// </summary>
        public Array6<byte> Input;

        /// <summary>
        /// The output channel indices that will be used by the <see cref="Dsp.AudioProcessor"/>.
        /// </summary>
        public Array6<byte> Output;

        /// <summary>
        /// The maximum number of channels supported.
        /// </summary>
        public ushort ChannelCountMax;

        /// <summary>
        /// The total channel count used.
        /// </summary>
        public ushort ChannelCount;

        /// <summary>
        /// The target sample rate.
        /// </summary>
        /// <remarks>This is in kHz.</remarks>
        public int SampleRate;

        /// <summary>
        /// The threshold.
        /// </summary>
        public float Threshold;

        /// <summary>
        /// The compressor ratio.
        /// </summary>
        public float Ratio;

        /// <summary>
        /// The attack time.
        /// <remarks>This is in microseconds.</remarks>
        /// </summary>
        public int AttackTime;

        /// <summary>
        /// The release time.
        /// <remarks>This is in microseconds.</remarks>
        /// </summary>
        public int ReleaseTime;

        /// <summary>
        /// The input gain.
        /// </summary>
        public float InputGain;

        /// <summary>
        /// The attack coefficient.
        /// </summary>
        public float AttackCoefficient;

        /// <summary>
        /// The release coefficient.
        /// </summary>
        public float ReleaseCoefficient;

        /// <summary>
        /// The output gain.
        /// </summary>
        public float OutputGain;

        /// <summary>
        /// The current usage status of the effect on the client side.
        /// </summary>
        public UsageState Status;

        /// <summary>
        /// Indicate if the makeup gain should be used.
        /// </summary>
        [MarshalAs(UnmanagedType.I1)]
        public bool MakeupGainEnabled;

        /// <summary>
        /// Indicate if the compressor effect should output statistics.
        /// </summary>
        [MarshalAs(UnmanagedType.I1)]
        public bool StatisticsEnabled;

        /// <summary>
        /// Indicate to the DSP that the user did a statistics reset.
        /// </summary>
        [MarshalAs(UnmanagedType.I1)]
        public bool StatisticsReset;

        /// <summary>
        /// Check if the <see cref="ChannelCount"/> is valid.
        /// </summary>
        /// <returns>Returns true if the <see cref="ChannelCount"/> is valid.</returns>
        public readonly bool IsChannelCountValid()
        {
            return EffectInParameterVersion1.IsChannelCountValid(ChannelCount);
        }

        /// <summary>
        /// Check if the <see cref="ChannelCountMax"/> is valid.
        /// </summary>
        /// <returns>Returns true if the <see cref="ChannelCountMax"/> is valid.</returns>
        public readonly bool IsChannelCountMaxValid()
        {
            return EffectInParameterVersion1.IsChannelCountValid(ChannelCountMax);
        }
    }
}
