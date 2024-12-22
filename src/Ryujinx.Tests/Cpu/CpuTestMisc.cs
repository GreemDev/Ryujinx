#define Misc

using ARMeilleure.State;
using NUnit.Framework;
using System;
using System.Collections.Generic;

namespace Ryujinx.Tests.Cpu
{
    [Category("Misc")]
    public sealed class CpuTestMisc : CpuTest
    {
#if Misc

        #region "ValueSource (Types)"
        private static IEnumerable<ulong> _1S_F_()
        {
            yield return 0x00000000FF7FFFFFul; // -Max Normal    (float.MinValue)
            yield return 0x0000000080800000ul; // -Min Normal
            yield return 0x00000000807FFFFFul; // -Max Subnormal
            yield return 0x0000000080000001ul; // -Min Subnormal (-float.Epsilon)
            yield return 0x000000007F7FFFFFul; // +Max Normal    (float.MaxValue)
            yield return 0x0000000000800000ul; // +Min Normal
            yield return 0x00000000007FFFFFul; // +Max Subnormal
            yield return 0x0000000000000001ul; // +Min Subnormal (float.Epsilon)

            if (!_noZeros)
            {
                yield return 0x0000000080000000ul; // -Zero
                yield return 0x0000000000000000ul; // +Zero
            }

            if (!_noInfs)
            {
                yield return 0x00000000FF800000ul; // -Infinity
                yield return 0x000000007F800000ul; // +Infinity
            }

            if (!_noNaNs)
            {
                yield return 0x00000000FFC00000ul; // -QNaN (all zeros payload) (float.NaN)
                yield return 0x00000000FFBFFFFFul; // -SNaN (all ones  payload)
                yield return 0x000000007FC00000ul; // +QNaN (all zeros payload) (-float.NaN) (DefaultNaN)
                yield return 0x000000007FBFFFFFul; // +SNaN (all ones  payload)
            }

            for (int cnt = 1; cnt <= RndCnt; cnt++)
            {
                ulong grbg = TestContext.CurrentContext.Random.NextUInt();
                ulong rnd1 = GenNormalS();
                ulong rnd2 = GenSubnormalS();

                yield return (grbg << 32) | rnd1;
                yield return (grbg << 32) | rnd2;
            }
        }
        #endregion

        private const int RndCnt = 2;

        private static readonly bool _noZeros;
        private static readonly bool _noInfs;
        private static readonly bool _noNaNs;

        #region "AluImm & Csel"
        [Test, Pairwise]
        public void Adds_Csinc_64bit([Values(0x0000000000000000ul, 0x7FFFFFFFFFFFFFFFul,
                                             0x8000000000000000ul, 0xFFFFFFFFFFFFFFFFul)] ulong xn,
                                     [Values(0u, 4095u)] uint imm,
                                     [Values(0b00u, 0b01u)] uint shift,          // <LSL #0, LSL #12>
                                     [Values(0b0000u, 0b0001u, 0b0010u, 0b0011u, // <EQ, NE, CS/HS, CC/LO,
                                             0b0100u, 0b0101u, 0b0110u, 0b0111u, //  MI, PL, VS, VC,
                                             0b1000u, 0b1001u, 0b1010u, 0b1011u, //  HI, LS, GE, LT,
                                             0b1100u, 0b1101u)] uint cond)       //  GT, LE>
        {
            uint opCmn = 0xB100001F; // ADDS  X31, X0,  #0,  LSL #0 -> CMN  X0, #0, LSL #0
            uint opCset = 0x9A9F07E0; // CSINC X0,  X31, X31, EQ     -> CSET X0, NE

            opCmn |= ((shift & 3) << 22) | ((imm & 4095) << 10);
            opCset |= ((cond & 15) << 12);

            SetContext(x0: xn);
            Opcode(opCmn);
            Opcode(opCset);
            Opcode(0xD65F03C0); // RET
            ExecuteOpcodes();

            CompareAgainstUnicorn();
        }

