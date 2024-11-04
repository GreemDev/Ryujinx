using System;

namespace Ryujinx.HLE.HOS.Services.Hid.Types.SharedMemory.DebugMouse
{
    [Flags]
    enum DebugMouseButton : uint
    {
        None = 0,
        Left = 1 << 0,
        Right = 1 << 1,
        Middle = 1 << 2,
        Forward = 1 << 3,
        Back = 1 << 4,
    }
}
