using Ryujinx.Common.SystemInterop;
using System;
using System.Runtime.Versioning;
using System.Threading;

namespace Ryujinx.Common.PreciseSleep
{
    /// <summary>
    /// A precise sleep event that uses Windows specific methods to increase clock resolution beyond 1ms,
    /// use the clock's phase for more precise waits, and potentially align timepoints with it.
    /// </summary>
    [SupportedOSPlatform("windows")]
    internal class WindowsSleepEvent : IPreciseSleepEvent
    {
        /// <summary>
        /// The clock can drift a bit, so add this to encourage the clock to still wait if the next tick is forecasted slightly before it.
        /// </summary>
        private const long ErrorBias = 50000;

        /// <summary>
        /// Allowed to be 0.05ms away from the clock granularity to reduce precision.
        /// </summary>
        private const long ClockAlignedBias = 50000;

        /// <summary>
        /// The fraction of clock granularity above the timepoint that will align it down to the lower timepoint.
        /// Currently set to the lower 1/4, so for 0.5ms granularity: 0.1ms would be rounded down, 0.2 ms would be rounded up.
        /// </summary>
        private const long ReverseTimePointFraction = 4;

        private readonly AutoResetEvent _waitEvent = new(false);
        private readonly WindowsGranularTimer _timer = WindowsGranularTimer.Instance;

        /// <summary>
        /// Set to true to disable timepoint realignment.
        /// </summary>
        public bool Precise { get; set; }

        public long AdjustTimePoint(long timePoint, long timeoutNs)
        {
            if (Precise || timePoint == long.MaxValue)
            {
                return timePoint;
            }

            // Does the timeout align with the host clock?

            long granularity = _timer.GranularityNs;
            long misalignment = timeoutNs % granularity;

            if ((misalignment < ClockAlignedBias || misalignment > granularity - ClockAlignedBias) && timeoutNs > ClockAlignedBias)
            {
                // Inaccurate sleep for 0.5ms increments, typically.

                (long low, long high) = _timer.ReturnNearestTicks(timePoint);

                if (timePoint - low < _timer.GranularityTicks / ReverseTimePointFraction)
                {
                    timePoint = low;
                }
                else
                {
                    timePoint = high;
                }
            }

            return timePoint;
        }

        public bool SleepUntil(long timePoint)
        {
            return _timer.SleepUntilTimePoint(_waitEvent, timePoint + (ErrorBias * PerformanceCounter.TicksPerMillisecond) / 1_000_000);
        }

        public void Sleep()
        {
            _waitEvent.WaitOne();
        }

        public void Signal()
        {
            _waitEvent.Set();
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);

            _waitEvent.Dispose();
        }
    }
}
