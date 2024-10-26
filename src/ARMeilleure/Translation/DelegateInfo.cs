using System;

namespace ARMeilleure.Translation
{
    class DelegateInfo
    {
#pragma warning disable IDE0052 // Remove unread private member
        private readonly Delegate _dlg; // Ensure that this delegate will not be garbage collected.
#pragma warning restore IDE0052

        public nint FuncPtr { get; }

        public DelegateInfo(Delegate dlg, nint funcPtr)
        {
            _dlg = dlg;
            FuncPtr = funcPtr;
        }
    }
}
