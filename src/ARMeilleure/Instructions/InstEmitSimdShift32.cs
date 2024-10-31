using ARMeilleure.Decoders;
using ARMeilleure.IntermediateRepresentation;
using ARMeilleure.State;
using ARMeilleure.Translation;
using System;
using System.Diagnostics;
using System.Reflection;
using static ARMeilleure.Instructions.InstEmitHelper;
using static ARMeilleure.Instructions.InstEmitSimdHelper;
using static ARMeilleure.Instructions.InstEmitSimdHelper32;
using static ARMeilleure.IntermediateRepresentation.Operand.Factory;

namespace ARMeilleure.Instructions
{
    static partial class InstEmit32
    {
        public static void Vqrshrn(ArmEmitterContext context)
        {
            OpCode32SimdShImm op = (OpCode32SimdShImm)context.CurrOp;

            EmitRoundShrImmSaturatingNarrowOp(context, op.U ? ShrImmSaturatingNarrowFlags.VectorZxZx : ShrImmSaturatingNarrowFlags.VectorSxSx);
        }

        public static void Vqrshrun(ArmEmitterContext context)
        {
            EmitRoundShrImmSaturatingNarrowOp(context, ShrImmSaturatingNarrowFlags.VectorSxZx);
        }

        public static void Vqshrn(ArmEmitterContext context)
        {
            OpCode32SimdShImm op = (OpCode32SimdShImm)context.CurrOp;

            EmitShrImmSaturatingNarrowOp(context, op.U ? ShrImmSaturatingNarrowFlags.VectorZxZx : ShrImmSaturatingNarrowFlags.VectorSxSx);
        }

        public static void Vqshrun(ArmEmitterContext context)
        {
            EmitShrImmSaturatingNarrowOp(context, ShrImmSaturatingNarrowFlags.VectorSxZx);
        }

        public static void Vrshr(ArmEmitterContext context)
        {
            EmitRoundShrImmOp(context, accumulate: false);
        }

        public static void Vrshrn(ArmEmitterContext context)
        {
            EmitRoundShrImmNarrowOp(context, signed: false);
        }

        public static void Vrsra(ArmEmitterContext context)
        {
            EmitRoundShrImmOp(context, accumulate: true);
        }

        public static void Vshl(ArmEmitterContext context)
        {
            OpCode32SimdShImm op = (OpCode32SimdShImm)context.CurrOp;

            EmitVectorUnaryOpZx32(context, (op1) => context.ShiftLeft(op1, Const(op.Shift)));
        }

        public static void Vshl_I(ArmEmitterContext context)
        {
            OpCode32SimdReg op = (OpCode32SimdReg)context.CurrOp;

            if (op.U)
            {
                EmitVectorBinaryOpZx32(context, (op1, op2) => EmitShlRegOp(context, op2, op1, op.Size, true));
            }
            else
            {
                EmitVectorBinaryOpSx32(context, (op1, op2) => EmitShlRegOp(context, op2, op1, op.Size, false));
            }
        }

        public static void Vshll(ArmEmitterContext context)
        {
            OpCode32SimdShImmLong op = (OpCode32SimdShImmLong)context.CurrOp;

            Operand res = context.VectorZero();

            int elems = op.GetBytesCount() >> op.Size;

            for (int index = 0; index < elems; index++)
            {
                Operand me = EmitVectorExtract32(context, op.Qm, op.Im + index, op.Size, !op.U);

                if (op.Size == 2)
                {
                    if (op.U)
                    {
                        me = context.ZeroExtend32(OperandType.I64, me);
                    }
                    else
                    {
                        me = context.SignExtend32(OperandType.I64, me);
                    }
                }

                me = context.ShiftLeft(me, Const(op.Shift));

                res = EmitVectorInsert(context, res, me, index, op.Size + 1);
            }

            context.Copy(GetVecA32(op.Qd), res);
        }

