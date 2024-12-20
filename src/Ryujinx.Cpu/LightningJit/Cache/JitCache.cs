using ARMeilleure.Memory;
using Ryujinx.Memory;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading;

namespace Ryujinx.Cpu.LightningJit.Cache
{
    static partial class JitCache
    {
        private static readonly int _pageSize = (int)MemoryBlock.GetPageSize();
        private static readonly int _pageMask = _pageSize - 1;

        private const int CodeAlignment = 4; // Bytes.
        private const int CacheSize = 2047 * 1024 * 1024;

        private static ReservedRegion _jitRegion;
        private static JitCacheInvalidation _jitCacheInvalidator;

        private static CacheMemoryAllocator _cacheAllocator;

        private static readonly List<CacheEntry> _cacheEntries = new();

        private static readonly Lock _lock = new();
        private static bool _initialized;

        [SupportedOSPlatform("windows")]
        [LibraryImport("kernel32.dll", SetLastError = true)]
        public static partial nint FlushInstructionCache(nint hProcess, nint lpAddress, nuint dwSize);

        public static void Initialize(IJitMemoryAllocator allocator)
        {
            if (_initialized)
            {
                return;
            }

            lock (_lock)
            {
                if (_initialized)
                {
                    return;
                }

                _jitRegion = new ReservedRegion(allocator, CacheSize);

                if (!OperatingSystem.IsWindows() && !OperatingSystem.IsMacOS())
                {
                    _jitCacheInvalidator = new JitCacheInvalidation(allocator);
                }

                _cacheAllocator = new CacheMemoryAllocator(CacheSize);

                _initialized = true;
            }
        }

        public unsafe static nint Map(ReadOnlySpan<byte> code)
        {
            lock (_lock)
            {
                Debug.Assert(_initialized);

                int funcOffset = Allocate(code.Length);

                nint funcPtr = _jitRegion.Pointer + funcOffset;

                if (OperatingSystem.IsMacOS() && RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
                {
                    unsafe
                    {
                        fixed (byte* codePtr = code)
                        {
                            JitSupportDarwin.Copy(funcPtr, (nint)codePtr, (ulong)code.Length);
                        }
                    }
                }
                else
                {
                    ReprotectAsWritable(funcOffset, code.Length);
                    code.CopyTo(new Span<byte>((void*)funcPtr, code.Length));
                    ReprotectAsExecutable(funcOffset, code.Length);

                    if (OperatingSystem.IsWindows() && RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
                    {
                        FlushInstructionCache(Process.GetCurrentProcess().Handle, funcPtr, (nuint)code.Length);
                    }
                    else
                    {
                        _jitCacheInvalidator?.Invalidate(funcPtr, (ulong)code.Length);
                    }
                }

                Add(funcOffset, code.Length);

                return funcPtr;
            }
        }

        public static void Unmap(nint pointer)
        {
            lock (_lock)
            {
                Debug.Assert(_initialized);

                int funcOffset = (int)(pointer.ToInt64() - _jitRegion.Pointer.ToInt64());

                if (TryFind(funcOffset, out CacheEntry entry, out int entryIndex) && entry.Offset == funcOffset)
                {
                    _cacheAllocator.Free(funcOffset, AlignCodeSize(entry.Size));
                    _cacheEntries.RemoveAt(entryIndex);
                }
            }
        }

        private static void ReprotectAsWritable(int offset, int size)
        {
            int endOffs = offset + size;

            int regionStart = offset & ~_pageMask;
            int regionEnd = (endOffs + _pageMask) & ~_pageMask;

            _jitRegion.Block.MapAsRwx((ulong)regionStart, (ulong)(regionEnd - regionStart));
        }

        private static void ReprotectAsExecutable(int offset, int size)
        {
            int endOffs = offset + size;

            int regionStart = offset & ~_pageMask;
            int regionEnd = (endOffs + _pageMask) & ~_pageMask;

            _jitRegion.Block.MapAsRx((ulong)regionStart, (ulong)(regionEnd - regionStart));
        }

        private static int Allocate(int codeSize)
        {
            codeSize = AlignCodeSize(codeSize);

            int allocOffset = _cacheAllocator.Allocate(codeSize);

            if (allocOffset < 0)
            {
                throw new OutOfMemoryException("JIT Cache exhausted.");
            }

            _jitRegion.ExpandIfNeeded((ulong)allocOffset + (ulong)codeSize);

            return allocOffset;
        }

        private static int AlignCodeSize(int codeSize)
        {
            return checked(codeSize + (CodeAlignment - 1)) & ~(CodeAlignment - 1);
        }

        private static void Add(int offset, int size)
        {
            CacheEntry entry = new(offset, size);

            int index = _cacheEntries.BinarySearch(entry);

            if (index < 0)
            {
                index = ~index;
            }

            _cacheEntries.Insert(index, entry);
        }

        public static bool TryFind(int offset, out CacheEntry entry, out int entryIndex)
        {
            lock (_lock)
            {
                int index = _cacheEntries.BinarySearch(new CacheEntry(offset, 0));

                if (index < 0)
                {
                    index = ~index - 1;
                }

                if (index >= 0)
                {
                    entry = _cacheEntries[index];
                    entryIndex = index;
                    return true;
                }
            }

            entry = default;
            entryIndex = 0;
            return false;
        }
    }
}
