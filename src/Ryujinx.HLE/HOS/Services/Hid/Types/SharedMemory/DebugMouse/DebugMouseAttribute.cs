using System;

namespace Ryujinx.HLE.HOS.Services.Hid.Types.SharedMemory.DebugMouse
{
    [Flags]
    enum DebugMouseAttribute : uint
    {
        None = 0,
        Transferable = 1 << 0,
        IsConnected = 1 << 1,
    }
}
