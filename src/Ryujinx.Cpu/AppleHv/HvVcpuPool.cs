using System;
using System.Runtime.Versioning;
using System.Threading;

namespace Ryujinx.Cpu.AppleHv
{
    [SupportedOSPlatform("macos")]
    class HvVcpuPool
    {
        // Since there's a limit on the number of VCPUs we can create,
        // and we assign one VCPU per guest thread, we need to ensure
        // there are enough VCPUs available for at least the maximum number of active guest threads.
        // To do that, we always destroy and re-create VCPUs that are above a given limit.
        // Those VCPUs are called "ephemeral" here because they are not kept for long.
        //
        // In the future, we might want to consider a smarter approach that only makes
        // VCPUs for threads that are not running frequently "ephemeral", but this is
        // complicated because VCPUs can only be destroyed by the same thread that created them.

        private const int MaxActiveVcpus = 4;

        public static readonly HvVcpuPool Instance = new();

        private int _totalVcpus;
        private readonly int _maxVcpus;

        public HvVcpuPool()
        {
            HvApi.hv_vm_get_max_vcpu_count(out uint maxVcpuCount).ThrowOnError();
            _maxVcpus = (int)maxVcpuCount;
        }

        public HvVcpu Create(HvAddressSpace addressSpace, IHvExecutionContext shadowContext, Action<IHvExecutionContext> swapContext)
        {
            HvVcpu vcpu = CreateNew(addressSpace, shadowContext);
            vcpu.NativeContext.Load(shadowContext);
            swapContext(vcpu.NativeContext);
            return vcpu;
        }

        public void Destroy(HvVcpu vcpu, Action<IHvExecutionContext> swapContext)
        {
            vcpu.ShadowContext.Load(vcpu.NativeContext);
            swapContext(vcpu.ShadowContext);
            DestroyVcpu(vcpu);
        }

        public void Return(HvVcpu vcpu, Action<IHvExecutionContext> swapContext)
        {
            if (vcpu.IsEphemeral)
            {
                Destroy(vcpu, swapContext);
            }
        }

        public HvVcpu Rent(HvAddressSpace addressSpace, IHvExecutionContext shadowContext, HvVcpu vcpu, Action<IHvExecutionContext> swapContext)
        {
            if (vcpu.IsEphemeral)
            {
                return Create(addressSpace, shadowContext, swapContext);
            }
            else
            {
                return vcpu;
            }
        }

        private unsafe HvVcpu CreateNew(HvAddressSpace addressSpace, IHvExecutionContext shadowContext)
        {
            int newCount = IncrementVcpuCount();
            bool isEphemeral = newCount > _maxVcpus - MaxActiveVcpus;

            // Create VCPU.
            HvVcpuExit* exitInfo = null;
            HvApi.hv_vcpu_create(out ulong vcpuHandle, ref exitInfo, nint.Zero).ThrowOnError();

            // Enable FP and SIMD instructions.
            HvApi.hv_vcpu_set_sys_reg(vcpuHandle, HvSysReg.CPACR_EL1, 0b11 << 20).ThrowOnError();

            addressSpace.InitializeMmu(vcpuHandle);

            HvExecutionContextVcpu nativeContext = new(vcpuHandle);

            HvVcpu vcpu = new(vcpuHandle, exitInfo, shadowContext, nativeContext, isEphemeral);

            vcpu.EnableAndUpdateVTimer();

            return vcpu;
        }

        private void DestroyVcpu(HvVcpu vcpu)
        {
            HvApi.hv_vcpu_destroy(vcpu.Handle).ThrowOnError();
            DecrementVcpuCount();
        }

        private int IncrementVcpuCount()
        {
            return Interlocked.Increment(ref _totalVcpus);
        }

        private void DecrementVcpuCount()
        {
            Interlocked.Decrement(ref _totalVcpus);
        }
    }
}
