using Ryujinx.Common.Logging;
using Ryujinx.HLE.HOS.Tamper.Operations;

namespace Ryujinx.HLE.HOS.Tamper
{
    class Register : IOperand
    {
        private ulong _register;
        private readonly string _alias;

        public Register(string alias)
        {
            _alias = alias;
        }

        public T Get<T>() where T : unmanaged
        {
            return (T)(dynamic)_register;
        }

        public void Set<T>(T value) where T : unmanaged
        {
            Logger.Debug?.Print(LogClass.TamperMachine, $"{_alias}: {value}");

            _register = (ulong)(dynamic)value;
        }
    }
}
