using Ryujinx.Audio.Common;
using Ryujinx.Audio.Renderer.Common;
using Ryujinx.Audio.Renderer.Dsp.Command;
using Ryujinx.Audio.Renderer.Dsp.State;
using Ryujinx.Audio.Renderer.Parameter;
using Ryujinx.Audio.Renderer.Server.Effect;
using Ryujinx.Audio.Renderer.Server.Mix;
using Ryujinx.Audio.Renderer.Server.Performance;
using Ryujinx.Audio.Renderer.Server.Sink;
using Ryujinx.Audio.Renderer.Server.Splitter;
using Ryujinx.Audio.Renderer.Server.Voice;
using Ryujinx.Audio.Renderer.Utils;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Ryujinx.Audio.Renderer.Server
{
    public class CommandGenerator
    {
        private readonly CommandBuffer _commandBuffer;
        private readonly RendererSystemContext _rendererContext;
        private readonly VoiceContext _voiceContext;
        private readonly MixContext _mixContext;
        private readonly EffectContext _effectContext;
        private readonly SinkContext _sinkContext;
        private readonly SplitterContext _splitterContext;
        private readonly PerformanceManager _performanceManager;

        public CommandGenerator(CommandBuffer commandBuffer, RendererSystemContext rendererContext, VoiceContext voiceContext, MixContext mixContext, EffectContext effectContext, SinkContext sinkContext, SplitterContext splitterContext, PerformanceManager performanceManager)
        {
            _commandBuffer = commandBuffer;
            _rendererContext = rendererContext;
            _voiceContext = voiceContext;
            _mixContext = mixContext;
            _effectContext = effectContext;
            _sinkContext = sinkContext;
            _splitterContext = splitterContext;
            _performanceManager = performanceManager;

            _commandBuffer.GenerateClearMixBuffer(Constants.InvalidNodeId);
        }

        private void GenerateDataSource(ref VoiceState voiceState, Memory<VoiceUpdateState> dspState, int channelIndex)
        {
            if (voiceState.MixId != Constants.UnusedMixId)
            {
                ref MixState mix = ref _mixContext.GetState(voiceState.MixId);

                _commandBuffer.GenerateDepopPrepare(
                    dspState,
                    _rendererContext.DepopBuffer,
                    mix.BufferCount,
                    mix.BufferOffset,
                    voiceState.NodeId,
                    voiceState.WasPlaying);
            }
            else if (voiceState.SplitterId != Constants.UnusedSplitterId)
            {
                int destinationId = 0;

                while (true)
                {
                    SplitterDestination destination = _splitterContext.GetDestination((int)voiceState.SplitterId, destinationId++);

                    if (destination.IsNull)
                    {
                        break;
                    }

                    if (destination.IsConfigured())
                    {
                        int mixId = destination.DestinationId;

                        if (mixId < _mixContext.GetCount() && mixId != Constants.UnusedSplitterIdInt)
                        {
                            ref MixState mix = ref _mixContext.GetState(mixId);

                            _commandBuffer.GenerateDepopPrepare(
                                dspState,
                                _rendererContext.DepopBuffer,
                                mix.BufferCount,
                                mix.BufferOffset,
                                voiceState.NodeId,
                                voiceState.WasPlaying);

                            destination.MarkAsNeedToUpdateInternalState();
                        }
                    }
                }
            }

            if (!voiceState.WasPlaying)
            {
                Debug.Assert(voiceState.SampleFormat != SampleFormat.Adpcm || channelIndex == 0);

                if (_rendererContext.BehaviourContext.IsWaveBufferVersion2Supported())
                {
                    _commandBuffer.GenerateDataSourceVersion2(
                        ref voiceState,
                        dspState,
                        (ushort)_rendererContext.MixBufferCount,
                        (ushort)channelIndex,
                        voiceState.NodeId);
                }
                else
                {
                    switch (voiceState.SampleFormat)
                    {
                        case SampleFormat.PcmInt16:
                            _commandBuffer.GeneratePcmInt16DataSourceVersion1(
                                ref voiceState,
                                dspState,
                                (ushort)_rendererContext.MixBufferCount,
                                (ushort)channelIndex,
                                voiceState.NodeId);
                            break;
                        case SampleFormat.PcmFloat:
                            _commandBuffer.GeneratePcmFloatDataSourceVersion1(
                                ref voiceState,
                                dspState,
                                (ushort)_rendererContext.MixBufferCount,
                                (ushort)channelIndex,
                                voiceState.NodeId);
                            break;
                        case SampleFormat.Adpcm:
                            _commandBuffer.GenerateAdpcmDataSourceVersion1(
                                ref voiceState,
                                dspState,
                                (ushort)_rendererContext.MixBufferCount,
                                voiceState.NodeId);
                            break;
                        default:
                            throw new NotImplementedException($"Unsupported data source {voiceState.SampleFormat}");
                    }
                }
            }
        }

        private void GenerateBiquadFilterForVoice(ref VoiceState voiceState, Memory<VoiceUpdateState> state, int baseIndex, int bufferOffset, int nodeId)
        {
            bool supportsOptimizedPath = _rendererContext.BehaviourContext.UseMultiTapBiquadFilterProcessing();

            if (supportsOptimizedPath && voiceState.BiquadFilters[0].Enable && voiceState.BiquadFilters[1].Enable)
            {
                Memory<byte> biquadStateRawMemory = SpanMemoryManager<byte>.Cast(state)[..(Unsafe.SizeOf<BiquadFilterState>() * Constants.VoiceBiquadFilterCount)];
                Memory<BiquadFilterState> stateMemory = SpanMemoryManager<BiquadFilterState>.Cast(biquadStateRawMemory);

                _commandBuffer.GenerateMultiTapBiquadFilter(baseIndex, voiceState.BiquadFilters.AsSpan(), stateMemory, bufferOffset, bufferOffset, voiceState.BiquadFilterNeedInitialization, nodeId);
            }
            else
            {
                for (int i = 0; i < voiceState.BiquadFilters.Length; i++)
                {
                    ref BiquadFilterParameter filter = ref voiceState.BiquadFilters[i];

                    if (filter.Enable)
                    {
                        Memory<byte> biquadStateRawMemory = SpanMemoryManager<byte>.Cast(state)[..(Unsafe.SizeOf<BiquadFilterState>() * Constants.VoiceBiquadFilterCount)];
                        Memory<BiquadFilterState> stateMemory = SpanMemoryManager<BiquadFilterState>.Cast(biquadStateRawMemory);

                        _commandBuffer.GenerateBiquadFilter(
                            baseIndex,
                            ref filter,
                            stateMemory.Slice(i, 1),
                            bufferOffset,
                            bufferOffset,
                            !voiceState.BiquadFilterNeedInitialization[i],
                            nodeId);
                    }
                }
            }
        }

        private void GenerateVoiceMixWithSplitter(
            SplitterDestination destination,
            Memory<VoiceUpdateState> state,
            uint bufferOffset,
            uint bufferCount,
            uint bufferIndex,
            int nodeId)
        {
            ReadOnlySpan<float> mixVolumes = destination.MixBufferVolume;
            ReadOnlySpan<float> previousMixVolumes = destination.PreviousMixBufferVolume;

            ref BiquadFilterParameter bqf0 = ref destination.GetBiquadFilterParameter(0);
            ref BiquadFilterParameter bqf1 = ref destination.GetBiquadFilterParameter(1);

            Memory<BiquadFilterState> bqfState = _splitterContext.GetBiquadFilterState(destination);

            bool isFirstMixBuffer = true;

            for (int i = 0; i < bufferCount; i++)
            {
                float previousMixVolume = previousMixVolumes[i];
                float mixVolume = mixVolumes[i];

                if (mixVolume != 0.0f || previousMixVolume != 0.0f)
                {
                    if (bqf0.Enable && bqf1.Enable)
                    {
                        _commandBuffer.GenerateMultiTapBiquadFilterAndMix(
                            previousMixVolume,
                            mixVolume,
                            bufferIndex,
                            bufferOffset + (uint)i,
                            i,
                            state,
                            ref bqf0,
                            ref bqf1,
                            bqfState[..1],
                            bqfState.Slice(1, 1),
                            bqfState.Slice(2, 1),
                            bqfState.Slice(3, 1),
                            !destination.IsBiquadFilterEnabledPrev(),
                            !destination.IsBiquadFilterEnabledPrev(),
                            true,
                            isFirstMixBuffer,
                            nodeId);

                        destination.UpdateBiquadFilterEnabledPrev(0);
                        destination.UpdateBiquadFilterEnabledPrev(1);
                    }
                    else if (bqf0.Enable)
                    {
                        _commandBuffer.GenerateBiquadFilterAndMix(
                            previousMixVolume,
                            mixVolume,
                            bufferIndex,
                            bufferOffset + (uint)i,
                            i,
                            state,
                            ref bqf0,
                            bqfState[..1],
                            bqfState.Slice(1, 1),
                            !destination.IsBiquadFilterEnabledPrev(),
                            true,
                            isFirstMixBuffer,
                            nodeId);

                        destination.UpdateBiquadFilterEnabledPrev(0);
                    }
                    else if (bqf1.Enable)
                    {
                        _commandBuffer.GenerateBiquadFilterAndMix(
                            previousMixVolume,
                            mixVolume,
                            bufferIndex,
                            bufferOffset + (uint)i,
                            i,
                            state,
                            ref bqf1,
                            bqfState[..1],
                            bqfState.Slice(1, 1),
                            !destination.IsBiquadFilterEnabledPrev(),
                            true,
                            isFirstMixBuffer,
                            nodeId);

                        destination.UpdateBiquadFilterEnabledPrev(1);
                    }

                    isFirstMixBuffer = false;
                }
            }
        }

        private void GenerateVoiceMix(
            ReadOnlySpan<float> mixVolumes,
            ReadOnlySpan<float> previousMixVolumes,
            Memory<VoiceUpdateState> state,
            uint bufferOffset,
            uint bufferCount,
            uint bufferIndex,
            int nodeId)
        {
            if (bufferCount > Constants.VoiceChannelCountMax)
            {
                _commandBuffer.GenerateMixRampGrouped(
                    bufferCount,
                    bufferIndex,
                    bufferOffset,
                    previousMixVolumes,
                    mixVolumes,
                    state,
                    nodeId);
            }
            else
            {
                for (int i = 0; i < bufferCount; i++)
                {
                    float previousMixVolume = previousMixVolumes[i];
                    float mixVolume = mixVolumes[i];

                    if (mixVolume != 0.0f || previousMixVolume != 0.0f)
                    {
                        _commandBuffer.GenerateMixRamp(
                            previousMixVolume,
                            mixVolume,
                            bufferIndex,
                            bufferOffset + (uint)i,
                            i,
                            state,
                            nodeId);
                    }
                }
            }
        }

        private void GenerateVoice(ref VoiceState voiceState)
        {
            int nodeId = voiceState.NodeId;
            uint channelsCount = voiceState.ChannelsCount;

            for (int channelIndex = 0; channelIndex < channelsCount; channelIndex++)
            {
                Memory<VoiceUpdateState> dspStateMemory = _voiceContext.GetUpdateStateForDsp(voiceState.ChannelResourceIds[channelIndex]);

                ref VoiceChannelResource channelResource = ref _voiceContext.GetChannelResource(voiceState.ChannelResourceIds[channelIndex]);

                PerformanceDetailType dataSourceDetailType = PerformanceDetailType.Adpcm;

                if (voiceState.SampleFormat == SampleFormat.PcmInt16)
                {
                    dataSourceDetailType = PerformanceDetailType.PcmInt16;
                }
                else if (voiceState.SampleFormat == SampleFormat.PcmFloat)
                {
                    dataSourceDetailType = PerformanceDetailType.PcmFloat;
                }

                bool performanceInitialized = false;

                PerformanceEntryAddresses performanceEntry = new();

                if (_performanceManager != null && _performanceManager.IsTargetNodeId(nodeId) && _performanceManager.GetNextEntry(out performanceEntry, dataSourceDetailType, PerformanceEntryType.Voice, nodeId))
                {
                    performanceInitialized = true;

                    GeneratePerformance(ref performanceEntry, PerformanceCommand.Type.Start, nodeId);
                }

                GenerateDataSource(ref voiceState, dspStateMemory, channelIndex);

                if (performanceInitialized)
                {
                    GeneratePerformance(ref performanceEntry, PerformanceCommand.Type.End, nodeId);
                }

                if (voiceState.WasPlaying)
                {
                    voiceState.PreviousVolume = 0.0f;
                }
                else if (voiceState.HasAnyDestination())
                {
                    performanceInitialized = false;

                    if (_performanceManager != null && _performanceManager.IsTargetNodeId(nodeId) && _performanceManager.GetNextEntry(out performanceEntry, PerformanceDetailType.BiquadFilter, PerformanceEntryType.Voice, nodeId))
                    {
                        performanceInitialized = true;

                        GeneratePerformance(ref performanceEntry, PerformanceCommand.Type.Start, nodeId);
                    }

                    GenerateBiquadFilterForVoice(ref voiceState, dspStateMemory, (int)_rendererContext.MixBufferCount, channelIndex, nodeId);

                    if (performanceInitialized)
                    {
                        GeneratePerformance(ref performanceEntry, PerformanceCommand.Type.End, nodeId);
                    }

                    performanceInitialized = false;

                    if (_performanceManager != null && _performanceManager.IsTargetNodeId(nodeId) && _performanceManager.GetNextEntry(out performanceEntry, PerformanceDetailType.VolumeRamp, PerformanceEntryType.Voice, nodeId))
                    {
                        performanceInitialized = true;

                        GeneratePerformance(ref performanceEntry, PerformanceCommand.Type.Start, nodeId);
                    }

                    _commandBuffer.GenerateVolumeRamp(
                        voiceState.PreviousVolume,
                        voiceState.Volume,
                        _rendererContext.MixBufferCount + (uint)channelIndex,
                        nodeId);

                    if (performanceInitialized)
                    {
                        GeneratePerformance(ref performanceEntry, PerformanceCommand.Type.End, nodeId);
                    }

                    voiceState.PreviousVolume = voiceState.Volume;

                    if (voiceState.MixId == Constants.UnusedMixId)
                    {
                        if (voiceState.SplitterId != Constants.UnusedSplitterId)
                        {
                            int destinationId = channelIndex;

                            while (true)
                            {
                                SplitterDestination destination = _splitterContext.GetDestination((int)voiceState.SplitterId, destinationId);

                                if (destination.IsNull)
                                {
                                    break;
                                }

                                destinationId += (int)channelsCount;

                                if (destination.IsConfigured())
                                {
                                    int mixId = destination.DestinationId;

                                    if (mixId < _mixContext.GetCount() && mixId != Constants.UnusedSplitterIdInt)
                                    {
                                        ref MixState mix = ref _mixContext.GetState(mixId);

                                        if (destination.IsBiquadFilterEnabled())
                                        {
                                            GenerateVoiceMixWithSplitter(
                                                destination,
                                                dspStateMemory,
                                                mix.BufferOffset,
                                                mix.BufferCount,
                                                _rendererContext.MixBufferCount + (uint)channelIndex,
                                                nodeId);
                                        }
                                        else
                                        {
                                            GenerateVoiceMix(
                                                destination.MixBufferVolume,
                                                destination.PreviousMixBufferVolume,
                                                dspStateMemory,
                                                mix.BufferOffset,
                                                mix.BufferCount,
                                                _rendererContext.MixBufferCount + (uint)channelIndex,
                                                nodeId);
                                        }

                                        destination.MarkAsNeedToUpdateInternalState();
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        ref MixState mix = ref _mixContext.GetState(voiceState.MixId);

                        performanceInitialized = false;

                        if (_performanceManager != null && _performanceManager.IsTargetNodeId(nodeId) && _performanceManager.GetNextEntry(out performanceEntry, PerformanceDetailType.Mix, PerformanceEntryType.Voice, nodeId))
                        {
                            performanceInitialized = true;

                            GeneratePerformance(ref performanceEntry, PerformanceCommand.Type.Start, nodeId);
                        }

                        GenerateVoiceMix(
                            channelResource.Mix.AsSpan(),
                            channelResource.PreviousMix.AsSpan(),
                            dspStateMemory,
                            mix.BufferOffset,
                            mix.BufferCount,
                            _rendererContext.MixBufferCount + (uint)channelIndex,
                            nodeId);

                        if (performanceInitialized)
                        {
                            GeneratePerformance(ref performanceEntry, PerformanceCommand.Type.End, nodeId);
                        }

                        channelResource.UpdateState();
                    }

                    for (int i = 0; i < voiceState.BiquadFilterNeedInitialization.Length; i++)
                    {
                        voiceState.BiquadFilterNeedInitialization[i] = voiceState.BiquadFilters[i].Enable;
                    }
                }
            }
        }

        public void GenerateVoices()
        {
            for (int i = 0; i < _voiceContext.GetCount(); i++)
            {
                ref VoiceState sortedState = ref _voiceContext.GetSortedState(i);

                if (!sortedState.ShouldSkip() && sortedState.UpdateForCommandGeneration(_voiceContext))
                {
                    int nodeId = sortedState.NodeId;

                    PerformanceEntryAddresses performanceEntry = new();

                    bool performanceInitialized = false;

                    if (_performanceManager != null && _performanceManager.GetNextEntry(out performanceEntry, PerformanceEntryType.Voice, nodeId))
                    {
                        performanceInitialized = true;

                        GeneratePerformance(ref performanceEntry, PerformanceCommand.Type.Start, nodeId);
                    }

                    GenerateVoice(ref sortedState);

                    if (performanceInitialized)
                    {
                        GeneratePerformance(ref performanceEntry, PerformanceCommand.Type.End, nodeId);
                    }
                }
            }

            _splitterContext.UpdateInternalState();
        }

        public void GeneratePerformance(ref PerformanceEntryAddresses performanceEntryAddresses, PerformanceCommand.Type type, int nodeId)
        {
            _commandBuffer.GeneratePerformance(ref performanceEntryAddresses, type, nodeId);
        }

        private void GenerateBufferMixerEffect(int bufferOffset, BufferMixEffect effect, int nodeId)
        {
            Debug.Assert(effect.Type == EffectType.BufferMix);

            if (effect.IsEnabled)
            {
                for (int i = 0; i < effect.Parameter.MixesCount; i++)
                {
                    if (effect.Parameter.Volumes[i] != 0.0f)
                    {
                        _commandBuffer.GenerateMix(
                            (uint)bufferOffset + effect.Parameter.Input[i],
                            (uint)bufferOffset + effect.Parameter.Output[i],
                            nodeId,
                            effect.Parameter.Volumes[i]);
                    }
                }
            }
        }

        private void GenerateAuxEffect(uint bufferOffset, AuxiliaryBufferEffect effect, int nodeId)
        {
            Debug.Assert(effect.Type == EffectType.AuxiliaryBuffer);

            if (effect.IsEnabled)
            {
                effect.GetWorkBuffer(0);
                effect.GetWorkBuffer(1);
            }

            if (effect.State.SendBufferInfoBase != 0 && effect.State.ReturnBufferInfoBase != 0)
            {
                int i = 0;
                uint writeOffset = 0;
                for (uint channelIndex = effect.Parameter.ChannelCount; channelIndex != 0; channelIndex--)
                {
                    uint newUpdateCount = writeOffset + _commandBuffer.CommandList.SampleCount;

                    uint updateCount;

                    if (channelIndex != 1)
                    {
                        updateCount = 0;
                    }
                    else
                    {
                        updateCount = newUpdateCount;
                    }

                    _commandBuffer.GenerateAuxEffect(
                        bufferOffset,
                        effect.Parameter.Input[i],
                        effect.Parameter.Output[i],
                        ref effect.State,
                        effect.IsEnabled,
                        effect.Parameter.BufferStorageSize,
                        effect.State.SendBufferInfoBase,
                        effect.State.ReturnBufferInfoBase,
                        updateCount,
                        writeOffset,
                        nodeId);

                    writeOffset = newUpdateCount;

                    i++;
                }
            }
        }

        private void GenerateDelayEffect(uint bufferOffset, DelayEffect effect, int nodeId, bool newEffectChannelMappingSupported)
        {
            Debug.Assert(effect.Type == EffectType.Delay);

            ulong workBuffer = effect.GetWorkBuffer(-1);

            _commandBuffer.GenerateDelayEffect(bufferOffset, effect.Parameter, effect.State, effect.IsEnabled, workBuffer, nodeId, newEffectChannelMappingSupported);
        }

        private void GenerateReverbEffect(uint bufferOffset, ReverbEffect effect, int nodeId, bool isLongSizePreDelaySupported, bool newEffectChannelMappingSupported)
        {
            Debug.Assert(effect.Type == EffectType.Reverb);

            ulong workBuffer = effect.GetWorkBuffer(-1);

            _commandBuffer.GenerateReverbEffect(bufferOffset, effect.Parameter, effect.State, effect.IsEnabled, workBuffer, nodeId, isLongSizePreDelaySupported, newEffectChannelMappingSupported);
        }

        private void GenerateReverb3dEffect(uint bufferOffset, Reverb3dEffect effect, int nodeId, bool newEffectChannelMappingSupported)
        {
            Debug.Assert(effect.Type == EffectType.Reverb3d);

            ulong workBuffer = effect.GetWorkBuffer(-1);

            _commandBuffer.GenerateReverb3dEffect(bufferOffset, effect.Parameter, effect.State, effect.IsEnabled, workBuffer, nodeId, newEffectChannelMappingSupported);
        }

        private void GenerateBiquadFilterEffect(uint bufferOffset, BiquadFilterEffect effect, int nodeId)
        {
            Debug.Assert(effect.Type == EffectType.BiquadFilter);

            if (effect.IsEnabled)
            {
                bool needInitialization = effect.Parameter.Status == UsageState.Invalid ||
                    (effect.Parameter.Status == UsageState.New && !_rendererContext.BehaviourContext.IsBiquadFilterEffectStateClearBugFixed());

                BiquadFilterParameter parameter = new()
                {
                    Enable = true,
                };

                effect.Parameter.Denominator.AsSpan().CopyTo(parameter.Denominator.AsSpan());
                effect.Parameter.Numerator.AsSpan().CopyTo(parameter.Numerator.AsSpan());

                for (int i = 0; i < effect.Parameter.ChannelCount; i++)
                {
                    _commandBuffer.GenerateBiquadFilter(
                        (int)bufferOffset,
                        ref parameter,
                        effect.State.Slice(i, 1),
                        effect.Parameter.Input[i],
                        effect.Parameter.Output[i],
                        needInitialization,
                        nodeId);
                }
            }
            else
            {
                for (int i = 0; i < effect.Parameter.ChannelCount; i++)
                {
                    uint inputBufferIndex = bufferOffset + effect.Parameter.Input[i];
                    uint outputBufferIndex = bufferOffset + effect.Parameter.Output[i];

                    // If the input and output isn't the same, generate a command.
                    if (inputBufferIndex != outputBufferIndex)
                    {
                        _commandBuffer.GenerateCopyMixBuffer(inputBufferIndex, outputBufferIndex, nodeId);
                    }
                }
            }
        }

        private void GenerateLimiterEffect(uint bufferOffset, LimiterEffect effect, int nodeId, int effectId)
        {
            Debug.Assert(effect.Type == EffectType.Limiter);

            ulong workBuffer = effect.GetWorkBuffer(-1);

            if (_rendererContext.BehaviourContext.IsEffectInfoVersion2Supported())
            {
                Memory<EffectResultState> dspResultState;

                if (effect.Parameter.StatisticsEnabled)
                {
                    dspResultState = _effectContext.GetDspStateMemory(effectId);
                }
                else
                {
                    dspResultState = Memory<EffectResultState>.Empty;
                }

                _commandBuffer.GenerateLimiterEffectVersion2(bufferOffset, effect.Parameter, effect.State, dspResultState, effect.IsEnabled, workBuffer, nodeId);
            }
            else
            {
                _commandBuffer.GenerateLimiterEffectVersion1(bufferOffset, effect.Parameter, effect.State, effect.IsEnabled, workBuffer, nodeId);
            }
        }

        private void GenerateCaptureEffect(uint bufferOffset, CaptureBufferEffect effect, int nodeId)
        {
            Debug.Assert(effect.Type == EffectType.CaptureBuffer);

            if (effect.IsEnabled)
            {
                effect.GetWorkBuffer(0);
            }

            if (effect.State.SendBufferInfoBase != 0)
            {
                int i = 0;
                uint writeOffset = 0;

                for (uint channelIndex = effect.Parameter.ChannelCount; channelIndex != 0; channelIndex--)
                {
                    uint newUpdateCount = writeOffset + _commandBuffer.CommandList.SampleCount;

                    uint updateCount;

                    if (channelIndex != 1)
                    {
                        updateCount = 0;
                    }
                    else
                    {
                        updateCount = newUpdateCount;
                    }

                    _commandBuffer.GenerateCaptureEffect(
                        bufferOffset,
                        effect.Parameter.Input[i],
                        effect.State.SendBufferInfo,
                        effect.IsEnabled,
                        effect.Parameter.BufferStorageSize,
                        effect.State.SendBufferInfoBase,
                        updateCount,
                        writeOffset,
                        nodeId);

                    writeOffset = newUpdateCount;

                    i++;
                }
            }
        }

        private void GenerateCompressorEffect(uint bufferOffset, CompressorEffect effect, int nodeId, int effectId)
        {
            Debug.Assert(effect.Type == EffectType.Compressor);

            Memory<EffectResultState> dspResultState;

            if (effect.Parameter.StatisticsEnabled)
            {
                dspResultState = _effectContext.GetDspStateMemory(effectId);
            }
            else
            {
                dspResultState = Memory<EffectResultState>.Empty;
            }

            _commandBuffer.GenerateCompressorEffect(
                bufferOffset,
                effect.Parameter,
                effect.State,
                dspResultState,
                effect.IsEnabled,
                nodeId);
        }

        private void GenerateEffect(ref MixState mix, int effectId, BaseEffect effect)
        {
            int nodeId = mix.NodeId;

            bool isFinalMix = mix.MixId == Constants.FinalMixId;

            PerformanceEntryAddresses performanceEntry = new();

            bool performanceInitialized = false;

            if (_performanceManager != null && _performanceManager.GetNextEntry(
                out performanceEntry,
                effect.GetPerformanceDetailType(),
                isFinalMix ? PerformanceEntryType.FinalMix : PerformanceEntryType.SubMix,
                nodeId))
            {
                performanceInitialized = true;

                GeneratePerformance(ref performanceEntry, PerformanceCommand.Type.Start, nodeId);
            }

            switch (effect.Type)
            {
                case EffectType.BufferMix:
                    GenerateBufferMixerEffect((int)mix.BufferOffset, (BufferMixEffect)effect, nodeId);
                    break;
                case EffectType.AuxiliaryBuffer:
                    GenerateAuxEffect(mix.BufferOffset, (AuxiliaryBufferEffect)effect, nodeId);
                    break;
                case EffectType.Delay:
                    GenerateDelayEffect(mix.BufferOffset, (DelayEffect)effect, nodeId, _rendererContext.BehaviourContext.IsNewEffectChannelMappingSupported());
                    break;
                case EffectType.Reverb:
                    GenerateReverbEffect(mix.BufferOffset, (ReverbEffect)effect, nodeId, mix.IsLongSizePreDelaySupported, _rendererContext.BehaviourContext.IsNewEffectChannelMappingSupported());
                    break;
                case EffectType.Reverb3d:
                    GenerateReverb3dEffect(mix.BufferOffset, (Reverb3dEffect)effect, nodeId, _rendererContext.BehaviourContext.IsNewEffectChannelMappingSupported());
                    break;
                case EffectType.BiquadFilter:
                    GenerateBiquadFilterEffect(mix.BufferOffset, (BiquadFilterEffect)effect, nodeId);
                    break;
                case EffectType.Limiter:
                    GenerateLimiterEffect(mix.BufferOffset, (LimiterEffect)effect, nodeId, effectId);
                    break;
                case EffectType.CaptureBuffer:
                    GenerateCaptureEffect(mix.BufferOffset, (CaptureBufferEffect)effect, nodeId);
                    break;
                case EffectType.Compressor:
                    GenerateCompressorEffect(mix.BufferOffset, (CompressorEffect)effect, nodeId, effectId);
                    break;
                default:
                    throw new NotImplementedException($"Unsupported effect type {effect.Type}");
            }

            if (performanceInitialized)
            {
                GeneratePerformance(ref performanceEntry, PerformanceCommand.Type.End, nodeId);
            }

            effect.UpdateForCommandGeneration();
        }

        private void GenerateEffects(ref MixState mix)
        {
            ReadOnlySpan<int> effectProcessingOrderArray = mix.EffectProcessingOrderArray;

            Debug.Assert(_effectContext.GetCount() == 0 || !effectProcessingOrderArray.IsEmpty);

            for (int i = 0; i < _effectContext.GetCount(); i++)
            {
                int effectOrder = effectProcessingOrderArray[i];

                if (effectOrder == Constants.InvalidProcessingOrder)
                {
                    break;
                }

                // BaseEffect is a class, we don't need to pass it by ref
                BaseEffect effect = _effectContext.GetEffect(effectOrder);

                Debug.Assert(effect.Type != EffectType.Invalid);
                Debug.Assert(effect.MixId == mix.MixId);

                if (!effect.ShouldSkip())
                {
                    GenerateEffect(ref mix, effectOrder, effect);
                }
            }
        }

        private void GenerateMixWithSplitter(
            uint inputBufferIndex,
            uint outputBufferIndex,
            float volume,
            SplitterDestination destination,
            ref bool isFirstMixBuffer,
            int nodeId)
        {
            ref BiquadFilterParameter bqf0 = ref destination.GetBiquadFilterParameter(0);
            ref BiquadFilterParameter bqf1 = ref destination.GetBiquadFilterParameter(1);

            Memory<BiquadFilterState> bqfState = _splitterContext.GetBiquadFilterState(destination);

            if (bqf0.Enable && bqf1.Enable)
            {
                _commandBuffer.GenerateMultiTapBiquadFilterAndMix(
                    0f,
                    volume,
                    inputBufferIndex,
                    outputBufferIndex,
                    0,
                    Memory<VoiceUpdateState>.Empty,
                    ref bqf0,
                    ref bqf1,
                    bqfState[..1],
                    bqfState.Slice(1, 1),
                    bqfState.Slice(2, 1),
                    bqfState.Slice(3, 1),
                    !destination.IsBiquadFilterEnabledPrev(),
                    !destination.IsBiquadFilterEnabledPrev(),
                    false,
                    isFirstMixBuffer,
                    nodeId);

                destination.UpdateBiquadFilterEnabledPrev(0);
                destination.UpdateBiquadFilterEnabledPrev(1);
            }
            else if (bqf0.Enable)
            {
                _commandBuffer.GenerateBiquadFilterAndMix(
                    0f,
                    volume,
                    inputBufferIndex,
                    outputBufferIndex,
                    0,
                    Memory<VoiceUpdateState>.Empty,
                    ref bqf0,
                    bqfState[..1],
                    bqfState.Slice(1, 1),
                    !destination.IsBiquadFilterEnabledPrev(),
                    false,
                    isFirstMixBuffer,
                    nodeId);

                destination.UpdateBiquadFilterEnabledPrev(0);
            }
            else if (bqf1.Enable)
            {
                _commandBuffer.GenerateBiquadFilterAndMix(
                    0f,
                    volume,
                    inputBufferIndex,
                    outputBufferIndex,
                    0,
                    Memory<VoiceUpdateState>.Empty,
                    ref bqf1,
                    bqfState[..1],
                    bqfState.Slice(1, 1),
                    !destination.IsBiquadFilterEnabledPrev(),
                    false,
                    isFirstMixBuffer,
                    nodeId);

                destination.UpdateBiquadFilterEnabledPrev(1);
            }

            isFirstMixBuffer = false;
        }

        private void GenerateMix(ref MixState mix)
        {
            if (mix.HasAnyDestination())
            {
                Debug.Assert(mix.DestinationMixId != Constants.UnusedMixId || mix.DestinationSplitterId != Constants.UnusedSplitterId);

                if (mix.DestinationMixId == Constants.UnusedMixId)
                {
                    if (mix.DestinationSplitterId != Constants.UnusedSplitterId)
                    {
                        int destinationId = 0;

                        while (true)
                        {
                            int destinationIndex = destinationId++;

                            SplitterDestination destination = _splitterContext.GetDestination((int)mix.DestinationSplitterId, destinationIndex);

                            if (destination.IsNull)
                            {
                                break;
                            }

                            if (destination.IsConfigured())
                            {
                                int mixId = destination.DestinationId;

                                if (mixId < _mixContext.GetCount() && mixId != Constants.UnusedSplitterIdInt)
                                {
                                    ref MixState destinationMix = ref _mixContext.GetState(mixId);

                                    uint inputBufferIndex = mix.BufferOffset + ((uint)destinationIndex % mix.BufferCount);

                                    bool isFirstMixBuffer = true;

                                    for (uint bufferDestinationIndex = 0; bufferDestinationIndex < destinationMix.BufferCount; bufferDestinationIndex++)
                                    {
                                        float volume = mix.Volume * destination.GetMixVolume((int)bufferDestinationIndex);

                                        if (volume != 0.0f)
                                        {
                                            if (destination.IsBiquadFilterEnabled())
                                            {
                                                GenerateMixWithSplitter(
                                                    inputBufferIndex,
                                                    destinationMix.BufferOffset + bufferDestinationIndex,
                                                    volume,
                                                    destination,
                                                    ref isFirstMixBuffer,
                                                    mix.NodeId);
                                            }
                                            else
                                            {
                                                _commandBuffer.GenerateMix(
                                                    inputBufferIndex,
                                                    destinationMix.BufferOffset + bufferDestinationIndex,
                                                    mix.NodeId,
                                                    volume);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                else
                {
                    ref MixState destinationMix = ref _mixContext.GetState(mix.DestinationMixId);

                    for (uint bufferIndex = 0; bufferIndex < mix.BufferCount; bufferIndex++)
                    {
                        for (uint bufferDestinationIndex = 0; bufferDestinationIndex < destinationMix.BufferCount; bufferDestinationIndex++)
                        {
                            float volume = mix.Volume * mix.GetMixBufferVolume((int)bufferIndex, (int)bufferDestinationIndex);

                            if (volume != 0.0f)
                            {
                                _commandBuffer.GenerateMix(
                                    mix.BufferOffset + bufferIndex,
                                    destinationMix.BufferOffset + bufferDestinationIndex,
                                    mix.NodeId,
                                    volume);
                            }
                        }
                    }
                }
            }
        }

        private void GenerateSubMix(ref MixState subMix)
        {
            _commandBuffer.GenerateDepopForMixBuffers(
                _rendererContext.DepopBuffer,
                subMix.BufferOffset,
                subMix.BufferCount,
                subMix.NodeId,
                subMix.SampleRate);

            GenerateEffects(ref subMix);

            PerformanceEntryAddresses performanceEntry = new();

            int nodeId = subMix.NodeId;

            bool performanceInitialized = false;

            if (_performanceManager != null && _performanceManager.IsTargetNodeId(nodeId) && _performanceManager.GetNextEntry(out performanceEntry, PerformanceDetailType.Mix, PerformanceEntryType.SubMix, nodeId))
            {
                performanceInitialized = true;

                GeneratePerformance(ref performanceEntry, PerformanceCommand.Type.Start, nodeId);
            }

            GenerateMix(ref subMix);

            if (performanceInitialized)
            {
                GeneratePerformance(ref performanceEntry, PerformanceCommand.Type.End, nodeId);
            }
        }

        public void GenerateSubMixes()
        {
            for (int id = 0; id < _mixContext.GetCount(); id++)
            {
                ref MixState sortedState = ref _mixContext.GetSortedState(id);

                if (sortedState.IsUsed && sortedState.MixId != Constants.FinalMixId)
                {
                    int nodeId = sortedState.NodeId;

                    PerformanceEntryAddresses performanceEntry = new();

                    bool performanceInitialized = false;

                    if (_performanceManager != null && _performanceManager.GetNextEntry(out performanceEntry, PerformanceEntryType.SubMix, nodeId))
                    {
                        performanceInitialized = true;

                        GeneratePerformance(ref performanceEntry, PerformanceCommand.Type.Start, nodeId);
                    }

                    GenerateSubMix(ref sortedState);

                    if (performanceInitialized)
                    {
                        GeneratePerformance(ref performanceEntry, PerformanceCommand.Type.End, nodeId);
                    }
                }
            }
        }

        private void GenerateFinalMix()
        {
            ref MixState finalMix = ref _mixContext.GetFinalState();

            _commandBuffer.GenerateDepopForMixBuffers(
                _rendererContext.DepopBuffer,
                finalMix.BufferOffset,
                finalMix.BufferCount,
                finalMix.NodeId,
                finalMix.SampleRate);

            GenerateEffects(ref finalMix);

            PerformanceEntryAddresses performanceEntry = new();

            int nodeId = finalMix.NodeId;

            bool performanceInitialized = false;

            if (_performanceManager != null && _performanceManager.IsTargetNodeId(nodeId) && _performanceManager.GetNextEntry(out performanceEntry, PerformanceDetailType.Mix, PerformanceEntryType.FinalMix, nodeId))
            {
                performanceInitialized = true;

                GeneratePerformance(ref performanceEntry, PerformanceCommand.Type.Start, nodeId);
            }

            // Only generate volume command if the volume isn't 100%.
            if (finalMix.Volume != 1.0f)
            {
                for (uint bufferIndex = 0; bufferIndex < finalMix.BufferCount; bufferIndex++)
                {
                    bool performanceSubInitialized = false;

                    if (_performanceManager != null && _performanceManager.IsTargetNodeId(nodeId) && _performanceManager.GetNextEntry(out performanceEntry, PerformanceDetailType.VolumeRamp, PerformanceEntryType.FinalMix, nodeId))
                    {
                        performanceSubInitialized = true;

                        GeneratePerformance(ref performanceEntry, PerformanceCommand.Type.Start, nodeId);
                    }

                    _commandBuffer.GenerateVolume(
                        finalMix.Volume,
                        finalMix.BufferOffset + bufferIndex,
                        nodeId);

                    if (performanceSubInitialized)
                    {
                        GeneratePerformance(ref performanceEntry, PerformanceCommand.Type.End, nodeId);
                    }
                }
            }

            if (performanceInitialized)
            {
                GeneratePerformance(ref performanceEntry, PerformanceCommand.Type.End, nodeId);
            }
        }

        public void GenerateFinalMixes()
        {
            int nodeId = _mixContext.GetFinalState().NodeId;

            PerformanceEntryAddresses performanceEntry = new();

            bool performanceInitialized = false;

            if (_performanceManager != null && _performanceManager.GetNextEntry(out performanceEntry, PerformanceEntryType.FinalMix, nodeId))
            {
                performanceInitialized = true;

                GeneratePerformance(ref performanceEntry, PerformanceCommand.Type.Start, nodeId);
            }

            GenerateFinalMix();

            if (performanceInitialized)
            {
                GeneratePerformance(ref performanceEntry, PerformanceCommand.Type.End, nodeId);
            }
        }

        private void GenerateCircularBuffer(CircularBufferSink sink, ref MixState finalMix)
        {
            _commandBuffer.GenerateCircularBuffer(finalMix.BufferOffset, sink, Constants.InvalidNodeId);
        }

        private void GenerateDevice(DeviceSink sink, ref MixState finalMix)
        {
            if (_commandBuffer.CommandList.SampleRate != 48000 && sink.UpsamplerState == null)
            {
                sink.UpsamplerState = _rendererContext.UpsamplerManager.Allocate();
            }

            bool useCustomDownMixingCommand = _rendererContext.ChannelCount == 2 && sink.Parameter.DownMixParameterEnabled;

            if (useCustomDownMixingCommand)
            {
                _commandBuffer.GenerateDownMixSurroundToStereo(
                    finalMix.BufferOffset,
                    sink.Parameter.Input.AsSpan(),
                    sink.Parameter.Input.AsSpan(),
                    sink.DownMixCoefficients,
                    Constants.InvalidNodeId);
            }
            // NOTE: We do the downmixing at the DSP level as it's easier that way.
            else if (_rendererContext.ChannelCount == 2 && sink.Parameter.InputCount == 6)
            {
                _commandBuffer.GenerateDownMixSurroundToStereo(
                    finalMix.BufferOffset,
                    sink.Parameter.Input.AsSpan(),
                    sink.Parameter.Input.AsSpan(),
                    Constants.DefaultSurroundToStereoCoefficients,
                    Constants.InvalidNodeId);
            }

            CommandList commandList = _commandBuffer.CommandList;

            if (sink.UpsamplerState != null)
            {
                _commandBuffer.GenerateUpsample(
                    finalMix.BufferOffset,
                    sink.UpsamplerState,
                    sink.Parameter.InputCount,
                    sink.Parameter.Input.AsSpan(),
                    commandList.BufferCount,
                    commandList.SampleCount,
                    commandList.SampleRate,
                    Constants.InvalidNodeId);
            }

            _commandBuffer.GenerateDeviceSink(
                finalMix.BufferOffset,
                sink,
                _rendererContext.SessionId,
                commandList.Buffers,
                Constants.InvalidNodeId);
        }

        private void GenerateSink(BaseSink sink, ref MixState finalMix)
        {
            bool performanceInitialized = false;

            PerformanceEntryAddresses performanceEntry = new();

            if (_performanceManager != null && _performanceManager.GetNextEntry(out performanceEntry, PerformanceEntryType.Sink, sink.NodeId))
            {
                performanceInitialized = true;

                GeneratePerformance(ref performanceEntry, PerformanceCommand.Type.Start, sink.NodeId);
            }

            if (!sink.ShouldSkip)
            {
                switch (sink.Type)
                {
                    case SinkType.CircularBuffer:
                        GenerateCircularBuffer((CircularBufferSink)sink, ref finalMix);
                        break;
                    case SinkType.Device:
                        GenerateDevice((DeviceSink)sink, ref finalMix);
                        break;
                    default:
                        throw new NotImplementedException($"Unsupported sink type {sink.Type}");
                }

                sink.UpdateForCommandGeneration();
            }

            if (performanceInitialized)
            {
                GeneratePerformance(ref performanceEntry, PerformanceCommand.Type.End, sink.NodeId);
            }
        }

        public void GenerateSinks()
        {
            ref MixState finalMix = ref _mixContext.GetFinalState();

            for (int i = 0; i < _sinkContext.GetCount(); i++)
            {
                // BaseSink is a class, we don't need to pass it by ref
                BaseSink sink = _sinkContext.GetSink(i);

                if (sink.IsUsed)
                {
                    GenerateSink(sink, ref finalMix);
                }
            }
        }
    }
}