        [Test, Pairwise]
        public void Adds_Csinc_32bit([Values(0x00000000u, 0x7FFFFFFFu,
                                             0x80000000u, 0xFFFFFFFFu)] uint wn,
                                     [Values(0u, 4095u)] uint imm,
                                     [Values(0b00u, 0b01u)] uint shift,          // <LSL #0, LSL #12>
                                     [Values(0b0000u, 0b0001u, 0b0010u, 0b0011u, // <EQ, NE, CS/HS, CC/LO,
                                             0b0100u, 0b0101u, 0b0110u, 0b0111u, //  MI, PL, VS, VC,
                                             0b1000u, 0b1001u, 0b1010u, 0b1011u, //  HI, LS, GE, LT,
                                             0b1100u, 0b1101u)] uint cond)       //  GT, LE>
        {
            uint opCmn = 0x3100001F; // ADDS  W31, W0,  #0,  LSL #0 -> CMN  W0, #0, LSL #0
            uint opCset = 0x1A9F07E0; // CSINC W0,  W31, W31, EQ     -> CSET W0, NE

            opCmn |= ((shift & 3) << 22) | ((imm & 4095) << 10);
            opCset |= ((cond & 15) << 12);

            SetContext(x0: wn);
            Opcode(opCmn);
            Opcode(opCset);
            Opcode(0xD65F03C0); // RET
            ExecuteOpcodes();

            CompareAgainstUnicorn();
        }

        [Test, Pairwise]
        public void Subs_Csinc_64bit([Values(0x0000000000000000ul, 0x7FFFFFFFFFFFFFFFul,
                                             0x8000000000000000ul, 0xFFFFFFFFFFFFFFFFul)] ulong xn,
                                     [Values(0u, 4095u)] uint imm,
                                     [Values(0b00u, 0b01u)] uint shift,          // <LSL #0, LSL #12>
                                     [Values(0b0000u, 0b0001u, 0b0010u, 0b0011u, // <EQ, NE, CS/HS, CC/LO,
                                             0b0100u, 0b0101u, 0b0110u, 0b0111u, //  MI, PL, VS, VC,
                                             0b1000u, 0b1001u, 0b1010u, 0b1011u, //  HI, LS, GE, LT,
                                             0b1100u, 0b1101u)] uint cond)       //  GT, LE>
        {
            uint opCmp = 0xF100001F; // SUBS  X31, X0,  #0,  LSL #0 -> CMP  X0, #0, LSL #0
            uint opCset = 0x9A9F07E0; // CSINC X0,  X31, X31, EQ     -> CSET X0, NE

            opCmp |= ((shift & 3) << 22) | ((imm & 4095) << 10);
            opCset |= ((cond & 15) << 12);

            SetContext(x0: xn);
            Opcode(opCmp);
            Opcode(opCset);
            Opcode(0xD65F03C0); // RET
            ExecuteOpcodes();

            CompareAgainstUnicorn();
        }

        [Test, Pairwise]
        public void Subs_Csinc_32bit([Values(0x00000000u, 0x7FFFFFFFu,
                                             0x80000000u, 0xFFFFFFFFu)] uint wn,
                                     [Values(0u, 4095u)] uint imm,
                                     [Values(0b00u, 0b01u)] uint shift,          // <LSL #0, LSL #12>
                                     [Values(0b0000u, 0b0001u, 0b0010u, 0b0011u, // <EQ, NE, CS/HS, CC/LO,
                                             0b0100u, 0b0101u, 0b0110u, 0b0111u, //  MI, PL, VS, VC,
                                             0b1000u, 0b1001u, 0b1010u, 0b1011u, //  HI, LS, GE, LT,
                                             0b1100u, 0b1101u)] uint cond)       //  GT, LE>
        {
            uint opCmp = 0x7100001F; // SUBS  W31, W0,  #0,  LSL #0 -> CMP  W0, #0, LSL #0
            uint opCset = 0x1A9F07E0; // CSINC W0,  W31, W31, EQ     -> CSET W0, NE

            opCmp |= ((shift & 3) << 22) | ((imm & 4095) << 10);
            opCset |= ((cond & 15) << 12);

            SetContext(x0: wn);
            Opcode(opCmp);
            Opcode(opCset);
            Opcode(0xD65F03C0); // RET
            ExecuteOpcodes();

            CompareAgainstUnicorn();
        }
        #endregion

