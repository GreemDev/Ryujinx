using System;

namespace ARMeilleure.Memory
{
    public interface IJitMemoryBlock : IDisposable
    {
        nint Pointer { get; }

        void Commit(ulong offset, ulong size);

        void MapAsRw(ulong offset, ulong size);
        void MapAsRx(ulong offset, ulong size);
        void MapAsRwx(ulong offset, ulong size);
    }
}
