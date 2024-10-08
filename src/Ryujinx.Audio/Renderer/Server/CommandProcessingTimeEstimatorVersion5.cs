using Ryujinx.Audio.Renderer.Dsp.Command;
using System;
using System.Diagnostics;

namespace Ryujinx.Audio.Renderer.Server
{
    /// <summary>
    /// <see cref="ICommandProcessingTimeEstimator"/> version 5. (added with REV11)
    /// </summary>
    public class CommandProcessingTimeEstimatorVersion5 : CommandProcessingTimeEstimatorVersion4
    {
        public CommandProcessingTimeEstimatorVersion5(uint sampleCount, uint bufferCount) : base(sampleCount, bufferCount) { }

        public override uint Estimate(DelayCommand command)
        {
            Debug.Assert(SampleCount == 160 || SampleCount == 240);

            if (SampleCount == 160)
            {
                if (command.Enabled)
                {
                    return command.Parameter.ChannelCount switch
                    {
                        1 => 8929,
                        2 => 25501,
                        4 => 47760,
                        6 => 82203,
                        _ => throw new NotImplementedException($"{command.Parameter.ChannelCount}"),
                    };
                }

                return command.Parameter.ChannelCount switch
                {
                    1 => (uint)1295.20f,
                    2 => (uint)1213.60f,
                    4 => (uint)942.03f,
                    6 => (uint)1001.6f,
                    _ => throw new NotImplementedException($"{command.Parameter.ChannelCount}"),
                };
            }

            if (command.Enabled)
            {
                return command.Parameter.ChannelCount switch
                {
                    1 => 11941,
                    2 => 37197,
                    4 => 69750,
                    6 => 12004,
                    _ => throw new NotImplementedException($"{command.Parameter.ChannelCount}"),
                };
            }

            return command.Parameter.ChannelCount switch
            {
                1 => (uint)997.67f,
                2 => (uint)977.63f,
                4 => (uint)792.31f,
                6 => (uint)875.43f,
                _ => throw new NotImplementedException($"{command.Parameter.ChannelCount}"),
            };
        }

        public override uint Estimate(ReverbCommand command)
        {
            Debug.Assert(SampleCount == 160 || SampleCount == 240);

            if (SampleCount == 160)
            {
                if (command.Enabled)
                {
                    return command.Parameter.ChannelCount switch
                    {
                        1 => 81475,
                        2 => 84975,
                        4 => 91625,
                        6 => 95332,
                        _ => throw new NotImplementedException($"{command.Parameter.ChannelCount}"),
                    };
                }

                return command.Parameter.ChannelCount switch
                {
                    1 => (uint)536.30f,
                    2 => (uint)588.80f,
                    4 => (uint)643.70f,
                    6 => (uint)706.0f,
                    _ => throw new NotImplementedException($"{command.Parameter.ChannelCount}"),
                };
            }

            if (command.Enabled)
            {
                return command.Parameter.ChannelCount switch
                {
                    1 => 120170,
                    2 => 125260,
                    4 => 135750,
                    6 => 141130,
                    _ => throw new NotImplementedException($"{command.Parameter.ChannelCount}"),
                };
            }

            return command.Parameter.ChannelCount switch
            {
                1 => (uint)617.64f,
                2 => (uint)659.54f,
                4 => (uint)711.44f,
                6 => (uint)778.07f,
                _ => throw new NotImplementedException($"{command.Parameter.ChannelCount}"),
            };
        }

        public override uint Estimate(Reverb3dCommand command)
        {
            Debug.Assert(SampleCount == 160 || SampleCount == 240);

            if (SampleCount == 160)
            {
                if (command.Enabled)
                {
                    return command.Parameter.ChannelCount switch
                    {
                        1 => 116750,
                        2 => 125910,
                        4 => 146340,
                        6 => 165810,
                        _ => throw new NotImplementedException($"{command.Parameter.ChannelCount}"),
                    };
                }

                return command.Parameter.ChannelCount switch
                {
                    1 => 735,
                    2 => (uint)766.62f,
                    4 => (uint)834.07f,
                    6 => (uint)875.44f,
                    _ => throw new NotImplementedException($"{command.Parameter.ChannelCount}"),
                };
            }

            if (command.Enabled)
            {
                return command.Parameter.ChannelCount switch
                {
                    1 => 170290,
                    2 => 183880,
                    4 => 214700,
                    6 => 243850,
                    _ => throw new NotImplementedException($"{command.Parameter.ChannelCount}"),
                };
            }

            return command.Parameter.ChannelCount switch
            {
                1 => (uint)508.47f,
                2 => (uint)582.45f,
                4 => (uint)626.42f,
                6 => (uint)682.47f,
                _ => throw new NotImplementedException($"{command.Parameter.ChannelCount}"),
            };
        }

        public override uint Estimate(CompressorCommand command)
        {
            Debug.Assert(SampleCount == 160 || SampleCount == 240);

            if (SampleCount == 160)
            {
                if (command.Enabled)
                {
                    if (command.Parameter.StatisticsEnabled)
                    {
                        return command.Parameter.ChannelCount switch
                        {
                            1 => 22100,
                            2 => 33211,
                            4 => 41587,
                            6 => 58819,
                            _ => throw new NotImplementedException($"{command.Parameter.ChannelCount}"),
                        };
                    }
                    else
                    {
                        return command.Parameter.ChannelCount switch
                        {
                            1 => 19052,
                            2 => 29852,
                            4 => 37904,
                            6 => 55020,
                            _ => throw new NotImplementedException($"{command.Parameter.ChannelCount}"),
                        };
                    }
                }

                return command.Parameter.ChannelCount switch
                {
                    1 => (uint)630.12f,
                    2 => (uint)638.27f,
                    4 => (uint)705.86f,
                    6 => (uint)782.02f,
                    _ => throw new NotImplementedException($"{command.Parameter.ChannelCount}"),
                };
            }

            if (command.Enabled)
            {
                if (command.Parameter.StatisticsEnabled)
                {
                    return command.Parameter.ChannelCount switch
                    {
                        1 => 32518,
                        2 => 49102,
                        4 => 61685,
                        6 => 87250,
                        _ => throw new NotImplementedException($"{command.Parameter.ChannelCount}"),
                    };
                }
                else
                {
                    return command.Parameter.ChannelCount switch
                    {
                        1 => 27963,
                        2 => 44016,
                        4 => 56183,
                        6 => 81862,
                        _ => throw new NotImplementedException($"{command.Parameter.ChannelCount}"),
                    };
                }
            }

            return command.Parameter.ChannelCount switch
            {
                1 => (uint)840.14f,
                2 => (uint)826.1f,
                4 => (uint)901.88f,
                6 => (uint)965.29f,
                _ => throw new NotImplementedException($"{command.Parameter.ChannelCount}"),
            };
        }

        public override uint Estimate(BiquadFilterAndMixCommand command)
        {
            Debug.Assert(SampleCount == 160 || SampleCount == 240);

            if (command.HasVolumeRamp)
            {
                if (SampleCount == 160)
                {
                    return 5204;
                }

                return 6683;
            }
            else
            {
                if (SampleCount == 160)
                {
                    return 3427;
                }

                return 4752;
            }
        }

        public override uint Estimate(MultiTapBiquadFilterAndMixCommand command)
        {
            Debug.Assert(SampleCount == 160 || SampleCount == 240);

            if (command.HasVolumeRamp)
            {
                if (SampleCount == 160)
                {
                    return 7939;
                }

                return 10669;
            }
            else
            {
                if (SampleCount == 160)
                {
                    return 6256;
                }

                return 8683;
            }
        }
    }
}
