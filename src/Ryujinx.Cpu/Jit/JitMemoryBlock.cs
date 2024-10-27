using ARMeilleure.Memory;
using Ryujinx.Memory;
using System;

namespace Ryujinx.Cpu.Jit
{
    public class JitMemoryBlock : IJitMemoryBlock
    {
        private readonly MemoryBlock _impl;

        public nint Pointer => _impl.Pointer;

        public JitMemoryBlock(ulong size, MemoryAllocationFlags flags)
        {
            _impl = new MemoryBlock(size, flags);
        }

        public void Commit(ulong offset, ulong size) => _impl.Commit(offset, size);
        public void MapAsRw(ulong offset, ulong size) => _impl.Reprotect(offset, size, MemoryPermission.ReadAndWrite);
        public void MapAsRx(ulong offset, ulong size) => _impl.Reprotect(offset, size, MemoryPermission.ReadAndExecute);
        public void MapAsRwx(ulong offset, ulong size) => _impl.Reprotect(offset, size, MemoryPermission.ReadWriteExecute);

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            _impl.Dispose();
        }
    }
}
