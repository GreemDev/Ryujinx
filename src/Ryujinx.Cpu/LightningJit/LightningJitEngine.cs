using ARMeilleure.Memory;

namespace Ryujinx.Cpu.LightningJit
{
    public class LightningJitEngine : ICpuEngine
    {
        private readonly ITickSource _tickSource;

        public LightningJitEngine(ITickSource tickSource)
        {
            _tickSource = tickSource;
        }

        /// <inheritdoc/>
        public ICpuContext CreateCpuContext(IMemoryManager memoryManager, bool for64Bit)
        {
            return new LightningJitCpuContext(_tickSource, memoryManager, for64Bit);
        }
    }
}
