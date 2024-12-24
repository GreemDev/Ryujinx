using System;
using System.Buffers.Binary;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
// ReSharper disable InconsistentNaming

namespace Ryujinx.Common
{
    [StructLayout(LayoutKind.Sequential)]
    public struct Hash128(ulong low, ulong high) : IEquatable<Hash128>
    {
        public ulong Low = low;
        public ulong High = high;

        public readonly override string ToString() => $"{High:x16}{Low:x16}";

        public static bool operator ==(Hash128 x, Hash128 y) => x.Equals(y);

        public static bool operator !=(Hash128 x, Hash128 y) => !x.Equals(y);

        public readonly override bool Equals(object obj) => obj is Hash128 hash128 && Equals(hash128);

        public readonly bool Equals(Hash128 cmpObj) => Low == cmpObj.Low && High == cmpObj.High;

        public readonly override int GetHashCode() => HashCode.Combine(Low, High);

        public static Hash128 ComputeHash(ReadOnlySpan<byte> input) => Xxh3128bitsInternal(input, Xxh3KSecret, 0UL);

        #region Hash computation

        private const int StripeLen = 64;
        private const int AccNb = StripeLen / sizeof(ulong);
        private const int SecretConsumeRate = 8;
        private const int SecretLastAccStart = 7;
        private const int SecretMergeAccsStart = 11;
        private const int SecretSizeMin = 136;
        private const int MidSizeStartOffset = 3;
        private const int MidSizeLastOffset = 17;

        private const uint Prime32_1 = 0x9E3779B1U;
        private const uint Prime32_2 = 0x85EBCA77U;
        private const uint Prime32_3 = 0xC2B2AE3DU;
        private const uint Prime32_4 = 0x27D4EB2FU;
        private const uint Prime32_5 = 0x165667B1U;

        private const ulong Prime64_1 = 0x9E3779B185EBCA87UL;
        private const ulong Prime64_2 = 0xC2B2AE3D27D4EB4FUL;
        private const ulong Prime64_3 = 0x165667B19E3779F9UL;
        private const ulong Prime64_4 = 0x85EBCA77C2B2AE63UL;
        private const ulong Prime64_5 = 0x27D4EB2F165667C5UL;

        private static readonly ulong[] _xxh3InitAcc =
        [
            Prime32_3,
            Prime64_1,
            Prime64_2,
            Prime64_3,
            Prime64_4,
            Prime32_2,
            Prime64_5,
            Prime32_1
        ];

