using ARMeilleure.Memory;
using System.Runtime.Versioning;

namespace Ryujinx.Cpu.AppleHv
{
    [SupportedOSPlatform("macos")]
    public class HvEngine : ICpuEngine
    {
        private readonly ITickSource _tickSource;

        public HvEngine(ITickSource tickSource)
        {
            _tickSource = tickSource;
        }

        /// <inheritdoc/>
        public ICpuContext CreateCpuContext(IMemoryManager memoryManager, bool for64Bit, bool lowPower)
        {
            return new HvCpuContext(_tickSource, memoryManager, for64Bit, lowPower);
        }
    }
}
