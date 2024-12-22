namespace Ryujinx.HLE.HOS.Services.Apm
{
    class PerformanceState
    {
        public PerformanceState() { }

        public bool CpuOverclockEnabled;

        public PerformanceMode PerformanceMode = PerformanceMode.Default;
        public CpuBoostMode CpuBoostMode = CpuBoostMode.Disabled;

        public PerformanceConfiguration DefaultPerformanceConfiguration = PerformanceConfiguration.PerformanceConfiguration7;
        public PerformanceConfiguration BoostPerformanceConfiguration = PerformanceConfiguration.PerformanceConfiguration8;

        public PerformanceConfiguration GetCurrentPerformanceConfiguration(PerformanceMode performanceMode)
        {
            return performanceMode switch
            {
                PerformanceMode.Default => DefaultPerformanceConfiguration,
                PerformanceMode.Boost => BoostPerformanceConfiguration,
                _ => PerformanceConfiguration.PerformanceConfiguration7,
            };
        }
    }
}
