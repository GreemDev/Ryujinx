using OpenTK;
using System;

namespace Ryujinx.Ava.UI.Renderer
{
    internal class OpenTKBindingsContext : IBindingsContext
    {
        private readonly Func<string, nint> _getProcAddress;

        public OpenTKBindingsContext(Func<string, nint> getProcAddress)
        {
            _getProcAddress = getProcAddress;
        }

        public nint GetProcAddress(string procName)
        {
            return _getProcAddress(procName);
        }
    }
}
