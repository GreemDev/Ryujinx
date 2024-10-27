using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using static Ryujinx.Audio.Backends.SoundIo.Native.SoundIo;

namespace Ryujinx.Audio.Backends.SoundIo.Native
{
    public class SoundIoContext : IDisposable
    {
        private nint _context;
        private Action<SoundIoError> _onBackendDisconnect;
        private OnBackendDisconnectedDelegate _onBackendDisconnectNative;

        public nint Context => _context;

        internal SoundIoContext(nint context)
        {
            _context = context;
            _onBackendDisconnect = null;
            _onBackendDisconnectNative = null;
        }

        public SoundIoError Connect() => soundio_connect(_context);
        public void Disconnect() => soundio_disconnect(_context);

        public void FlushEvents() => soundio_flush_events(_context);

        public int OutputDeviceCount => soundio_output_device_count(_context);

        public int DefaultOutputDeviceIndex => soundio_default_output_device_index(_context);

        public Action<SoundIoError> OnBackendDisconnect
        {
            get { return _onBackendDisconnect; }
            set
            {
                _onBackendDisconnect = value;

                if (_onBackendDisconnect == null)
                {
                    _onBackendDisconnectNative = null;
                }
                else
                {
                    _onBackendDisconnectNative = (ctx, err) => _onBackendDisconnect(err);
                }

                GetContext().OnBackendDisconnected = Marshal.GetFunctionPointerForDelegate(_onBackendDisconnectNative);
            }
        }

        private ref SoundIoStruct GetContext()
        {
            unsafe
            {
                return ref Unsafe.AsRef<SoundIoStruct>((SoundIoStruct*)_context);
            }
        }

        public SoundIoDeviceContext GetOutputDevice(int index)
        {
            nint deviceContext = soundio_get_output_device(_context, index);

            if (deviceContext == nint.Zero)
            {
                return null;
            }

            return new SoundIoDeviceContext(deviceContext);
        }

        public static SoundIoContext Create()
        {
            nint context = soundio_create();

            if (context == nint.Zero)
            {
                return null;
            }

            return new SoundIoContext(context);
        }

        protected virtual void Dispose(bool disposing)
        {
            nint currentContext = Interlocked.Exchange(ref _context, nint.Zero);

            if (currentContext != nint.Zero)
            {
                soundio_destroy(currentContext);
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~SoundIoContext()
        {
            Dispose(false);
        }
    }
}
