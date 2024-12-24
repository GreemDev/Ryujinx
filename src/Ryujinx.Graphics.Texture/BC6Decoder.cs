using Ryujinx.Graphics.Texture.Utils;
using System;
using System.Runtime.InteropServices;

namespace Ryujinx.Graphics.Texture
{
    static class BC6Decoder
    {
        private const int HalfOne = 0x3C00;

        public static void Decode(Span<byte> output, ReadOnlySpan<byte> data, int width, int height, bool signed)
        {
            ReadOnlySpan<Block> blocks = MemoryMarshal.Cast<byte, Block>(data);

            Span<ulong> output64 = MemoryMarshal.Cast<byte, ulong>(output);

            int wInBlocks = (width + 3) / 4;
            int hInBlocks = (height + 3) / 4;

            for (int y = 0; y < hInBlocks; y++)
            {
                int y2 = y * 4;
                int bh = Math.Min(4, height - y2);

                for (int x = 0; x < wInBlocks; x++)
                {
                    int x2 = x * 4;
                    int bw = Math.Min(4, width - x2);

                    DecodeBlock(blocks[y * wInBlocks + x], output64[(y2 * width + x2)..], bw, bh, width, signed);
                }
            }
        }

        private static void DecodeBlock(Block block, Span<ulong> output, int w, int h, int width, bool signed)
        {
            int mode = (int)(block.Low & 3);
            if ((mode & 2) != 0)
            {
                mode = (int)(block.Low & 0x1f);
            }

            Span<RgbaColor32> endPoints = stackalloc RgbaColor32[4];
            int subsetCount = DecodeEndPoints(ref block, endPoints, mode, signed);
            if (subsetCount == 0)
            {
                // Mode is invalid, the spec mandates that hardware fills the block with
                // an opaque black color.
                for (int ty = 0; ty < h; ty++)
                {
                    int baseOffs = ty * width;

                    for (int tx = 0; tx < w; tx++)
                    {
                        output[baseOffs + tx] = (ulong)HalfOne << 48;
                    }
                }

                return;
            }

            int partition;
            int indexBitCount;
            ulong indices;

            if (subsetCount > 1)
            {
                partition = (int)((block.High >> 13) & 0x1F);
                indexBitCount = 3;

                int fixUpIndex = BC67Tables.FixUpIndices[subsetCount - 1][partition][1] * 3;
                ulong lowMask = (ulong.MaxValue >> (65 - fixUpIndex)) << 3;
                ulong highMask = ulong.MaxValue << (fixUpIndex + 3);

                indices = ((block.High >> 16) & highMask) | ((block.High >> 17) & lowMask) | ((block.High >> 18) & 3);
            }
            else
            {
                partition = 0;
                indexBitCount = 4;
                indices = (block.High & ~0xFUL) | ((block.High >> 1) & 7);
            }

            ulong indexMask = (1UL << indexBitCount) - 1;

            for (int ty = 0; ty < h; ty++)
            {
                int baseOffs = ty * width;

                for (int tx = 0; tx < w; tx++)
                {
                    int offs = baseOffs + tx;
                    int index = (int)(indices & indexMask);
                    int endPointBase = BC67Tables.PartitionTable[subsetCount - 1][partition][ty * 4 + tx] << 1;

                    RgbaColor32 color1 = endPoints[endPointBase];
                    RgbaColor32 color2 = endPoints[endPointBase + 1];

                    RgbaColor32 color = BC67Utils.Interpolate(color1, color2, index, indexBitCount);

                    output[offs] =
                        (ulong)FinishUnquantize(color.R, signed) |
                        ((ulong)FinishUnquantize(color.G, signed) << 16) |
                        ((ulong)FinishUnquantize(color.B, signed) << 32) |
                        ((ulong)HalfOne << 48);

                    indices >>= indexBitCount;
                }
            }
        }

