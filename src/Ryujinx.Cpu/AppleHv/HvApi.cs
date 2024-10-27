using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Ryujinx.Cpu.AppleHv
{
    struct HvVcpuExitException
    {
#pragma warning disable CS0649 // Field is never assigned to
        public ulong Syndrome;
        public ulong VirtualAddress;
        public ulong PhysicalAddress;
#pragma warning restore CS0649
    }

    enum HvExitReason : uint
    {
        Canceled,
        Exception,
        VTimerActivated,
        Unknown,
    }

    struct HvVcpuExit
    {
#pragma warning disable CS0649 // Field is never assigned to
        public HvExitReason Reason;
        public HvVcpuExitException Exception;
#pragma warning restore CS0649
    }

    enum HvReg : uint
    {
        X0,
        X1,
        X2,
        X3,
        X4,
        X5,
        X6,
        X7,
        X8,
        X9,
        X10,
        X11,
        X12,
        X13,
        X14,
        X15,
        X16,
        X17,
        X18,
        X19,
        X20,
        X21,
        X22,
        X23,
        X24,
        X25,
        X26,
        X27,
        X28,
        X29,
        FP = X29,
        X30,
        LR = X30,
        PC,
        FPCR,
        FPSR,
        CPSR,
    }

    enum HvSimdFPReg : uint
    {
        Q0,
        Q1,
        Q2,
        Q3,
        Q4,
        Q5,
        Q6,
        Q7,
        Q8,
        Q9,
        Q10,
        Q11,
        Q12,
        Q13,
        Q14,
        Q15,
        Q16,
        Q17,
        Q18,
        Q19,
        Q20,
        Q21,
        Q22,
        Q23,
        Q24,
        Q25,
        Q26,
        Q27,
        Q28,
        Q29,
        Q30,
        Q31,
    }

    enum HvSysReg : ushort
    {
        DBGBVR0_EL1 = 0x8004,
        DBGBCR0_EL1 = 0x8005,
        DBGWVR0_EL1 = 0x8006,
        DBGWCR0_EL1 = 0x8007,
        DBGBVR1_EL1 = 0x800c,
        DBGBCR1_EL1 = 0x800d,
        DBGWVR1_EL1 = 0x800e,
        DBGWCR1_EL1 = 0x800f,
        MDCCINT_EL1 = 0x8010,
        MDSCR_EL1 = 0x8012,
        DBGBVR2_EL1 = 0x8014,
        DBGBCR2_EL1 = 0x8015,
        DBGWVR2_EL1 = 0x8016,
        DBGWCR2_EL1 = 0x8017,
        DBGBVR3_EL1 = 0x801c,
        DBGBCR3_EL1 = 0x801d,
        DBGWVR3_EL1 = 0x801e,
        DBGWCR3_EL1 = 0x801f,
        DBGBVR4_EL1 = 0x8024,
        DBGBCR4_EL1 = 0x8025,
        DBGWVR4_EL1 = 0x8026,
        DBGWCR4_EL1 = 0x8027,
        DBGBVR5_EL1 = 0x802c,
        DBGBCR5_EL1 = 0x802d,
        DBGWVR5_EL1 = 0x802e,
        DBGWCR5_EL1 = 0x802f,
        DBGBVR6_EL1 = 0x8034,
        DBGBCR6_EL1 = 0x8035,
        DBGWVR6_EL1 = 0x8036,
        DBGWCR6_EL1 = 0x8037,
        DBGBVR7_EL1 = 0x803c,
        DBGBCR7_EL1 = 0x803d,
        DBGWVR7_EL1 = 0x803e,
        DBGWCR7_EL1 = 0x803f,
        DBGBVR8_EL1 = 0x8044,
        DBGBCR8_EL1 = 0x8045,
        DBGWVR8_EL1 = 0x8046,
        DBGWCR8_EL1 = 0x8047,
        DBGBVR9_EL1 = 0x804c,
        DBGBCR9_EL1 = 0x804d,
        DBGWVR9_EL1 = 0x804e,
        DBGWCR9_EL1 = 0x804f,
        DBGBVR10_EL1 = 0x8054,
        DBGBCR10_EL1 = 0x8055,
        DBGWVR10_EL1 = 0x8056,
        DBGWCR10_EL1 = 0x8057,
        DBGBVR11_EL1 = 0x805c,
        DBGBCR11_EL1 = 0x805d,
        DBGWVR11_EL1 = 0x805e,
        DBGWCR11_EL1 = 0x805f,
        DBGBVR12_EL1 = 0x8064,
        DBGBCR12_EL1 = 0x8065,
        DBGWVR12_EL1 = 0x8066,
        DBGWCR12_EL1 = 0x8067,
        DBGBVR13_EL1 = 0x806c,
        DBGBCR13_EL1 = 0x806d,
        DBGWVR13_EL1 = 0x806e,
        DBGWCR13_EL1 = 0x806f,
        DBGBVR14_EL1 = 0x8074,
        DBGBCR14_EL1 = 0x8075,
        DBGWVR14_EL1 = 0x8076,
        DBGWCR14_EL1 = 0x8077,
        DBGBVR15_EL1 = 0x807c,
        DBGBCR15_EL1 = 0x807d,
        DBGWVR15_EL1 = 0x807e,
        DBGWCR15_EL1 = 0x807f,
        MIDR_EL1 = 0xc000,
        MPIDR_EL1 = 0xc005,
        ID_AA64PFR0_EL1 = 0xc020,
        ID_AA64PFR1_EL1 = 0xc021,
        ID_AA64DFR0_EL1 = 0xc028,
        ID_AA64DFR1_EL1 = 0xc029,
        ID_AA64ISAR0_EL1 = 0xc030,
        ID_AA64ISAR1_EL1 = 0xc031,
        ID_AA64MMFR0_EL1 = 0xc038,
        ID_AA64MMFR1_EL1 = 0xc039,
        ID_AA64MMFR2_EL1 = 0xc03a,
        SCTLR_EL1 = 0xc080,
        CPACR_EL1 = 0xc082,
        TTBR0_EL1 = 0xc100,
        TTBR1_EL1 = 0xc101,
        TCR_EL1 = 0xc102,
        APIAKEYLO_EL1 = 0xc108,
        APIAKEYHI_EL1 = 0xc109,
        APIBKEYLO_EL1 = 0xc10a,
        APIBKEYHI_EL1 = 0xc10b,
        APDAKEYLO_EL1 = 0xc110,
        APDAKEYHI_EL1 = 0xc111,
        APDBKEYLO_EL1 = 0xc112,
        APDBKEYHI_EL1 = 0xc113,
        APGAKEYLO_EL1 = 0xc118,
        APGAKEYHI_EL1 = 0xc119,
        SPSR_EL1 = 0xc200,
        ELR_EL1 = 0xc201,
        SP_EL0 = 0xc208,
        AFSR0_EL1 = 0xc288,
        AFSR1_EL1 = 0xc289,
        ESR_EL1 = 0xc290,
        FAR_EL1 = 0xc300,
        PAR_EL1 = 0xc3a0,
        MAIR_EL1 = 0xc510,
        AMAIR_EL1 = 0xc518,
        VBAR_EL1 = 0xc600,
        CONTEXTIDR_EL1 = 0xc681,
        TPIDR_EL1 = 0xc684,
        CNTKCTL_EL1 = 0xc708,
        CSSELR_EL1 = 0xd000,
        TPIDR_EL0 = 0xde82,
        TPIDRRO_EL0 = 0xde83,
        CNTV_CTL_EL0 = 0xdf19,
        CNTV_CVAL_EL0 = 0xdf1a,
        SP_EL1 = 0xe208,
    }

    enum HvMemoryFlags : ulong
    {
        Read = 1UL << 0,
        Write = 1UL << 1,
        Exec = 1UL << 2,
    }

    enum HvResult : uint
    {
        Success = 0,
        Error = 0xfae94001,
        Busy = 0xfae94002,
        BadArgument = 0xfae94003,
        NoResources = 0xfae94005,
        NoDevice = 0xfae94006,
        Denied = 0xfae94007,
        Unsupported = 0xfae9400f,
    }

    enum HvInterruptType : uint
    {
        IRQ,
        FIQ,
    }

    struct HvSimdFPUchar16
    {
        public ulong Low;
        public ulong High;
    }

    static class HvResultExtensions
    {
        public static void ThrowOnError(this HvResult result)
        {
            if (result != HvResult.Success)
            {
                throw new Exception($"Unexpected result \"{result}\".");
            }
        }
    }

    [SupportedOSPlatform("macos")]
    static partial class HvApi
    {
        public const string LibraryName = "/System/Library/Frameworks/Hypervisor.framework/Hypervisor";

        [LibraryImport(LibraryName, SetLastError = true)]
        public static partial HvResult hv_vm_get_max_vcpu_count(out uint max_vcpu_count);

        [LibraryImport(LibraryName, SetLastError = true)]
        public static partial HvResult hv_vm_create(nint config);

        [LibraryImport(LibraryName, SetLastError = true)]
        public static partial HvResult hv_vm_destroy();

        [LibraryImport(LibraryName, SetLastError = true)]
        public static partial HvResult hv_vm_map(ulong addr, ulong ipa, ulong size, HvMemoryFlags flags);

        [LibraryImport(LibraryName, SetLastError = true)]
        public static partial HvResult hv_vm_unmap(ulong ipa, ulong size);

        [LibraryImport(LibraryName, SetLastError = true)]
        public static partial HvResult hv_vm_protect(ulong ipa, ulong size, HvMemoryFlags flags);

        [LibraryImport(LibraryName, SetLastError = true)]
        public unsafe static partial HvResult hv_vcpu_create(out ulong vcpu, ref HvVcpuExit* exit, nint config);

        [LibraryImport(LibraryName, SetLastError = true)]
        public unsafe static partial HvResult hv_vcpu_destroy(ulong vcpu);

        [LibraryImport(LibraryName, SetLastError = true)]
        public static partial HvResult hv_vcpu_run(ulong vcpu);

        [LibraryImport(LibraryName, SetLastError = true)]
        public static partial HvResult hv_vcpus_exit(ref ulong vcpus, uint vcpu_count);

        [LibraryImport(LibraryName, SetLastError = true)]
        public static partial HvResult hv_vcpu_set_vtimer_mask(ulong vcpu, [MarshalAs(UnmanagedType.Bool)] bool vtimer_is_masked);

        [LibraryImport(LibraryName, SetLastError = true)]
        public static partial HvResult hv_vcpu_get_reg(ulong vcpu, HvReg reg, out ulong value);

        [LibraryImport(LibraryName, SetLastError = true)]
        public static partial HvResult hv_vcpu_set_reg(ulong vcpu, HvReg reg, ulong value);

        [LibraryImport(LibraryName, SetLastError = true)]
        public static partial HvResult hv_vcpu_get_simd_fp_reg(ulong vcpu, HvSimdFPReg reg, out HvSimdFPUchar16 value);

        [LibraryImport(LibraryName, SetLastError = true)]
        public static partial HvResult hv_vcpu_set_simd_fp_reg(ulong vcpu, HvSimdFPReg reg, HvSimdFPUchar16 value); // DO NOT USE DIRECTLY!

        [LibraryImport(LibraryName, SetLastError = true)]
        public static partial HvResult hv_vcpu_get_sys_reg(ulong vcpu, HvSysReg reg, out ulong value);

        [LibraryImport(LibraryName, SetLastError = true)]
        public static partial HvResult hv_vcpu_set_sys_reg(ulong vcpu, HvSysReg reg, ulong value);

        [LibraryImport(LibraryName, SetLastError = true)]
        public static partial HvResult hv_vcpu_get_pending_interrupt(ulong vcpu, HvInterruptType type, [MarshalAs(UnmanagedType.Bool)] out bool pending);

        [LibraryImport(LibraryName, SetLastError = true)]
        public static partial HvResult hv_vcpu_set_pending_interrupt(ulong vcpu, HvInterruptType type, [MarshalAs(UnmanagedType.Bool)] bool pending);
    }
}
