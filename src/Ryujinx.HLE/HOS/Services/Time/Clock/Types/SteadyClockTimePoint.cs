using Ryujinx.Common.Utilities;
using System;
using System.Runtime.InteropServices;

namespace Ryujinx.HLE.HOS.Services.Time.Clock
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    struct SteadyClockTimePoint
    {
        public long TimePoint;
        public UInt128 ClockSourceId;

        public readonly ResultCode GetSpanBetween(SteadyClockTimePoint other, out long outSpan)
        {
            outSpan = 0;

            if (ClockSourceId == other.ClockSourceId)
            {
                try
                {
                    outSpan = checked(other.TimePoint - TimePoint);

                    return ResultCode.Success;
                }
                catch (OverflowException)
                {
                    return ResultCode.Overflow;
                }
            }

            return ResultCode.Overflow;
        }

        public static SteadyClockTimePoint GetRandom()
        {
            return new SteadyClockTimePoint
            {
                TimePoint = 0,
                ClockSourceId = Random.Shared.NextUInt128(),
            };
        }
    }
}
