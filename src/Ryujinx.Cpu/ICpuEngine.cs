using ARMeilleure.Memory;

namespace Ryujinx.Cpu
{
    /// <summary>
    /// CPU execution engine interface.
    /// </summary>
    public interface ICpuEngine
    {
        /// <summary>
        /// Creates a new CPU context that can be used to run code for multiple threads sharing an address space.
        /// </summary>
        /// <param name="memoryManager">Memory manager for the address space of the context</param>
        /// <param name="for64Bit">Indicates if the context will be used to run 64-bit or 32-bit Arm code</param>
        /// <returns>CPU context</returns>
        ICpuContext CreateCpuContext(IMemoryManager memoryManager, bool for64Bit);
    }
}
