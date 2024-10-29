using ARMeilleure.Common;
using ARMeilleure.Memory;
using Ryujinx.Cpu.LightningJit.Arm64.Target.Arm64;
using System;
using System.Runtime.InteropServices;

namespace Ryujinx.Cpu.LightningJit.Arm64
{
    static class A64Compiler
    {
        public static CompiledFunction Compile(
            CpuPreset cpuPreset,
            IMemoryManager memoryManager,
            ulong address,
            AddressTable<ulong> funcTable,
            nint dispatchStubPtr,
            Architecture targetArch)
        {
            if (targetArch == Architecture.Arm64)
            {
                return Compiler.Compile(cpuPreset, memoryManager, address, funcTable, dispatchStubPtr);
            }
            else
            {
                throw new PlatformNotSupportedException();
            }
        }
    }
}
