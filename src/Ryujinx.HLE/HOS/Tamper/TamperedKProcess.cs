using Ryujinx.Common.Logging;
using Ryujinx.HLE.Exceptions;
using Ryujinx.HLE.HOS.Kernel.Process;
using System.Runtime.CompilerServices;

namespace Ryujinx.HLE.HOS.Tamper
{
    class TamperedKProcess : ITamperedProcess
    {
        private readonly KProcess _process;

        public ProcessState State => _process.State;

        public bool TamperedCodeMemory { get; set; }

        public TamperedKProcess(KProcess process)
        {
            _process = process;
        }

        private void AssertMemoryRegion<T>(ulong va, bool isWrite) where T : unmanaged
        {
            ulong size = (ulong)Unsafe.SizeOf<T>();

            // TODO (Caian): This double check is workaround because CpuMemory.IsRangeMapped reports
            // some addresses as mapped even though they are not, i. e. 4 bytes from 0xffffffffffffff70.
            if (!_process.CpuMemory.IsMapped(va) || !_process.CpuMemory.IsRangeMapped(va, size))
            {
                throw new TamperExecutionException($"Unmapped memory access of {size} bytes at 0x{va:X16}");
            }

            if (!isWrite)
            {
                return;
            }

            // TODO (Caian): The JIT does not support invalidating a code region so writing to code memory may not work
            // as intended, so taint the operation to issue a warning later.
            if (isWrite && (va >= _process.MemoryManager.CodeRegionStart) && (va + size <= _process.MemoryManager.CodeRegionEnd))
            {
                TamperedCodeMemory = true;
            }
        }

        public T ReadMemory<T>(ulong va) where T : unmanaged
        {
            AssertMemoryRegion<T>(va, false);

            return _process.CpuMemory.Read<T>(va);
        }

        public void WriteMemory<T>(ulong va, T value) where T : unmanaged
        {
            AssertMemoryRegion<T>(va, true);
            _process.CpuMemory.Write(va, value);
        }

        public void PauseProcess()
        {
            Logger.Warning?.Print(LogClass.TamperMachine, "Process pausing is not supported!");
        }

        public void ResumeProcess()
        {
            Logger.Warning?.Print(LogClass.TamperMachine, "Process resuming is not supported!");
        }
    }
}
