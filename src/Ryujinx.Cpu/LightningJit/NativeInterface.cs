using ARMeilleure.Memory;
using Ryujinx.Cpu.LightningJit.State;
using System;

namespace Ryujinx.Cpu.LightningJit
{
    static class NativeInterface
    {
        private const int DczSizeLog2 = 4; // Log2 size in words
        private const int DczSizeInBytes = 4 << DczSizeLog2;

        private class ThreadContext
        {
            public ExecutionContext Context { get; }
            public IMemoryManager Memory { get; }
            public Translator Translator { get; }

            public ThreadContext(ExecutionContext context, IMemoryManager memory, Translator translator)
            {
                Context = context;
                Memory = memory;
                Translator = translator;
            }
        }

        [ThreadStatic]
        private static ThreadContext Context;

        public static void RegisterThread(ExecutionContext context, IMemoryManager memory, Translator translator)
        {
            Context = new ThreadContext(context, memory, translator);
        }

        public static void UnregisterThread()
        {
            Context = null;
        }

        public static void Break(ulong address, int imm)
        {
            GetContext().OnBreak(address, imm);
        }

        public static void SupervisorCall(ulong address, int imm)
        {
            GetContext().OnSupervisorCall(address, imm);
        }

        public static void Undefined(ulong address, int opCode)
        {
            GetContext().OnUndefined(address, opCode);
        }

        public static ulong GetCntfrqEl0()
        {
            return GetContext().CntfrqEl0;
        }

        public static ulong GetCntpctEl0()
        {
            return GetContext().CntpctEl0;
        }

        public static ulong GetFunctionAddress(nint framePointer, ulong address)
        {
            return (ulong)Context.Translator.GetOrTranslatePointer(framePointer, address, GetContext().ExecutionMode);
        }

        public static void InvalidateCacheLine(ulong address)
        {
            Context.Translator.InvalidateJitCacheRegion(address, DczSizeInBytes);
        }

        public static bool CheckSynchronization()
        {
            ExecutionContext context = GetContext();

            context.CheckInterrupt();

            return context.Running;
        }

        public static ExecutionContext GetContext()
        {
            return Context.Context;
        }

        public static IMemoryManager GetMemoryManager()
        {
            return Context.Memory;
        }
    }
}