        private static int DecodeEndPoints(ref Block block, Span<RgbaColor32> endPoints, int mode, bool signed)
        {
            ulong low = block.Low;
            ulong high = block.High;

            int r0 = 0, g0 = 0, b0 = 0, r1 = 0, g1 = 0, b1 = 0, r2 = 0, g2 = 0, b2 = 0, r3 = 0, g3 = 0, b3 = 0;
            int subsetCount;

            switch (mode)
            {
                case 0:
                    r0 = (int)(low >> 5) & 0x3FF;
                    g0 = (int)(low >> 15) & 0x3FF;
                    b0 = (int)(low >> 25) & 0x3FF;

                    if (signed)
                    {
                        r0 = SignExtend(r0, 10);
                        g0 = SignExtend(g0, 10);
                        b0 = SignExtend(b0, 10);
                    }

                    r1 = r0 + SignExtend((int)(low >> 35), 5);
                    g1 = g0 + SignExtend((int)(low >> 45), 5);
                    b1 = b0 + SignExtend((int)(low >> 55), 5);

                    r2 = r0 + SignExtend((int)(high >> 1), 5);
                    g2 = g0 + SignExtend((int)(((low << 2) & 0x10) | ((low >> 41) & 0xF)), 5);
                    b2 = b0 + SignExtend((int)(((low << 1) & 0x10) | ((high << 3) & 0x08) | (low >> 61)), 5);

                    r3 = r0 + SignExtend((int)(high >> 7), 5);
                    g3 = g0 + SignExtend((int)(((low >> 36) & 0x10) | ((low >> 51) & 0xF)), 5);
                    b3 = b0 + SignExtend((int)(
                        ((low) & 0x10) |
                        ((high >> 9) & 0x08) |
                        ((high >> 4) & 0x04) |
                        ((low >> 59) & 0x02) |
                        ((low >> 50) & 0x01)), 5);

                    r0 = Unquantize(r0, 10, signed);
                    g0 = Unquantize(g0, 10, signed);
                    b0 = Unquantize(b0, 10, signed);

                    r1 = Unquantize(r1 & 0x3FF, 10, signed);
                    g1 = Unquantize(g1 & 0x3FF, 10, signed);
                    b1 = Unquantize(b1 & 0x3FF, 10, signed);

                    r2 = Unquantize(r2 & 0x3FF, 10, signed);
                    g2 = Unquantize(g2 & 0x3FF, 10, signed);
                    b2 = Unquantize(b2 & 0x3FF, 10, signed);

                    r3 = Unquantize(r3 & 0x3FF, 10, signed);
                    g3 = Unquantize(g3 & 0x3FF, 10, signed);
                    b3 = Unquantize(b3 & 0x3FF, 10, signed);

                    subsetCount = 2;
                    break;
                case 1:
                    r0 = (int)(low >> 5) & 0x7F;
                    g0 = (int)(low >> 15) & 0x7F;
                    b0 = (int)(low >> 25) & 0x7F;

                    if (signed)
                    {
                        r0 = SignExtend(r0, 7);
                        g0 = SignExtend(g0, 7);
                        b0 = SignExtend(b0, 7);
                    }

                    r1 = r0 + SignExtend((int)(low >> 35), 6);
                    g1 = g0 + SignExtend((int)(low >> 45), 6);
                    b1 = b0 + SignExtend((int)(low >> 55), 6);

                    r2 = r0 + SignExtend((int)(high >> 1), 6);
                    g2 = g0 + SignExtend((int)(((low << 3) & 0x20) | ((low >> 20) & 0x10) | ((low >> 41) & 0x0F)), 6);
                    b2 = b0 + SignExtend((int)(
                        ((low >> 17) & 0x20) |
                        ((low >> 10) & 0x10) |
                        ((high << 3) & 0x08) |
                        (low >> 61)), 6);

                    r3 = r0 + SignExtend((int)(high >> 7), 6);
                    g3 = g0 + SignExtend((int)(((low << 1) & 0x30) | ((low >> 51) & 0xF)), 6);
                    b3 = b0 + SignExtend((int)(
                        ((low >> 28) & 0x20) |
                        ((low >> 30) & 0x10) |
                        ((low >> 29) & 0x08) |
                        ((low >> 21) & 0x04) |
                        ((low >> 12) & 0x03)), 6);

                    r0 = Unquantize(r0, 7, signed);
                    g0 = Unquantize(g0, 7, signed);
                    b0 = Unquantize(b0, 7, signed);

                    r1 = Unquantize(r1 & 0x7F, 7, signed);
                    g1 = Unquantize(g1 & 0x7F, 7, signed);
                    b1 = Unquantize(b1 & 0x7F, 7, signed);

                    r2 = Unquantize(r2 & 0x7F, 7, signed);
                    g2 = Unquantize(g2 & 0x7F, 7, signed);
                    b2 = Unquantize(b2 & 0x7F, 7, signed);

                    r3 = Unquantize(r3 & 0x7F, 7, signed);
                    g3 = Unquantize(g3 & 0x7F, 7, signed);
                    b3 = Unquantize(b3 & 0x7F, 7, signed);

                    subsetCount = 2;
                    break;
                case 2:
                    r0 = (int)(((low >> 30) & 0x400) | ((low >> 5) & 0x3FF));
                    g0 = (int)(((low >> 39) & 0x400) | ((low >> 15) & 0x3FF));
                    b0 = (int)(((low >> 49) & 0x400) | ((low >> 25) & 0x3FF));

                    if (signed)
                    {
                        r0 = SignExtend(r0, 11);
                        g0 = SignExtend(g0, 11);
                        b0 = SignExtend(b0, 11);
                    }

                    r1 = r0 + SignExtend((int)(low >> 35), 5);
                    g1 = g0 + SignExtend((int)(low >> 45), 4);
                    b1 = b0 + SignExtend((int)(low >> 55), 4);

                    r2 = r0 + SignExtend((int)(high >> 1), 5);
                    g2 = g0 + SignExtend((int)(low >> 41), 4);
                    b2 = b0 + SignExtend((int)(((high << 3) & 8) | (low >> 61)), 4);

                    r3 = r0 + SignExtend((int)(high >> 7), 5);
                    g3 = g0 + SignExtend((int)(low >> 51), 4);
                    b3 = b0 + SignExtend((int)(
                        ((high >> 9) & 8) |
                        ((high >> 4) & 4) |
                        ((low >> 59) & 2) |
                        ((low >> 50) & 1)), 4);

                    r0 = Unquantize(r0, 11, signed);
                    g0 = Unquantize(g0, 11, signed);
                    b0 = Unquantize(b0, 11, signed);

                    r1 = Unquantize(r1 & 0x7FF, 11, signed);
                    g1 = Unquantize(g1 & 0x7FF, 11, signed);
                    b1 = Unquantize(b1 & 0x7FF, 11, signed);

                    r2 = Unquantize(r2 & 0x7FF, 11, signed);
                    g2 = Unquantize(g2 & 0x7FF, 11, signed);
                    b2 = Unquantize(b2 & 0x7FF, 11, signed);

                    r3 = Unquantize(r3 & 0x7FF, 11, signed);
                    g3 = Unquantize(g3 & 0x7FF, 11, signed);
                    b3 = Unquantize(b3 & 0x7FF, 11, signed);

                    subsetCount = 2;
                    break;
                case 3:
                    r0 = (int)(low >> 5) & 0x3FF;
                    g0 = (int)(low >> 15) & 0x3FF;
                    b0 = (int)(low >> 25) & 0x3FF;

                    r1 = (int)(low >> 35) & 0x3FF;
                    g1 = (int)(low >> 45) & 0x3FF;
                    b1 = (int)(((high << 9) & 0x200) | (low >> 55));

                    if (signed)
                    {
                        r0 = SignExtend(r0, 10);
                        g0 = SignExtend(g0, 10);
                        b0 = SignExtend(b0, 10);

                        r1 = SignExtend(r1, 10);
                        g1 = SignExtend(g1, 10);
                        b1 = SignExtend(b1, 10);
                    }

                    r0 = Unquantize(r0, 10, signed);
                    g0 = Unquantize(g0, 10, signed);
                    b0 = Unquantize(b0, 10, signed);

                    r1 = Unquantize(r1, 10, signed);
                    g1 = Unquantize(g1, 10, signed);
                    b1 = Unquantize(b1, 10, signed);

                    subsetCount = 1;
                    break;
                case 6:
                    r0 = (int)(((low >> 29) & 0x400) | ((low >> 5) & 0x3FF));
                    g0 = (int)(((low >> 40) & 0x400) | ((low >> 15) & 0x3FF));
                    b0 = (int)(((low >> 49) & 0x400) | ((low >> 25) & 0x3FF));

                    if (signed)
                    {
                        r0 = SignExtend(r0, 11);
                        g0 = SignExtend(g0, 11);
                        b0 = SignExtend(b0, 11);
                    }

                    r1 = r0 + SignExtend((int)(low >> 35), 4);
                    g1 = g0 + SignExtend((int)(low >> 45), 5);
                    b1 = b0 + SignExtend((int)(low >> 55), 4);

                    r2 = r0 + SignExtend((int)(high >> 1), 4);
                    g2 = g0 + SignExtend((int)(((high >> 7) & 0x10) | ((low >> 41) & 0x0F)), 5);
                    b2 = b0 + SignExtend((int)(((high << 3) & 0x08) | ((low >> 61))), 4);

                    r3 = r0 + SignExtend((int)(high >> 7), 4);
                    g3 = g0 + SignExtend((int)(((low >> 36) & 0x10) | ((low >> 51) & 0x0F)), 5);
                    b3 = b0 + SignExtend((int)(
                        ((high >> 9) & 8) |
                        ((high >> 4) & 4) |
                        ((low >> 59) & 2) |
                        ((high >> 5) & 1)), 4);

                    r0 = Unquantize(r0, 11, signed);
                    g0 = Unquantize(g0, 11, signed);
                    b0 = Unquantize(b0, 11, signed);

                    r1 = Unquantize(r1 & 0x7FF, 11, signed);
                    g1 = Unquantize(g1 & 0x7FF, 11, signed);
                    b1 = Unquantize(b1 & 0x7FF, 11, signed);

                    r2 = Unquantize(r2 & 0x7FF, 11, signed);
                    g2 = Unquantize(g2 & 0x7FF, 11, signed);
                    b2 = Unquantize(b2 & 0x7FF, 11, signed);

                    r3 = Unquantize(r3 & 0x7FF, 11, signed);
                    g3 = Unquantize(g3 & 0x7FF, 11, signed);
                    b3 = Unquantize(b3 & 0x7FF, 11, signed);

                    subsetCount = 2;
                    break;
                case 7:
                    r0 = (int)(((low >> 34) & 0x400) | ((low >> 5) & 0x3FF));
                    g0 = (int)(((low >> 44) & 0x400) | ((low >> 15) & 0x3FF));
                    b0 = (int)(((high << 10) & 0x400) | ((low >> 25) & 0x3FF));

                    if (signed)
                    {
                        r0 = SignExtend(r0, 11);
                        g0 = SignExtend(g0, 11);
                        b0 = SignExtend(b0, 11);
                    }

                    r1 = (r0 + SignExtend((int)(low >> 35), 9)) & 0x7FF;
                    g1 = (g0 + SignExtend((int)(low >> 45), 9)) & 0x7FF;
                    b1 = (b0 + SignExtend((int)(low >> 55), 9)) & 0x7FF;

                    r0 = Unquantize(r0, 11, signed);
                    g0 = Unquantize(g0, 11, signed);
                    b0 = Unquantize(b0, 11, signed);

                    r1 = Unquantize(r1, 11, signed);
                    g1 = Unquantize(g1, 11, signed);
                    b1 = Unquantize(b1, 11, signed);

                    subsetCount = 1;
                    break;
                case 10:
                    r0 = (int)(((low >> 29) & 0x400) | ((low >> 5) & 0x3FF));
                    g0 = (int)(((low >> 39) & 0x400) | ((low >> 15) & 0x3FF));
                    b0 = (int)(((low >> 50) & 0x400) | ((low >> 25) & 0x3FF));

                    if (signed)
                    {
                        r0 = SignExtend(r0, 11);
                        g0 = SignExtend(g0, 11);
                        b0 = SignExtend(b0, 11);
                    }

                    r1 = r0 + SignExtend((int)(low >> 35), 4);
                    g1 = g0 + SignExtend((int)(low >> 45), 4);
                    b1 = b0 + SignExtend((int)(low >> 55), 5);

                    r2 = r0 + SignExtend((int)(high >> 1), 4);
                    g2 = g0 + SignExtend((int)(low >> 41), 4);
                    b2 = b0 + SignExtend((int)(((low >> 36) & 0x10) | ((high << 3) & 8) | (low >> 61)), 5);

                    r3 = r0 + SignExtend((int)(high >> 7), 4);
                    g3 = g0 + SignExtend((int)(low >> 51), 4);
                    b3 = b0 + SignExtend((int)(
                        ((high >> 7) & 0x10) |
                        ((high >> 9) & 0x08) |
                        ((high >> 4) & 0x06) |
                        ((low >> 50) & 0x01)), 5);

                    r0 = Unquantize(r0, 11, signed);
                    g0 = Unquantize(g0, 11, signed);
                    b0 = Unquantize(b0, 11, signed);

                    r1 = Unquantize(r1 & 0x7FF, 11, signed);
                    g1 = Unquantize(g1 & 0x7FF, 11, signed);
                    b1 = Unquantize(b1 & 0x7FF, 11, signed);

                    r2 = Unquantize(r2 & 0x7FF, 11, signed);
                    g2 = Unquantize(g2 & 0x7FF, 11, signed);
                    b2 = Unquantize(b2 & 0x7FF, 11, signed);

                    r3 = Unquantize(r3 & 0x7FF, 11, signed);
                    g3 = Unquantize(g3 & 0x7FF, 11, signed);
                    b3 = Unquantize(b3 & 0x7FF, 11, signed);

                    subsetCount = 2;
                    break;
                case 11:
                    r0 = (int)(((low >> 32) & 0x800) | ((low >> 34) & 0x400) | ((low >> 5) & 0x3FF));
                    g0 = (int)(((low >> 42) & 0x800) | ((low >> 44) & 0x400) | ((low >> 15) & 0x3FF));
                    b0 = (int)(((low >> 52) & 0x800) | ((high << 10) & 0x400) | ((low >> 25) & 0x3FF));

                    if (signed)
                    {
                        r0 = SignExtend(r0, 12);
                        g0 = SignExtend(g0, 12);
                        b0 = SignExtend(b0, 12);
                    }

                    r1 = (r0 + SignExtend((int)(low >> 35), 8)) & 0xFFF;
                    g1 = (g0 + SignExtend((int)(low >> 45), 8)) & 0xFFF;
                    b1 = (b0 + SignExtend((int)(low >> 55), 8)) & 0xFFF;

                    r0 = Unquantize(r0, 12, signed);
                    g0 = Unquantize(g0, 12, signed);
                    b0 = Unquantize(b0, 12, signed);

                    r1 = Unquantize(r1, 12, signed);
                    g1 = Unquantize(g1, 12, signed);
                    b1 = Unquantize(b1, 12, signed);

                    subsetCount = 1;
                    break;
                case 14:
                    r0 = (int)(low >> 5) & 0x1FF;
                    g0 = (int)(low >> 15) & 0x1FF;
                    b0 = (int)(low >> 25) & 0x1FF;

                    if (signed)
                    {
                        r0 = SignExtend(r0, 9);
                        g0 = SignExtend(g0, 9);
                        b0 = SignExtend(b0, 9);
                    }

                    r1 = r0 + SignExtend((int)(low >> 35), 5);
                    g1 = g0 + SignExtend((int)(low >> 45), 5);
                    b1 = b0 + SignExtend((int)(low >> 55), 5);

                    r2 = r0 + SignExtend((int)(high >> 1), 5);
                    g2 = g0 + SignExtend((int)(((low >> 20) & 0x10) | ((low >> 41) & 0xF)), 5);
                    b2 = b0 + SignExtend((int)(((low >> 10) & 0x10) | ((high << 3) & 8) | (low >> 61)), 5);

                    r3 = r0 + SignExtend((int)(high >> 7), 5);
                    g3 = g0 + SignExtend((int)(((low >> 36) & 0x10) | ((low >> 51) & 0xF)), 5);
                    b3 = b0 + SignExtend((int)(
                        ((low >> 30) & 0x10) |
                        ((high >> 9) & 0x08) |
                        ((high >> 4) & 0x04) |
                        ((low >> 59) & 0x02) |
                        ((low >> 50) & 0x01)), 5);

                    r0 = Unquantize(r0, 9, signed);
                    g0 = Unquantize(g0, 9, signed);
                    b0 = Unquantize(b0, 9, signed);

                    r1 = Unquantize(r1 & 0x1FF, 9, signed);
                    g1 = Unquantize(g1 & 0x1FF, 9, signed);
                    b1 = Unquantize(b1 & 0x1FF, 9, signed);

                    r2 = Unquantize(r2 & 0x1FF, 9, signed);
                    g2 = Unquantize(g2 & 0x1FF, 9, signed);
                    b2 = Unquantize(b2 & 0x1FF, 9, signed);

                    r3 = Unquantize(r3 & 0x1FF, 9, signed);
                    g3 = Unquantize(g3 & 0x1FF, 9, signed);
                    b3 = Unquantize(b3 & 0x1FF, 9, signed);

                    subsetCount = 2;
                    break;
                case 15:
                    r0 = (BitReverse6((int)(low >> 39) & 0x3F) << 10) | ((int)(low >> 5) & 0x3FF);
                    g0 = (BitReverse6((int)(low >> 49) & 0x3F) << 10) | ((int)(low >> 15) & 0x3FF);
                    b0 = ((BitReverse6((int)(low >> 59)) | (int)(high & 1)) << 10) | ((int)(low >> 25) & 0x3FF);

                    if (signed)
                    {
                        r0 = SignExtend(r0, 16);
                        g0 = SignExtend(g0, 16);
                        b0 = SignExtend(b0, 16);
                    }

                    r1 = (r0 + SignExtend((int)(low >> 35), 4)) & 0xFFFF;
                    g1 = (g0 + SignExtend((int)(low >> 45), 4)) & 0xFFFF;
                    b1 = (b0 + SignExtend((int)(low >> 55), 4)) & 0xFFFF;

                    subsetCount = 1;
                    break;
                case 18:
                    r0 = (int)(low >> 5) & 0xFF;
                    g0 = (int)(low >> 15) & 0xFF;
                    b0 = (int)(low >> 25) & 0xFF;

                    if (signed)
                    {
                        r0 = SignExtend(r0, 8);
                        g0 = SignExtend(g0, 8);
                        b0 = SignExtend(b0, 8);
                    }

                    r1 = r0 + SignExtend((int)(low >> 35), 6);
                    g1 = g0 + SignExtend((int)(low >> 45), 5);
                    b1 = b0 + SignExtend((int)(low >> 55), 5);

                    r2 = r0 + SignExtend((int)(high >> 1), 6);
                    g2 = g0 + SignExtend((int)(((low >> 20) & 0x10) | ((low >> 41) & 0xF)), 5);
                    b2 = b0 + SignExtend((int)(((low >> 10) & 0x10) | ((high << 3) & 8) | (low >> 61)), 5);

                    r3 = r0 + SignExtend((int)(high >> 7), 6);
                    g3 = g0 + SignExtend((int)(((low >> 9) & 0x10) | ((low >> 51) & 0xF)), 5);
                    b3 = b0 + SignExtend((int)(
                        ((low >> 30) & 0x18) |
                        ((low >> 21) & 0x04) |
                        ((low >> 59) & 0x02) |
                        ((low >> 50) & 0x01)), 5);

                    r0 = Unquantize(r0, 8, signed);
                    g0 = Unquantize(g0, 8, signed);
                    b0 = Unquantize(b0, 8, signed);

                    r1 = Unquantize(r1 & 0xFF, 8, signed);
                    g1 = Unquantize(g1 & 0xFF, 8, signed);
                    b1 = Unquantize(b1 & 0xFF, 8, signed);

                    r2 = Unquantize(r2 & 0xFF, 8, signed);
                    g2 = Unquantize(g2 & 0xFF, 8, signed);
                    b2 = Unquantize(b2 & 0xFF, 8, signed);

                    r3 = Unquantize(r3 & 0xFF, 8, signed);
                    g3 = Unquantize(g3 & 0xFF, 8, signed);
                    b3 = Unquantize(b3 & 0xFF, 8, signed);

                    subsetCount = 2;
                    break;
                case 22:
                    r0 = (int)(low >> 5) & 0xFF;
                    g0 = (int)(low >> 15) & 0xFF;
                    b0 = (int)(low >> 25) & 0xFF;

                    if (signed)
                    {
                        r0 = SignExtend(r0, 8);
                        g0 = SignExtend(g0, 8);
                        b0 = SignExtend(b0, 8);
                    }

                    r1 = r0 + SignExtend((int)(low >> 35), 5);
                    g1 = g0 + SignExtend((int)(low >> 45), 6);
                    b1 = b0 + SignExtend((int)(low >> 55), 5);

                    r2 = r0 + SignExtend((int)(high >> 1), 5);
                    g2 = g0 + SignExtend((int)(((low >> 18) & 0x20) | ((low >> 20) & 0x10) | ((low >> 41) & 0xF)), 6);
                    b2 = b0 + SignExtend((int)(((low >> 10) & 0x10) | ((high << 3) & 0x08) | (low >> 61)), 5);

                    r3 = r0 + SignExtend((int)(high >> 7), 5);
                    g3 = g0 + SignExtend((int)(((low >> 28) & 0x20) | ((low >> 36) & 0x10) | ((low >> 51) & 0x0F)), 6);
                    b3 = b0 + SignExtend((int)(
                        ((low >> 30) & 0x10) |
                        ((high >> 9) & 0x08) |
                        ((high >> 4) & 0x04) |
                        ((low >> 59) & 0x02) |
                        ((low >> 13) & 0x01)), 5);

                    r0 = Unquantize(r0, 8, signed);
                    g0 = Unquantize(g0, 8, signed);
                    b0 = Unquantize(b0, 8, signed);

                    r1 = Unquantize(r1 & 0xFF, 8, signed);
                    g1 = Unquantize(g1 & 0xFF, 8, signed);
                    b1 = Unquantize(b1 & 0xFF, 8, signed);

                    r2 = Unquantize(r2 & 0xFF, 8, signed);
                    g2 = Unquantize(g2 & 0xFF, 8, signed);
                    b2 = Unquantize(b2 & 0xFF, 8, signed);

                    r3 = Unquantize(r3 & 0xFF, 8, signed);
                    g3 = Unquantize(g3 & 0xFF, 8, signed);
                    b3 = Unquantize(b3 & 0xFF, 8, signed);

                    subsetCount = 2;
                    break;
                case 26:
                    r0 = (int)(low >> 5) & 0xFF;
                    g0 = (int)(low >> 15) & 0xFF;
                    b0 = (int)(low >> 25) & 0xFF;

                    if (signed)
                    {
                        r0 = SignExtend(r0, 8);
                        g0 = SignExtend(g0, 8);
                        b0 = SignExtend(b0, 8);
                    }

                    r1 = r0 + SignExtend((int)(low >> 35), 5);
                    g1 = g0 + SignExtend((int)(low >> 45), 5);
                    b1 = b0 + SignExtend((int)(low >> 55), 6);

                    r2 = r0 + SignExtend((int)(high >> 1), 5);
                    g2 = g0 + SignExtend((int)(((low >> 20) & 0x10) | ((low >> 41) & 0xF)), 5);
                    b2 = b0 + SignExtend((int)(
                        ((low >> 18) & 0x20) |
                        ((low >> 10) & 0x10) |
                        ((high << 3) & 0x08) |
                        (low >> 61)), 6);

                    r3 = r0 + SignExtend((int)(high >> 7), 5);
                    g3 = g0 + SignExtend((int)(((low >> 36) & 0x10) | ((low >> 51) & 0xF)), 5);
                    b3 = b0 + SignExtend((int)(
                        ((low >> 28) & 0x20) |
                        ((low >> 30) & 0x10) |
                        ((high >> 9) & 0x08) |
                        ((high >> 4) & 0x04) |
                        ((low >> 12) & 0x02) |
                        ((low >> 50) & 0x01)), 6);

                    r0 = Unquantize(r0, 8, signed);
                    g0 = Unquantize(g0, 8, signed);
                    b0 = Unquantize(b0, 8, signed);

                    r1 = Unquantize(r1 & 0xFF, 8, signed);
                    g1 = Unquantize(g1 & 0xFF, 8, signed);
                    b1 = Unquantize(b1 & 0xFF, 8, signed);

                    r2 = Unquantize(r2 & 0xFF, 8, signed);
                    g2 = Unquantize(g2 & 0xFF, 8, signed);
                    b2 = Unquantize(b2 & 0xFF, 8, signed);

                    r3 = Unquantize(r3 & 0xFF, 8, signed);
                    g3 = Unquantize(g3 & 0xFF, 8, signed);
                    b3 = Unquantize(b3 & 0xFF, 8, signed);

                    subsetCount = 2;
                    break;
                case 30:
                    r0 = (int)(low >> 5) & 0x3F;
                    g0 = (int)(low >> 15) & 0x3F;
                    b0 = (int)(low >> 25) & 0x3F;

                    r1 = (int)(low >> 35) & 0x3F;
                    g1 = (int)(low >> 45) & 0x3F;
                    b1 = (int)(low >> 55) & 0x3F;

                    r2 = (int)(high >> 1) & 0x3F;
                    g2 = (int)(((low >> 16) & 0x20) | ((low >> 20) & 0x10) | ((low >> 41) & 0xF));
                    b2 = (int)(((low >> 17) & 0x20) | ((low >> 10) & 0x10) | ((high << 3) & 0x08) | (low >> 61));

                    r3 = (int)(high >> 7) & 0x3F;
                    g3 = (int)(((low >> 26) & 0x20) | ((low >> 7) & 0x10) | ((low >> 51) & 0xF));
                    b3 = (int)(
                        ((low >> 28) & 0x20) |
                        ((low >> 30) & 0x10) |
                        ((low >> 29) & 0x08) |
                        ((low >> 21) & 0x04) |
                        ((low >> 12) & 0x03));

                    if (signed)
                    {
                        r0 = SignExtend(r0, 6);
                        g0 = SignExtend(g0, 6);
                        b0 = SignExtend(b0, 6);

                        r1 = SignExtend(r1, 6);
                        g1 = SignExtend(g1, 6);
                        b1 = SignExtend(b1, 6);

                        r2 = SignExtend(r2, 6);
                        g2 = SignExtend(g2, 6);
                        b2 = SignExtend(b2, 6);

                        r3 = SignExtend(r3, 6);
                        g3 = SignExtend(g3, 6);
                        b3 = SignExtend(b3, 6);
                    }

                    r0 = Unquantize(r0, 6, signed);
                    g0 = Unquantize(g0, 6, signed);
                    b0 = Unquantize(b0, 6, signed);

                    r1 = Unquantize(r1, 6, signed);
                    g1 = Unquantize(g1, 6, signed);
                    b1 = Unquantize(b1, 6, signed);

                    r2 = Unquantize(r2, 6, signed);
                    g2 = Unquantize(g2, 6, signed);
                    b2 = Unquantize(b2, 6, signed);

                    r3 = Unquantize(r3, 6, signed);
                    g3 = Unquantize(g3, 6, signed);
                    b3 = Unquantize(b3, 6, signed);

                    subsetCount = 2;
                    break;
                default:
                    subsetCount = 0;
                    break;
            }

            if (subsetCount > 0)
            {
                endPoints[0] = new RgbaColor32(r0, g0, b0, HalfOne);
                endPoints[1] = new RgbaColor32(r1, g1, b1, HalfOne);

                if (subsetCount > 1)
                {
                    endPoints[2] = new RgbaColor32(r2, g2, b2, HalfOne);
                    endPoints[3] = new RgbaColor32(r3, g3, b3, HalfOne);
                }
            }

            return subsetCount;
        }

