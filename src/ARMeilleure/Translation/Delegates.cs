using ARMeilleure.Instructions;
using ARMeilleure.State;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;

namespace ARMeilleure.Translation
{
    static class Delegates
    {
        public static bool TryGetDelegateFuncPtrByIndex(int index, out nint funcPtr)
        {
            if (index >= 0 && index < _delegates.Count)
            {
                funcPtr = _delegates.Values[index].FuncPtr; // O(1).

                return true;
            }
            else
            {
                funcPtr = default;

                return false;
            }
        }

        public static nint GetDelegateFuncPtrByIndex(int index)
        {
            if (index < 0 || index >= _delegates.Count)
            {
                throw new ArgumentOutOfRangeException($"({nameof(index)} = {index})");
            }

            return _delegates.Values[index].FuncPtr; // O(1).
        }

        public static nint GetDelegateFuncPtr(MethodInfo info)
        {
            ArgumentNullException.ThrowIfNull(info);

            string key = GetKey(info);

            if (!_delegates.TryGetValue(key, out DelegateInfo dlgInfo)) // O(log(n)).
            {
                throw new KeyNotFoundException($"({nameof(key)} = {key})");
            }

            return dlgInfo.FuncPtr;
        }

        public static int GetDelegateIndex(MethodInfo info)
        {
            ArgumentNullException.ThrowIfNull(info);

            string key = GetKey(info);

            int index = _delegates.IndexOfKey(key); // O(log(n)).

            if (index == -1)
            {
                throw new KeyNotFoundException($"({nameof(key)} = {key})");
            }

            return index;
        }

        private static void SetDelegateInfo(Delegate dlg, nint funcPtr)
        {
            string key = GetKey(dlg.Method);

            _delegates.Add(key, new DelegateInfo(dlg, funcPtr)); // ArgumentException (key).
        }

        private static string GetKey(MethodInfo info)
        {
            return $"{info.DeclaringType.Name}.{info.Name}";
        }

        private static readonly SortedList<string, DelegateInfo> _delegates;

