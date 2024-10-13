using System;
using System.Globalization;

namespace Ryujinx.Common.Utilities
{
    public static class UInt128Utils
    {
        public static UInt128 FromHex(string hex) =>
            new(
                ulong.Parse(hex.AsSpan(0, 16), NumberStyles.HexNumber),
                ulong.Parse(hex.AsSpan(16), NumberStyles.HexNumber)
            );

        public static Int128 NextInt128(this Random rand) =>
            new((ulong)rand.NextInt64(), (ulong)rand.NextInt64());

        public static UInt128 NextUInt128(this Random rand) =>
            new((ulong)rand.NextInt64(), (ulong)rand.NextInt64());
    }
}
