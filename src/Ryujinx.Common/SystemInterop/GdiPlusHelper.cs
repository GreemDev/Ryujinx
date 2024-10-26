using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Ryujinx.Common.SystemInterop
{
    [SupportedOSPlatform("windows")]
    public static partial class GdiPlusHelper
    {
        private const string LibraryName = "gdiplus.dll";

        private static readonly nint _initToken;

        static GdiPlusHelper()
        {
            CheckStatus(GdiplusStartup(out _initToken, StartupInputEx.Default, out _));
        }

        private static void CheckStatus(int gdiStatus)
        {
            if (gdiStatus != 0)
            {
                throw new Exception($"GDI Status Error: {gdiStatus}");
            }
        }

        private struct StartupInputEx
        {
            public int GdiplusVersion;

#pragma warning disable CS0649 // Field is never assigned to
            public nint DebugEventCallback;
            public int SuppressBackgroundThread;
            public int SuppressExternalCodecs;
            public int StartupParameters;
#pragma warning restore CS0649

            public static StartupInputEx Default => new()
            {
                // We assume Windows 8 and upper
                GdiplusVersion = 2,
                DebugEventCallback = nint.Zero,
                SuppressBackgroundThread = 0,
                SuppressExternalCodecs = 0,
                StartupParameters = 0,
            };
        }

        private struct StartupOutput
        {
            public nint NotificationHook;
            public nint NotificationUnhook;
        }

        [LibraryImport(LibraryName)]
        private static partial int GdiplusStartup(out nint token, in StartupInputEx input, out StartupOutput output);

        [LibraryImport(LibraryName)]
        private static partial int GdipCreateFromHWND(nint hwnd, out nint graphics);

        [LibraryImport(LibraryName)]
        private static partial int GdipDeleteGraphics(nint graphics);

        [LibraryImport(LibraryName)]
        private static partial int GdipGetDpiX(nint graphics, out float dpi);

        public static float GetDpiX(nint hwnd)
        {
            CheckStatus(GdipCreateFromHWND(hwnd, out nint graphicsHandle));
            CheckStatus(GdipGetDpiX(graphicsHandle, out float result));
            CheckStatus(GdipDeleteGraphics(graphicsHandle));

            return result;
        }
    }
}