        public static void Vshll2(ArmEmitterContext context)
        {
            OpCode32Simd op = (OpCode32Simd)context.CurrOp;

            Operand res = context.VectorZero();

            int elems = op.GetBytesCount() >> op.Size;

            for (int index = 0; index < elems; index++)
            {
                Operand me = EmitVectorExtract32(context, op.Qm, op.Im + index, op.Size, !op.U);

                if (op.Size == 2)
                {
                    if (op.U)
                    {
                        me = context.ZeroExtend32(OperandType.I64, me);
                    }
                    else
                    {
                        me = context.SignExtend32(OperandType.I64, me);
                    }
                }

                me = context.ShiftLeft(me, Const(8 << op.Size));

                res = EmitVectorInsert(context, res, me, index, op.Size + 1);
            }

            context.Copy(GetVecA32(op.Qd), res);
        }

        public static void Vshr(ArmEmitterContext context)
        {
            OpCode32SimdShImm op = (OpCode32SimdShImm)context.CurrOp;
            int shift = GetImmShr(op);
            int maxShift = (8 << op.Size) - 1;

            if (op.U)
            {
                EmitVectorUnaryOpZx32(context, (op1) => (shift > maxShift) ? Const(op1.Type, 0) : context.ShiftRightUI(op1, Const(shift)));
            }
            else
            {
                EmitVectorUnaryOpSx32(context, (op1) => context.ShiftRightSI(op1, Const(Math.Min(maxShift, shift))));
            }
        }

        public static void Vshrn(ArmEmitterContext context)
        {
            OpCode32SimdShImm op = (OpCode32SimdShImm)context.CurrOp;
            int shift = GetImmShr(op);

            EmitVectorUnaryNarrowOp32(context, (op1) => context.ShiftRightUI(op1, Const(shift)));
        }

        public static void Vsli_I(ArmEmitterContext context)
        {
            OpCode32SimdShImm op = (OpCode32SimdShImm)context.CurrOp;
            int shift = op.Shift;
            int eSize = 8 << op.Size;

            ulong mask = shift != 0 ? ulong.MaxValue >> (64 - shift) : 0UL;

            Operand res = GetVec(op.Qd);

            int elems = op.GetBytesCount() >> op.Size;

            for (int index = 0; index < elems; index++)
            {
                Operand me = EmitVectorExtractZx(context, op.Qm, op.Im + index, op.Size);

                Operand neShifted = context.ShiftLeft(me, Const(shift));

                Operand de = EmitVectorExtractZx(context, op.Qd, op.Id + index, op.Size);

                Operand deMasked = context.BitwiseAnd(de, Const(mask));

                Operand e = context.BitwiseOr(neShifted, deMasked);

                res = EmitVectorInsert(context, res, e, op.Id + index, op.Size);
            }

            context.Copy(GetVec(op.Qd), res);
        }

        public static void Vsra(ArmEmitterContext context)
        {
            OpCode32SimdShImm op = (OpCode32SimdShImm)context.CurrOp;
            int shift = GetImmShr(op);
            int maxShift = (8 << op.Size) - 1;

            if (op.U)
            {
                EmitVectorImmBinaryQdQmOpZx32(context, (op1, op2) =>
                {
                    Operand shiftRes = shift > maxShift ? Const(op2.Type, 0) : context.ShiftRightUI(op2, Const(shift));

                    return context.Add(op1, shiftRes);
                });
            }
            else
            {
                EmitVectorImmBinaryQdQmOpSx32(context, (op1, op2) => context.Add(op1, context.ShiftRightSI(op2, Const(Math.Min(maxShift, shift)))));
            }
        }