        static Delegates()
        {
            _delegates = new SortedList<string, DelegateInfo>();

            var dlgMathAbs = new MathAbs(Math.Abs);
            var dlgMathCeiling = new MathCeiling(Math.Ceiling);
            var dlgMathFloor = new MathFloor(Math.Floor);
            var dlgMathRound = new MathRound(Math.Round);
            var dlgMathTruncate = new MathTruncate(Math.Truncate);

            var dlgMathFAbs = new MathFAbs(MathF.Abs);
            var dlgMathFCeiling = new MathFCeiling(MathF.Ceiling);
            var dlgMathFFloor = new MathFFloor(MathF.Floor);
            var dlgMathFRound = new MathFRound(MathF.Round);
            var dlgMathFTruncate = new MathFTruncate(MathF.Truncate);

            var dlgNativeInterfaceBreak = new NativeInterfaceBreak(NativeInterface.Break);
            var dlgNativeInterfaceCheckSynchronization = new NativeInterfaceCheckSynchronization(NativeInterface.CheckSynchronization);
            var dlgNativeInterfaceEnqueueForRejit = new NativeInterfaceEnqueueForRejit(NativeInterface.EnqueueForRejit);
            var dlgNativeInterfaceGetCntfrqEl0 = new NativeInterfaceGetCntfrqEl0(NativeInterface.GetCntfrqEl0);
            var dlgNativeInterfaceGetCntpctEl0 = new NativeInterfaceGetCntpctEl0(NativeInterface.GetCntpctEl0);
            var dlgNativeInterfaceGetCntvctEl0 = new NativeInterfaceGetCntvctEl0(NativeInterface.GetCntvctEl0);
            var dlgNativeInterfaceGetCtrEl0 = new NativeInterfaceGetCtrEl0(NativeInterface.GetCtrEl0);
            var dlgNativeInterfaceGetDczidEl0 = new NativeInterfaceGetDczidEl0(NativeInterface.GetDczidEl0);
            var dlgNativeInterfaceGetFunctionAddress = new NativeInterfaceGetFunctionAddress(NativeInterface.GetFunctionAddress);
            var dlgNativeInterfaceInvalidateCacheLine = new NativeInterfaceInvalidateCacheLine(NativeInterface.InvalidateCacheLine);
            var dlgNativeInterfaceReadByte = new NativeInterfaceReadByte(NativeInterface.ReadByte);
            var dlgNativeInterfaceReadUInt16 = new NativeInterfaceReadUInt16(NativeInterface.ReadUInt16);
            var dlgNativeInterfaceReadUInt32 = new NativeInterfaceReadUInt32(NativeInterface.ReadUInt32);
            var dlgNativeInterfaceReadUInt64 = new NativeInterfaceReadUInt64(NativeInterface.ReadUInt64);
            var dlgNativeInterfaceReadVector128 = new NativeInterfaceReadVector128(NativeInterface.ReadVector128);
            var dlgNativeInterfaceSignalMemoryTracking = new NativeInterfaceSignalMemoryTracking(NativeInterface.SignalMemoryTracking);
            var dlgNativeInterfaceSupervisorCall = new NativeInterfaceSupervisorCall(NativeInterface.SupervisorCall);
            var dlgNativeInterfaceThrowInvalidMemoryAccess = new NativeInterfaceThrowInvalidMemoryAccess(NativeInterface.ThrowInvalidMemoryAccess);
            var dlgNativeInterfaceUndefined = new NativeInterfaceUndefined(NativeInterface.Undefined);
            var dlgNativeInterfaceWriteByte = new NativeInterfaceWriteByte(NativeInterface.WriteByte);
            var dlgNativeInterfaceWriteUInt16 = new NativeInterfaceWriteUInt16(NativeInterface.WriteUInt16);
            var dlgNativeInterfaceWriteUInt32 = new NativeInterfaceWriteUInt32(NativeInterface.WriteUInt32);
            var dlgNativeInterfaceWriteUInt64 = new NativeInterfaceWriteUInt64(NativeInterface.WriteUInt64);
            var dlgNativeInterfaceWriteVector128 = new NativeInterfaceWriteVector128(NativeInterface.WriteVector128);

            var dlgSoftFallbackCountLeadingSigns = new SoftFallbackCountLeadingSigns(SoftFallback.CountLeadingSigns);
            var dlgSoftFallbackCountLeadingZeros = new SoftFallbackCountLeadingZeros(SoftFallback.CountLeadingZeros);
            var dlgSoftFallbackCrc32b = new SoftFallbackCrc32b(SoftFallback.Crc32b);
            var dlgSoftFallbackCrc32cb = new SoftFallbackCrc32cb(SoftFallback.Crc32cb);
            var dlgSoftFallbackCrc32ch = new SoftFallbackCrc32ch(SoftFallback.Crc32ch);
            var dlgSoftFallbackCrc32cw = new SoftFallbackCrc32cw(SoftFallback.Crc32cw);
            var dlgSoftFallbackCrc32cx = new SoftFallbackCrc32cx(SoftFallback.Crc32cx);
            var dlgSoftFallbackCrc32h = new SoftFallbackCrc32h(SoftFallback.Crc32h);
            var dlgSoftFallbackCrc32w = new SoftFallbackCrc32w(SoftFallback.Crc32w);
            var dlgSoftFallbackCrc32x = new SoftFallbackCrc32x(SoftFallback.Crc32x);
            var dlgSoftFallbackDecrypt = new SoftFallbackDecrypt(SoftFallback.Decrypt);
            var dlgSoftFallbackEncrypt = new SoftFallbackEncrypt(SoftFallback.Encrypt);
            var dlgSoftFallbackFixedRotate = new SoftFallbackFixedRotate(SoftFallback.FixedRotate);
            var dlgSoftFallbackHashChoose = new SoftFallbackHashChoose(SoftFallback.HashChoose);
            var dlgSoftFallbackHashLower = new SoftFallbackHashLower(SoftFallback.HashLower);
            var dlgSoftFallbackHashMajority = new SoftFallbackHashMajority(SoftFallback.HashMajority);
            var dlgSoftFallbackHashParity = new SoftFallbackHashParity(SoftFallback.HashParity);
            var dlgSoftFallbackHashUpper = new SoftFallbackHashUpper(SoftFallback.HashUpper);
            var dlgSoftFallbackInverseMixColumns = new SoftFallbackInverseMixColumns(SoftFallback.InverseMixColumns);
            var dlgSoftFallbackMixColumns = new SoftFallbackMixColumns(SoftFallback.MixColumns);
            var dlgSoftFallbackPolynomialMult64_128 = new SoftFallbackPolynomialMult64_128(SoftFallback.PolynomialMult64_128);
            var dlgSoftFallbackSatF32ToS32 = new SoftFallbackSatF32ToS32(SoftFallback.SatF32ToS32);
            var dlgSoftFallbackSatF32ToS64 = new SoftFallbackSatF32ToS64(SoftFallback.SatF32ToS64);
            var dlgSoftFallbackSatF32ToU32 = new SoftFallbackSatF32ToU32(SoftFallback.SatF32ToU32);
            var dlgSoftFallbackSatF32ToU64 = new SoftFallbackSatF32ToU64(SoftFallback.SatF32ToU64);
            var dlgSoftFallbackSatF64ToS32 = new SoftFallbackSatF64ToS32(SoftFallback.SatF64ToS32);
            var dlgSoftFallbackSatF64ToS64 = new SoftFallbackSatF64ToS64(SoftFallback.SatF64ToS64);
            var dlgSoftFallbackSatF64ToU32 = new SoftFallbackSatF64ToU32(SoftFallback.SatF64ToU32);
            var dlgSoftFallbackSatF64ToU64 = new SoftFallbackSatF64ToU64(SoftFallback.SatF64ToU64);
            var dlgSoftFallbackSha1SchedulePart1 = new SoftFallbackSha1SchedulePart1(SoftFallback.Sha1SchedulePart1);
            var dlgSoftFallbackSha1SchedulePart2 = new SoftFallbackSha1SchedulePart2(SoftFallback.Sha1SchedulePart2);
            var dlgSoftFallbackSha256SchedulePart1 = new SoftFallbackSha256SchedulePart1(SoftFallback.Sha256SchedulePart1);
            var dlgSoftFallbackSha256SchedulePart2 = new SoftFallbackSha256SchedulePart2(SoftFallback.Sha256SchedulePart2);
            var dlgSoftFallbackSignedShrImm64 = new SoftFallbackSignedShrImm64(SoftFallback.SignedShrImm64);
            var dlgSoftFallbackTbl1 = new SoftFallbackTbl1(SoftFallback.Tbl1);
            var dlgSoftFallbackTbl2 = new SoftFallbackTbl2(SoftFallback.Tbl2);
            var dlgSoftFallbackTbl3 = new SoftFallbackTbl3(SoftFallback.Tbl3);
            var dlgSoftFallbackTbl4 = new SoftFallbackTbl4(SoftFallback.Tbl4);
            var dlgSoftFallbackTbx1 = new SoftFallbackTbx1(SoftFallback.Tbx1);
            var dlgSoftFallbackTbx2 = new SoftFallbackTbx2(SoftFallback.Tbx2);
            var dlgSoftFallbackTbx3 = new SoftFallbackTbx3(SoftFallback.Tbx3);
            var dlgSoftFallbackTbx4 = new SoftFallbackTbx4(SoftFallback.Tbx4);
            var dlgSoftFallbackUnsignedShrImm64 = new SoftFallbackUnsignedShrImm64(SoftFallback.UnsignedShrImm64);

            var dlgSoftFloat16_32FPConvert = new SoftFloat16_32FPConvert(SoftFloat16_32.FPConvert);
            var dlgSoftFloat16_64FPConvert = new SoftFloat16_64FPConvert(SoftFloat16_64.FPConvert);

            var dlgSoftFloat32FPAdd = new SoftFloat32FPAdd(SoftFloat32.FPAdd);
            var dlgSoftFloat32FPAddFpscr = new SoftFloat32FPAddFpscr(SoftFloat32.FPAddFpscr); // A32 only.
            var dlgSoftFloat32FPCompare = new SoftFloat32FPCompare(SoftFloat32.FPCompare);
            var dlgSoftFloat32FPCompareEQ = new SoftFloat32FPCompareEQ(SoftFloat32.FPCompareEQ);
            var dlgSoftFloat32FPCompareEQFpscr = new SoftFloat32FPCompareEQFpscr(SoftFloat32.FPCompareEQFpscr); // A32 only.
            var dlgSoftFloat32FPCompareGE = new SoftFloat32FPCompareGE(SoftFloat32.FPCompareGE);
            var dlgSoftFloat32FPCompareGEFpscr = new SoftFloat32FPCompareGEFpscr(SoftFloat32.FPCompareGEFpscr); // A32 only.
            var dlgSoftFloat32FPCompareGT = new SoftFloat32FPCompareGT(SoftFloat32.FPCompareGT);
            var dlgSoftFloat32FPCompareGTFpscr = new SoftFloat32FPCompareGTFpscr(SoftFloat32.FPCompareGTFpscr); // A32 only.
            var dlgSoftFloat32FPCompareLE = new SoftFloat32FPCompareLE(SoftFloat32.FPCompareLE);
            var dlgSoftFloat32FPCompareLEFpscr = new SoftFloat32FPCompareLEFpscr(SoftFloat32.FPCompareLEFpscr); // A32 only.
            var dlgSoftFloat32FPCompareLT = new SoftFloat32FPCompareLT(SoftFloat32.FPCompareLT);
            var dlgSoftFloat32FPCompareLTFpscr = new SoftFloat32FPCompareLTFpscr(SoftFloat32.FPCompareLTFpscr); // A32 only.
            var dlgSoftFloat32FPDiv = new SoftFloat32FPDiv(SoftFloat32.FPDiv);
            var dlgSoftFloat32FPMax = new SoftFloat32FPMax(SoftFloat32.FPMax);
            var dlgSoftFloat32FPMaxFpscr = new SoftFloat32FPMaxFpscr(SoftFloat32.FPMaxFpscr); // A32 only.
            var dlgSoftFloat32FPMaxNum = new SoftFloat32FPMaxNum(SoftFloat32.FPMaxNum);
            var dlgSoftFloat32FPMaxNumFpscr = new SoftFloat32FPMaxNumFpscr(SoftFloat32.FPMaxNumFpscr); // A32 only.
            var dlgSoftFloat32FPMin = new SoftFloat32FPMin(SoftFloat32.FPMin);
            var dlgSoftFloat32FPMinFpscr = new SoftFloat32FPMinFpscr(SoftFloat32.FPMinFpscr); // A32 only.
            var dlgSoftFloat32FPMinNum = new SoftFloat32FPMinNum(SoftFloat32.FPMinNum);
            var dlgSoftFloat32FPMinNumFpscr = new SoftFloat32FPMinNumFpscr(SoftFloat32.FPMinNumFpscr); // A32 only.
            var dlgSoftFloat32FPMul = new SoftFloat32FPMul(SoftFloat32.FPMul);
            var dlgSoftFloat32FPMulFpscr = new SoftFloat32FPMulFpscr(SoftFloat32.FPMulFpscr); // A32 only.
            var dlgSoftFloat32FPMulAdd = new SoftFloat32FPMulAdd(SoftFloat32.FPMulAdd);
            var dlgSoftFloat32FPMulAddFpscr = new SoftFloat32FPMulAddFpscr(SoftFloat32.FPMulAddFpscr); // A32 only.
            var dlgSoftFloat32FPMulSub = new SoftFloat32FPMulSub(SoftFloat32.FPMulSub);
            var dlgSoftFloat32FPMulSubFpscr = new SoftFloat32FPMulSubFpscr(SoftFloat32.FPMulSubFpscr); // A32 only.
            var dlgSoftFloat32FPMulX = new SoftFloat32FPMulX(SoftFloat32.FPMulX);
            var dlgSoftFloat32FPNegMulAdd = new SoftFloat32FPNegMulAdd(SoftFloat32.FPNegMulAdd);
            var dlgSoftFloat32FPNegMulSub = new SoftFloat32FPNegMulSub(SoftFloat32.FPNegMulSub);
            var dlgSoftFloat32FPRecipEstimate = new SoftFloat32FPRecipEstimate(SoftFloat32.FPRecipEstimate);
            var dlgSoftFloat32FPRecipEstimateFpscr = new SoftFloat32FPRecipEstimateFpscr(SoftFloat32.FPRecipEstimateFpscr); // A32 only.
            var dlgSoftFloat32FPRecipStep = new SoftFloat32FPRecipStep(SoftFloat32.FPRecipStep); // A32 only.
            var dlgSoftFloat32FPRecipStepFused = new SoftFloat32FPRecipStepFused(SoftFloat32.FPRecipStepFused);
            var dlgSoftFloat32FPRecpX = new SoftFloat32FPRecpX(SoftFloat32.FPRecpX);
            var dlgSoftFloat32FPRSqrtEstimate = new SoftFloat32FPRSqrtEstimate(SoftFloat32.FPRSqrtEstimate);
            var dlgSoftFloat32FPRSqrtEstimateFpscr = new SoftFloat32FPRSqrtEstimateFpscr(SoftFloat32.FPRSqrtEstimateFpscr); // A32 only.
            var dlgSoftFloat32FPRSqrtStep = new SoftFloat32FPRSqrtStep(SoftFloat32.FPRSqrtStep); // A32 only.
            var dlgSoftFloat32FPRSqrtStepFused = new SoftFloat32FPRSqrtStepFused(SoftFloat32.FPRSqrtStepFused);
            var dlgSoftFloat32FPSqrt = new SoftFloat32FPSqrt(SoftFloat32.FPSqrt);
            var dlgSoftFloat32FPSub = new SoftFloat32FPSub(SoftFloat32.FPSub);

            var dlgSoftFloat32_16FPConvert = new SoftFloat32_16FPConvert(SoftFloat32_16.FPConvert);

            var dlgSoftFloat64FPAdd = new SoftFloat64FPAdd(SoftFloat64.FPAdd);
            var dlgSoftFloat64FPAddFpscr = new SoftFloat64FPAddFpscr(SoftFloat64.FPAddFpscr); // A32 only.
            var dlgSoftFloat64FPCompare = new SoftFloat64FPCompare(SoftFloat64.FPCompare);
            var dlgSoftFloat64FPCompareEQ = new SoftFloat64FPCompareEQ(SoftFloat64.FPCompareEQ);
            var dlgSoftFloat64FPCompareEQFpscr = new SoftFloat64FPCompareEQFpscr(SoftFloat64.FPCompareEQFpscr); // A32 only.
            var dlgSoftFloat64FPCompareGE = new SoftFloat64FPCompareGE(SoftFloat64.FPCompareGE);
            var dlgSoftFloat64FPCompareGEFpscr = new SoftFloat64FPCompareGEFpscr(SoftFloat64.FPCompareGEFpscr); // A32 only.
            var dlgSoftFloat64FPCompareGT = new SoftFloat64FPCompareGT(SoftFloat64.FPCompareGT);
            var dlgSoftFloat64FPCompareGTFpscr = new SoftFloat64FPCompareGTFpscr(SoftFloat64.FPCompareGTFpscr); // A32 only.
            var dlgSoftFloat64FPCompareLE = new SoftFloat64FPCompareLE(SoftFloat64.FPCompareLE);
            var dlgSoftFloat64FPCompareLEFpscr = new SoftFloat64FPCompareLEFpscr(SoftFloat64.FPCompareLEFpscr); // A32 only.
            var dlgSoftFloat64FPCompareLT = new SoftFloat64FPCompareLT(SoftFloat64.FPCompareLT);
            var dlgSoftFloat64FPCompareLTFpscr = new SoftFloat64FPCompareLTFpscr(SoftFloat64.FPCompareLTFpscr); // A32 only.
            var dlgSoftFloat64FPDiv = new SoftFloat64FPDiv(SoftFloat64.FPDiv);
            var dlgSoftFloat64FPMax = new SoftFloat64FPMax(SoftFloat64.FPMax);
            var dlgSoftFloat64FPMaxFpscr = new SoftFloat64FPMaxFpscr(SoftFloat64.FPMaxFpscr); // A32 only.
            var dlgSoftFloat64FPMaxNum = new SoftFloat64FPMaxNum(SoftFloat64.FPMaxNum);
            var dlgSoftFloat64FPMaxNumFpscr = new SoftFloat64FPMaxNumFpscr(SoftFloat64.FPMaxNumFpscr); // A32 only.
            var dlgSoftFloat64FPMin = new SoftFloat64FPMin(SoftFloat64.FPMin);
            var dlgSoftFloat64FPMinFpscr = new SoftFloat64FPMinFpscr(SoftFloat64.FPMinFpscr); // A32 only.
            var dlgSoftFloat64FPMinNum = new SoftFloat64FPMinNum(SoftFloat64.FPMinNum);
            var dlgSoftFloat64FPMinNumFpscr = new SoftFloat64FPMinNumFpscr(SoftFloat64.FPMinNumFpscr); // A32 only.
            var dlgSoftFloat64FPMul = new SoftFloat64FPMul(SoftFloat64.FPMul);
            var dlgSoftFloat64FPMulFpscr = new SoftFloat64FPMulFpscr(SoftFloat64.FPMulFpscr); // A32 only.
            var dlgSoftFloat64FPMulAdd = new SoftFloat64FPMulAdd(SoftFloat64.FPMulAdd);
            var dlgSoftFloat64FPMulAddFpscr = new SoftFloat64FPMulAddFpscr(SoftFloat64.FPMulAddFpscr); // A32 only.
            var dlgSoftFloat64FPMulSub = new SoftFloat64FPMulSub(SoftFloat64.FPMulSub);
            var dlgSoftFloat64FPMulSubFpscr = new SoftFloat64FPMulSubFpscr(SoftFloat64.FPMulSubFpscr); // A32 only.
            var dlgSoftFloat64FPMulX = new SoftFloat64FPMulX(SoftFloat64.FPMulX);
            var dlgSoftFloat64FPNegMulAdd = new SoftFloat64FPNegMulAdd(SoftFloat64.FPNegMulAdd);
            var dlgSoftFloat64FPNegMulSub = new SoftFloat64FPNegMulSub(SoftFloat64.FPNegMulSub);
            var dlgSoftFloat64FPRecipEstimate = new SoftFloat64FPRecipEstimate(SoftFloat64.FPRecipEstimate);
            var dlgSoftFloat64FPRecipEstimateFpscr = new SoftFloat64FPRecipEstimateFpscr(SoftFloat64.FPRecipEstimateFpscr); // A32 only.
            var dlgSoftFloat64FPRecipStep = new SoftFloat64FPRecipStep(SoftFloat64.FPRecipStep); // A32 only.
            var dlgSoftFloat64FPRecipStepFused = new SoftFloat64FPRecipStepFused(SoftFloat64.FPRecipStepFused);
            var dlgSoftFloat64FPRecpX = new SoftFloat64FPRecpX(SoftFloat64.FPRecpX);
            var dlgSoftFloat64FPRSqrtEstimate = new SoftFloat64FPRSqrtEstimate(SoftFloat64.FPRSqrtEstimate);
            var dlgSoftFloat64FPRSqrtEstimateFpscr = new SoftFloat64FPRSqrtEstimateFpscr(SoftFloat64.FPRSqrtEstimateFpscr); // A32 only.
            var dlgSoftFloat64FPRSqrtStep = new SoftFloat64FPRSqrtStep(SoftFloat64.FPRSqrtStep); // A32 only.
            var dlgSoftFloat64FPRSqrtStepFused = new SoftFloat64FPRSqrtStepFused(SoftFloat64.FPRSqrtStepFused);
            var dlgSoftFloat64FPSqrt = new SoftFloat64FPSqrt(SoftFloat64.FPSqrt);
            var dlgSoftFloat64FPSub = new SoftFloat64FPSub(SoftFloat64.FPSub);

            var dlgSoftFloat64_16FPConvert = new SoftFloat64_16FPConvert(SoftFloat64_16.FPConvert);

            SetDelegateInfo(dlgMathAbs, Marshal.GetFunctionPointerForDelegate<MathAbs>(dlgMathAbs));
            SetDelegateInfo(dlgMathCeiling, Marshal.GetFunctionPointerForDelegate<MathCeiling>(dlgMathCeiling));
            SetDelegateInfo(dlgMathFloor, Marshal.GetFunctionPointerForDelegate<MathFloor>(dlgMathFloor));
            SetDelegateInfo(dlgMathRound, Marshal.GetFunctionPointerForDelegate<MathRound>(dlgMathRound));
            SetDelegateInfo(dlgMathTruncate, Marshal.GetFunctionPointerForDelegate<MathTruncate>(dlgMathTruncate));

            SetDelegateInfo(dlgMathFAbs, Marshal.GetFunctionPointerForDelegate<MathFAbs>(dlgMathFAbs));
            SetDelegateInfo(dlgMathFCeiling, Marshal.GetFunctionPointerForDelegate<MathFCeiling>(dlgMathFCeiling));
            SetDelegateInfo(dlgMathFFloor, Marshal.GetFunctionPointerForDelegate<MathFFloor>(dlgMathFFloor));
            SetDelegateInfo(dlgMathFRound, Marshal.GetFunctionPointerForDelegate<MathFRound>(dlgMathFRound));
            SetDelegateInfo(dlgMathFTruncate, Marshal.GetFunctionPointerForDelegate<MathFTruncate>(dlgMathFTruncate));

            SetDelegateInfo(dlgNativeInterfaceBreak, Marshal.GetFunctionPointerForDelegate<NativeInterfaceBreak>(dlgNativeInterfaceBreak));
            SetDelegateInfo(dlgNativeInterfaceCheckSynchronization, Marshal.GetFunctionPointerForDelegate<NativeInterfaceCheckSynchronization>(dlgNativeInterfaceCheckSynchronization));
            SetDelegateInfo(dlgNativeInterfaceEnqueueForRejit, Marshal.GetFunctionPointerForDelegate<NativeInterfaceEnqueueForRejit>(dlgNativeInterfaceEnqueueForRejit));
            SetDelegateInfo(dlgNativeInterfaceGetCntfrqEl0, Marshal.GetFunctionPointerForDelegate<NativeInterfaceGetCntfrqEl0>(dlgNativeInterfaceGetCntfrqEl0));
            SetDelegateInfo(dlgNativeInterfaceGetCntpctEl0, Marshal.GetFunctionPointerForDelegate<NativeInterfaceGetCntpctEl0>(dlgNativeInterfaceGetCntpctEl0));
            SetDelegateInfo(dlgNativeInterfaceGetCntvctEl0, Marshal.GetFunctionPointerForDelegate<NativeInterfaceGetCntvctEl0>(dlgNativeInterfaceGetCntvctEl0));
            SetDelegateInfo(dlgNativeInterfaceGetCtrEl0, Marshal.GetFunctionPointerForDelegate<NativeInterfaceGetCtrEl0>(dlgNativeInterfaceGetCtrEl0));
            SetDelegateInfo(dlgNativeInterfaceGetDczidEl0, Marshal.GetFunctionPointerForDelegate<NativeInterfaceGetDczidEl0>(dlgNativeInterfaceGetDczidEl0));
            SetDelegateInfo(dlgNativeInterfaceGetFunctionAddress, Marshal.GetFunctionPointerForDelegate<NativeInterfaceGetFunctionAddress>(dlgNativeInterfaceGetFunctionAddress));
            SetDelegateInfo(dlgNativeInterfaceInvalidateCacheLine, Marshal.GetFunctionPointerForDelegate<NativeInterfaceInvalidateCacheLine>(dlgNativeInterfaceInvalidateCacheLine));
            SetDelegateInfo(dlgNativeInterfaceReadByte, Marshal.GetFunctionPointerForDelegate<NativeInterfaceReadByte>(dlgNativeInterfaceReadByte));
            SetDelegateInfo(dlgNativeInterfaceReadUInt16, Marshal.GetFunctionPointerForDelegate<NativeInterfaceReadUInt16>(dlgNativeInterfaceReadUInt16));
            SetDelegateInfo(dlgNativeInterfaceReadUInt32, Marshal.GetFunctionPointerForDelegate<NativeInterfaceReadUInt32>(dlgNativeInterfaceReadUInt32));
            SetDelegateInfo(dlgNativeInterfaceReadUInt64, Marshal.GetFunctionPointerForDelegate<NativeInterfaceReadUInt64>(dlgNativeInterfaceReadUInt64));
            SetDelegateInfo(dlgNativeInterfaceReadVector128, Marshal.GetFunctionPointerForDelegate<NativeInterfaceReadVector128>(dlgNativeInterfaceReadVector128));
            SetDelegateInfo(dlgNativeInterfaceSignalMemoryTracking, Marshal.GetFunctionPointerForDelegate<NativeInterfaceSignalMemoryTracking>(dlgNativeInterfaceSignalMemoryTracking));
            SetDelegateInfo(dlgNativeInterfaceSupervisorCall, Marshal.GetFunctionPointerForDelegate<NativeInterfaceSupervisorCall>(dlgNativeInterfaceSupervisorCall));
            SetDelegateInfo(dlgNativeInterfaceThrowInvalidMemoryAccess, Marshal.GetFunctionPointerForDelegate<NativeInterfaceThrowInvalidMemoryAccess>(dlgNativeInterfaceThrowInvalidMemoryAccess));
            SetDelegateInfo(dlgNativeInterfaceUndefined, Marshal.GetFunctionPointerForDelegate<NativeInterfaceUndefined>(dlgNativeInterfaceUndefined));
            SetDelegateInfo(dlgNativeInterfaceWriteByte, Marshal.GetFunctionPointerForDelegate<NativeInterfaceWriteByte>(dlgNativeInterfaceWriteByte));
            SetDelegateInfo(dlgNativeInterfaceWriteUInt16, Marshal.GetFunctionPointerForDelegate<NativeInterfaceWriteUInt16>(dlgNativeInterfaceWriteUInt16));
            SetDelegateInfo(dlgNativeInterfaceWriteUInt32, Marshal.GetFunctionPointerForDelegate<NativeInterfaceWriteUInt32>(dlgNativeInterfaceWriteUInt32));
            SetDelegateInfo(dlgNativeInterfaceWriteUInt64, Marshal.GetFunctionPointerForDelegate<NativeInterfaceWriteUInt64>(dlgNativeInterfaceWriteUInt64));
            SetDelegateInfo(dlgNativeInterfaceWriteVector128, Marshal.GetFunctionPointerForDelegate<NativeInterfaceWriteVector128>(dlgNativeInterfaceWriteVector128));

            SetDelegateInfo(dlgSoftFallbackCountLeadingSigns, Marshal.GetFunctionPointerForDelegate<SoftFallbackCountLeadingSigns>(dlgSoftFallbackCountLeadingSigns));
            SetDelegateInfo(dlgSoftFallbackCountLeadingZeros, Marshal.GetFunctionPointerForDelegate<SoftFallbackCountLeadingZeros>(dlgSoftFallbackCountLeadingZeros));
            SetDelegateInfo(dlgSoftFallbackCrc32b, Marshal.GetFunctionPointerForDelegate<SoftFallbackCrc32b>(dlgSoftFallbackCrc32b));
            SetDelegateInfo(dlgSoftFallbackCrc32cb, Marshal.GetFunctionPointerForDelegate<SoftFallbackCrc32cb>(dlgSoftFallbackCrc32cb));
            SetDelegateInfo(dlgSoftFallbackCrc32ch, Marshal.GetFunctionPointerForDelegate<SoftFallbackCrc32ch>(dlgSoftFallbackCrc32ch));
            SetDelegateInfo(dlgSoftFallbackCrc32cw, Marshal.GetFunctionPointerForDelegate<SoftFallbackCrc32cw>(dlgSoftFallbackCrc32cw));
            SetDelegateInfo(dlgSoftFallbackCrc32cx, Marshal.GetFunctionPointerForDelegate<SoftFallbackCrc32cx>(dlgSoftFallbackCrc32cx));
            SetDelegateInfo(dlgSoftFallbackCrc32h, Marshal.GetFunctionPointerForDelegate<SoftFallbackCrc32h>(dlgSoftFallbackCrc32h));
            SetDelegateInfo(dlgSoftFallbackCrc32w, Marshal.GetFunctionPointerForDelegate<SoftFallbackCrc32w>(dlgSoftFallbackCrc32w));
            SetDelegateInfo(dlgSoftFallbackCrc32x, Marshal.GetFunctionPointerForDelegate<SoftFallbackCrc32x>(dlgSoftFallbackCrc32x));
            SetDelegateInfo(dlgSoftFallbackDecrypt, Marshal.GetFunctionPointerForDelegate<SoftFallbackDecrypt>(dlgSoftFallbackDecrypt));
            SetDelegateInfo(dlgSoftFallbackEncrypt, Marshal.GetFunctionPointerForDelegate<SoftFallbackEncrypt>(dlgSoftFallbackEncrypt));
            SetDelegateInfo(dlgSoftFallbackFixedRotate, Marshal.GetFunctionPointerForDelegate<SoftFallbackFixedRotate>(dlgSoftFallbackFixedRotate));
            SetDelegateInfo(dlgSoftFallbackHashChoose, Marshal.GetFunctionPointerForDelegate<SoftFallbackHashChoose>(dlgSoftFallbackHashChoose));
            SetDelegateInfo(dlgSoftFallbackHashLower, Marshal.GetFunctionPointerForDelegate<SoftFallbackHashLower>(dlgSoftFallbackHashLower));
            SetDelegateInfo(dlgSoftFallbackHashMajority, Marshal.GetFunctionPointerForDelegate<SoftFallbackHashMajority>(dlgSoftFallbackHashMajority));
            SetDelegateInfo(dlgSoftFallbackHashParity, Marshal.GetFunctionPointerForDelegate<SoftFallbackHashParity>(dlgSoftFallbackHashParity));
            SetDelegateInfo(dlgSoftFallbackHashUpper, Marshal.GetFunctionPointerForDelegate<SoftFallbackHashUpper>(dlgSoftFallbackHashUpper));
            SetDelegateInfo(dlgSoftFallbackInverseMixColumns, Marshal.GetFunctionPointerForDelegate<SoftFallbackInverseMixColumns>(dlgSoftFallbackInverseMixColumns));
            SetDelegateInfo(dlgSoftFallbackMixColumns, Marshal.GetFunctionPointerForDelegate<SoftFallbackMixColumns>(dlgSoftFallbackMixColumns));
            SetDelegateInfo(dlgSoftFallbackPolynomialMult64_128, Marshal.GetFunctionPointerForDelegate<SoftFallbackPolynomialMult64_128>(dlgSoftFallbackPolynomialMult64_128));
            SetDelegateInfo(dlgSoftFallbackSatF32ToS32, Marshal.GetFunctionPointerForDelegate<SoftFallbackSatF32ToS32>(dlgSoftFallbackSatF32ToS32));
            SetDelegateInfo(dlgSoftFallbackSatF32ToS64, Marshal.GetFunctionPointerForDelegate<SoftFallbackSatF32ToS64>(dlgSoftFallbackSatF32ToS64));
            SetDelegateInfo(dlgSoftFallbackSatF32ToU32, Marshal.GetFunctionPointerForDelegate<SoftFallbackSatF32ToU32>(dlgSoftFallbackSatF32ToU32));
            SetDelegateInfo(dlgSoftFallbackSatF32ToU64, Marshal.GetFunctionPointerForDelegate<SoftFallbackSatF32ToU64>(dlgSoftFallbackSatF32ToU64));
            SetDelegateInfo(dlgSoftFallbackSatF64ToS32, Marshal.GetFunctionPointerForDelegate<SoftFallbackSatF64ToS32>(dlgSoftFallbackSatF64ToS32));
            SetDelegateInfo(dlgSoftFallbackSatF64ToS64, Marshal.GetFunctionPointerForDelegate<SoftFallbackSatF64ToS64>(dlgSoftFallbackSatF64ToS64));
            SetDelegateInfo(dlgSoftFallbackSatF64ToU32, Marshal.GetFunctionPointerForDelegate<SoftFallbackSatF64ToU32>(dlgSoftFallbackSatF64ToU32));
            SetDelegateInfo(dlgSoftFallbackSatF64ToU64, Marshal.GetFunctionPointerForDelegate<SoftFallbackSatF64ToU64>(dlgSoftFallbackSatF64ToU64));
            SetDelegateInfo(dlgSoftFallbackSha1SchedulePart1, Marshal.GetFunctionPointerForDelegate<SoftFallbackSha1SchedulePart1>(dlgSoftFallbackSha1SchedulePart1));
            SetDelegateInfo(dlgSoftFallbackSha1SchedulePart2, Marshal.GetFunctionPointerForDelegate<SoftFallbackSha1SchedulePart2>(dlgSoftFallbackSha1SchedulePart2));
            SetDelegateInfo(dlgSoftFallbackSha256SchedulePart1, Marshal.GetFunctionPointerForDelegate<SoftFallbackSha256SchedulePart1>(dlgSoftFallbackSha256SchedulePart1));
            SetDelegateInfo(dlgSoftFallbackSha256SchedulePart2, Marshal.GetFunctionPointerForDelegate<SoftFallbackSha256SchedulePart2>(dlgSoftFallbackSha256SchedulePart2));
            SetDelegateInfo(dlgSoftFallbackSignedShrImm64, Marshal.GetFunctionPointerForDelegate<SoftFallbackSignedShrImm64>(dlgSoftFallbackSignedShrImm64));
            SetDelegateInfo(dlgSoftFallbackTbl1, Marshal.GetFunctionPointerForDelegate<SoftFallbackTbl1>(dlgSoftFallbackTbl1));
            SetDelegateInfo(dlgSoftFallbackTbl2, Marshal.GetFunctionPointerForDelegate<SoftFallbackTbl2>(dlgSoftFallbackTbl2));
            SetDelegateInfo(dlgSoftFallbackTbl3, Marshal.GetFunctionPointerForDelegate<SoftFallbackTbl3>(dlgSoftFallbackTbl3));
            SetDelegateInfo(dlgSoftFallbackTbl4, Marshal.GetFunctionPointerForDelegate<SoftFallbackTbl4>(dlgSoftFallbackTbl4));
            SetDelegateInfo(dlgSoftFallbackTbx1, Marshal.GetFunctionPointerForDelegate<SoftFallbackTbx1>(dlgSoftFallbackTbx1));
            SetDelegateInfo(dlgSoftFallbackTbx2, Marshal.GetFunctionPointerForDelegate<SoftFallbackTbx2>(dlgSoftFallbackTbx2));
            SetDelegateInfo(dlgSoftFallbackTbx3, Marshal.GetFunctionPointerForDelegate<SoftFallbackTbx3>(dlgSoftFallbackTbx3));
            SetDelegateInfo(dlgSoftFallbackTbx4, Marshal.GetFunctionPointerForDelegate<SoftFallbackTbx4>(dlgSoftFallbackTbx4));
            SetDelegateInfo(dlgSoftFallbackUnsignedShrImm64, Marshal.GetFunctionPointerForDelegate<SoftFallbackUnsignedShrImm64>(dlgSoftFallbackUnsignedShrImm64));

            SetDelegateInfo(dlgSoftFloat16_32FPConvert, Marshal.GetFunctionPointerForDelegate<SoftFloat16_32FPConvert>(dlgSoftFloat16_32FPConvert));
            SetDelegateInfo(dlgSoftFloat16_64FPConvert, Marshal.GetFunctionPointerForDelegate<SoftFloat16_64FPConvert>(dlgSoftFloat16_64FPConvert));

            SetDelegateInfo(dlgSoftFloat32FPAdd, Marshal.GetFunctionPointerForDelegate<SoftFloat32FPAdd>(dlgSoftFloat32FPAdd));
            SetDelegateInfo(dlgSoftFloat32FPAddFpscr, Marshal.GetFunctionPointerForDelegate<SoftFloat32FPAddFpscr>(dlgSoftFloat32FPAddFpscr)); // A32 only.
            SetDelegateInfo(dlgSoftFloat32FPCompare, Marshal.GetFunctionPointerForDelegate<SoftFloat32FPCompare>(dlgSoftFloat32FPCompare));
            SetDelegateInfo(dlgSoftFloat32FPCompareEQ, Marshal.GetFunctionPointerForDelegate<SoftFloat32FPCompareEQ>(dlgSoftFloat32FPCompareEQ));
            SetDelegateInfo(dlgSoftFloat32FPCompareEQFpscr, Marshal.GetFunctionPointerForDelegate<SoftFloat32FPCompareEQFpscr>(dlgSoftFloat32FPCompareEQFpscr)); // A32 only.
            SetDelegateInfo(dlgSoftFloat32FPCompareGE, Marshal.GetFunctionPointerForDelegate<SoftFloat32FPCompareGE>(dlgSoftFloat32FPCompareGE));
            SetDelegateInfo(dlgSoftFloat32FPCompareGEFpscr, Marshal.GetFunctionPointerForDelegate<SoftFloat32FPCompareGEFpscr>(dlgSoftFloat32FPCompareGEFpscr)); // A32 only.
            SetDelegateInfo(dlgSoftFloat32FPCompareGT, Marshal.GetFunctionPointerForDelegate<SoftFloat32FPCompareGT>(dlgSoftFloat32FPCompareGT));
            SetDelegateInfo(dlgSoftFloat32FPCompareGTFpscr, Marshal.GetFunctionPointerForDelegate<SoftFloat32FPCompareGTFpscr>(dlgSoftFloat32FPCompareGTFpscr)); // A32 only.
            SetDelegateInfo(dlgSoftFloat32FPCompareLE, Marshal.GetFunctionPointerForDelegate<SoftFloat32FPCompareLE>(dlgSoftFloat32FPCompareLE));
            SetDelegateInfo(dlgSoftFloat32FPCompareLEFpscr, Marshal.GetFunctionPointerForDelegate<SoftFloat32FPCompareLEFpscr>(dlgSoftFloat32FPCompareLEFpscr)); // A32 only.
            SetDelegateInfo(dlgSoftFloat32FPCompareLT, Marshal.GetFunctionPointerForDelegate<SoftFloat32FPCompareLT>(dlgSoftFloat32FPCompareLT));
            SetDelegateInfo(dlgSoftFloat32FPCompareLTFpscr, Marshal.GetFunctionPointerForDelegate<SoftFloat32FPCompareLTFpscr>(dlgSoftFloat32FPCompareLTFpscr)); // A32 only.
            SetDelegateInfo(dlgSoftFloat32FPDiv, Marshal.GetFunctionPointerForDelegate<SoftFloat32FPDiv>(dlgSoftFloat32FPDiv));
            SetDelegateInfo(dlgSoftFloat32FPMax, Marshal.GetFunctionPointerForDelegate<SoftFloat32FPMax>(dlgSoftFloat32FPMax));
            SetDelegateInfo(dlgSoftFloat32FPMaxFpscr, Marshal.GetFunctionPointerForDelegate<SoftFloat32FPMaxFpscr>(dlgSoftFloat32FPMaxFpscr)); // A32 only.
            SetDelegateInfo(dlgSoftFloat32FPMaxNum, Marshal.GetFunctionPointerForDelegate<SoftFloat32FPMaxNum>(dlgSoftFloat32FPMaxNum));
            SetDelegateInfo(dlgSoftFloat32FPMaxNumFpscr, Marshal.GetFunctionPointerForDelegate<SoftFloat32FPMaxNumFpscr>(dlgSoftFloat32FPMaxNumFpscr)); // A32 only.
            SetDelegateInfo(dlgSoftFloat32FPMin, Marshal.GetFunctionPointerForDelegate<SoftFloat32FPMin>(dlgSoftFloat32FPMin));
            SetDelegateInfo(dlgSoftFloat32FPMinFpscr, Marshal.GetFunctionPointerForDelegate<SoftFloat32FPMinFpscr>(dlgSoftFloat32FPMinFpscr)); // A32 only.
            SetDelegateInfo(dlgSoftFloat32FPMinNum, Marshal.GetFunctionPointerForDelegate<SoftFloat32FPMinNum>(dlgSoftFloat32FPMinNum));
            SetDelegateInfo(dlgSoftFloat32FPMinNumFpscr, Marshal.GetFunctionPointerForDelegate<SoftFloat32FPMinNumFpscr>(dlgSoftFloat32FPMinNumFpscr)); // A32 only.
            SetDelegateInfo(dlgSoftFloat32FPMul, Marshal.GetFunctionPointerForDelegate<SoftFloat32FPMul>(dlgSoftFloat32FPMul));
            SetDelegateInfo(dlgSoftFloat32FPMulFpscr, Marshal.GetFunctionPointerForDelegate<SoftFloat32FPMulFpscr>(dlgSoftFloat32FPMulFpscr)); // A32 only.
            SetDelegateInfo(dlgSoftFloat32FPMulAdd, Marshal.GetFunctionPointerForDelegate<SoftFloat32FPMulAdd>(dlgSoftFloat32FPMulAdd));
            SetDelegateInfo(dlgSoftFloat32FPMulAddFpscr, Marshal.GetFunctionPointerForDelegate<SoftFloat32FPMulAddFpscr>(dlgSoftFloat32FPMulAddFpscr)); // A32 only.
            SetDelegateInfo(dlgSoftFloat32FPMulSub, Marshal.GetFunctionPointerForDelegate<SoftFloat32FPMulSub>(dlgSoftFloat32FPMulSub));
            SetDelegateInfo(dlgSoftFloat32FPMulSubFpscr, Marshal.GetFunctionPointerForDelegate<SoftFloat32FPMulSubFpscr>(dlgSoftFloat32FPMulSubFpscr)); // A32 only.
            SetDelegateInfo(dlgSoftFloat32FPMulX, Marshal.GetFunctionPointerForDelegate<SoftFloat32FPMulX>(dlgSoftFloat32FPMulX));
            SetDelegateInfo(dlgSoftFloat32FPNegMulAdd, Marshal.GetFunctionPointerForDelegate<SoftFloat32FPNegMulAdd>(dlgSoftFloat32FPNegMulAdd));
            SetDelegateInfo(dlgSoftFloat32FPNegMulSub, Marshal.GetFunctionPointerForDelegate<SoftFloat32FPNegMulSub>(dlgSoftFloat32FPNegMulSub));
            SetDelegateInfo(dlgSoftFloat32FPRecipEstimate, Marshal.GetFunctionPointerForDelegate<SoftFloat32FPRecipEstimate>(dlgSoftFloat32FPRecipEstimate));
            SetDelegateInfo(dlgSoftFloat32FPRecipEstimateFpscr, Marshal.GetFunctionPointerForDelegate<SoftFloat32FPRecipEstimateFpscr>(dlgSoftFloat32FPRecipEstimateFpscr)); // A32 only.
            SetDelegateInfo(dlgSoftFloat32FPRecipStep, Marshal.GetFunctionPointerForDelegate<SoftFloat32FPRecipStep>(dlgSoftFloat32FPRecipStep)); // A32 only.
            SetDelegateInfo(dlgSoftFloat32FPRecipStepFused, Marshal.GetFunctionPointerForDelegate<SoftFloat32FPRecipStepFused>(dlgSoftFloat32FPRecipStepFused));
            SetDelegateInfo(dlgSoftFloat32FPRecpX, Marshal.GetFunctionPointerForDelegate<SoftFloat32FPRecpX>(dlgSoftFloat32FPRecpX));
            SetDelegateInfo(dlgSoftFloat32FPRSqrtEstimate, Marshal.GetFunctionPointerForDelegate<SoftFloat32FPRSqrtEstimate>(dlgSoftFloat32FPRSqrtEstimate));
            SetDelegateInfo(dlgSoftFloat32FPRSqrtEstimateFpscr, Marshal.GetFunctionPointerForDelegate<SoftFloat32FPRSqrtEstimateFpscr>(dlgSoftFloat32FPRSqrtEstimateFpscr)); // A32 only.
            SetDelegateInfo(dlgSoftFloat32FPRSqrtStep, Marshal.GetFunctionPointerForDelegate<SoftFloat32FPRSqrtStep>(dlgSoftFloat32FPRSqrtStep)); // A32 only.
            SetDelegateInfo(dlgSoftFloat32FPRSqrtStepFused, Marshal.GetFunctionPointerForDelegate<SoftFloat32FPRSqrtStepFused>(dlgSoftFloat32FPRSqrtStepFused));
            SetDelegateInfo(dlgSoftFloat32FPSqrt, Marshal.GetFunctionPointerForDelegate<SoftFloat32FPSqrt>(dlgSoftFloat32FPSqrt));
            SetDelegateInfo(dlgSoftFloat32FPSub, Marshal.GetFunctionPointerForDelegate<SoftFloat32FPSub>(dlgSoftFloat32FPSub));

            SetDelegateInfo(dlgSoftFloat32_16FPConvert, Marshal.GetFunctionPointerForDelegate<SoftFloat32_16FPConvert>(dlgSoftFloat32_16FPConvert));

            SetDelegateInfo(dlgSoftFloat64FPAdd, Marshal.GetFunctionPointerForDelegate<SoftFloat64FPAdd>(dlgSoftFloat64FPAdd));
            SetDelegateInfo(dlgSoftFloat64FPAddFpscr, Marshal.GetFunctionPointerForDelegate<SoftFloat64FPAddFpscr>(dlgSoftFloat64FPAddFpscr)); // A32 only.
            SetDelegateInfo(dlgSoftFloat64FPCompare, Marshal.GetFunctionPointerForDelegate<SoftFloat64FPCompare>(dlgSoftFloat64FPCompare));
            SetDelegateInfo(dlgSoftFloat64FPCompareEQ, Marshal.GetFunctionPointerForDelegate<SoftFloat64FPCompareEQ>(dlgSoftFloat64FPCompareEQ));
            SetDelegateInfo(dlgSoftFloat64FPCompareEQFpscr, Marshal.GetFunctionPointerForDelegate<SoftFloat64FPCompareEQFpscr>(dlgSoftFloat64FPCompareEQFpscr)); // A32 only.
            SetDelegateInfo(dlgSoftFloat64FPCompareGE, Marshal.GetFunctionPointerForDelegate<SoftFloat64FPCompareGE>(dlgSoftFloat64FPCompareGE));
            SetDelegateInfo(dlgSoftFloat64FPCompareGEFpscr, Marshal.GetFunctionPointerForDelegate<SoftFloat64FPCompareGEFpscr>(dlgSoftFloat64FPCompareGEFpscr)); // A32 only.
            SetDelegateInfo(dlgSoftFloat64FPCompareGT, Marshal.GetFunctionPointerForDelegate<SoftFloat64FPCompareGT>(dlgSoftFloat64FPCompareGT));
            SetDelegateInfo(dlgSoftFloat64FPCompareGTFpscr, Marshal.GetFunctionPointerForDelegate<SoftFloat64FPCompareGTFpscr>(dlgSoftFloat64FPCompareGTFpscr)); // A32 only.
            SetDelegateInfo(dlgSoftFloat64FPCompareLE, Marshal.GetFunctionPointerForDelegate<SoftFloat64FPCompareLE>(dlgSoftFloat64FPCompareLE));
            SetDelegateInfo(dlgSoftFloat64FPCompareLEFpscr, Marshal.GetFunctionPointerForDelegate<SoftFloat64FPCompareLEFpscr>(dlgSoftFloat64FPCompareLEFpscr)); // A32 only.
            SetDelegateInfo(dlgSoftFloat64FPCompareLT, Marshal.GetFunctionPointerForDelegate<SoftFloat64FPCompareLT>(dlgSoftFloat64FPCompareLT));
            SetDelegateInfo(dlgSoftFloat64FPCompareLTFpscr, Marshal.GetFunctionPointerForDelegate<SoftFloat64FPCompareLTFpscr>(dlgSoftFloat64FPCompareLTFpscr)); // A32 only.
            SetDelegateInfo(dlgSoftFloat64FPDiv, Marshal.GetFunctionPointerForDelegate<SoftFloat64FPDiv>(dlgSoftFloat64FPDiv));
            SetDelegateInfo(dlgSoftFloat64FPMax, Marshal.GetFunctionPointerForDelegate<SoftFloat64FPMax>(dlgSoftFloat64FPMax));
            SetDelegateInfo(dlgSoftFloat64FPMaxFpscr, Marshal.GetFunctionPointerForDelegate<SoftFloat64FPMaxFpscr>(dlgSoftFloat64FPMaxFpscr)); // A32 only.
            SetDelegateInfo(dlgSoftFloat64FPMaxNum, Marshal.GetFunctionPointerForDelegate<SoftFloat64FPMaxNum>(dlgSoftFloat64FPMaxNum));
            SetDelegateInfo(dlgSoftFloat64FPMaxNumFpscr, Marshal.GetFunctionPointerForDelegate<SoftFloat64FPMaxNumFpscr>(dlgSoftFloat64FPMaxNumFpscr)); // A32 only.
            SetDelegateInfo(dlgSoftFloat64FPMin, Marshal.GetFunctionPointerForDelegate<SoftFloat64FPMin>(dlgSoftFloat64FPMin));
            SetDelegateInfo(dlgSoftFloat64FPMinFpscr, Marshal.GetFunctionPointerForDelegate<SoftFloat64FPMinFpscr>(dlgSoftFloat64FPMinFpscr)); // A32 only.
            SetDelegateInfo(dlgSoftFloat64FPMinNum, Marshal.GetFunctionPointerForDelegate<SoftFloat64FPMinNum>(dlgSoftFloat64FPMinNum));
            SetDelegateInfo(dlgSoftFloat64FPMinNumFpscr, Marshal.GetFunctionPointerForDelegate<SoftFloat64FPMinNumFpscr>(dlgSoftFloat64FPMinNumFpscr)); // A32 only.
            SetDelegateInfo(dlgSoftFloat64FPMul, Marshal.GetFunctionPointerForDelegate<SoftFloat64FPMul>(dlgSoftFloat64FPMul));
            SetDelegateInfo(dlgSoftFloat64FPMulFpscr, Marshal.GetFunctionPointerForDelegate<SoftFloat64FPMulFpscr>(dlgSoftFloat64FPMulFpscr)); // A32 only.
            SetDelegateInfo(dlgSoftFloat64FPMulAdd, Marshal.GetFunctionPointerForDelegate<SoftFloat64FPMulAdd>(dlgSoftFloat64FPMulAdd));
            SetDelegateInfo(dlgSoftFloat64FPMulAddFpscr, Marshal.GetFunctionPointerForDelegate<SoftFloat64FPMulAddFpscr>(dlgSoftFloat64FPMulAddFpscr)); // A32 only.
            SetDelegateInfo(dlgSoftFloat64FPMulSub, Marshal.GetFunctionPointerForDelegate<SoftFloat64FPMulSub>(dlgSoftFloat64FPMulSub));
            SetDelegateInfo(dlgSoftFloat64FPMulSubFpscr, Marshal.GetFunctionPointerForDelegate<SoftFloat64FPMulSubFpscr>(dlgSoftFloat64FPMulSubFpscr)); // A32 only.
            SetDelegateInfo(dlgSoftFloat64FPMulX, Marshal.GetFunctionPointerForDelegate<SoftFloat64FPMulX>(dlgSoftFloat64FPMulX));
            SetDelegateInfo(dlgSoftFloat64FPNegMulAdd, Marshal.GetFunctionPointerForDelegate<SoftFloat64FPNegMulAdd>(dlgSoftFloat64FPNegMulAdd));
            SetDelegateInfo(dlgSoftFloat64FPNegMulSub, Marshal.GetFunctionPointerForDelegate<SoftFloat64FPNegMulSub>(dlgSoftFloat64FPNegMulSub));
            SetDelegateInfo(dlgSoftFloat64FPRecipEstimate, Marshal.GetFunctionPointerForDelegate<SoftFloat64FPRecipEstimate>(dlgSoftFloat64FPRecipEstimate));
            SetDelegateInfo(dlgSoftFloat64FPRecipEstimateFpscr, Marshal.GetFunctionPointerForDelegate<SoftFloat64FPRecipEstimateFpscr>(dlgSoftFloat64FPRecipEstimateFpscr)); // A32 only.
            SetDelegateInfo(dlgSoftFloat64FPRecipStep, Marshal.GetFunctionPointerForDelegate<SoftFloat64FPRecipStep>(dlgSoftFloat64FPRecipStep)); // A32 only.
            SetDelegateInfo(dlgSoftFloat64FPRecipStepFused, Marshal.GetFunctionPointerForDelegate<SoftFloat64FPRecipStepFused>(dlgSoftFloat64FPRecipStepFused));
            SetDelegateInfo(dlgSoftFloat64FPRecpX, Marshal.GetFunctionPointerForDelegate<SoftFloat64FPRecpX>(dlgSoftFloat64FPRecpX));
            SetDelegateInfo(dlgSoftFloat64FPRSqrtEstimate, Marshal.GetFunctionPointerForDelegate<SoftFloat64FPRSqrtEstimate>(dlgSoftFloat64FPRSqrtEstimate));
            SetDelegateInfo(dlgSoftFloat64FPRSqrtEstimateFpscr, Marshal.GetFunctionPointerForDelegate<SoftFloat64FPRSqrtEstimateFpscr>(dlgSoftFloat64FPRSqrtEstimateFpscr)); // A32 only.
            SetDelegateInfo(dlgSoftFloat64FPRSqrtStep, Marshal.GetFunctionPointerForDelegate<SoftFloat64FPRSqrtStep>(dlgSoftFloat64FPRSqrtStep)); // A32 only.
            SetDelegateInfo(dlgSoftFloat64FPRSqrtStepFused, Marshal.GetFunctionPointerForDelegate<SoftFloat64FPRSqrtStepFused>(dlgSoftFloat64FPRSqrtStepFused));
            SetDelegateInfo(dlgSoftFloat64FPSqrt, Marshal.GetFunctionPointerForDelegate<SoftFloat64FPSqrt>(dlgSoftFloat64FPSqrt));
            SetDelegateInfo(dlgSoftFloat64FPSub, Marshal.GetFunctionPointerForDelegate<SoftFloat64FPSub>(dlgSoftFloat64FPSub));

            SetDelegateInfo(dlgSoftFloat64_16FPConvert, Marshal.GetFunctionPointerForDelegate<SoftFloat64_16FPConvert>(dlgSoftFloat64_16FPConvert));
        }

