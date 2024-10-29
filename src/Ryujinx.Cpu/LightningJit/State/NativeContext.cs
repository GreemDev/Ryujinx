using ARMeilleure.Memory;
using ARMeilleure.State;
using System;
using System.Runtime.CompilerServices;

namespace Ryujinx.Cpu.LightningJit.State
{
    class NativeContext : IDisposable
    {
        private unsafe struct NativeCtxStorage
        {
            public fixed ulong X[32];
            public fixed ulong V[64];
            public uint Flags;
            public uint FpFlags;
            public long TpidrEl0;
            public long TpidrroEl0;
            public int Counter;
            public uint HostFpFlags;
            public ulong DispatchAddress;
            public int Running;
        }

        private static NativeCtxStorage _dummyStorage = new();

        private readonly IJitMemoryBlock _block;

        public nint BasePtr => _block.Pointer;

        public NativeContext(IJitMemoryAllocator allocator)
        {
            _block = allocator.Allocate((ulong)Unsafe.SizeOf<NativeCtxStorage>());
        }

        public ulong GetPc()
        {
            // TODO: More precise tracking of PC value.
            return GetStorage().DispatchAddress;
        }

        public unsafe ulong GetX(int index)
        {
            if ((uint)index >= 32)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return GetStorage().X[index];
        }

        public unsafe void SetX(int index, ulong value)
        {
            if ((uint)index >= 32)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            GetStorage().X[index] = value;
        }

        public unsafe V128 GetV(int index)
        {
            if ((uint)index >= 32)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return new V128(GetStorage().V[index * 2 + 0], GetStorage().V[index * 2 + 1]);
        }

        public unsafe void SetV(int index, V128 value)
        {
            if ((uint)index >= 32)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            GetStorage().V[index * 2 + 0] = value.Extract<ulong>(0);
            GetStorage().V[index * 2 + 1] = value.Extract<ulong>(1);
        }

        public unsafe uint GetPstate()
        {
            return GetStorage().Flags;
        }

        public unsafe void SetPstate(uint value)
        {
            GetStorage().Flags = value;
        }

        public unsafe uint GetFPState(uint mask = uint.MaxValue)
        {
            return GetStorage().FpFlags & mask;
        }

        public unsafe void SetFPState(uint value, uint mask = uint.MaxValue)
        {
            GetStorage().FpFlags = (value & mask) | (GetStorage().FpFlags & ~mask);
        }

        public long GetTpidrEl0() => GetStorage().TpidrEl0;
        public void SetTpidrEl0(long value) => GetStorage().TpidrEl0 = value;

        public long GetTpidrroEl0() => GetStorage().TpidrroEl0;
        public void SetTpidrroEl0(long value) => GetStorage().TpidrroEl0 = value;

        public int GetCounter() => GetStorage().Counter;
        public void SetCounter(int value) => GetStorage().Counter = value;

        public bool GetRunning() => GetStorage().Running != 0;
        public void SetRunning(bool value) => GetStorage().Running = value ? 1 : 0;

        public unsafe static int GetXOffset()
        {
            return StorageOffset(ref _dummyStorage, ref _dummyStorage.X[0]);
        }

        public unsafe static int GetVOffset()
        {
            return StorageOffset(ref _dummyStorage, ref _dummyStorage.V[0]);
        }

        public static int GetFlagsOffset()
        {
            return StorageOffset(ref _dummyStorage, ref _dummyStorage.Flags);
        }

        public static int GetFpFlagsOffset()
        {
            return StorageOffset(ref _dummyStorage, ref _dummyStorage.FpFlags);
        }

        public static int GetTpidrEl0Offset()
        {
            return StorageOffset(ref _dummyStorage, ref _dummyStorage.TpidrEl0);
        }

        public static int GetTpidrroEl0Offset()
        {
            return StorageOffset(ref _dummyStorage, ref _dummyStorage.TpidrroEl0);
        }

        public static int GetCounterOffset()
        {
            return StorageOffset(ref _dummyStorage, ref _dummyStorage.Counter);
        }

        public static int GetHostFpFlagsOffset()
        {
            return StorageOffset(ref _dummyStorage, ref _dummyStorage.HostFpFlags);
        }

        public static int GetDispatchAddressOffset()
        {
            return StorageOffset(ref _dummyStorage, ref _dummyStorage.DispatchAddress);
        }

        public static int GetRunningOffset()
        {
            return StorageOffset(ref _dummyStorage, ref _dummyStorage.Running);
        }

        private static int StorageOffset<T>(ref NativeCtxStorage storage, ref T target)
        {
            return (int)Unsafe.ByteOffset(ref Unsafe.As<NativeCtxStorage, T>(ref storage), ref target);
        }

        private unsafe ref NativeCtxStorage GetStorage() => ref Unsafe.AsRef<NativeCtxStorage>((void*)_block.Pointer);

        public void Dispose() => _block.Dispose();
    }
}
