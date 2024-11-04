using Ryujinx.HLE.HOS.Services.Hid.Types.SharedMemory.Common;
using System.Runtime.InteropServices;

namespace Ryujinx.HLE.HOS.Services.Hid.Types.SharedMemory.DebugMouse
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    struct DebugMouseState : ISampledDataStruct
    {
        public ulong SamplingNumber;
        public int X;
        public int Y;
        public int DeltaX;
        public int DeltaY;
        public int WheelDeltaX;
        public int WheelDeltaY;
        public DebugMouseButton Buttons;
        public DebugMouseAttribute Attributes;
    }
}