        private delegate double MathAbs(double value);
        private delegate double MathCeiling(double a);
        private delegate double MathFloor(double d);
        private delegate double MathRound(double value, MidpointRounding mode);
        private delegate double MathTruncate(double d);

        private delegate float MathFAbs(float x);
        private delegate float MathFCeiling(float x);
        private delegate float MathFFloor(float x);
        private delegate float MathFRound(float x, MidpointRounding mode);
        private delegate float MathFTruncate(float x);

        private delegate void NativeInterfaceBreak(ulong address, int imm);
        private delegate bool NativeInterfaceCheckSynchronization();
        private delegate void NativeInterfaceEnqueueForRejit(ulong address);
        private delegate ulong NativeInterfaceGetCntfrqEl0();
        private delegate ulong NativeInterfaceGetCntpctEl0();
        private delegate ulong NativeInterfaceGetCntvctEl0();
        private delegate ulong NativeInterfaceGetCtrEl0();
        private delegate ulong NativeInterfaceGetDczidEl0();
        private delegate ulong NativeInterfaceGetFunctionAddress(ulong address);
        private delegate void NativeInterfaceInvalidateCacheLine(ulong address);
        private delegate byte NativeInterfaceReadByte(ulong address);
        private delegate ushort NativeInterfaceReadUInt16(ulong address);
        private delegate uint NativeInterfaceReadUInt32(ulong address);
        private delegate ulong NativeInterfaceReadUInt64(ulong address);
        private delegate V128 NativeInterfaceReadVector128(ulong address);
        private delegate void NativeInterfaceSignalMemoryTracking(ulong address, ulong size, bool write);
        private delegate void NativeInterfaceSupervisorCall(ulong address, int imm);
        private delegate void NativeInterfaceThrowInvalidMemoryAccess(ulong address);
        private delegate void NativeInterfaceUndefined(ulong address, int opCode);
        private delegate void NativeInterfaceWriteByte(ulong address, byte value);
        private delegate void NativeInterfaceWriteUInt16(ulong address, ushort value);
        private delegate void NativeInterfaceWriteUInt32(ulong address, uint value);
        private delegate void NativeInterfaceWriteUInt64(ulong address, ulong value);
        private delegate void NativeInterfaceWriteVector128(ulong address, V128 value);

