using OpenTK.Graphics.OpenGL;
using Ryujinx.Common.Configuration;
using Ryujinx.Common.Logging;
using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace Ryujinx.Graphics.OpenGL
{
    public static class Debugger
    {
        private static DebugProc _debugCallback;

        private static int _counter;

        public static void Initialize(GraphicsDebugLevel logLevel)
        {
            // Disable everything
            GL.DebugMessageControl(DebugSourceControl.DontCare, DebugTypeControl.DontCare, DebugSeverityControl.DontCare, 0, (int[])null, false);

            if (logLevel == GraphicsDebugLevel.None)
            {
                GL.Disable(EnableCap.DebugOutputSynchronous);
                GL.DebugMessageCallback(null, nint.Zero);

                return;
            }

            GL.Enable(EnableCap.DebugOutputSynchronous);

            if (logLevel == GraphicsDebugLevel.Error)
            {
                GL.DebugMessageControl(DebugSourceControl.DontCare, DebugTypeControl.DebugTypeError, DebugSeverityControl.DontCare, 0, (int[])null, true);
            }
            else if (logLevel == GraphicsDebugLevel.Slowdowns)
            {
                GL.DebugMessageControl(DebugSourceControl.DontCare, DebugTypeControl.DebugTypeError, DebugSeverityControl.DontCare, 0, (int[])null, true);
                GL.DebugMessageControl(DebugSourceControl.DontCare, DebugTypeControl.DebugTypePerformance, DebugSeverityControl.DontCare, 0, (int[])null, true);
            }
            else
            {
                GL.DebugMessageControl(DebugSourceControl.DontCare, DebugTypeControl.DontCare, DebugSeverityControl.DontCare, 0, (int[])null, true);
            }

            _counter = 0;
            _debugCallback = GLDebugHandler;

            GL.DebugMessageCallback(_debugCallback, nint.Zero);

            Logger.Warning?.Print(LogClass.Gpu, "OpenGL Debugging is enabled. Performance will be negatively impacted.");
        }

        private static void GLDebugHandler(
            DebugSource source,
            DebugType type,
            int id,
            DebugSeverity severity,
            int length,
            nint message,
            nint userParam)
        {
            string msg = Marshal.PtrToStringUTF8(message).Replace('\n', ' ');

            switch (type)
            {
                case DebugType.DebugTypeError:
                    Logger.Error?.Print(LogClass.Gpu, $"{severity}: {msg}\nCallStack={Environment.StackTrace}", "GLERROR");
                    break;
                case DebugType.DebugTypePerformance:
                    Logger.Warning?.Print(LogClass.Gpu, $"{severity}: {msg}", "GLPERF");
                    break;
                case DebugType.DebugTypePushGroup:
                    Logger.Info?.Print(LogClass.Gpu, $"{{ ({id}) {severity}: {msg}", "GLINFO");
                    break;
                case DebugType.DebugTypePopGroup:
                    Logger.Info?.Print(LogClass.Gpu, $"}} ({id}) {severity}: {msg}", "GLINFO");
                    break;
                default:
                    if (source == DebugSource.DebugSourceApplication)
                    {
                        Logger.Info?.Print(LogClass.Gpu, $"{type} {severity}: {msg}", "GLINFO");
                    }
                    else
                    {
                        Logger.Debug?.Print(LogClass.Gpu, $"{type} {severity}: {msg}", "GLDEBUG");
                    }
                    break;
            }
        }

        // Useful debug helpers
        public static void PushGroup(string dbgMsg)
        {
            int counter = Interlocked.Increment(ref _counter);

            GL.PushDebugGroup(DebugSourceExternal.DebugSourceApplication, counter, dbgMsg.Length, dbgMsg);
        }

        public static void PopGroup()
        {
            GL.PopDebugGroup();
        }

        public static void Print(string dbgMsg, DebugType type = DebugType.DebugTypeMarker, DebugSeverity severity = DebugSeverity.DebugSeverityNotification, int id = 999999)
        {
            GL.DebugMessageInsert(DebugSourceExternal.DebugSourceApplication, type, id, severity, dbgMsg.Length, dbgMsg);
        }
    }
}
