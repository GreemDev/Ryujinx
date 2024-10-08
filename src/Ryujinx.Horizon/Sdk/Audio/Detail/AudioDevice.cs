using Ryujinx.Audio.Renderer.Device;
using Ryujinx.Audio.Renderer.Server;
using Ryujinx.Horizon.Common;
using Ryujinx.Horizon.Sdk.Applet;
using Ryujinx.Horizon.Sdk.OsTypes;
using Ryujinx.Horizon.Sdk.Sf;
using Ryujinx.Horizon.Sdk.Sf.Hipc;
using System;

namespace Ryujinx.Horizon.Sdk.Audio.Detail
{
    partial class AudioDevice : IAudioDevice, IDisposable
    {
        private readonly VirtualDeviceSessionRegistry _registry;
        private readonly VirtualDeviceSession[] _sessions;
        private readonly bool _isUsbDeviceSupported;

        private SystemEventType _audioEvent;
        private SystemEventType _audioInputEvent;
        private SystemEventType _audioOutputEvent;

        public AudioDevice(VirtualDeviceSessionRegistry registry, AppletResourceUserId appletResourceId, uint revision)
        {
            _registry = registry;

            BehaviourContext behaviourContext = new();
            behaviourContext.SetUserRevision((int)revision);

            _isUsbDeviceSupported = behaviourContext.IsAudioUsbDeviceOutputSupported();
            _sessions = registry.GetSessionByAppletResourceId(appletResourceId.Id);

            Os.CreateSystemEvent(out _audioEvent, EventClearMode.AutoClear, interProcess: true);
            Os.CreateSystemEvent(out _audioInputEvent, EventClearMode.AutoClear, interProcess: true);
            Os.CreateSystemEvent(out _audioOutputEvent, EventClearMode.AutoClear, interProcess: true);
        }

        private bool TryGetDeviceByName(out VirtualDeviceSession result, string name, bool ignoreRevLimitation = false)
        {
            result = null;

            foreach (VirtualDeviceSession session in _sessions)
            {
                if (session.Device.Name.Equals(name))
                {
                    if (!ignoreRevLimitation && !_isUsbDeviceSupported && session.Device.IsUsbDevice())
                    {
                        return false;
                    }

                    result = session;

                    return true;
                }
            }

            return false;
        }

        [CmifCommand(0)]
        public Result ListAudioDeviceName([Buffer(HipcBufferFlags.Out | HipcBufferFlags.MapAlias)] Span<DeviceName> names, out int nameCount)
        {
            int count = 0;

            foreach (VirtualDeviceSession session in _sessions)
            {
                if (!_isUsbDeviceSupported && session.Device.IsUsbDevice())
                {
                    continue;
                }

                if (count >= names.Length)
                {
                    break;
                }

                names[count] = new DeviceName(session.Device.Name);

                count++;
            }

            nameCount = count;

            return Result.Success;
        }

        [CmifCommand(1)]
        public Result SetAudioDeviceOutputVolume([Buffer(HipcBufferFlags.In | HipcBufferFlags.MapAlias)] ReadOnlySpan<DeviceName> name, float volume)
        {
            if (name.Length > 0 && TryGetDeviceByName(out VirtualDeviceSession result, name[0].ToString(), ignoreRevLimitation: true))
            {
                if (!_isUsbDeviceSupported && result.Device.IsUsbDevice())
                {
                    result = _sessions[0];
                }

                result.Volume = volume;
            }

            return Result.Success;
        }

        [CmifCommand(2)]
        public Result GetAudioDeviceOutputVolume([Buffer(HipcBufferFlags.In | HipcBufferFlags.MapAlias)] ReadOnlySpan<DeviceName> name, out float volume)
        {
            if (name.Length > 0 && TryGetDeviceByName(out VirtualDeviceSession result, name[0].ToString()))
            {
                volume = result.Volume;
            }
            else
            {
                volume = 0f;
            }

            return Result.Success;
        }

        [CmifCommand(3)]
        public Result GetActiveAudioDeviceName([Buffer(HipcBufferFlags.Out | HipcBufferFlags.MapAlias)] Span<DeviceName> name)
        {
            VirtualDevice device = _registry.ActiveDevice;

            if (!_isUsbDeviceSupported && device.IsUsbDevice())
            {
                device = _registry.DefaultDevice;
            }

            if (name.Length > 0)
            {
                name[0] = new DeviceName(device.Name);
            }

            return Result.Success;
        }

        [CmifCommand(4)]
        public Result QueryAudioDeviceSystemEvent([CopyHandle] out int eventHandle)
        {
            eventHandle = Os.GetReadableHandleOfSystemEvent(ref _audioEvent);

            return Result.Success;
        }

