using System.Numerics;

namespace Ryujinx.Common
{
    public static class BitUtils
    {
        public static T AlignUp<T>(T value, T size) where T : IBinaryInteger<T>
            => (value + (size - T.One)) & -size;

        public static T AlignDown<T>(T value, T size) where T : IBinaryInteger<T>
            => value & -size;

        public static T DivRoundUp<T>(T value, T dividend) where T : IBinaryInteger<T>
            => (value + (dividend - T.One)) / dividend;

        public static int Pow2RoundDown(int value) => BitOperations.IsPow2(value) ? value : Pow2RoundUp(value) >> 1;

        public static long ReverseBits64(long value) => (long)ReverseBits64((ulong)value);

        public static int Pow2RoundUp(int value)
        {
            value--;

            value |= (value >> 1);
            value |= (value >> 2);
            value |= (value >> 4);
            value |= (value >> 8);
            value |= (value >> 16);

            return ++value;
        }

        private static ulong ReverseBits64(ulong value)
        {
            value = ((value & 0xaaaaaaaaaaaaaaaa) >> 1) | ((value & 0x5555555555555555) << 1);
            value = ((value & 0xcccccccccccccccc) >> 2) | ((value & 0x3333333333333333) << 2);
            value = ((value & 0xf0f0f0f0f0f0f0f0) >> 4) | ((value & 0x0f0f0f0f0f0f0f0f) << 4);
            value = ((value & 0xff00ff00ff00ff00) >> 8) | ((value & 0x00ff00ff00ff00ff) << 8);
            value = ((value & 0xffff0000ffff0000) >> 16) | ((value & 0x0000ffff0000ffff) << 16);

            return (value >> 32) | (value << 32);
        }
    }
}
