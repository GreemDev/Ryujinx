using Ryujinx.Common;
using Ryujinx.Cpu.Signal;
using Ryujinx.Memory;
using Ryujinx.Memory.Tracking;
using System;
using System.Runtime.InteropServices;

namespace Ryujinx.Cpu
{
    public class MemoryEhMeilleure : IDisposable
    {
        public delegate ulong TrackingEventDelegate(ulong address, ulong size, bool write);

        private readonly MemoryTracking _tracking;
        private readonly TrackingEventDelegate _trackingEvent;

        private readonly ulong _pageSize;

        private readonly ulong _baseAddress;
        private readonly ulong _mirrorAddress;

        public MemoryEhMeilleure(MemoryBlock addressSpace, MemoryBlock addressSpaceMirror, MemoryTracking tracking, TrackingEventDelegate trackingEvent = null)
        {
            _baseAddress = (ulong)addressSpace.Pointer;

            ulong endAddress = _baseAddress + addressSpace.Size;

            _tracking = tracking;
            _trackingEvent = trackingEvent ?? VirtualMemoryEvent;

            _pageSize = MemoryBlock.GetPageSize();

            bool added = NativeSignalHandler.AddTrackedRegion((nuint)_baseAddress, (nuint)endAddress, Marshal.GetFunctionPointerForDelegate(_trackingEvent));

            if (!added)
            {
                throw new InvalidOperationException("Number of allowed tracked regions exceeded.");
            }

            if (OperatingSystem.IsWindows() && addressSpaceMirror != null)
            {
                // Add a tracking event with no signal handler for the mirror on Windows.
                // The native handler has its own code to check for the partial overlap race when regions are protected by accident,
                // and when there is no signal handler present.

                _mirrorAddress = (ulong)addressSpaceMirror.Pointer;
                ulong endAddressMirror = _mirrorAddress + addressSpace.Size;

                bool addedMirror = NativeSignalHandler.AddTrackedRegion((nuint)_mirrorAddress, (nuint)endAddressMirror, nint.Zero);

                if (!addedMirror)
                {
                    throw new InvalidOperationException("Number of allowed tracked regions exceeded.");
                }
            }
        }

        private ulong VirtualMemoryEvent(ulong address, ulong size, bool write)
        {
            ulong pageSize = _pageSize;
            ulong addressAligned = BitUtils.AlignDown(address, pageSize);
            ulong endAddressAligned = BitUtils.AlignUp(address + size, pageSize);
            ulong sizeAligned = endAddressAligned - addressAligned;

            if (_tracking.VirtualMemoryEvent(addressAligned, sizeAligned, write))
            {
                return _baseAddress + address;
            }

            return 0;
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);

            NativeSignalHandler.RemoveTrackedRegion((nuint)_baseAddress);

            if (_mirrorAddress != 0)
            {
                NativeSignalHandler.RemoveTrackedRegion((nuint)_mirrorAddress);
            }
        }
    }
}
