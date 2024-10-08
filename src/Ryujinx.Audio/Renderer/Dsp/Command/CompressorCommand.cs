using Ryujinx.Audio.Renderer.Dsp.Effect;
using Ryujinx.Audio.Renderer.Dsp.State;
using Ryujinx.Audio.Renderer.Parameter;
using Ryujinx.Audio.Renderer.Parameter.Effect;
using Ryujinx.Audio.Renderer.Server.Effect;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Ryujinx.Audio.Renderer.Dsp.Command
{
    public class CompressorCommand : ICommand
    {
        private const int FixedPointPrecision = 15;

        public bool Enabled { get; set; }

        public int NodeId { get; }

        public CommandType CommandType => CommandType.Compressor;

        public uint EstimatedProcessingTime { get; set; }

        public CompressorParameter Parameter => _parameter;
        public Memory<CompressorState> State { get; }
        public Memory<EffectResultState> ResultState { get; }
        public ushort[] OutputBufferIndices { get; }
        public ushort[] InputBufferIndices { get; }
        public bool IsEffectEnabled { get; }

        private CompressorParameter _parameter;

        public CompressorCommand(uint bufferOffset, CompressorParameter parameter, Memory<CompressorState> state, Memory<EffectResultState> resultState, bool isEnabled, int nodeId)
        {
            Enabled = true;
            NodeId = nodeId;
            _parameter = parameter;
            State = state;
            ResultState = resultState;

            IsEffectEnabled = isEnabled;

            InputBufferIndices = new ushort[Constants.VoiceChannelCountMax];
            OutputBufferIndices = new ushort[Constants.VoiceChannelCountMax];

            for (int i = 0; i < _parameter.ChannelCount; i++)
            {
                InputBufferIndices[i] = (ushort)(bufferOffset + _parameter.Input[i]);
                OutputBufferIndices[i] = (ushort)(bufferOffset + _parameter.Output[i]);
            }
        }

        public void Process(CommandList context)
        {
            ref CompressorState state = ref State.Span[0];

            if (IsEffectEnabled)
            {
                if (_parameter.Status == UsageState.Invalid)
                {
                    state = new CompressorState(ref _parameter);
                }
                else if (_parameter.Status == UsageState.New)
                {
                    state.UpdateParameter(ref _parameter);
                }
            }

            ProcessCompressor(context, ref state);
        }

        private unsafe void ProcessCompressor(CommandList context, ref CompressorState state)
        {
            Debug.Assert(_parameter.IsChannelCountValid());

            if (IsEffectEnabled && _parameter.IsChannelCountValid())
            {
                if (!ResultState.IsEmpty && _parameter.StatisticsReset)
                {
                    ref CompressorStatistics statistics = ref MemoryMarshal.Cast<byte, CompressorStatistics>(ResultState.Span[0].SpecificData)[0];

                    statistics.Reset(_parameter.ChannelCount);
                }

                Span<IntPtr> inputBuffers = stackalloc IntPtr[_parameter.ChannelCount];
                Span<IntPtr> outputBuffers = stackalloc IntPtr[_parameter.ChannelCount];
                Span<float> channelInput = stackalloc float[_parameter.ChannelCount];
                ExponentialMovingAverage inputMovingAverage = state.InputMovingAverage;
                float unknown4 = state.Unknown4;
                ExponentialMovingAverage compressionGainAverage = state.CompressionGainAverage;
                float previousCompressionEmaAlpha = state.PreviousCompressionEmaAlpha;

                for (int i = 0; i < _parameter.ChannelCount; i++)
                {
                    inputBuffers[i] = context.GetBufferPointer(InputBufferIndices[i]);
                    outputBuffers[i] = context.GetBufferPointer(OutputBufferIndices[i]);
                }

                for (int sampleIndex = 0; sampleIndex < context.SampleCount; sampleIndex++)
                {
                    for (int channelIndex = 0; channelIndex < _parameter.ChannelCount; channelIndex++)
                    {
                        channelInput[channelIndex] = *((float*)inputBuffers[channelIndex] + sampleIndex);
                    }

                    float mean = FloatingPointHelper.MeanSquare(channelInput);
                    float newMean = inputMovingAverage.Update(mean, _parameter.InputGain);
                    float y = FloatingPointHelper.Log10(newMean) * 10.0f;
                    float z = 1.0f;

                    bool unknown10OutOfRange = y >= state.Unknown10;

                    if (newMean < 1.0e-10f)
                    {
                        y = -100.0f;

                        unknown10OutOfRange = state.Unknown10 <= -100.0f;
                    }

                    if (unknown10OutOfRange)
                    {
                        float tmpGain;

                        if (y >= state.Unknown14)
                        {
                            tmpGain = ((1.0f / _parameter.Ratio) - 1.0f) * (y - _parameter.Threshold);
                        }
                        else
                        {
                            tmpGain = (y - state.Unknown10) * ((y - state.Unknown10) * -state.CompressorGainReduction);
                        }

                        z = FloatingPointHelper.DecibelToLinear(tmpGain);
                    }

                    float unknown4New = z;
                    float compressionEmaAlpha;

                    if ((unknown4 - z) <= 0.08f)
                    {
                        compressionEmaAlpha = _parameter.ReleaseCoefficient;

                        if ((unknown4 - z) >= -0.08f)
                        {
                            if (MathF.Abs(compressionGainAverage.Read() - z) >= 0.001f)
                            {
                                unknown4New = unknown4;
                            }

                            compressionEmaAlpha = previousCompressionEmaAlpha;
                        }
                    }
                    else
                    {
                        compressionEmaAlpha = _parameter.AttackCoefficient;
                    }

                    float compressionGain = compressionGainAverage.Update(z, compressionEmaAlpha);

                    for (int channelIndex = 0; channelIndex < _parameter.ChannelCount; channelIndex++)
                    {
                        *((float*)outputBuffers[channelIndex] + sampleIndex) = channelInput[channelIndex] * compressionGain * state.OutputGain;
                    }

                    unknown4 = unknown4New;
                    previousCompressionEmaAlpha = compressionEmaAlpha;

                    if (!ResultState.IsEmpty)
                    {
                        ref CompressorStatistics statistics = ref MemoryMarshal.Cast<byte, CompressorStatistics>(ResultState.Span[0].SpecificData)[0];

                        statistics.MinimumGain = MathF.Min(statistics.MinimumGain, compressionGain * state.OutputGain);
                        statistics.MaximumMean = MathF.Max(statistics.MaximumMean, mean);

                        for (int channelIndex = 0; channelIndex < _parameter.ChannelCount; channelIndex++)
                        {
                            statistics.LastSamples[channelIndex] = MathF.Abs(channelInput[channelIndex] * (1f / 32768f));
                        }
                    }
                }

                state.InputMovingAverage = inputMovingAverage;
                state.Unknown4 = unknown4;
                state.CompressionGainAverage = compressionGainAverage;
                state.PreviousCompressionEmaAlpha = previousCompressionEmaAlpha;
            }
            else
            {
                for (int i = 0; i < _parameter.ChannelCount; i++)
                {
                    if (InputBufferIndices[i] != OutputBufferIndices[i])
                    {
                        context.CopyBuffer(OutputBufferIndices[i], InputBufferIndices[i]);
                    }
                }
            }
        }
    }
}
