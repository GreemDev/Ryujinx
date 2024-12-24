using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading;
using static Ryujinx.Common.Memory.PartialUnmaps.PartialUnmapHelpers;

namespace Ryujinx.Common.Memory.PartialUnmaps
{
    /// <summary>
    /// State for partial unmaps. Intended to be used on Windows.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public partial struct PartialUnmapState
    {
        public NativeReaderWriterLock PartialUnmapLock;
        public int PartialUnmapsCount;
        public ThreadLocalMap<int> LocalCounts;

        public readonly static int PartialUnmapLockOffset;
        public readonly static int PartialUnmapsCountOffset;
        public readonly static int LocalCountsOffset;

        public readonly static nint GlobalState;

        [SupportedOSPlatform("windows")]
        [LibraryImport("kernel32.dll")]
        private static partial int GetCurrentThreadId();

        [SupportedOSPlatform("windows")]
        [LibraryImport("kernel32.dll", SetLastError = true)]
        private static partial nint OpenThread(int dwDesiredAccess, [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle, uint dwThreadId);

        [SupportedOSPlatform("windows")]
        [LibraryImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool CloseHandle(nint hObject);

        [SupportedOSPlatform("windows")]
        [LibraryImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool GetExitCodeThread(nint hThread, out uint lpExitCode);

        /// <summary>
        /// Creates a global static PartialUnmapState and populates the field offsets.
        /// </summary>
        static unsafe PartialUnmapState()
        {
            PartialUnmapState instance = new();

            PartialUnmapLockOffset = OffsetOf(ref instance, ref instance.PartialUnmapLock);
            PartialUnmapsCountOffset = OffsetOf(ref instance, ref instance.PartialUnmapsCount);
            LocalCountsOffset = OffsetOf(ref instance, ref instance.LocalCounts);

            int size = Unsafe.SizeOf<PartialUnmapState>();
            GlobalState = Marshal.AllocHGlobal(size);
            Unsafe.InitBlockUnaligned((void*)GlobalState, 0, (uint)size);
        }

        /// <summary>
        /// Resets the global state.
        /// </summary>
        public static unsafe void Reset()
        {
            int size = Unsafe.SizeOf<PartialUnmapState>();
            Unsafe.InitBlockUnaligned((void*)GlobalState, 0, (uint)size);
        }

        /// <summary>
        /// Gets a reference to the global state.
        /// </summary>
        /// <returns>A reference to the global state</returns>
        public static unsafe ref PartialUnmapState GetRef()
        {
            return ref Unsafe.AsRef<PartialUnmapState>((void*)GlobalState);
        }

        /// <summary>
        /// Checks if an access violation handler should retry execution due to a fault caused by partial unmap.
        /// </summary>
        /// <remarks>
        /// Due to Windows limitations, <see cref="UnmapView"/> might need to unmap more memory than requested.
        /// The additional memory that was unmapped is later remapped, however this leaves a time gap where the
        /// memory might be accessed but is unmapped. Users of the API must compensate for that by catching the
        /// access violation and retrying if it happened between the unmap and remap operation.
        /// This method can be used to decide if retrying in such cases is necessary or not.
        ///
        /// This version of the function is not used, but serves as a reference for the native
        /// implementation in ARMeilleure.
        /// </remarks>
        /// <returns>True if execution should be retried, false otherwise</returns>
        [SupportedOSPlatform("windows")]
        public bool RetryFromAccessViolation()
        {
            PartialUnmapLock.AcquireReaderLock();

            int threadID = GetCurrentThreadId();
            int threadIndex = LocalCounts.GetOrReserve(threadID, 0);

            if (threadIndex == -1)
            {
                // Out of thread local space... try again later.

                PartialUnmapLock.ReleaseReaderLock();

                return true;
            }

            ref int threadLocalPartialUnmapsCount = ref LocalCounts.GetValue(threadIndex);

            bool retry = threadLocalPartialUnmapsCount != PartialUnmapsCount;
            if (retry)
            {
                threadLocalPartialUnmapsCount = PartialUnmapsCount;
            }

            PartialUnmapLock.ReleaseReaderLock();

            return retry;
        }

        /// <summary>
        /// Iterates and trims threads in the thread -> count map that
        /// are no longer active.
        /// </summary>
        [SupportedOSPlatform("windows")]
        public void TrimThreads()
        {
            const uint ExitCodeStillActive = 259;
            const int ThreadQueryInformation = 0x40;

            Span<int> ids = LocalCounts.ThreadIds.AsSpan();

            for (int i = 0; i < ids.Length; i++)
            {
                int id = ids[i];

                if (id != 0)
                {
                    nint handle = OpenThread(ThreadQueryInformation, false, (uint)id);

                    if (handle == nint.Zero)
                    {
                        Interlocked.CompareExchange(ref ids[i], 0, id);
                    }
                    else
                    {
                        GetExitCodeThread(handle, out uint exitCode);

                        if (exitCode != ExitCodeStillActive)
                        {
                            Interlocked.CompareExchange(ref ids[i], 0, id);
                        }

                        CloseHandle(handle);
                    }
                }
            }
        }
    }
}
