using Ryujinx.Audio.Renderer.Common;
using Ryujinx.Audio.Renderer.Dsp.State;
using Ryujinx.Audio.Renderer.Parameter;
using Ryujinx.Audio.Renderer.Parameter.Effect;
using Ryujinx.Audio.Renderer.Server.MemoryPool;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Ryujinx.Audio.Renderer.Server.Effect
{
    /// <summary>
    /// Server state for a compressor effect.
    /// </summary>
    public class CompressorEffect : BaseEffect
    {
        /// <summary>
        /// The compressor parameter.
        /// </summary>
        public CompressorParameter Parameter;

        /// <summary>
        /// The compressor state.
        /// </summary>
        public Memory<CompressorState> State { get; }

        /// <summary>
        /// Create a new <see cref="CompressorEffect"/>.
        /// </summary>
        public CompressorEffect()
        {
            State = new CompressorState[1];
        }

        public override EffectType TargetEffectType => EffectType.Compressor;

        public override ulong GetWorkBuffer(int index)
        {
            return GetSingleBuffer();
        }

        public override void Update(out BehaviourParameter.ErrorInfo updateErrorInfo, in EffectInParameterVersion1 parameter, PoolMapper mapper)
        {
            // Nintendo doesn't do anything here but we still require updateErrorInfo to be initialised.
            updateErrorInfo = new BehaviourParameter.ErrorInfo();
        }

        public override void Update(out BehaviourParameter.ErrorInfo updateErrorInfo, in EffectInParameterVersion2 parameter, PoolMapper mapper)
        {
            Debug.Assert(IsTypeValid(in parameter));

            UpdateParameterBase(in parameter);

            Parameter = MemoryMarshal.Cast<byte, CompressorParameter>(parameter.SpecificData)[0];
            IsEnabled = parameter.IsEnabled;

            updateErrorInfo = new BehaviourParameter.ErrorInfo();
        }

        public override void UpdateForCommandGeneration()
        {
            UpdateUsageStateForCommandGeneration();

            Parameter.Status = UsageState.Enabled;
            Parameter.StatisticsReset = false;
        }

        public override void InitializeResultState(ref EffectResultState state)
        {
            ref CompressorStatistics statistics = ref MemoryMarshal.Cast<byte, CompressorStatistics>(state.SpecificData)[0];

            statistics.Reset(Parameter.ChannelCount);
        }

        public override void UpdateResultState(ref EffectResultState destState, ref EffectResultState srcState)
        {
            destState = srcState;
        }
    }
}
