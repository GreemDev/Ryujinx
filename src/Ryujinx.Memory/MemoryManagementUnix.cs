using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using static Ryujinx.Memory.MemoryManagerUnixHelper;

namespace Ryujinx.Memory
{
    [SupportedOSPlatform("linux")]
    [SupportedOSPlatform("macos")]
    static class MemoryManagementUnix
    {
        private static readonly ConcurrentDictionary<nint, ulong> _allocations = new();

        public static nint Allocate(ulong size, bool forJit)
        {
            return AllocateInternal(size, MmapProts.PROT_READ | MmapProts.PROT_WRITE, forJit);
        }

        public static nint Reserve(ulong size, bool forJit)
        {
            return AllocateInternal(size, MmapProts.PROT_NONE, forJit);
        }

        private static nint AllocateInternal(ulong size, MmapProts prot, bool forJit, bool shared = false)
        {
            MmapFlags flags = MmapFlags.MAP_ANONYMOUS;

            if (shared)
            {
                flags |= MmapFlags.MAP_SHARED | MmapFlags.MAP_UNLOCKED;
            }
            else
            {
                flags |= MmapFlags.MAP_PRIVATE;
            }

            if (prot == MmapProts.PROT_NONE)
            {
                flags |= MmapFlags.MAP_NORESERVE;
            }

            if (OperatingSystem.IsMacOSVersionAtLeast(10, 14) && forJit)
            {
                flags |= MmapFlags.MAP_JIT_DARWIN;

                if (prot == (MmapProts.PROT_READ | MmapProts.PROT_WRITE))
                {
                    prot |= MmapProts.PROT_EXEC;
                }
            }

            nint ptr = Mmap(nint.Zero, size, prot, flags, -1, 0);

            if (ptr == MAP_FAILED)
            {
                throw new SystemException(Marshal.GetLastPInvokeErrorMessage());
            }

            if (!_allocations.TryAdd(ptr, size))
            {
                // This should be impossible, kernel shouldn't return an already mapped address.
                throw new InvalidOperationException();
            }

            return ptr;
        }

        public static void Commit(nint address, ulong size, bool forJit)
        {
            MmapProts prot = MmapProts.PROT_READ | MmapProts.PROT_WRITE;

            if (OperatingSystem.IsMacOSVersionAtLeast(10, 14) && forJit)
            {
                prot |= MmapProts.PROT_EXEC;
            }

            if (mprotect(address, size, prot) != 0)
            {
                throw new SystemException(Marshal.GetLastPInvokeErrorMessage());
            }
        }

        public static void Decommit(nint address, ulong size)
        {
            // Must be writable for madvise to work properly.
            if (mprotect(address, size, MmapProts.PROT_READ | MmapProts.PROT_WRITE) != 0)
            {
                throw new SystemException(Marshal.GetLastPInvokeErrorMessage());
            }

            if (madvise(address, size, MADV_REMOVE) != 0)
            {
                throw new SystemException(Marshal.GetLastPInvokeErrorMessage());
            }

            if (mprotect(address, size, MmapProts.PROT_NONE) != 0)
            {
                throw new SystemException(Marshal.GetLastPInvokeErrorMessage());
            }
        }

        public static bool Reprotect(nint address, ulong size, MemoryPermission permission)
        {
            return mprotect(address, size, GetProtection(permission)) == 0;
        }

        private static MmapProts GetProtection(MemoryPermission permission)
        {
            return permission switch
            {
                MemoryPermission.None => MmapProts.PROT_NONE,
                MemoryPermission.Read => MmapProts.PROT_READ,
                MemoryPermission.ReadAndWrite => MmapProts.PROT_READ | MmapProts.PROT_WRITE,
                MemoryPermission.ReadAndExecute => MmapProts.PROT_READ | MmapProts.PROT_EXEC,
                MemoryPermission.ReadWriteExecute => MmapProts.PROT_READ | MmapProts.PROT_WRITE | MmapProts.PROT_EXEC,
                MemoryPermission.Execute => MmapProts.PROT_EXEC,
                _ => throw new MemoryProtectionException(permission),
            };
        }

        public static bool Free(nint address)
        {
            if (_allocations.TryRemove(address, out ulong size))
            {
                return munmap(address, size) == 0;
            }

            return false;
        }

        public static bool Unmap(nint address, ulong size)
        {
            return munmap(address, size) == 0;
        }

        public unsafe static nint CreateSharedMemory(ulong size, bool reserve)
        {
            int fd;

            if (OperatingSystem.IsMacOS())
            {
                byte[] memName = "Ryujinx-XXXXXX"u8.ToArray();

                fixed (byte* pMemName = memName)
                {
                    fd = shm_open((nint)pMemName, 0x2 | 0x200 | 0x800 | 0x400, 384); // O_RDWR | O_CREAT | O_EXCL | O_TRUNC, 0600
                    if (fd == -1)
                    {
                        throw new SystemException(Marshal.GetLastPInvokeErrorMessage());
                    }

                    if (shm_unlink((nint)pMemName) != 0)
                    {
                        throw new SystemException(Marshal.GetLastPInvokeErrorMessage());
                    }
                }
            }
            else
            {
                byte[] fileName = "/dev/shm/Ryujinx-XXXXXX"u8.ToArray();

                fixed (byte* pFileName = fileName)
                {
                    fd = mkstemp((nint)pFileName);
                    if (fd == -1)
                    {
                        throw new SystemException(Marshal.GetLastPInvokeErrorMessage());
                    }

                    if (unlink((nint)pFileName) != 0)
                    {
                        throw new SystemException(Marshal.GetLastPInvokeErrorMessage());
                    }
                }
            }

            if (ftruncate(fd, (nint)size) != 0)
            {
                throw new SystemException(Marshal.GetLastPInvokeErrorMessage());
            }

            return fd;
        }

        public static void DestroySharedMemory(nint handle)
        {
            close(handle.ToInt32());
        }

        public static nint MapSharedMemory(nint handle, ulong size)
        {
            return Mmap(nint.Zero, size, MmapProts.PROT_READ | MmapProts.PROT_WRITE, MmapFlags.MAP_SHARED, handle.ToInt32(), 0);
        }

        public static void UnmapSharedMemory(nint address, ulong size)
        {
            munmap(address, size);
        }

        public static void MapView(nint sharedMemory, ulong srcOffset, nint location, ulong size)
        {
            Mmap(location, size, MmapProts.PROT_READ | MmapProts.PROT_WRITE, MmapFlags.MAP_FIXED | MmapFlags.MAP_SHARED, sharedMemory.ToInt32(), (long)srcOffset);
        }

        public static void UnmapView(nint location, ulong size)
        {
            Mmap(location, size, MmapProts.PROT_NONE, MmapFlags.MAP_FIXED | MmapFlags.MAP_PRIVATE | MmapFlags.MAP_ANONYMOUS | MmapFlags.MAP_NORESERVE, -1, 0);
        }
    }
}