        public static void EmitRoundShrImmOp(ArmEmitterContext context, bool accumulate)
        {
            OpCode32SimdShImm op = (OpCode32SimdShImm)context.CurrOp;
            int shift = GetImmShr(op);
            long roundConst = 1L << (shift - 1);

            if (op.U)
            {
                switch (op.Size)
                {
                    case < 2:
                        EmitVectorUnaryOpZx32(context, (op1) =>
                        {
                            op1 = context.Add(op1, Const(op1.Type, roundConst));

                            return context.ShiftRightUI(op1, Const(shift));
                        }, accumulate);
                        break;

                    case 2:
                        EmitVectorUnaryOpZx32(context, (op1) =>
                        {
                            op1 = context.ZeroExtend32(OperandType.I64, op1);
                            op1 = context.Add(op1, Const(op1.Type, roundConst));

                            return context.ConvertI64ToI32(context.ShiftRightUI(op1, Const(shift)));
                        }, accumulate);
                        break;

                    default: // case 3
                        {
                            EmitVectorUnaryOpZx32(context, (op1) => EmitShrImm64(context, op1, signed: false, roundConst, shift), accumulate);
                            break;
                        }
                }
            }
            else
            {
                switch (op.Size)
                {
                    case < 2:
                        EmitVectorUnaryOpSx32(context, (op1) =>
                        {
                            op1 = context.Add(op1, Const(op1.Type, roundConst));

                            return context.ShiftRightSI(op1, Const(shift));
                        }, accumulate);
                        break;

                    case 2:
                        EmitVectorUnaryOpSx32(context, (op1) =>
                        {
                            op1 = context.SignExtend32(OperandType.I64, op1);
                            op1 = context.Add(op1, Const(op1.Type, roundConst));

                            return context.ConvertI64ToI32(context.ShiftRightSI(op1, Const(shift)));
                        }, accumulate);
                        break;

                    default: // case 3
                        EmitVectorUnaryOpZx32(context, (op1) => EmitShrImm64(context, op1, signed: true, roundConst, shift), accumulate);
                        break;
                }
            }
        }

        private static void EmitRoundShrImmNarrowOp(ArmEmitterContext context, bool signed)
        {
            OpCode32SimdShImm op = (OpCode32SimdShImm)context.CurrOp;

            int shift = GetImmShr(op);
            long roundConst = 1L << (shift - 1);

            EmitVectorUnaryNarrowOp32(context, (op1) =>
            {
                if (op.Size <= 1)
                {
                    op1 = context.Add(op1, Const(op1.Type, roundConst));
                    op1 = signed ? context.ShiftRightSI(op1, Const(shift)) : context.ShiftRightUI(op1, Const(shift));
                }
                else /* if (op.Size == 2 && round) */
                {
                    op1 = EmitShrImm64(context, op1, signed, roundConst, shift); // shift <= 32
                }

                return op1;
            }, signed);
        }

        private static Operand EmitShlRegOp(ArmEmitterContext context, Operand op, Operand shiftLsB, int size, bool unsigned)
        {
            if (shiftLsB.Type == OperandType.I64)
            {
                shiftLsB = context.ConvertI64ToI32(shiftLsB);
            }

            shiftLsB = context.SignExtend8(OperandType.I32, shiftLsB);
            Debug.Assert((uint)size < 4u);

            Operand negShiftLsB = context.Negate(shiftLsB);

            Operand isPositive = context.ICompareGreaterOrEqual(shiftLsB, Const(0));

            Operand shl = context.ShiftLeft(op, shiftLsB);
            Operand shr = unsigned ? context.ShiftRightUI(op, negShiftLsB) : context.ShiftRightSI(op, negShiftLsB);

            Operand res = context.ConditionalSelect(isPositive, shl, shr);

            if (unsigned)
            {
                Operand isOutOfRange = context.BitwiseOr(
                    context.ICompareGreaterOrEqual(shiftLsB, Const(8 << size)),
                    context.ICompareGreaterOrEqual(negShiftLsB, Const(8 << size)));

                return context.ConditionalSelect(isOutOfRange, Const(op.Type, 0), res);
            }
            else
            {
                Operand isOutOfRange0 = context.ICompareGreaterOrEqual(shiftLsB, Const(8 << size));
                Operand isOutOfRangeN = context.ICompareGreaterOrEqual(negShiftLsB, Const(8 << size));

                // Also zero if shift is too negative, but value was positive.
                isOutOfRange0 = context.BitwiseOr(isOutOfRange0, context.BitwiseAnd(isOutOfRangeN, context.ICompareGreaterOrEqual(op, Const(op.Type, 0))));

                Operand min = (op.Type == OperandType.I64) ? Const(-1L) : Const(-1);

                return context.ConditionalSelect(isOutOfRange0, Const(op.Type, 0), context.ConditionalSelect(isOutOfRangeN, min, res));
            }
        }