        private static int SignExtend(int value, int bits)
        {
            int shift = 32 - bits;
            return (value << shift) >> shift;
        }

        private static int Unquantize(int value, int bits, bool signed)
        {
            if (signed)
            {
                if (bits >= 16)
                {
                    return value;
                }
                else
                {
                    bool sign = value < 0;

                    if (sign)
                    {
                        value = -value;
                    }

                    if (value == 0)
                    {
                        return value;
                    }
                    else if (value >= ((1 << (bits - 1)) - 1))
                    {
                        value = 0x7FFF;
                    }
                    else
                    {
                        value = ((value << 15) + 0x4000) >> (bits - 1);
                    }

                    if (sign)
                    {
                        value = -value;
                    }
                }
            }
            else
            {
                if (bits >= 15 || value == 0)
                {
                    return value;
                }
                else if (value == ((1 << bits) - 1))
                {
                    return 0xFFFF;
                }
                else
                {
                    return ((value << 16) + 0x8000) >> bits;
                }
            }

            return value;
        }

        private static ushort FinishUnquantize(int value, bool signed)
        {
            if (signed)
            {
                value = value < 0 ? -((-value * 31) >> 5) : (value * 31) >> 5;

                int sign = 0;
                if (value < 0)
                {
                    sign = 0x8000;
                    value = -value;
                }

                return (ushort)(sign | value);
            }
            else
            {
                return (ushort)((value * 31) >> 6);
            }
        }

        private static int BitReverse6(int value)
        {
            value = ((value >> 1) & 0x55) | ((value << 1) & 0xaa);
            value = ((value >> 2) & 0x33) | ((value << 2) & 0xcc);
            value = ((value >> 4) & 0x0f) | ((value << 4) & 0xf0);
            return value >> 2;
        }
    }
}
