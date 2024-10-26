using Ryujinx.Memory;
using System;
using System.Diagnostics;

namespace Ryujinx.Cpu.Jit.HostTracked
{
    class AddressSpacePartitionMultiAllocation : IDisposable
    {
        private readonly AddressSpacePartitionAllocation _baseMemory;
        private AddressSpacePartitionAllocation _baseMemoryRo;
        private AddressSpacePartitionAllocation _baseMemoryNone;

        public AddressSpacePartitionMultiAllocation(AddressSpacePartitionAllocation baseMemory)
        {
            _baseMemory = baseMemory;
        }

        public void MapView(MemoryBlock srcBlock, ulong srcOffset, ulong dstOffset, ulong size)
        {
            _baseMemory.MapView(srcBlock, srcOffset, dstOffset, size);

            if (_baseMemoryRo.IsValid)
            {
                _baseMemoryRo.MapView(srcBlock, srcOffset, dstOffset, size);
                _baseMemoryRo.Reprotect(dstOffset, size, MemoryPermission.Read, false);
            }
        }

        public void LateMapView(MemoryBlock srcBlock, ulong srcOffset, ulong dstOffset, ulong size)
        {
            _baseMemoryRo.MapView(srcBlock, srcOffset, dstOffset, size);
            _baseMemoryRo.Reprotect(dstOffset, size, MemoryPermission.Read, false);
        }

        public void UnmapView(MemoryBlock srcBlock, ulong offset, ulong size)
        {
            _baseMemory.UnmapView(srcBlock, offset, size);

            if (_baseMemoryRo.IsValid)
            {
                _baseMemoryRo.UnmapView(srcBlock, offset, size);
            }
        }

        public void Reprotect(ulong offset, ulong size, MemoryPermission permission, bool throwOnFail)
        {
            _baseMemory.Reprotect(offset, size, permission, throwOnFail);
        }

        public nint GetPointer(ulong offset, ulong size)
        {
            return _baseMemory.GetPointer(offset, size);
        }

        public bool LazyInitMirrorForProtection(AddressSpacePartitioned addressSpace, ulong blockAddress, ulong blockSize, MemoryPermission permission)
        {
            if (permission == MemoryPermission.None && !_baseMemoryNone.IsValid)
            {
                _baseMemoryNone = addressSpace.CreateAsPartitionAllocation(blockAddress, blockSize);
            }
            else if (permission == MemoryPermission.Read && !_baseMemoryRo.IsValid)
            {
                _baseMemoryRo = addressSpace.CreateAsPartitionAllocation(blockAddress, blockSize);

                return true;
            }

            return false;
        }

        public nint GetPointerForProtection(ulong offset, ulong size, MemoryPermission permission)
        {
            AddressSpacePartitionAllocation allocation = permission switch
            {
                MemoryPermission.ReadAndWrite => _baseMemory,
                MemoryPermission.Read => _baseMemoryRo,
                MemoryPermission.None => _baseMemoryNone,
                _ => throw new ArgumentException($"Invalid protection \"{permission}\"."),
            };

            Debug.Assert(allocation.IsValid);

            return allocation.GetPointer(offset, size);
        }

        public void Dispose()
        {
            _baseMemory.Dispose();

            if (_baseMemoryRo.IsValid)
            {
                _baseMemoryRo.Dispose();
            }

            if (_baseMemoryNone.IsValid)
            {
                _baseMemoryNone.Dispose();
            }
        }
    }
}
