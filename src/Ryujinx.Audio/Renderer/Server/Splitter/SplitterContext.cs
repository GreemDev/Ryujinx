using Ryujinx.Audio.Renderer.Common;
using Ryujinx.Audio.Renderer.Dsp.State;
using Ryujinx.Audio.Renderer.Parameter;
using Ryujinx.Audio.Renderer.Utils;
using Ryujinx.Common;
using Ryujinx.Common.Extensions;
using System;
using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Ryujinx.Audio.Renderer.Server.Splitter
{
    /// <summary>
    /// Splitter context.
    /// </summary>
    public class SplitterContext
    {
        /// <summary>
        /// Amount of biquad filter states per splitter destination.
        /// </summary>
        public const int BqfStatesPerDestination = 4;

        /// <summary>
        /// Storage for <see cref="SplitterState"/>.
        /// </summary>
        private Memory<SplitterState> _splitters;

        /// <summary>
        /// Storage for <see cref="SplitterDestinationVersion1"/>.
        /// </summary>
        private Memory<SplitterDestinationVersion1> _splitterDestinationsV1;

        /// <summary>
        /// Storage for <see cref="SplitterDestinationVersion2"/>.
        /// </summary>
        private Memory<SplitterDestinationVersion2> _splitterDestinationsV2;

        /// <summary>
        /// Splitter biquad filtering states.
        /// </summary>
        private Memory<BiquadFilterState> _splitterBqfStates;

        /// <summary>
        /// Version of the splitter context that is being used, currently can be 1 or 2.
        /// </summary>
        public int Version { get; private set; }

        /// <summary>
        /// If set to true, trust the user destination count in <see cref="SplitterState.Update(SplitterContext, in SplitterInParameter, ref SequenceReader{byte})"/>.
        /// </summary>
        public bool IsBugFixed { get; private set; }

        /// <summary>
        /// If set to true, the previous mix volume is explicitly resetted using the input parameter, instead of implicitly on first use.
        /// </summary>
        public bool IsSplitterPrevVolumeResetSupported { get; private set; }

        /// <summary>
        /// Initialize <see cref="SplitterContext"/>.
        /// </summary>
        /// <param name="behaviourContext">The behaviour context.</param>
        /// <param name="parameter">The audio renderer configuration.</param>
        /// <param name="workBufferAllocator">The <see cref="WorkBufferAllocator"/>.</param>
        /// <param name="splitterBqfStates">Memory to store the biquad filtering state for splitters during processing.</param>
        /// <returns>Return true if the initialization was successful.</returns>
        public bool Initialize(
            ref BehaviourContext behaviourContext,
            ref AudioRendererConfiguration parameter,
            WorkBufferAllocator workBufferAllocator,
            Memory<BiquadFilterState> splitterBqfStates)
        {
            if (!behaviourContext.IsSplitterSupported() || parameter.SplitterCount <= 0 || parameter.SplitterDestinationCount <= 0)
            {
                Setup(Memory<SplitterState>.Empty, Memory<SplitterDestinationVersion1>.Empty, Memory<SplitterDestinationVersion2>.Empty, false);

                return true;
            }

            Memory<SplitterState> splitters = workBufferAllocator.Allocate<SplitterState>(parameter.SplitterCount, SplitterState.Alignment);

            if (splitters.IsEmpty)
            {
                return false;
            }

            int splitterId = 0;

            foreach (ref SplitterState splitter in splitters.Span)
            {
                splitter = new SplitterState(splitterId++);
            }

            Memory<SplitterDestinationVersion1> splitterDestinationsV1 = Memory<SplitterDestinationVersion1>.Empty;
            Memory<SplitterDestinationVersion2> splitterDestinationsV2 = Memory<SplitterDestinationVersion2>.Empty;

            if (!behaviourContext.IsBiquadFilterParameterForSplitterEnabled())
            {
                Version = 1;

                splitterDestinationsV1 = workBufferAllocator.Allocate<SplitterDestinationVersion1>(parameter.SplitterDestinationCount,
                    SplitterDestinationVersion1.Alignment);

                if (splitterDestinationsV1.IsEmpty)
                {
                    return false;
                }

                int splitterDestinationId = 0;
                foreach (ref SplitterDestinationVersion1 data in splitterDestinationsV1.Span)
                {
                    data = new SplitterDestinationVersion1(splitterDestinationId++);
                }
            }
            else
            {
                Version = 2;

                splitterDestinationsV2 = workBufferAllocator.Allocate<SplitterDestinationVersion2>(parameter.SplitterDestinationCount,
                    SplitterDestinationVersion2.Alignment);

                if (splitterDestinationsV2.IsEmpty)
                {
                    return false;
                }

                int splitterDestinationId = 0;
                foreach (ref SplitterDestinationVersion2 data in splitterDestinationsV2.Span)
                {
                    data = new SplitterDestinationVersion2(splitterDestinationId++);
                }

                if (parameter.SplitterDestinationCount > 0)
                {
                    // Official code stores it in the SplitterDestinationVersion2 struct,
                    // but we don't to avoid using unsafe code.

                    splitterBqfStates.Span.Clear();
                    _splitterBqfStates = splitterBqfStates;
                }
                else
                {
                    _splitterBqfStates = Memory<BiquadFilterState>.Empty;
                }
            }

            IsSplitterPrevVolumeResetSupported = behaviourContext.IsSplitterPrevVolumeResetSupported();

            SplitterState.InitializeSplitters(splitters.Span);

            Setup(splitters, splitterDestinationsV1, splitterDestinationsV2, behaviourContext.IsSplitterBugFixed());

            return true;
        }

        /// <summary>
        /// Get the work buffer size while adding the size needed for splitter to operate.
        /// </summary>
        /// <param name="size">The current size.</param>
        /// <param name="behaviourContext">The behaviour context.</param>
        /// <param name="parameter">The renderer configuration.</param>
        /// <returns>Return the new size taking splitter into account.</returns>
        public static ulong GetWorkBufferSize(ulong size, ref BehaviourContext behaviourContext, ref AudioRendererConfiguration parameter)
        {
            if (behaviourContext.IsSplitterSupported())
            {
                size = WorkBufferAllocator.GetTargetSize<SplitterState>(size, parameter.SplitterCount, SplitterState.Alignment);

                if (behaviourContext.IsBiquadFilterParameterForSplitterEnabled())
                {
                    size = WorkBufferAllocator.GetTargetSize<SplitterDestinationVersion2>(size, parameter.SplitterDestinationCount, SplitterDestinationVersion2.Alignment);
                }
                else
                {
                    size = WorkBufferAllocator.GetTargetSize<SplitterDestinationVersion1>(size, parameter.SplitterDestinationCount, SplitterDestinationVersion1.Alignment);
                }

                if (behaviourContext.IsSplitterBugFixed())
                {
                    size = WorkBufferAllocator.GetTargetSize<int>(size, parameter.SplitterDestinationCount, 0x10);
                }

                return size;
            }

            return size;
        }

        /// <summary>
        /// Setup the <see cref="SplitterContext"/> instance.
        /// </summary>
        /// <param name="splitters">The <see cref="SplitterState"/> storage.</param>
        /// <param name="splitterDestinationsV1">The <see cref="SplitterDestinationVersion1"/> storage.</param>
        /// <param name="splitterDestinationsV2">The <see cref="SplitterDestinationVersion2"/> storage.</param>
        /// <param name="isBugFixed">If set to true, trust the user destination count in <see cref="SplitterState.Update(SplitterContext, in SplitterInParameter, ref SequenceReader{byte})"/>.</param>
        private void Setup(
            Memory<SplitterState> splitters,
            Memory<SplitterDestinationVersion1> splitterDestinationsV1,
            Memory<SplitterDestinationVersion2> splitterDestinationsV2,
            bool isBugFixed)
        {
            _splitters = splitters;
            _splitterDestinationsV1 = splitterDestinationsV1;
            _splitterDestinationsV2 = splitterDestinationsV2;
            IsBugFixed = isBugFixed;
        }

        /// <summary>
        /// Clear the new connection flag.
        /// </summary>
        private void ClearAllNewConnectionFlag()
        {
            foreach (ref SplitterState splitter in _splitters.Span)
            {
                splitter.ClearNewConnectionFlag();
            }
        }

        /// <summary>
        /// Get the destination count using the count of splitter.
        /// </summary>
        /// <returns>The destination count using the count of splitter.</returns>
        public int GetDestinationCountPerStateForCompatibility()
        {
            if (_splitters.IsEmpty)
            {
                return 0;
            }

            int length = _splitterDestinationsV2.IsEmpty ? _splitterDestinationsV1.Length : _splitterDestinationsV2.Length;

            return length / _splitters.Length;
        }

        /// <summary>
        /// Update one or multiple <see cref="SplitterState"/> from user parameters.
        /// </summary>
        /// <param name="inputHeader">The splitter header.</param>
        /// <param name="input">The raw data after the splitter header.</param>
        private void UpdateState(in SplitterInParameterHeader inputHeader, ref SequenceReader<byte> input)
        {
            for (int i = 0; i < inputHeader.SplitterCount; i++)
            {
                ref readonly SplitterInParameter parameter = ref input.GetRefOrRefToCopy<SplitterInParameter>(out _);

                Debug.Assert(parameter.IsMagicValid());

                if (parameter.IsMagicValid())
                {
                    if (parameter.Id >= 0 && parameter.Id < _splitters.Length)
                    {
                        ref SplitterState splitter = ref GetState(parameter.Id);

                        splitter.Update(this, in parameter, ref input);
                    }

                    // NOTE: there are 12 bytes of unused/unknown data after the destination IDs array.
                    input.Advance(0xC);
                }
                else
                {
                    input.Rewind(Unsafe.SizeOf<SplitterInParameter>());
                    break;
                }
            }
        }

        /// <summary>
        /// Update one splitter destination data from user parameters.
        /// </summary>
        /// <param name="input">The raw data after the splitter header.</param>
        /// <returns>True if the update was successful, false otherwise</returns>
        private bool UpdateData<T>(ref SequenceReader<byte> input) where T : unmanaged, ISplitterDestinationInParameter
        {
            ref readonly T parameter = ref input.GetRefOrRefToCopy<T>(out _);

            Debug.Assert(parameter.IsMagicValid());

            if (parameter.IsMagicValid())
            {
                int length = _splitterDestinationsV2.IsEmpty ? _splitterDestinationsV1.Length : _splitterDestinationsV2.Length;

                if (parameter.Id >= 0 && parameter.Id < length)
                {
                    SplitterDestination destination = GetDestination(parameter.Id);

                    destination.Update(parameter, IsSplitterPrevVolumeResetSupported);
                }

                return true;
            }
            else
            {
                input.Rewind(Unsafe.SizeOf<T>());

                return false;
            }
        }

        /// <summary>
        /// Update one or multiple splitter destination data from user parameters.
        /// </summary>
        /// <param name="inputHeader">The splitter header.</param>
        /// <param name="input">The raw data after the splitter header.</param>
        private void UpdateData(in SplitterInParameterHeader inputHeader, ref SequenceReader<byte> input)
        {
            for (int i = 0; i < inputHeader.SplitterDestinationCount; i++)
            {
                if (Version == 1)
                {
                    if (!UpdateData<SplitterDestinationInParameterVersion1>(ref input))
                    {
                        break;
                    }
                }
                else if (Version == 2)
                {
                    if (!UpdateData<SplitterDestinationInParameterVersion2>(ref input))
                    {
                        break;
                    }
                }
                else
                {
                    Debug.Fail($"Invalid splitter context version {Version}.");
                }
            }
        }

        /// <summary>
        /// Update splitter from user parameters.
        /// </summary>
        /// <param name="input">The input raw user data.</param>
        /// <returns>Return true if the update was successful.</returns>
        public bool Update(ref SequenceReader<byte> input)
        {
            if (!UsingSplitter())
            {
                return true;
            }

            ref readonly SplitterInParameterHeader header = ref input.GetRefOrRefToCopy<SplitterInParameterHeader>(out _);

            if (header.IsMagicValid())
            {
                ClearAllNewConnectionFlag();

                UpdateState(in header, ref input);
                UpdateData(in header, ref input);

                input.SetConsumed(BitUtils.AlignUp(input.Consumed, 0x10));

                return true;
            }
            else
            {
                input.Rewind(Unsafe.SizeOf<SplitterInParameterHeader>());

                return false;
            }
        }

        /// <summary>
        /// Get a reference to a <see cref="SplitterState"/> at the given <paramref name="id"/>.
        /// </summary>
        /// <param name="id">The index to use.</param>
        /// <returns>A reference to a <see cref="SplitterState"/> at the given <paramref name="id"/>.</returns>
        public ref SplitterState GetState(int id)
        {
            return ref SpanIOHelper.GetFromMemory(_splitters, id, (uint)_splitters.Length);
        }

        /// <summary>
        /// Get a reference to the splitter destination data at the given <paramref name="id"/>.
        /// </summary>
        /// <param name="id">The index to use.</param>
        /// <returns>A reference to the splitter destination data at the given <paramref name="id"/>.</returns>
        public SplitterDestination GetDestination(int id)
        {
            if (_splitterDestinationsV2.IsEmpty)
            {
                return new SplitterDestination(ref SpanIOHelper.GetFromMemory(_splitterDestinationsV1, id, (uint)_splitterDestinationsV1.Length));
            }
            else
            {
                return new SplitterDestination(ref SpanIOHelper.GetFromMemory(_splitterDestinationsV2, id, (uint)_splitterDestinationsV2.Length));
            }
        }

        /// <summary>
        /// Get a <see cref="SplitterDestination"/> in the <see cref="SplitterState"/> at <paramref name="id"/> and pass <paramref name="destinationId"/> to <see cref="SplitterState.GetData(int)"/>.
        /// </summary>
        /// <param name="id">The index to use to get the <see cref="SplitterState"/>.</param>
        /// <param name="destinationId">The index of the <see cref="SplitterDestination"/>.</param>
        /// <returns>A <see cref="SplitterDestination"/>.</returns>
        public SplitterDestination GetDestination(int id, int destinationId)
        {
            ref SplitterState splitter = ref GetState(id);

            return splitter.GetData(destinationId);
        }

        /// <summary>
        /// Gets the biquad filter state for a given splitter destination.
        /// </summary>
        /// <param name="destination">The splitter destination.</param>
        /// <returns>Biquad filter state for the specified destination.</returns>
        public Memory<BiquadFilterState> GetBiquadFilterState(SplitterDestination destination)
        {
            return _splitterBqfStates.Slice(destination.Id * BqfStatesPerDestination, BqfStatesPerDestination);
        }

        /// <summary>
        /// Return true if the audio renderer has any splitters.
        /// </summary>
        /// <returns>True if the audio renderer has any splitters.</returns>
        public bool UsingSplitter()
        {
            return !_splitters.IsEmpty && (!_splitterDestinationsV1.IsEmpty || !_splitterDestinationsV2.IsEmpty);
        }

        /// <summary>
        /// Update the internal state of all splitters.
        /// </summary>
        public void UpdateInternalState()
        {
            foreach (ref SplitterState splitter in _splitters.Span)
            {
                splitter.UpdateInternalState();
            }
        }
    }
}