        // Roots.
        [Explicit]
        [TestCase(0xFFFFFFFDu)]
        [TestCase(0x00000005u)]
        public void Misc1(uint a)
        {
            // ((a + 3) * (a - 5)) / ((a + 5) * (a - 3)) = 0

            /*
            ADD W2, W0, 3
            SUB W1, W0, #5
            MUL W2, W2, W1
            ADD W1, W0, 5
            SUB W0, W0, #3
            MUL W0, W1, W0
            SDIV W0, W2, W0
            RET
            */

            SetContext(x0: a);
            Opcode(0x11000C02);
            Opcode(0x51001401);
            Opcode(0x1B017C42);
            Opcode(0x11001401);
            Opcode(0x51000C00);
            Opcode(0x1B007C20);
            Opcode(0x1AC00C40);
            Opcode(0xD65F03C0);
            ExecuteOpcodes();

            Assert.That(GetContext().GetX(0), Is.Zero);
        }

        // 18 integer solutions.
        [Explicit]
        [TestCase(-20f, -5f)]
        [TestCase(-12f, -6f)]
        [TestCase(-12f, 3f)]
        [TestCase(-8f, -8f)]
        [TestCase(-6f, -12f)]
        [TestCase(-5f, -20f)]
        [TestCase(-4f, 2f)]
        [TestCase(-3f, 12f)]
        [TestCase(-2f, 4f)]
        [TestCase(2f, -4f)]
        [TestCase(3f, -12f)]
        [TestCase(4f, -2f)]
        [TestCase(5f, 20f)]
        [TestCase(6f, 12f)]
        [TestCase(8f, 8f)]
        [TestCase(12f, -3f)]
        [TestCase(12f, 6f)]
        [TestCase(20f, 5f)]
        public void Misc2(float a, float b)
        {
            // 1 / ((1 / a + 1 / b) ^ 2) = 16

            /*
            FMOV S2, 1.0e+0
            FDIV S0, S2, S0
            FDIV S1, S2, S1
            FADD S0, S0, S1
            FDIV S0, S2, S0
            FMUL S0, S0, S0
            RET
            */

            SetContext(v0: MakeVectorScalar(a), v1: MakeVectorScalar(b));
            Opcode(0x1E2E1002);
            Opcode(0x1E201840);
            Opcode(0x1E211841);
            Opcode(0x1E212800);
            Opcode(0x1E201840);
            Opcode(0x1E200800);
            Opcode(0xD65F03C0);
            ExecuteOpcodes();

            Assert.That(GetContext().GetV(0).As<float>(), Is.EqualTo(16f));
        }

        // 18 integer solutions.
        [Explicit]
        [TestCase(-20d, -5d)]
        [TestCase(-12d, -6d)]
        [TestCase(-12d, 3d)]
        [TestCase(-8d, -8d)]
        [TestCase(-6d, -12d)]
        [TestCase(-5d, -20d)]
        [TestCase(-4d, 2d)]
        [TestCase(-3d, 12d)]
        [TestCase(-2d, 4d)]
        [TestCase(2d, -4d)]
        [TestCase(3d, -12d)]
        [TestCase(4d, -2d)]
        [TestCase(5d, 20d)]
        [TestCase(6d, 12d)]
        [TestCase(8d, 8d)]
        [TestCase(12d, -3d)]
        [TestCase(12d, 6d)]
        [TestCase(20d, 5d)]
        public void Misc3(double a, double b)
        {
            // 1 / ((1 / a + 1 / b) ^ 2) = 16

            /*
            FMOV D2, 1.0e+0
            FDIV D0, D2, D0
            FDIV D1, D2, D1
            FADD D0, D0, D1
            FDIV D0, D2, D0
            FMUL D0, D0, D0
            RET
            */

            SetContext(v0: MakeVectorScalar(a), v1: MakeVectorScalar(b));
            Opcode(0x1E6E1002);
            Opcode(0x1E601840);
            Opcode(0x1E611841);
            Opcode(0x1E612800);
            Opcode(0x1E601840);
            Opcode(0x1E600800);
            Opcode(0xD65F03C0);
            ExecuteOpcodes();

            Assert.That(GetContext().GetV(0).As<double>(), Is.EqualTo(16d));
        }

