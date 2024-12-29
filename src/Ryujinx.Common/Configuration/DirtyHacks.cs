using System;

namespace Ryujinx.Common.Configuration
{
    [Flags]
    public enum DirtyHacks
    {
        None = 0,
        Xc2MenuSoftlockFix = 1 << 10
    }
}