        [CmifCommand(5)]
        public Result GetActiveChannelCount(out int channelCount)
        {
            VirtualDevice device = _registry.ActiveDevice;

            if (!_isUsbDeviceSupported && device.IsUsbDevice())
            {
                device = _registry.DefaultDevice;
            }

            channelCount = (int)device.ChannelCount;

            return Result.Success;
        }

        [CmifCommand(6)] // 3.0.0+
        public Result ListAudioDeviceNameAuto([Buffer(HipcBufferFlags.Out | HipcBufferFlags.AutoSelect)] Span<DeviceName> names, out int nameCount)
        {
            return ListAudioDeviceName(names, out nameCount);
        }

        [CmifCommand(7)] // 3.0.0+
        public Result SetAudioDeviceOutputVolumeAuto([Buffer(HipcBufferFlags.In | HipcBufferFlags.AutoSelect)] ReadOnlySpan<DeviceName> name, float volume)
        {
            return SetAudioDeviceOutputVolume(name, volume);
        }

        [CmifCommand(8)] // 3.0.0+
        public Result GetAudioDeviceOutputVolumeAuto([Buffer(HipcBufferFlags.In | HipcBufferFlags.AutoSelect)] ReadOnlySpan<DeviceName> name, out float volume)
        {
            return GetAudioDeviceOutputVolume(name, out volume);
        }

        [CmifCommand(10)] // 3.0.0+
        public Result GetActiveAudioDeviceNameAuto([Buffer(HipcBufferFlags.Out | HipcBufferFlags.AutoSelect)] Span<DeviceName> name)
        {
            return GetActiveAudioDeviceName(name);
        }

        [CmifCommand(11)] // 3.0.0+
        public Result QueryAudioDeviceInputEvent([CopyHandle] out int eventHandle)
        {
            eventHandle = Os.GetReadableHandleOfSystemEvent(ref _audioInputEvent);

            return Result.Success;
        }

        [CmifCommand(12)] // 3.0.0+
        public Result QueryAudioDeviceOutputEvent([CopyHandle] out int eventHandle)
        {
            eventHandle = Os.GetReadableHandleOfSystemEvent(ref _audioOutputEvent);

            return Result.Success;
        }

        [CmifCommand(13)] // 13.0.0+
        public Result GetActiveAudioOutputDeviceName([Buffer(HipcBufferFlags.Out | HipcBufferFlags.MapAlias)] Span<DeviceName> name)
        {
            if (name.Length > 0)
            {
                name[0] = new DeviceName(_registry.ActiveDevice.GetOutputDeviceName());
            }

            return Result.Success;
        }

        [CmifCommand(14)] // 13.0.0+
        public Result ListAudioOutputDeviceName([Buffer(HipcBufferFlags.Out | HipcBufferFlags.MapAlias)] Span<DeviceName> names, out int nameCount)
        {
            int count = 0;

            foreach (VirtualDeviceSession session in _sessions)
            {
                if (!_isUsbDeviceSupported && session.Device.IsUsbDevice())
                {
                    continue;
                }

                if (count >= names.Length)
                {
                    break;
                }

                names[count] = new DeviceName(session.Device.GetOutputDeviceName());

                count++;
            }

            nameCount = count;

            return Result.Success;
        }

        [CmifCommand(15)] // 17.0.0+
        public Result AcquireAudioOutputDeviceNotification([CopyHandle] out int eventHandle, ulong deviceId)
        {
            eventHandle = 0;

            return AudioResult.NotImplemented;
        }

        [CmifCommand(16)] // 17.0.0+
        public Result ReleaseAudioOutputDeviceNotification(ulong deviceId)
        {
            return AudioResult.NotImplemented;
        }

        [CmifCommand(17)] // 17.0.0+
        public Result AcquireAudioInputDeviceNotification([CopyHandle] out int eventHandle, ulong deviceId)
        {
            eventHandle = 0;

            return AudioResult.NotImplemented;
        }

        [CmifCommand(18)] // 17.0.0+
        public Result ReleaseAudioInputDeviceNotification(ulong deviceId)
        {
            return AudioResult.NotImplemented;
        }

        [CmifCommand(19)] // 18.0.0+
        public Result SetAudioDeviceOutputVolumeAutoTuneEnabled(bool enabled)
        {
            return AudioResult.NotImplemented;
        }

        [CmifCommand(20)] // 18.0.0+
        public Result IsAudioDeviceOutputVolumeAutoTuneEnabled(out bool enabled)
        {
            enabled = false;

            return AudioResult.NotImplemented;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                Os.DestroySystemEvent(ref _audioEvent);
                Os.DestroySystemEvent(ref _audioInputEvent);
                Os.DestroySystemEvent(ref _audioOutputEvent);
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
