using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Ryujinx.Ava.UI.Helpers
{
    [SupportedOSPlatform("windows")]
    internal partial class Win32NativeInterop
    {
        internal const int GWLP_WNDPROC = -4;

        [Flags]
        public enum ClassStyles : uint
        {
            CsClassdc = 0x40,
            CsOwndc = 0x20,
        }

        [Flags]
        public enum WindowStyles : uint
        {
            WsChild = 0x40000000,
        }

        public enum Cursors : uint
        {
            IdcArrow = 32512,
        }

        [SuppressMessage("Design", "CA1069: Enums values should not be duplicated")]
        public enum WindowsMessages : uint
        {
            NcHitTest = 0x0084,
        }

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        internal delegate nint WindowProc(nint hWnd, WindowsMessages msg, nint wParam, nint lParam);

        [StructLayout(LayoutKind.Sequential)]
        public struct WndClassEx
        {
            public int cbSize;
            public ClassStyles style;
            public nint lpfnWndProc; // not WndProc
            public int cbClsExtra;
            public int cbWndExtra;
            public nint hInstance;
            public nint hIcon;
            public nint hCursor;
            public nint hbrBackground;
            public nint lpszMenuName;
            public nint lpszClassName;
            public nint hIconSm;

            public WndClassEx()
            {
                cbSize = Marshal.SizeOf<WndClassEx>();
            }
        }

        public static nint CreateEmptyCursor()
        {
            return CreateCursor(nint.Zero, 0, 0, 1, 1, [0xFF], [0x00]);
        }

        public static nint CreateArrowCursor()
        {
            return LoadCursor(nint.Zero, (nint)Cursors.IdcArrow);
        }

        [LibraryImport("user32.dll")]
        public static partial nint SetCursor(nint handle);

        [LibraryImport("user32.dll")]
        public static partial nint CreateCursor(nint hInst, int xHotSpot, int yHotSpot, int nWidth, int nHeight, [In] byte[] pvAndPlane, [In] byte[] pvXorPlane);

        [LibraryImport("user32.dll", SetLastError = true, EntryPoint = "RegisterClassExW")]
        public static partial ushort RegisterClassEx(ref WndClassEx param);

        [LibraryImport("user32.dll", SetLastError = true, EntryPoint = "UnregisterClassW")]
        public static partial short UnregisterClass([MarshalAs(UnmanagedType.LPWStr)] string lpClassName, nint instance);

        [LibraryImport("user32.dll", EntryPoint = "DefWindowProcW")]
        public static partial nint DefWindowProc(nint hWnd, WindowsMessages msg, nint wParam, nint lParam);

        [LibraryImport("kernel32.dll", EntryPoint = "GetModuleHandleA")]
        public static partial nint GetModuleHandle([MarshalAs(UnmanagedType.LPStr)] string lpModuleName);

        [LibraryImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool DestroyWindow(nint hwnd);

        [LibraryImport("user32.dll", SetLastError = true, EntryPoint = "LoadCursorA")]
        public static partial nint LoadCursor(nint hInstance, nint lpCursorName);

        [LibraryImport("user32.dll", SetLastError = true, EntryPoint = "CreateWindowExW")]
        public static partial nint CreateWindowEx(
           uint dwExStyle,
           [MarshalAs(UnmanagedType.LPWStr)] string lpClassName,
           [MarshalAs(UnmanagedType.LPWStr)] string lpWindowName,
           WindowStyles dwStyle,
           int x,
           int y,
           int nWidth,
           int nHeight,
           nint hWndParent,
           nint hMenu,
           nint hInstance,
           nint lpParam);

        [LibraryImport("user32.dll", SetLastError = true)]
        public static partial nint SetWindowLongPtrW(nint hWnd, int nIndex, nint value);
    }
}
