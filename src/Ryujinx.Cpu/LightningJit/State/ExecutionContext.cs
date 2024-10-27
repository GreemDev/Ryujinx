using ARMeilleure.Memory;
using ARMeilleure.State;
using System;

namespace Ryujinx.Cpu.LightningJit.State
{
    public class ExecutionContext : IExecutionContext
    {
        private const int MinCountForCheck = 4000;

        private readonly NativeContext _nativeContext;

        internal nint NativeContextPtr => _nativeContext.BasePtr;

        private bool _interrupted;
        private readonly ICounter _counter;

        public ulong Pc => _nativeContext.GetPc();

        public ulong CntfrqEl0 => _counter.Frequency;
        public ulong CntpctEl0 => _counter.Counter;

        public long TpidrEl0
        {
            get => _nativeContext.GetTpidrEl0();
            set => _nativeContext.SetTpidrEl0(value);
        }

        public long TpidrroEl0
        {
            get => _nativeContext.GetTpidrroEl0();
            set => _nativeContext.SetTpidrroEl0(value);
        }

        public uint Pstate
        {
            get => _nativeContext.GetPstate();
            set => _nativeContext.SetPstate(value);
        }

        public uint Fpsr
        {
            get => _nativeContext.GetFPState((uint)FPSR.Mask);
            set => _nativeContext.SetFPState(value, (uint)FPSR.Mask);
        }

        public uint Fpcr
        {
            get => _nativeContext.GetFPState((uint)FPCR.Mask);
            set => _nativeContext.SetFPState(value, (uint)FPCR.Mask);
        }

        public bool IsAarch32 { get; set; }

        internal ExecutionMode ExecutionMode
        {
            get
            {
                if (IsAarch32)
                {
                    return (Pstate & (1u << 5)) != 0
                        ? ExecutionMode.Aarch32Thumb
                        : ExecutionMode.Aarch32Arm;
                }
                else
                {
                    return ExecutionMode.Aarch64;
                }
            }
        }

        public bool Running
        {
            get => _nativeContext.GetRunning();
            private set => _nativeContext.SetRunning(value);
        }

        private readonly ExceptionCallbackNoArgs _interruptCallback;
        private readonly ExceptionCallback _breakCallback;
        private readonly ExceptionCallback _supervisorCallback;
        private readonly ExceptionCallback _undefinedCallback;

        public ExecutionContext(IJitMemoryAllocator allocator, ICounter counter, ExceptionCallbacks exceptionCallbacks)
        {
            _nativeContext = new NativeContext(allocator);
            _counter = counter;
            _interruptCallback = exceptionCallbacks.InterruptCallback;
            _breakCallback = exceptionCallbacks.BreakCallback;
            _supervisorCallback = exceptionCallbacks.SupervisorCallback;
            _undefinedCallback = exceptionCallbacks.UndefinedCallback;

            Running = true;

            _nativeContext.SetCounter(MinCountForCheck);
        }

        public ulong GetX(int index) => _nativeContext.GetX(index);
        public void SetX(int index, ulong value) => _nativeContext.SetX(index, value);

        public V128 GetV(int index) => _nativeContext.GetV(index);
        public void SetV(int index, V128 value) => _nativeContext.SetV(index, value);

        internal void CheckInterrupt()
        {
            if (_interrupted)
            {
                _interrupted = false;

                _interruptCallback?.Invoke(this);
            }

            _nativeContext.SetCounter(MinCountForCheck);
        }

        public void RequestInterrupt()
        {
            _interrupted = true;
        }

        internal void OnBreak(ulong address, int imm)
        {
            _breakCallback?.Invoke(this, address, imm);
        }

        internal void OnSupervisorCall(ulong address, int imm)
        {
            _supervisorCallback?.Invoke(this, address, imm);
        }

        internal void OnUndefined(ulong address, int opCode)
        {
            _undefinedCallback?.Invoke(this, address, opCode);
        }

        public void StopRunning()
        {
            Running = false;

            _nativeContext.SetCounter(0);
        }

        protected virtual void Dispose(bool disposing)
        {
            _nativeContext.Dispose();
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