        private delegate ulong SoftFallbackCountLeadingSigns(ulong value, int size);
        private delegate ulong SoftFallbackCountLeadingZeros(ulong value, int size);
        private delegate uint SoftFallbackCrc32b(uint crc, byte value);
        private delegate uint SoftFallbackCrc32cb(uint crc, byte value);
        private delegate uint SoftFallbackCrc32ch(uint crc, ushort value);
        private delegate uint SoftFallbackCrc32cw(uint crc, uint value);
        private delegate uint SoftFallbackCrc32cx(uint crc, ulong value);
        private delegate uint SoftFallbackCrc32h(uint crc, ushort value);
        private delegate uint SoftFallbackCrc32w(uint crc, uint value);
        private delegate uint SoftFallbackCrc32x(uint crc, ulong value);
        private delegate V128 SoftFallbackDecrypt(V128 value, V128 roundKey);
        private delegate V128 SoftFallbackEncrypt(V128 value, V128 roundKey);
        private delegate uint SoftFallbackFixedRotate(uint hash_e);
        private delegate V128 SoftFallbackHashChoose(V128 hash_abcd, uint hash_e, V128 wk);
        private delegate V128 SoftFallbackHashLower(V128 hash_abcd, V128 hash_efgh, V128 wk);
        private delegate V128 SoftFallbackHashMajority(V128 hash_abcd, uint hash_e, V128 wk);
        private delegate V128 SoftFallbackHashParity(V128 hash_abcd, uint hash_e, V128 wk);
        private delegate V128 SoftFallbackHashUpper(V128 hash_abcd, V128 hash_efgh, V128 wk);
        private delegate V128 SoftFallbackInverseMixColumns(V128 value);
        private delegate V128 SoftFallbackMixColumns(V128 value);
        private delegate V128 SoftFallbackPolynomialMult64_128(ulong op1, ulong op2);
        private delegate int SoftFallbackSatF32ToS32(float value);
        private delegate long SoftFallbackSatF32ToS64(float value);
        private delegate uint SoftFallbackSatF32ToU32(float value);
        private delegate ulong SoftFallbackSatF32ToU64(float value);
        private delegate int SoftFallbackSatF64ToS32(double value);
        private delegate long SoftFallbackSatF64ToS64(double value);
        private delegate uint SoftFallbackSatF64ToU32(double value);
        private delegate ulong SoftFallbackSatF64ToU64(double value);
        private delegate V128 SoftFallbackSha1SchedulePart1(V128 w0_3, V128 w4_7, V128 w8_11);
        private delegate V128 SoftFallbackSha1SchedulePart2(V128 tw0_3, V128 w12_15);
        private delegate V128 SoftFallbackSha256SchedulePart1(V128 w0_3, V128 w4_7);
        private delegate V128 SoftFallbackSha256SchedulePart2(V128 w0_3, V128 w8_11, V128 w12_15);
        private delegate long SoftFallbackSignedShrImm64(long value, long roundConst, int shift);
        private delegate V128 SoftFallbackTbl1(V128 vector, int bytes, V128 tb0);
        private delegate V128 SoftFallbackTbl2(V128 vector, int bytes, V128 tb0, V128 tb1);
        private delegate V128 SoftFallbackTbl3(V128 vector, int bytes, V128 tb0, V128 tb1, V128 tb2);
        private delegate V128 SoftFallbackTbl4(V128 vector, int bytes, V128 tb0, V128 tb1, V128 tb2, V128 tb3);
        private delegate V128 SoftFallbackTbx1(V128 dest, V128 vector, int bytes, V128 tb0);
        private delegate V128 SoftFallbackTbx2(V128 dest, V128 vector, int bytes, V128 tb0, V128 tb1);
        private delegate V128 SoftFallbackTbx3(V128 dest, V128 vector, int bytes, V128 tb0, V128 tb1, V128 tb2);
        private delegate V128 SoftFallbackTbx4(V128 dest, V128 vector, int bytes, V128 tb0, V128 tb1, V128 tb2, V128 tb3);
        private delegate ulong SoftFallbackUnsignedShrImm64(ulong value, long roundConst, int shift);