        private static ReadOnlySpan<byte> Xxh3KSecret =>
        [
            0xb8,
            0xfe,
            0x6c,
            0x39,
            0x23,
            0xa4,
            0x4b,
            0xbe,
            0x7c,
            0x01,
            0x81,
            0x2c,
            0xf7,
            0x21,
            0xad,
            0x1c,
            0xde,
            0xd4,
            0x6d,
            0xe9,
            0x83,
            0x90,
            0x97,
            0xdb,
            0x72,
            0x40,
            0xa4,
            0xa4,
            0xb7,
            0xb3,
            0x67,
            0x1f,
            0xcb,
            0x79,
            0xe6,
            0x4e,
            0xcc,
            0xc0,
            0xe5,
            0x78,
            0x82,
            0x5a,
            0xd0,
            0x7d,
            0xcc,
            0xff,
            0x72,
            0x21,
            0xb8,
            0x08,
            0x46,
            0x74,
            0xf7,
            0x43,
            0x24,
            0x8e,
            0xe0,
            0x35,
            0x90,
            0xe6,
            0x81,
            0x3a,
            0x26,
            0x4c,
            0x3c,
            0x28,
            0x52,
            0xbb,
            0x91,
            0xc3,
            0x00,
            0xcb,
            0x88,
            0xd0,
            0x65,
            0x8b,
            0x1b,
            0x53,
            0x2e,
            0xa3,
            0x71,
            0x64,
            0x48,
            0x97,
            0xa2,
            0x0d,
            0xf9,
            0x4e,
            0x38,
            0x19,
            0xef,
            0x46,
            0xa9,
            0xde,
            0xac,
            0xd8,
            0xa8,
            0xfa,
            0x76,
            0x3f,
            0xe3,
            0x9c,
            0x34,
            0x3f,
            0xf9,
            0xdc,
            0xbb,
            0xc7,
            0xc7,
            0x0b,
            0x4f,
            0x1d,
            0x8a,
            0x51,
            0xe0,
            0x4b,
            0xcd,
            0xb4,
            0x59,
            0x31,
            0xc8,
            0x9f,
            0x7e,
            0xc9,
            0xd9,
            0x78,
            0x73,
            0x64,
            0xea,
            0xc5,
            0xac,
            0x83,
            0x34,
            0xd3,
            0xeb,
            0xc3,
            0xc5,
            0x81,
            0xa0,
            0xff,
            0xfa,
            0x13,
            0x63,
            0xeb,
            0x17,
            0x0d,
            0xdd,
            0x51,
            0xb7,
            0xf0,
            0xda,
            0x49,
            0xd3,
            0x16,
            0x55,
            0x26,
            0x29,
            0xd4,
            0x68,
            0x9e,
            0x2b,
            0x16,
            0xbe,
            0x58,
            0x7d,
            0x47,
            0xa1,
            0xfc,
            0x8f,
            0xf8,
            0xb8,
            0xd1,
            0x7a,
            0xd0,
            0x31,
            0xce,
            0x45,
            0xcb,
            0x3a,
            0x8f,
            0x95,
            0x16,
            0x04,
            0x28,
            0xaf,
            0xd7,
            0xfb,
            0xca,
            0xbb,
            0x4b,
            0x40,
            0x7e
        ];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong Mult32To64(ulong x, ulong y) => (uint)x * (ulong)(uint)y;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Hash128 Mult64To128(ulong lhs, ulong rhs)
        {
            ulong high = Math.BigMul(lhs, rhs, out ulong low);

            return new Hash128
            {
                Low = low,
                High = high,
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong Mul128Fold64(ulong lhs, ulong rhs)
        {
            Hash128 product = Mult64To128(lhs, rhs);

            return product.Low ^ product.High;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong XorShift64(ulong v64, int shift)
        {
            Debug.Assert(shift is >= 0 and < 64);

            return v64 ^ (v64 >> shift);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong Xxh3Avalanche(ulong h64)
        {
            h64 = XorShift64(h64, 37);
            h64 *= 0x165667919E3779F9UL;
            h64 = XorShift64(h64, 32);

            return h64;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong Xxh64Avalanche(ulong h64)
        {
            h64 ^= h64 >> 33;
            h64 *= Prime64_2;
            h64 ^= h64 >> 29;
            h64 *= Prime64_3;
            h64 ^= h64 >> 32;

            return h64;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private unsafe static void Xxh3Accumulate512(Span<ulong> acc, ReadOnlySpan<byte> input, ReadOnlySpan<byte> secret)
        {
            if (Avx2.IsSupported)
            {
                fixed (ulong* pAcc = acc)
                {
                    fixed (byte* pInput = input, pSecret = secret)
                    {
                        Vector256<ulong>* xAcc = (Vector256<ulong>*)pAcc;
                        Vector256<byte>* xInput = (Vector256<byte>*)pInput;
                        Vector256<byte>* xSecret = (Vector256<byte>*)pSecret;

                        for (ulong i = 0; i < StripeLen / 32; i++)
                        {
                            Vector256<byte> dataVec = xInput[i];
                            Vector256<byte> keyVec = xSecret[i];
                            Vector256<byte> dataKey = Avx2.Xor(dataVec, keyVec);
                            Vector256<uint> dataKeyLo = Avx2.Shuffle(dataKey.AsUInt32(), 0b00110001);
                            Vector256<ulong> product = Avx2.Multiply(dataKey.AsUInt32(), dataKeyLo);
                            Vector256<uint> dataSwap = Avx2.Shuffle(dataVec.AsUInt32(), 0b01001110);
                            Vector256<ulong> sum = Avx2.Add(xAcc[i], dataSwap.AsUInt64());
                            xAcc[i] = Avx2.Add(product, sum);
                        }
                    }
                }
            }
            else if (Sse2.IsSupported)
            {
                fixed (ulong* pAcc = acc)
                {
                    fixed (byte* pInput = input, pSecret = secret)
                    {
                        Vector128<ulong>* xAcc = (Vector128<ulong>*)pAcc;
                        Vector128<byte>* xInput = (Vector128<byte>*)pInput;
                        Vector128<byte>* xSecret = (Vector128<byte>*)pSecret;

                        for (ulong i = 0; i < StripeLen / 16; i++)
                        {
                            Vector128<byte> dataVec = xInput[i];
                            Vector128<byte> keyVec = xSecret[i];
                            Vector128<byte> dataKey = Sse2.Xor(dataVec, keyVec);
                            Vector128<uint> dataKeyLo = Sse2.Shuffle(dataKey.AsUInt32(), 0b00110001);
                            Vector128<ulong> product = Sse2.Multiply(dataKey.AsUInt32(), dataKeyLo);
                            Vector128<uint> dataSwap = Sse2.Shuffle(dataVec.AsUInt32(), 0b01001110);
                            Vector128<ulong> sum = Sse2.Add(xAcc[i], dataSwap.AsUInt64());
                            xAcc[i] = Sse2.Add(product, sum);
                        }
                    }
                }
            }
            else
            {
                for (int i = 0; i < AccNb; i++)
                {
                    ulong dataVal = BinaryPrimitives.ReadUInt64LittleEndian(input[(i * sizeof(ulong))..]);
                    ulong dataKey = dataVal ^ BinaryPrimitives.ReadUInt64LittleEndian(secret[(i * sizeof(ulong))..]);
                    acc[i ^ 1] += dataVal;
                    acc[i] += Mult32To64((uint)dataKey, dataKey >> 32);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private unsafe static void Xxh3ScrambleAcc(Span<ulong> acc, ReadOnlySpan<byte> secret)
        {
            if (Avx2.IsSupported)
            {
                fixed (ulong* pAcc = acc)
                {
                    fixed (byte* pSecret = secret)
                    {
                        Vector256<uint> prime32 = Vector256.Create(Prime32_1);
                        Vector256<ulong>* xAcc = (Vector256<ulong>*)pAcc;
                        Vector256<byte>* xSecret = (Vector256<byte>*)pSecret;

                        for (ulong i = 0; i < StripeLen / 32; i++)
                        {
                            Vector256<ulong> accVec = xAcc[i];
                            Vector256<ulong> shifted = Avx2.ShiftRightLogical(accVec, 47);
                            Vector256<ulong> dataVec = Avx2.Xor(accVec, shifted);

                            Vector256<byte> keyVec = xSecret[i];
                            Vector256<uint> dataKey = Avx2.Xor(dataVec.AsUInt32(), keyVec.AsUInt32());

                            Vector256<uint> dataKeyHi = Avx2.Shuffle(dataKey.AsUInt32(), 0b00110001);
                            Vector256<ulong> prodLo = Avx2.Multiply(dataKey, prime32);
                            Vector256<ulong> prodHi = Avx2.Multiply(dataKeyHi, prime32);

                            xAcc[i] = Avx2.Add(prodLo, Avx2.ShiftLeftLogical(prodHi, 32));
                        }
                    }
                }
            }
            else if (Sse2.IsSupported)
            {
                fixed (ulong* pAcc = acc)
                {
                    fixed (byte* pSecret = secret)
                    {
                        Vector128<uint> prime32 = Vector128.Create(Prime32_1);
                        Vector128<ulong>* xAcc = (Vector128<ulong>*)pAcc;
                        Vector128<byte>* xSecret = (Vector128<byte>*)pSecret;

                        for (ulong i = 0; i < StripeLen / 16; i++)
                        {
                            Vector128<ulong> accVec = xAcc[i];
                            Vector128<ulong> shifted = Sse2.ShiftRightLogical(accVec, 47);
                            Vector128<ulong> dataVec = Sse2.Xor(accVec, shifted);

                            Vector128<byte> keyVec = xSecret[i];
                            Vector128<uint> dataKey = Sse2.Xor(dataVec.AsUInt32(), keyVec.AsUInt32());

                            Vector128<uint> dataKeyHi = Sse2.Shuffle(dataKey.AsUInt32(), 0b00110001);
                            Vector128<ulong> prodLo = Sse2.Multiply(dataKey, prime32);
                            Vector128<ulong> prodHi = Sse2.Multiply(dataKeyHi, prime32);

                            xAcc[i] = Sse2.Add(prodLo, Sse2.ShiftLeftLogical(prodHi, 32));
                        }
                    }
                }
            }
            else
            {
                for (int i = 0; i < AccNb; i++)
                {
                    ulong key64 = BinaryPrimitives.ReadUInt64LittleEndian(secret[(i * sizeof(ulong))..]);
                    ulong acc64 = acc[i];
                    acc64 = XorShift64(acc64, 47);
                    acc64 ^= key64;
                    acc64 *= Prime32_1;
                    acc[i] = acc64;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void Xxh3Accumulate(Span<ulong> acc, ReadOnlySpan<byte> input, ReadOnlySpan<byte> secret, int nbStripes)
        {
            for (int n = 0; n < nbStripes; n++)
            {
                ReadOnlySpan<byte> inData = input[(n * StripeLen)..];
                Xxh3Accumulate512(acc, inData, secret[(n * SecretConsumeRate)..]);
            }
        }

        private static void Xxh3HashLongInternalLoop(Span<ulong> acc, ReadOnlySpan<byte> input, ReadOnlySpan<byte> secret)
        {
            int nbStripesPerBlock = (secret.Length - StripeLen) / SecretConsumeRate;
            int blockLen = StripeLen * nbStripesPerBlock;
            int nbBlocks = (input.Length - 1) / blockLen;

            Debug.Assert(secret.Length >= SecretSizeMin);

            for (int n = 0; n < nbBlocks; n++)
            {
                Xxh3Accumulate(acc, input[(n * blockLen)..], secret, nbStripesPerBlock);
                Xxh3ScrambleAcc(acc, secret[^StripeLen..]);
            }

            Debug.Assert(input.Length > StripeLen);

            int nbStripes = (input.Length - 1 - (blockLen * nbBlocks)) / StripeLen;
            Debug.Assert(nbStripes <= (secret.Length / SecretConsumeRate));
            Xxh3Accumulate(acc, input[(nbBlocks * blockLen)..], secret, nbStripes);

            ReadOnlySpan<byte> p = input[^StripeLen..];
            Xxh3Accumulate512(acc, p, secret[(secret.Length - StripeLen - SecretLastAccStart)..]);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong Xxh3Mix2Accs(Span<ulong> acc, ReadOnlySpan<byte> secret)
        {
            return Mul128Fold64(
                acc[0] ^ BinaryPrimitives.ReadUInt64LittleEndian(secret),
                acc[1] ^ BinaryPrimitives.ReadUInt64LittleEndian(secret[8..]));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong Xxh3MergeAccs(Span<ulong> acc, ReadOnlySpan<byte> secret, ulong start)
        {
            ulong result64 = start;

            for (int i = 0; i < 4; i++)
            {
                result64 += Xxh3Mix2Accs(acc[(2 * i)..], secret[(16 * i)..]);
            }

            return Xxh3Avalanche(result64);
        }

        [SkipLocalsInit]
        private static Hash128 Xxh3HashLong128bInternal(ReadOnlySpan<byte> input, ReadOnlySpan<byte> secret)
        {
            Span<ulong> acc = stackalloc ulong[AccNb];
            _xxh3InitAcc.CopyTo(acc);

            Xxh3HashLongInternalLoop(acc, input, secret);

            Debug.Assert(acc.Length == 8);
            Debug.Assert(secret.Length >= acc.Length * sizeof(ulong) + SecretMergeAccsStart);

            return new Hash128
            {
                Low = Xxh3MergeAccs(acc, secret[SecretMergeAccsStart..], (ulong)input.Length * Prime64_1),
                High = Xxh3MergeAccs(
                    acc,
                    secret[(secret.Length - acc.Length * sizeof(ulong) - SecretMergeAccsStart)..],
                    ~((ulong)input.Length * Prime64_2)),
            };
        }

        private static Hash128 Xxh3Len1To3128b(ReadOnlySpan<byte> input, ReadOnlySpan<byte> secret, ulong seed)
        {
            Debug.Assert(1 <= input.Length && input.Length <= 3);

            byte c1 = input[0];
            byte c2 = input[input.Length >> 1];
            byte c3 = input[^1];

            uint combinedL = ((uint)c1 << 16) | ((uint)c2 << 24) | c3 | ((uint)input.Length << 8);
            uint combinedH = BitOperations.RotateLeft(BinaryPrimitives.ReverseEndianness(combinedL), 13);
            ulong bitFlipL = (BinaryPrimitives.ReadUInt32LittleEndian(secret) ^ BinaryPrimitives.ReadUInt32LittleEndian(secret[4..])) + seed;
            ulong bitFlipH = (BinaryPrimitives.ReadUInt32LittleEndian(secret[8..]) ^ BinaryPrimitives.ReadUInt32LittleEndian(secret[12..])) - seed;
            ulong keyedLo = combinedL ^ bitFlipL;
            ulong keyedHi = combinedH ^ bitFlipH;

            return new Hash128
            {
                Low = Xxh64Avalanche(keyedLo),
                High = Xxh64Avalanche(keyedHi),
            };
        }

        private static Hash128 Xxh3Len4To8128b(ReadOnlySpan<byte> input, ReadOnlySpan<byte> secret, ulong seed)
        {
            Debug.Assert(4 <= input.Length && input.Length <= 8);

            seed ^= BinaryPrimitives.ReverseEndianness((uint)seed) << 32;

            uint inputLo = BinaryPrimitives.ReadUInt32LittleEndian(input);
            uint inputHi = BinaryPrimitives.ReadUInt32LittleEndian(input[^4..]);
            ulong input64 = inputLo + ((ulong)inputHi << 32);
            ulong bitFlip = (BinaryPrimitives.ReadUInt64LittleEndian(secret[16..]) ^ BinaryPrimitives.ReadUInt64LittleEndian(secret[24..])) + seed;
            ulong keyed = input64 ^ bitFlip;

            Hash128 m128 = Mult64To128(keyed, Prime64_1 + ((ulong)input.Length << 2));

            m128.High += m128.Low << 1;
            m128.Low ^= m128.High >> 3;

            m128.Low = XorShift64(m128.Low, 35);
            m128.Low *= 0x9FB21C651E98DF25UL;
            m128.Low = XorShift64(m128.Low, 28);
            m128.High = Xxh3Avalanche(m128.High);

            return m128;
        }

        private static Hash128 Xxh3Len9To16128b(ReadOnlySpan<byte> input, ReadOnlySpan<byte> secret, ulong seed)
        {
            Debug.Assert(9 <= input.Length && input.Length <= 16);

            ulong bitFlipL = (BinaryPrimitives.ReadUInt64LittleEndian(secret[32..]) ^ BinaryPrimitives.ReadUInt64LittleEndian(secret[40..])) - seed;
            ulong bitFlipH = (BinaryPrimitives.ReadUInt64LittleEndian(secret[48..]) ^ BinaryPrimitives.ReadUInt64LittleEndian(secret[56..])) + seed;
            ulong inputLo = BinaryPrimitives.ReadUInt64LittleEndian(input);
            ulong inputHi = BinaryPrimitives.ReadUInt64LittleEndian(input[^8..]);

            Hash128 m128 = Mult64To128(inputLo ^ inputHi ^ bitFlipL, Prime64_1);
            m128.Low += ((ulong)input.Length - 1) << 54;
            inputHi ^= bitFlipH;
            m128.High += inputHi + Mult32To64((uint)inputHi, Prime32_2 - 1);
            m128.Low ^= BinaryPrimitives.ReverseEndianness(m128.High);

            Hash128 h128 = Mult64To128(m128.Low, Prime64_2);
            h128.High += m128.High * Prime64_2;
            h128.Low = Xxh3Avalanche(h128.Low);
            h128.High = Xxh3Avalanche(h128.High);

            return h128;
        }

        private static Hash128 Xxh3Len0To16128b(ReadOnlySpan<byte> input, ReadOnlySpan<byte> secret, ulong seed)
        {
            Debug.Assert(input.Length <= 16);

            if (input.Length > 8)
            {
                return Xxh3Len9To16128b(input, secret, seed);
            }

            if (input.Length >= 4)
            {
                return Xxh3Len4To8128b(input, secret, seed);
            }

            if (input.Length != 0)
            {
                return Xxh3Len1To3128b(input, secret, seed);
            }

            Hash128 h128 = new();
            ulong bitFlipL = BinaryPrimitives.ReadUInt64LittleEndian(secret[64..]) ^ BinaryPrimitives.ReadUInt64LittleEndian(secret[72..]);
            ulong bitFlipH = BinaryPrimitives.ReadUInt64LittleEndian(secret[80..]) ^ BinaryPrimitives.ReadUInt64LittleEndian(secret[88..]);
            h128.Low = Xxh64Avalanche(seed ^ bitFlipL);
            h128.High = Xxh64Avalanche(seed ^ bitFlipH);

            return h128;
        }

        private static ulong Xxh3Mix16b(ReadOnlySpan<byte> input, ReadOnlySpan<byte> secret, ulong seed)
        {
            ulong inputLo = BinaryPrimitives.ReadUInt64LittleEndian(input);
            ulong inputHi = BinaryPrimitives.ReadUInt64LittleEndian(input[8..]);

            return Mul128Fold64(
                inputLo ^ (BinaryPrimitives.ReadUInt64LittleEndian(secret) + seed),
                inputHi ^ (BinaryPrimitives.ReadUInt64LittleEndian(secret[8..]) - seed));
        }

        private static Hash128 Xxh128Mix32b(Hash128 acc, ReadOnlySpan<byte> input, ReadOnlySpan<byte> input2, ReadOnlySpan<byte> secret, ulong seed)
        {
            acc.Low += Xxh3Mix16b(input, secret, seed);
            acc.Low ^= BinaryPrimitives.ReadUInt64LittleEndian(input2) + BinaryPrimitives.ReadUInt64LittleEndian(input2[8..]);
            acc.High += Xxh3Mix16b(input2, secret[16..], seed);
            acc.High ^= BinaryPrimitives.ReadUInt64LittleEndian(input) + BinaryPrimitives.ReadUInt64LittleEndian(input[8..]);

            return acc;
        }

        private static Hash128 Xxh3Len17To128128b(ReadOnlySpan<byte> input, ReadOnlySpan<byte> secret, ulong seed)
        {
            Debug.Assert(secret.Length >= SecretSizeMin);
            Debug.Assert(16 < input.Length && input.Length <= 128);

            Hash128 acc = new()
            {
                Low = (ulong)input.Length * Prime64_1,
                High = 0,
            };

            if (input.Length > 32)
            {
                if (input.Length > 64)
                {
                    if (input.Length > 96)
                    {
                        acc = Xxh128Mix32b(acc, input[48..], input[^64..], secret[96..], seed);
                    }
                    acc = Xxh128Mix32b(acc, input[32..], input[^48..], secret[64..], seed);
                }
                acc = Xxh128Mix32b(acc, input[16..], input[^32..], secret[32..], seed);
            }
            acc = Xxh128Mix32b(acc, input, input[^16..], secret, seed);

            Hash128 h128 = new()
            {
                Low = acc.Low + acc.High,
                High = acc.Low * Prime64_1 + acc.High * Prime64_4 + ((ulong)input.Length - seed) * Prime64_2,
            };
            h128.Low = Xxh3Avalanche(h128.Low);
            h128.High = 0UL - Xxh3Avalanche(h128.High);

            return h128;
        }

        private static Hash128 Xxh3Len129To240128b(ReadOnlySpan<byte> input, ReadOnlySpan<byte> secret, ulong seed)
        {
            Debug.Assert(secret.Length >= SecretSizeMin);
            Debug.Assert(128 < input.Length && input.Length <= 240);

            Hash128 acc = new();

            int nbRounds = input.Length / 32;
            acc.Low = (ulong)input.Length * Prime64_1;
            acc.High = 0;

            for (int i = 0; i < 4; i++)
            {
                acc = Xxh128Mix32b(acc, input[(32 * i)..], input[(32 * i + 16)..], secret[(32 * i)..], seed);
            }

            acc.Low = Xxh3Avalanche(acc.Low);
            acc.High = Xxh3Avalanche(acc.High);
            Debug.Assert(nbRounds >= 4);

            for (int i = 4; i < nbRounds; i++)
            {
                acc = Xxh128Mix32b(acc, input[(32 * i)..], input[(32 * i + 16)..], secret[(MidSizeStartOffset + 32 * (i - 4))..], seed);
            }

            acc = Xxh128Mix32b(acc, input[^16..], input[^32..], secret[(SecretSizeMin - MidSizeLastOffset - 16)..], 0UL - seed);

            Hash128 h128 = new()
            {
                Low = acc.Low + acc.High,
                High = acc.Low * Prime64_1 + acc.High * Prime64_4 + ((ulong)input.Length - seed) * Prime64_2,
            };
            h128.Low = Xxh3Avalanche(h128.Low);
            h128.High = 0UL - Xxh3Avalanche(h128.High);

            return h128;
        }

        private static Hash128 Xxh3128bitsInternal(ReadOnlySpan<byte> input, ReadOnlySpan<byte> secret, ulong seed)
        {
            Debug.Assert(secret.Length >= SecretSizeMin);

            return input.Length switch
            {
                <= 16 => Xxh3Len0To16128b(input, secret, seed),
                <= 128 => Xxh3Len17To128128b(input, secret, seed),
                <= 240 => Xxh3Len129To240128b(input, secret, seed),
                _ => Xxh3HashLong128bInternal(input, secret)
            };
        }

        #endregion
    }
}
