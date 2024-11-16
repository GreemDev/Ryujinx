using ARMeilleure.Memory;

namespace Ryujinx.Cpu.Jit
{
    public class JitEngine : ICpuEngine
    {
        private readonly ITickSource _tickSource;

        public JitEngine(ITickSource tickSource)
        {
            _tickSource = tickSource;
        }

        /// <inheritdoc/>
        public ICpuContext CreateCpuContext(IMemoryManager memoryManager, bool for64Bit, bool lowPower)
        {
            return new JitCpuContext(_tickSource, memoryManager, for64Bit, lowPower);
        }
    }
}