        [Flags]
        private enum ShrImmSaturatingNarrowFlags
        {
            Scalar = 1 << 0,
            SignedSrc = 1 << 1,
            SignedDst = 1 << 2,

            Round = 1 << 3,

            ScalarSxSx = Scalar | SignedSrc | SignedDst,
            ScalarSxZx = Scalar | SignedSrc,
            ScalarZxZx = Scalar,

            VectorSxSx = SignedSrc | SignedDst,
            VectorSxZx = SignedSrc,
            VectorZxZx = 0,
        }

        private static void EmitRoundShrImmSaturatingNarrowOp(ArmEmitterContext context, ShrImmSaturatingNarrowFlags flags)
        {
            EmitShrImmSaturatingNarrowOp(context, ShrImmSaturatingNarrowFlags.Round | flags);
        }

        private static void EmitShrImmSaturatingNarrowOp(ArmEmitterContext context, ShrImmSaturatingNarrowFlags flags)
        {
            OpCode32SimdShImm op = (OpCode32SimdShImm)context.CurrOp;

            bool scalar = (flags & ShrImmSaturatingNarrowFlags.Scalar) != 0;
            bool signedSrc = (flags & ShrImmSaturatingNarrowFlags.SignedSrc) != 0;
            bool signedDst = (flags & ShrImmSaturatingNarrowFlags.SignedDst) != 0;
            bool round = (flags & ShrImmSaturatingNarrowFlags.Round) != 0;

            if (scalar)
            {
                // TODO: Support scalar operation.
                throw new NotImplementedException();
            }

            int shift = GetImmShr(op);
            long roundConst = 1L << (shift - 1);

            EmitVectorUnaryNarrowOp32(context, (op1) =>
            {
                if (op.Size <= 1 || !round)
                {
                    if (round)
                    {
                        op1 = context.Add(op1, Const(op1.Type, roundConst));
                    }

                    op1 = signedSrc ? context.ShiftRightSI(op1, Const(shift)) : context.ShiftRightUI(op1, Const(shift));
                }
                else /* if (op.Size == 2 && round) */
                {
                    op1 = EmitShrImm64(context, op1, signedSrc, roundConst, shift); // shift <= 32
                }

                return EmitSatQ(context, op1, 8 << op.Size, signedSrc, signedDst);
            }, signedSrc);
        }

        private static int GetImmShr(OpCode32SimdShImm op)
        {
            return (8 << op.Size) - op.Shift; // Shr amount is flipped.
        }

        // dst64 = (Int(src64, signed) + roundConst) >> shift;
        private static Operand EmitShrImm64(
            ArmEmitterContext context,
            Operand value,
            bool signed,
            long roundConst,
            int shift)
        {
            MethodInfo info = signed
                ? typeof(SoftFallback).GetMethod(nameof(SoftFallback.SignedShrImm64))
                : typeof(SoftFallback).GetMethod(nameof(SoftFallback.UnsignedShrImm64));

            return context.Call(info, value, Const(roundConst), Const(shift));
        }

        private static Operand EmitSatQ(ArmEmitterContext context, Operand value, int eSize, bool signedSrc, bool signedDst)
        {
            Debug.Assert(eSize <= 32);

            long intMin = signedDst ? -(1L << (eSize - 1)) : 0;
            long intMax = signedDst ? (1L << (eSize - 1)) - 1 : (1L << eSize) - 1;

            Operand gt = signedSrc
                ? context.ICompareGreater(value, Const(value.Type, intMax))
                : context.ICompareGreaterUI(value, Const(value.Type, intMax));

            Operand lt = signedSrc
                ? context.ICompareLess(value, Const(value.Type, intMin))
                : context.ICompareLessUI(value, Const(value.Type, intMin));

            value = context.ConditionalSelect(gt, Const(value.Type, intMax), value);
            value = context.ConditionalSelect(lt, Const(value.Type, intMin), value);

            Operand lblNoSat = Label();

            context.BranchIfFalse(lblNoSat, context.BitwiseOr(gt, lt));

            SetFpFlag(context, FPState.QcFlag, Const(1));

            context.MarkLabel(lblNoSat);

            return value;
        }
    }
}
