using Ryujinx.Common.Memory;
using System;

namespace Ryujinx.Audio.Renderer.Parameter
{
    /// <summary>
    /// Generic interface for the splitter destination parameters.
    /// </summary>
    public interface ISplitterDestinationInParameter
    {
        /// <summary>
        /// Target splitter destination data id.
        /// </summary>
        int Id { get; }

        /// <summary>
        /// The mix to output the result of the splitter.
        /// </summary>
        int DestinationId { get; }

        /// <summary>
        /// Biquad filter parameters.
        /// </summary>
        Array2<BiquadFilterParameter> BiquadFilters { get; }

        /// <summary>
        /// Set to true if in use.
        /// </summary>
        bool IsUsed { get; }

        /// <summary>
        /// Set to true to force resetting the previous mix volumes.
        /// </summary>
        bool ResetPrevVolume { get; }

        /// <summary>
        /// Mix buffer volumes.
        /// </summary>
        /// <remarks>Used when a splitter id is specified in the mix.</remarks>
        Span<float> MixBufferVolume { get; }

        /// <summary>
        /// Check if the magic is valid.
        /// </summary>
        /// <returns>Returns true if the magic is valid.</returns>
        bool IsMagicValid();
    }
}
