using System;
using System.Numerics;

namespace Ryujinx.Graphics.Nvdec.FFmpeg.H264
{
    struct H264BitStreamWriter(byte[] workBuffer)
    {
        private const int BufferSize = 8;

        private int _offset = 0;
        private int _buffer = 0;
        private int _bufferPos = 0;

        public void WriteBit(bool value)
        {
            WriteBits(value ? 1 : 0, 1);
        }

        public void WriteBits(int value, int valueSize)
        {
            int valuePos = 0;

            int remaining = valueSize;

            while (remaining > 0)
            {
                int copySize = remaining;

                int free = GetFreeBufferBits();

                if (copySize > free)
                {
                    copySize = free;
                }

                int mask = (1 << copySize) - 1;

                int srcShift = (valueSize - valuePos) - copySize;
                int dstShift = (BufferSize - _bufferPos) - copySize;

                _buffer |= ((value >> srcShift) & mask) << dstShift;

                valuePos += copySize;
                _bufferPos += copySize;
                remaining -= copySize;
            }
        }

        private int GetFreeBufferBits()
        {
            if (_bufferPos == BufferSize)
                Flush();

            return BufferSize - _bufferPos;
        }

        public void Flush()
        {
            if (_bufferPos != 0)
            {
                workBuffer[_offset++] = (byte)_buffer;

                _buffer = 0;
                _bufferPos = 0;
            }
        }

        public void End()
        {
            WriteBit(true);

            Flush();
        }

        public readonly Span<byte> AsSpan()
            => new Span<byte>(workBuffer)[.._offset];

        public void WriteU(uint value, int valueSize) => WriteBits((int)value, valueSize);
        public void WriteSe(int value) => WriteExpGolombCodedInt(value);
        public void WriteUe(uint value) => WriteExpGolombCodedUInt(value);

        private void WriteExpGolombCodedInt(int value)
        {
            int sign = value <= 0 ? 0 : 1;

            if (value < 0)
            {
                value = -value;
            }

            value = (value << 1) - sign;

            WriteExpGolombCodedUInt((uint)value);
        }

        private void WriteExpGolombCodedUInt(uint value)
        {
            int size = 32 - BitOperations.LeadingZeroCount(value + 1);

            WriteBits(1, size);

            value -= (1u << (size - 1)) - 1;

            WriteBits((int)value, size - 1);
        }
    }
}