        [Test, Ignore("The Tester supports only one return point.")]
        public void MiscF([Range(0u, 92u, 1u)] uint a)
        {
            static ulong Fn(uint n)
            {
                ulong x = 0, y = 1, z;

                if (n == 0)
                {
                    return x;
                }

                for (uint i = 2; i <= n; i++)
                {
                    z = x + y;
                    x = y;
                    y = z;
                }

                return y;
            }

            /*
            0x0000000000001000: MOV W4, W0
            0x0000000000001004: CBZ W0, #0x34
            0x0000000000001008: CMP W0, #1
            0x000000000000100C: B.LS #0x34
            0x0000000000001010: MOVZ W2, #0x2
            0x0000000000001014: MOVZ X1, #0x1
            0x0000000000001018: MOVZ X3, #0
            0x000000000000101C: ADD X0, X3, X1
            0x0000000000001020: ADD W2, W2, #1
            0x0000000000001024: MOV X3, X1
            0x0000000000001028: MOV X1, X0
            0x000000000000102C: CMP W4, W2
            0x0000000000001030: B.HS #-0x14
            0x0000000000001034: RET
            0x0000000000001038: MOVZ X0, #0
            0x000000000000103C: RET
            0x0000000000001040: MOVZ X0, #0x1
            0x0000000000001044: RET
            */

            SetContext(x0: a);
            Opcode(0x2A0003E4);
            Opcode(0x340001A0);
            Opcode(0x7100041F);
            Opcode(0x540001A9);
            Opcode(0x52800042);
            Opcode(0xD2800021);
            Opcode(0xD2800003);
            Opcode(0x8B010060);
            Opcode(0x11000442);
            Opcode(0xAA0103E3);
            Opcode(0xAA0003E1);
            Opcode(0x6B02009F);
            Opcode(0x54FFFF62);
            Opcode(0xD65F03C0);
            Opcode(0xD2800000);
            Opcode(0xD65F03C0);
            Opcode(0xD2800020);
            Opcode(0xD65F03C0);
            ExecuteOpcodes();

            Assert.That(GetContext().GetX(0), Is.EqualTo(Fn(a)));
        }

        [Explicit]
        [Test]
        public void MiscR()
        {
            const ulong Result = 5;

            /*
            0x0000000000001000: MOV X0, #2
            0x0000000000001004: MOV X1, #3
            0x0000000000001008: ADD X0, X0, X1
            0x000000000000100C: RET
            */

            Opcode(0xD2800040);
            Opcode(0xD2800061);
            Opcode(0x8B010000);
            Opcode(0xD65F03C0);
            ExecuteOpcodes();

            Assert.That(GetContext().GetX(0), Is.EqualTo(Result));

            Reset();

            /*
            0x0000000000001000: MOV X0, #3
            0x0000000000001004: MOV X1, #2
            0x0000000000001008: ADD X0, X0, X1
            0x000000000000100C: RET
            */

            Opcode(0xD2800060);
            Opcode(0xD2800041);
            Opcode(0x8B010000);
            Opcode(0xD65F03C0);
            ExecuteOpcodes();

            Assert.That(GetContext().GetX(0), Is.EqualTo(Result));
        }

