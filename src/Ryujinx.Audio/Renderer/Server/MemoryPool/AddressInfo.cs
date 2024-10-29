using System;
using System.Runtime.InteropServices;
using CpuAddress = System.UInt64;
using DspAddress = System.UInt64;

namespace Ryujinx.Audio.Renderer.Server.MemoryPool
{
    /// <summary>
    /// Represents the information of a region shared between the CPU and DSP. 
    /// </summary>
    public struct AddressInfo
    {
        /// <summary>
        /// The target CPU address of the region.
        /// </summary>
        public CpuAddress CpuAddress;

        /// <summary>
        /// The size of the region.
        /// </summary>
        public ulong Size;

        private unsafe MemoryPoolState* _memoryPools;

        /// <summary>
        /// The forced DSP address of the region.
        /// </summary>
        public DspAddress ForceMappedDspAddress;

        private readonly unsafe ref MemoryPoolState MemoryPoolState => ref *_memoryPools;

        public readonly unsafe bool HasMemoryPoolState => (nint)_memoryPools != nint.Zero;

        /// <summary>
        /// Create an new empty <see cref="AddressInfo"/>.
        /// </summary>
        /// <returns>A new empty <see cref="AddressInfo"/>.</returns>
        public static AddressInfo Create()
        {
            return Create(0, 0);
        }

        /// <summary>
        /// Create a new <see cref="AddressInfo"/>.
        /// </summary>
        /// <param name="cpuAddress">The target <see cref="CpuAddress"/> of the region.</param>
        /// <param name="size">The target size of the region.</param>
        /// <returns>A new <see cref="AddressInfo"/>.</returns>
        public static AddressInfo Create(CpuAddress cpuAddress, ulong size)
        {
            unsafe
            {
                return new AddressInfo
                {
                    CpuAddress = cpuAddress,
                    _memoryPools = MemoryPoolState.Null,
                    Size = size,
                    ForceMappedDspAddress = 0,
                };
            }
        }

        /// <summary>
        /// Setup the CPU address and size of the <see cref="AddressInfo"/>.
        /// </summary>
        /// <param name="cpuAddress">The target <see cref="CpuAddress"/> of the region.</param>
        /// <param name="size">The size of the region.</param>
        public void Setup(CpuAddress cpuAddress, ulong size)
        {
            CpuAddress = cpuAddress;
            Size = size;
            ForceMappedDspAddress = 0;

            unsafe
            {
                _memoryPools = MemoryPoolState.Null;
            }
        }

        /// <summary>
        /// Set the <see cref="MemoryPoolState"/> associated.
        /// </summary>
        /// <param name="memoryPoolState">The <see cref="MemoryPoolState"/> associated.</param>
        public void SetupMemoryPool(Span<MemoryPoolState> memoryPoolState)
        {
            unsafe
            {
                fixed (MemoryPoolState* ptr = &MemoryMarshal.GetReference(memoryPoolState))
                {
                    SetupMemoryPool(ptr);
                }
            }
        }

        /// <summary>
        /// Set the <see cref="MemoryPoolState"/> associated.
        /// </summary>
        /// <param name="memoryPoolState">The <see cref="MemoryPoolState"/> associated.</param>
        public unsafe void SetupMemoryPool(MemoryPoolState* memoryPoolState)
        {
            _memoryPools = memoryPoolState;
        }

        /// <summary>
        /// Check if the <see cref="MemoryPoolState"/> is mapped.
        /// </summary>
        /// <returns>Returns true if the <see cref="MemoryPoolState"/> is mapped.</returns>
        public readonly bool HasMappedMemoryPool()
        {
            return HasMemoryPoolState && MemoryPoolState.IsMapped();
        }

        /// <summary>
        /// Get the DSP address associated to the <see cref="AddressInfo"/>.
        /// </summary>
        /// <param name="markUsed">If true, mark the <see cref="MemoryPoolState"/> as used.</param>
        /// <returns>Returns the DSP address associated to the <see cref="AddressInfo"/>.</returns>
        public readonly DspAddress GetReference(bool markUsed)
        {
            if (!HasMappedMemoryPool())
            {
                return ForceMappedDspAddress;
            }

            if (markUsed)
            {
                MemoryPoolState.IsUsed = true;
            }

            return MemoryPoolState.Translate(CpuAddress, Size);
        }
    }
}