        private delegate float SoftFloat16_32FPConvert(ushort valueBits);

        private delegate double SoftFloat16_64FPConvert(ushort valueBits);

        private delegate float SoftFloat32FPAdd(float value1, float value2);
        private delegate float SoftFloat32FPAddFpscr(float value1, float value2, bool standardFpscr);
        private delegate int SoftFloat32FPCompare(float value1, float value2, bool signalNaNs);
        private delegate float SoftFloat32FPCompareEQ(float value1, float value2);
        private delegate float SoftFloat32FPCompareEQFpscr(float value1, float value2, bool standardFpscr);
        private delegate float SoftFloat32FPCompareGE(float value1, float value2);
        private delegate float SoftFloat32FPCompareGEFpscr(float value1, float value2, bool standardFpscr);
        private delegate float SoftFloat32FPCompareGT(float value1, float value2);
        private delegate float SoftFloat32FPCompareGTFpscr(float value1, float value2, bool standardFpscr);
        private delegate float SoftFloat32FPCompareLE(float value1, float value2);
        private delegate float SoftFloat32FPCompareLEFpscr(float value1, float value2, bool standardFpscr);
        private delegate float SoftFloat32FPCompareLT(float value1, float value2);
        private delegate float SoftFloat32FPCompareLTFpscr(float value1, float value2, bool standardFpscr);
        private delegate float SoftFloat32FPDiv(float value1, float value2);
        private delegate float SoftFloat32FPMax(float value1, float value2);
        private delegate float SoftFloat32FPMaxFpscr(float value1, float value2, bool standardFpscr);
        private delegate float SoftFloat32FPMaxNum(float value1, float value2);
        private delegate float SoftFloat32FPMaxNumFpscr(float value1, float value2, bool standardFpscr);
        private delegate float SoftFloat32FPMin(float value1, float value2);
        private delegate float SoftFloat32FPMinFpscr(float value1, float value2, bool standardFpscr);
        private delegate float SoftFloat32FPMinNum(float value1, float value2);
        private delegate float SoftFloat32FPMinNumFpscr(float value1, float value2, bool standardFpscr);
        private delegate float SoftFloat32FPMul(float value1, float value2);
        private delegate float SoftFloat32FPMulFpscr(float value1, float value2, bool standardFpscr);
        private delegate float SoftFloat32FPMulAdd(float valueA, float value1, float value2);
        private delegate float SoftFloat32FPMulAddFpscr(float valueA, float value1, float value2, bool standardFpscr);
        private delegate float SoftFloat32FPMulSub(float valueA, float value1, float value2);
        private delegate float SoftFloat32FPMulSubFpscr(float valueA, float value1, float value2, bool standardFpscr);
        private delegate float SoftFloat32FPMulX(float value1, float value2);
        private delegate float SoftFloat32FPNegMulAdd(float valueA, float value1, float value2);
        private delegate float SoftFloat32FPNegMulSub(float valueA, float value1, float value2);
        private delegate float SoftFloat32FPRecipEstimate(float value);
        private delegate float SoftFloat32FPRecipEstimateFpscr(float value, bool standardFpscr);
        private delegate float SoftFloat32FPRecipStep(float value1, float value2);
        private delegate float SoftFloat32FPRecipStepFused(float value1, float value2);
        private delegate float SoftFloat32FPRecpX(float value);
        private delegate float SoftFloat32FPRSqrtEstimate(float value);
        private delegate float SoftFloat32FPRSqrtEstimateFpscr(float value, bool standardFpscr);
        private delegate float SoftFloat32FPRSqrtStep(float value1, float value2);
        private delegate float SoftFloat32FPRSqrtStepFused(float value1, float value2);
        private delegate float SoftFloat32FPSqrt(float value);
        private delegate float SoftFloat32FPSub(float value1, float value2);