        [Explicit]
        [TestCase(0ul)]
        [TestCase(1ul)]
        [TestCase(2ul)]
        [TestCase(42ul)]
        public void SanityCheck(ulong a)
        {
            uint opcode = 0xD503201F; // NOP
            ExecutionContext context = SingleOpcode(opcode, x0: a);

            Assert.That(context.GetX(0), Is.EqualTo(a));
        }

        [Explicit]
        [Test, Pairwise]
        public void Misc4([ValueSource(nameof(_1S_F_))] ulong a,
                          [ValueSource(nameof(_1S_F_))] ulong b,
                          [ValueSource(nameof(_1S_F_))] ulong c,
                          [Values(0ul, 1ul, 2ul, 3ul)] ulong displacement)
        {
            if (!BitConverter.IsLittleEndian)
            {
                Assert.Ignore();
            }

            for (ulong gapOffset = 0; gapOffset < displacement; gapOffset++)
            {
                SetWorkingMemory(gapOffset, TestContext.CurrentContext.Random.NextByte());
            }

            SetWorkingMemory(0x0 + displacement, BitConverter.GetBytes((uint)b));

            SetWorkingMemory(0x4 + displacement, BitConverter.GetBytes((uint)c));

            SetWorkingMemory(0x8 + displacement, TestContext.CurrentContext.Random.NextByte());
            SetWorkingMemory(0x9 + displacement, TestContext.CurrentContext.Random.NextByte());
            SetWorkingMemory(0xA + displacement, TestContext.CurrentContext.Random.NextByte());
            SetWorkingMemory(0xB + displacement, TestContext.CurrentContext.Random.NextByte());

            SetContext(
                x0: DataBaseAddress + displacement,
                v0: MakeVectorE0E1(a, TestContext.CurrentContext.Random.NextULong()),
                v1: MakeVectorE0E1(TestContext.CurrentContext.Random.NextULong(), TestContext.CurrentContext.Random.NextULong()),
                v2: MakeVectorE0E1(TestContext.CurrentContext.Random.NextULong(), TestContext.CurrentContext.Random.NextULong()),
                overflow: TestContext.CurrentContext.Random.NextBool(),
                carry: TestContext.CurrentContext.Random.NextBool(),
                zero: TestContext.CurrentContext.Random.NextBool(),
                negative: TestContext.CurrentContext.Random.NextBool());

            Opcode(0xBD400001); // LDR   S1, [X0,#0]
            Opcode(0xBD400402); // LDR   S2, [X0,#4]
            Opcode(0x1E215801); // FMIN  S1, S0, S1
            Opcode(0x1E222000); // FCMP  S0, S2
            Opcode(0x1E214C40); // FCSEL S0, S2, S1, MI
            Opcode(0xBD000800); // STR   S0, [X0,#8]
            Opcode(0xD65F03C0); // RET
            ExecuteOpcodes();

            CompareAgainstUnicorn();
        }

        [Explicit]
        [Test]
        public void Misc5([ValueSource(nameof(_1S_F_))] ulong a)
        {
            SetContext(
                v0: MakeVectorE0E1(a, TestContext.CurrentContext.Random.NextULong()),
                v1: MakeVectorE0E1(TestContext.CurrentContext.Random.NextULong(), TestContext.CurrentContext.Random.NextULong()),
                overflow: TestContext.CurrentContext.Random.NextBool(),
                carry: TestContext.CurrentContext.Random.NextBool(),
                zero: TestContext.CurrentContext.Random.NextBool(),
                negative: TestContext.CurrentContext.Random.NextBool());

            Opcode(0x1E202008); // FCMP  S0, #0.0
            Opcode(0x1E2E1001); // FMOV  S1, #1.0
            Opcode(0x1E215800); // FMIN  S0, S0, S1
            Opcode(0x1E2703E1); // FMOV  S1, WZR
            Opcode(0x1E204C20); // FCSEL S0, S1, S0, MI
            Opcode(0xD65F03C0); // RET
            ExecuteOpcodes();

            CompareAgainstUnicorn();
        }
#endif
    }
}
