using System;
using System.Runtime.InteropServices;

namespace Ryujinx.Cpu.Signal
{
    static partial class UnixSignalHandlerRegistration
    {
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public unsafe struct SigSet
        {
            fixed long sa_mask[16];
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct SigAction
        {
            public nint sa_handler;
            public SigSet sa_mask;
            public int sa_flags;
            public nint sa_restorer;
        }

        private const int SIGSEGV = 11;
        private const int SIGBUS = 10;
        private const int SA_SIGINFO = 0x00000004;

        [LibraryImport("libc", SetLastError = true)]
        private static partial int sigaction(int signum, ref SigAction sigAction, out SigAction oldAction);

        [LibraryImport("libc", SetLastError = true)]
        private static partial int sigaction(int signum, nint sigAction, out SigAction oldAction);

        [LibraryImport("libc", SetLastError = true)]
        private static partial int sigemptyset(ref SigSet set);

        public static SigAction GetSegfaultExceptionHandler()
        {
            int result = sigaction(SIGSEGV, nint.Zero, out SigAction old);

            if (result != 0)
            {
                throw new InvalidOperationException($"Could not get SIGSEGV sigaction. Error: {result}");
            }

            return old;
        }

        public static SigAction RegisterExceptionHandler(nint action)
        {
            SigAction sig = new()
            {
                sa_handler = action,
                sa_flags = SA_SIGINFO,
            };

            sigemptyset(ref sig.sa_mask);

            int result = sigaction(SIGSEGV, ref sig, out SigAction old);

            if (result != 0)
            {
                throw new InvalidOperationException($"Could not register SIGSEGV sigaction. Error: {result}");
            }

            if (OperatingSystem.IsMacOS())
            {
                result = sigaction(SIGBUS, ref sig, out _);

                if (result != 0)
                {
                    throw new InvalidOperationException($"Could not register SIGBUS sigaction. Error: {result}");
                }
            }

            return old;
        }

        public static bool RestoreExceptionHandler(SigAction oldAction)
        {
            return sigaction(SIGSEGV, ref oldAction, out SigAction _) == 0 && (!OperatingSystem.IsMacOS() || sigaction(SIGBUS, ref oldAction, out SigAction _) == 0);
        }
    }
}
