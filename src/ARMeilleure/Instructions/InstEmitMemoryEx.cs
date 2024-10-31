using ARMeilleure.Decoders;
using ARMeilleure.IntermediateRepresentation;
using ARMeilleure.Translation;
using System;
using System.Diagnostics;
using static ARMeilleure.Instructions.InstEmitHelper;
using static ARMeilleure.Instructions.InstEmitMemoryExHelper;
using static ARMeilleure.IntermediateRepresentation.Operand.Factory;

namespace ARMeilleure.Instructions
{
    static partial class InstEmit
    {
        [Flags]
        private enum AccessType
        {
            None = 0,
            Ordered = 1,
            Exclusive = 2,
            OrderedEx = Ordered | Exclusive,
        }

        public static void Clrex(ArmEmitterContext context)
        {
            EmitClearExclusive(context);
        }

        public static void Csdb(ArmEmitterContext context)
        {
            // Execute as no-op.
        }

        public static void Dmb(ArmEmitterContext context) => EmitBarrier(context);
        public static void Dsb(ArmEmitterContext context) => EmitBarrier(context);

        public static void Ldar(ArmEmitterContext context) => EmitLdr(context, AccessType.Ordered);
        public static void Ldaxr(ArmEmitterContext context) => EmitLdr(context, AccessType.OrderedEx);
        public static void Ldxr(ArmEmitterContext context) => EmitLdr(context, AccessType.Exclusive);
        public static void Ldxp(ArmEmitterContext context) => EmitLdp(context, AccessType.Exclusive);
        public static void Ldaxp(ArmEmitterContext context) => EmitLdp(context, AccessType.OrderedEx);

        private static void EmitLdr(ArmEmitterContext context, AccessType accType)
        {
            EmitLoadEx(context, accType, pair: false);
        }

        private static void EmitLdp(ArmEmitterContext context, AccessType accType)
        {
            EmitLoadEx(context, accType, pair: true);
        }

        private static void EmitLoadEx(ArmEmitterContext context, AccessType accType, bool pair)
        {
            OpCodeMemEx op = (OpCodeMemEx)context.CurrOp;

            bool ordered = (accType & AccessType.Ordered) != 0;
            bool exclusive = (accType & AccessType.Exclusive) != 0;

            if (ordered)
            {
                EmitBarrier(context);
            }

            Operand address = context.Copy(GetIntOrSP(context, op.Rn));

            if (pair)
            {
                // Exclusive loads should be atomic. For pairwise loads, we need to
                // read all the data at once. For a 32-bits pairwise load, we do a
                // simple 64-bits load, for a 128-bits load, we need to call a special
                // method to read 128-bits atomically.
                switch (op.Size)
                {
                    case 2:
                        {
                            Operand value = EmitLoadExclusive(context, address, exclusive, 3);

                            Operand valueLow = context.ConvertI64ToI32(value);

                            valueLow = context.ZeroExtend32(OperandType.I64, valueLow);

                            Operand valueHigh = context.ShiftRightUI(value, Const(32));

                            SetIntOrZR(context, op.Rt, valueLow);
                            SetIntOrZR(context, op.Rt2, valueHigh);
                            break;
                        }

                    case 3:
                        {
                            Operand value = EmitLoadExclusive(context, address, exclusive, 4);

                            Operand valueLow = context.VectorExtract(OperandType.I64, value, 0);
                            Operand valueHigh = context.VectorExtract(OperandType.I64, value, 1);

                            SetIntOrZR(context, op.Rt, valueLow);
                            SetIntOrZR(context, op.Rt2, valueHigh);
                            break;
                        }

                    default:
                        throw new InvalidOperationException($"Invalid load size of {1 << op.Size} bytes.");
                }
            }
            else
            {
                // 8, 16, 32 or 64-bits (non-pairwise) load.
                Operand value = EmitLoadExclusive(context, address, exclusive, op.Size);

                SetIntOrZR(context, op.Rt, value);
            }
        }

        public static void Prfm(ArmEmitterContext context)
        {
            // Memory Prefetch, execute as no-op.
        }

        public static void Stlr(ArmEmitterContext context) => EmitStr(context, AccessType.Ordered);
        public static void Stlxr(ArmEmitterContext context) => EmitStr(context, AccessType.OrderedEx);
        public static void Stxr(ArmEmitterContext context) => EmitStr(context, AccessType.Exclusive);
        public static void Stxp(ArmEmitterContext context) => EmitStp(context, AccessType.Exclusive);
        public static void Stlxp(ArmEmitterContext context) => EmitStp(context, AccessType.OrderedEx);

        private static void EmitStr(ArmEmitterContext context, AccessType accType)
        {
            EmitStoreEx(context, accType, pair: false);
        }

        private static void EmitStp(ArmEmitterContext context, AccessType accType)
        {
            EmitStoreEx(context, accType, pair: true);
        }

        private static void EmitStoreEx(ArmEmitterContext context, AccessType accType, bool pair)
        {
            OpCodeMemEx op = (OpCodeMemEx)context.CurrOp;

            bool ordered = (accType & AccessType.Ordered) != 0;
            bool exclusive = (accType & AccessType.Exclusive) != 0;

            Operand address = context.Copy(GetIntOrSP(context, op.Rn));

            Operand t = GetIntOrZR(context, op.Rt);

            if (pair)
            {
                Debug.Assert(op.Size == 2 || op.Size == 3, "Invalid size for pairwise store.");

                Operand t2 = GetIntOrZR(context, op.Rt2);

                Operand value;

                if (op.Size == 2)
                {
                    value = context.BitwiseOr(t, context.ShiftLeft(t2, Const(32)));
                }
                else /* if (op.Size == 3) */
                {
                    value = context.VectorInsert(context.VectorZero(), t, 0);
                    value = context.VectorInsert(value, t2, 1);
                }

                EmitStoreExclusive(context, address, value, exclusive, op.Size + 1, op.Rs, a32: false);
            }
            else
            {
                EmitStoreExclusive(context, address, t, exclusive, op.Size, op.Rs, a32: false);
            }

            if (ordered)
            {
                EmitBarrier(context);
            }
        }

        private static void EmitBarrier(ArmEmitterContext context)
        {
            context.MemoryBarrier();
        }
    }
}
