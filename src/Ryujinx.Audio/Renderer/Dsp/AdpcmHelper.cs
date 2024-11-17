using Ryujinx.Audio.Renderer.Dsp.State;
using Ryujinx.Common.Logging;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Ryujinx.Audio.Renderer.Dsp
{
    public static class AdpcmHelper
    {
        private const int FixedPointPrecision = 11;
        private const int SamplesPerFrame = 14;
        private const int NibblesPerFrame = SamplesPerFrame + 2;
        private const int BytesPerFrame = 8;
#pragma warning disable IDE0051 // Remove unused private member
        private const int BitsPerFrame = BytesPerFrame * 8;
#pragma warning restore IDE0051

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint GetAdpcmDataSize(int sampleCount)
        {
            Debug.Assert(sampleCount >= 0);

            int frames = sampleCount / SamplesPerFrame;
            int extraSize = 0;

            if ((sampleCount % SamplesPerFrame) != 0)
            {
                extraSize = (sampleCount % SamplesPerFrame) / 2 + 1 + (sampleCount % 2);
            }

            return (uint)(BytesPerFrame * frames + extraSize);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetAdpcmOffsetFromSampleOffset(int sampleOffset)
        {
            Debug.Assert(sampleOffset >= 0);

            return GetNibblesFromSampleCount(sampleOffset) / 2;
        }

        public static int NibbleToSample(int nibble)
        {
            int frames = nibble / NibblesPerFrame;
            int extraNibbles = nibble % NibblesPerFrame;
            int samples = SamplesPerFrame * frames;

            return samples + extraNibbles - 2;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetNibblesFromSampleCount(int sampleCount)
        {
            byte headerSize = 0;

            if ((sampleCount % SamplesPerFrame) != 0)
            {
                headerSize = 2;
            }

            return sampleCount % SamplesPerFrame + NibblesPerFrame * (sampleCount / SamplesPerFrame) + headerSize;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static short Saturate(int value)
        {
            if (value > short.MaxValue)
            {
                value = short.MaxValue;
            }

            if (value < short.MinValue)
            {
                value = short.MinValue;
            }

            return (short)value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static short GetCoefficientAtIndex(ReadOnlySpan<short> coefficients, int index)
        {
            if ((uint)index >= (uint)coefficients.Length)
            {
                Logger.Error?.Print(LogClass.AudioRenderer, $"Out of bound read for coefficient at index {index}");

                return 0;
            }

            return coefficients[index];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Decode(Span<short> output, ReadOnlySpan<byte> input, int startSampleOffset, int endSampleOffset, int offset, int count, ReadOnlySpan<short> coefficients, ref AdpcmLoopContext loopContext)
        {
            if (input.IsEmpty || endSampleOffset < startSampleOffset)
            {
                return 0;
            }

            byte predScale = (byte)loopContext.PredScale;
            byte scale = (byte)(predScale & 0xF);
            byte coefficientIndex = (byte)((predScale >> 4) & 0xF);
            short history0 = loopContext.History0;
            short history1 = loopContext.History1;
            short coefficient0 = GetCoefficientAtIndex(coefficients, coefficientIndex * 2 + 0);
            short coefficient1 = GetCoefficientAtIndex(coefficients, coefficientIndex * 2 + 1);

            int decodedCount = Math.Min(count, endSampleOffset - startSampleOffset - offset);
            int nibbles = GetNibblesFromSampleCount(offset + startSampleOffset);
            int remaining = decodedCount;
            int outputBufferIndex = 0;
            int inputIndex = 0;

            ReadOnlySpan<byte> targetInput;

            targetInput = input[(nibbles / 2)..];

            while (remaining > 0)
            {
                int samplesCount;

                if (((uint)nibbles % NibblesPerFrame) == 0)
                {
                    predScale = targetInput[inputIndex++];

                    scale = (byte)(predScale & 0xF);

                    coefficientIndex = (byte)((predScale >> 4) & 0xF);

                    coefficient0 = GetCoefficientAtIndex(coefficients, coefficientIndex * 2);
                    coefficient1 = GetCoefficientAtIndex(coefficients, coefficientIndex * 2 + 1);

                    nibbles += 2;

                    samplesCount = Math.Min(remaining, SamplesPerFrame);
                }
                else
                {
                    samplesCount = 1;
                }

                int scaleFixedPoint = FixedPointHelper.ToFixed(1.0f, FixedPointPrecision) << scale;

                if (samplesCount < SamplesPerFrame)
                {
                    for (int i = 0; i < samplesCount; i++)
                    {
                        int value = targetInput[inputIndex];

                        int sample;

                        if ((nibbles & 1) != 0)
                        {
                            sample = (value << 28) >> 28;

                            inputIndex++;
                        }
                        else
                        {
                            sample = (value << 24) >> 28;
                        }

                        nibbles++;

                        int prediction = coefficient0 * history0 + coefficient1 * history1;

                        sample = FixedPointHelper.RoundUpAndToInt(sample * scaleFixedPoint + prediction, FixedPointPrecision);

                        short saturatedSample = Saturate(sample);

                        history1 = history0;
                        history0 = saturatedSample;

                        output[outputBufferIndex++] = saturatedSample;

                        remaining--;
                    }
                }
                else
                {
                    for (int i = 0; i < SamplesPerFrame / 2; i++)
                    {
                        int value = targetInput[inputIndex];

                        int sample0;
                        int sample1;

                        sample0 = (value << 24) >> 28;
                        sample1 = (value << 28) >> 28;

                        inputIndex++;

                        int prediction0 = coefficient0 * history0 + coefficient1 * history1;
                        sample0 = FixedPointHelper.RoundUpAndToInt(sample0 * scaleFixedPoint + prediction0, FixedPointPrecision);
                        short saturatedSample0 = Saturate(sample0);

                        int prediction1 = coefficient0 * saturatedSample0 + coefficient1 * history0;
                        sample1 = FixedPointHelper.RoundUpAndToInt(sample1 * scaleFixedPoint + prediction1, FixedPointPrecision);
                        short saturatedSample1 = Saturate(sample1);

                        history1 = saturatedSample0;
                        history0 = saturatedSample1;

                        output[outputBufferIndex++] = saturatedSample0;
                        output[outputBufferIndex++] = saturatedSample1;
                    }

                    nibbles += SamplesPerFrame;
                    remaining -= SamplesPerFrame;
                }
            }

            loopContext.PredScale = predScale;
            loopContext.History0 = history0;
            loopContext.History1 = history1;

            return decodedCount;
        }
    }
}