        private delegate ushort SoftFloat32_16FPConvert(float value);

        private delegate double SoftFloat64FPAdd(double value1, double value2);
        private delegate double SoftFloat64FPAddFpscr(double value1, double value2, bool standardFpscr);
        private delegate int SoftFloat64FPCompare(double value1, double value2, bool signalNaNs);
        private delegate double SoftFloat64FPCompareEQ(double value1, double value2);
        private delegate double SoftFloat64FPCompareEQFpscr(double value1, double value2, bool standardFpscr);
        private delegate double SoftFloat64FPCompareGE(double value1, double value2);
        private delegate double SoftFloat64FPCompareGEFpscr(double value1, double value2, bool standardFpscr);
        private delegate double SoftFloat64FPCompareGT(double value1, double value2);
        private delegate double SoftFloat64FPCompareGTFpscr(double value1, double value2, bool standardFpscr);
        private delegate double SoftFloat64FPCompareLE(double value1, double value2);
        private delegate double SoftFloat64FPCompareLEFpscr(double value1, double value2, bool standardFpscr);
        private delegate double SoftFloat64FPCompareLT(double value1, double value2);
        private delegate double SoftFloat64FPCompareLTFpscr(double value1, double value2, bool standardFpscr);
        private delegate double SoftFloat64FPDiv(double value1, double value2);
        private delegate double SoftFloat64FPMax(double value1, double value2);
        private delegate double SoftFloat64FPMaxFpscr(double value1, double value2, bool standardFpscr);
        private delegate double SoftFloat64FPMaxNum(double value1, double value2);
        private delegate double SoftFloat64FPMaxNumFpscr(double value1, double value2, bool standardFpscr);
        private delegate double SoftFloat64FPMin(double value1, double value2);
        private delegate double SoftFloat64FPMinFpscr(double value1, double value2, bool standardFpscr);
        private delegate double SoftFloat64FPMinNum(double value1, double value2);
        private delegate double SoftFloat64FPMinNumFpscr(double value1, double value2, bool standardFpscr);
        private delegate double SoftFloat64FPMul(double value1, double value2);
        private delegate double SoftFloat64FPMulFpscr(double value1, double value2, bool standardFpscr);
        private delegate double SoftFloat64FPMulAdd(double valueA, double value1, double value2);
        private delegate double SoftFloat64FPMulAddFpscr(double valueA, double value1, double value2, bool standardFpscr);
        private delegate double SoftFloat64FPMulSub(double valueA, double value1, double value2);
        private delegate double SoftFloat64FPMulSubFpscr(double valueA, double value1, double value2, bool standardFpscr);
        private delegate double SoftFloat64FPMulX(double value1, double value2);
        private delegate double SoftFloat64FPNegMulAdd(double valueA, double value1, double value2);
        private delegate double SoftFloat64FPNegMulSub(double valueA, double value1, double value2);
        private delegate double SoftFloat64FPRecipEstimate(double value);
        private delegate double SoftFloat64FPRecipEstimateFpscr(double value, bool standardFpscr);
        private delegate double SoftFloat64FPRecipStep(double value1, double value2);
        private delegate double SoftFloat64FPRecipStepFused(double value1, double value2);
        private delegate double SoftFloat64FPRecpX(double value);
        private delegate double SoftFloat64FPRSqrtEstimate(double value);
        private delegate double SoftFloat64FPRSqrtEstimateFpscr(double value, bool standardFpscr);
        private delegate double SoftFloat64FPRSqrtStep(double value1, double value2);
        private delegate double SoftFloat64FPRSqrtStepFused(double value1, double value2);
        private delegate double SoftFloat64FPSqrt(double value);
        private delegate double SoftFloat64FPSub(double value1, double value2);

        private delegate ushort SoftFloat64_16FPConvert(double value);
    }
}
