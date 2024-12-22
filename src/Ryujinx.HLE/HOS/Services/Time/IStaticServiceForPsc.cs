using Ryujinx.Common;
using Ryujinx.Cpu;
using Ryujinx.HLE.HOS.Ipc;
using Ryujinx.HLE.HOS.Services.Time.Clock;
using Ryujinx.HLE.HOS.Services.Time.StaticService;
using Ryujinx.HLE.HOS.Services.Time.TimeZone;
using Ryujinx.Horizon.Common;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace Ryujinx.HLE.HOS.Services.Time
{
    [Service("time:s", TimePermissions.System)]
    [Service("time:su", TimePermissions.SystemUpdate)]
    class IStaticServiceForPsc : IpcService
    {
        private readonly TimeManager _timeManager;
        private readonly TimePermissions _permissions;

        private int _timeSharedMemoryNativeHandle;

        public IStaticServiceForPsc(ServiceCtx context, TimePermissions permissions) : this(TimeManager.Instance, permissions) { }

        public IStaticServiceForPsc(TimeManager manager, TimePermissions permissions)
        {
            _permissions = permissions;
            _timeManager = manager;
        }

        [CommandCmif(0)]
        // GetStandardUserSystemClock() -> object<nn::timesrv::detail::service::ISystemClock>
        public ResultCode GetStandardUserSystemClock(ServiceCtx context)
        {
            MakeObject(context, new ISystemClock(_timeManager.StandardUserSystemClock,
                (_permissions & TimePermissions.UserSystemClockWritableMask) != 0,
                (_permissions & TimePermissions.BypassUninitialized) != 0));

            return ResultCode.Success;
        }

        [CommandCmif(1)]
        // GetStandardNetworkSystemClock() -> object<nn::timesrv::detail::service::ISystemClock>
        public ResultCode GetStandardNetworkSystemClock(ServiceCtx context)
        {
            MakeObject(context, new ISystemClock(_timeManager.StandardNetworkSystemClock,
                (_permissions & TimePermissions.NetworkSystemClockWritableMask) != 0,
                (_permissions & TimePermissions.BypassUninitialized) != 0));

            return ResultCode.Success;
        }

        [CommandCmif(2)]
        // GetStandardSteadyClock() -> object<nn::timesrv::detail::service::ISteadyClock>
        public ResultCode GetStandardSteadyClock(ServiceCtx context)
        {
            MakeObject(context, new ISteadyClock(_timeManager.StandardSteadyClock,
                (_permissions & TimePermissions.SteadyClockWritableMask) != 0,
                (_permissions & TimePermissions.BypassUninitialized) != 0));

            return ResultCode.Success;
        }

        [CommandCmif(3)]
        // GetTimeZoneService() -> object<nn::timesrv::detail::service::ITimeZoneService>
        public ResultCode GetTimeZoneService(ServiceCtx context)
        {
            MakeObject(context, new ITimeZoneServiceForPsc(_timeManager.TimeZone.Manager,
                (_permissions & TimePermissions.TimeZoneWritableMask) != 0));

            return ResultCode.Success;
        }

        [CommandCmif(4)]
        // GetStandardLocalSystemClock() -> object<nn::timesrv::detail::service::ISystemClock>
        public ResultCode GetStandardLocalSystemClock(ServiceCtx context)
        {
            MakeObject(context, new ISystemClock(_timeManager.StandardLocalSystemClock,
                (_permissions & TimePermissions.LocalSystemClockWritableMask) != 0,
                (_permissions & TimePermissions.BypassUninitialized) != 0));

            return ResultCode.Success;
        }

        [CommandCmif(5)] // 4.0.0+
        // GetEphemeralNetworkSystemClock() -> object<nn::timesrv::detail::service::ISystemClock>
        public ResultCode GetEphemeralNetworkSystemClock(ServiceCtx context)
        {
            MakeObject(context, new ISystemClock(_timeManager.StandardNetworkSystemClock,
                (_permissions & TimePermissions.NetworkSystemClockWritableMask) != 0,
                (_permissions & TimePermissions.BypassUninitialized) != 0));

            return ResultCode.Success;
        }

        [CommandCmif(20)] // 6.0.0+
        // GetSharedMemoryNativeHandle() -> handle<copy>
        public ResultCode GetSharedMemoryNativeHandle(ServiceCtx context)
        {
            if (_timeSharedMemoryNativeHandle == 0)
            {
                if (context.Process.HandleTable.GenerateHandle(_timeManager.SharedMemory.GetSharedMemory(), out _timeSharedMemoryNativeHandle) != Result.Success)
                {
                    throw new InvalidOperationException("Out of handles!");
                }
            }

            context.Response.HandleDesc = IpcHandleDesc.MakeCopy(_timeSharedMemoryNativeHandle);

            return ResultCode.Success;
        }

        [CommandCmif(50)] // 4.0.0+
        // SetStandardSteadyClockInternalOffset(nn::TimeSpanType internal_offset)
        public ResultCode SetStandardSteadyClockInternalOffset(ServiceCtx context)
        {
            // This is only implemented in glue's StaticService.
            return ResultCode.NotImplemented;
        }

        [CommandCmif(51)] // 9.0.0+
        // GetStandardSteadyClockRtcValue() -> u64
        public ResultCode GetStandardSteadyClockRtcValue(ServiceCtx context)
        {
            // This is only implemented in glue's StaticService.
            return ResultCode.NotImplemented;
        }

        [CommandCmif(100)]
        // IsStandardUserSystemClockAutomaticCorrectionEnabled() -> bool
        public ResultCode IsStandardUserSystemClockAutomaticCorrectionEnabled(ServiceCtx context)
        {
            StandardUserSystemClockCore userClock = _timeManager.StandardUserSystemClock;

            if (!userClock.IsInitialized())
            {
                return ResultCode.UninitializedClock;
            }

            context.ResponseData.Write(userClock.IsAutomaticCorrectionEnabled());

            return ResultCode.Success;
        }

        [CommandCmif(101)]
        // SetStandardUserSystemClockAutomaticCorrectionEnabled(b8)
        public ResultCode SetStandardUserSystemClockAutomaticCorrectionEnabled(ServiceCtx context)
        {
            SteadyClockCore steadyClock = _timeManager.StandardSteadyClock;
            StandardUserSystemClockCore userClock = _timeManager.StandardUserSystemClock;

            if (!userClock.IsInitialized() || !steadyClock.IsInitialized())
            {
                return ResultCode.UninitializedClock;
            }

            if ((_permissions & TimePermissions.UserSystemClockWritableMask) == 0)
            {
                return ResultCode.PermissionDenied;
            }

            bool autoCorrectionEnabled = context.RequestData.ReadBoolean();

            ITickSource tickSource = context.Device.System.TickSource;

            ResultCode result = userClock.SetAutomaticCorrectionEnabled(tickSource, autoCorrectionEnabled);

            if (result == ResultCode.Success)
            {
                _timeManager.SharedMemory.SetAutomaticCorrectionEnabled(autoCorrectionEnabled);

                SteadyClockTimePoint currentTimePoint = userClock.GetSteadyClockCore().GetCurrentTimePoint(tickSource);

                userClock.SetAutomaticCorrectionUpdatedTime(currentTimePoint);
                userClock.SignalAutomaticCorrectionEvent();
            }

            return result;
        }

        [CommandCmif(102)] // 5.0.0+
        // GetStandardUserSystemClockInitialYear() -> u32
        public ResultCode GetStandardUserSystemClockInitialYear(ServiceCtx context)
        {
            // This is only implemented in glue's StaticService.
            return ResultCode.NotImplemented;
        }

        [CommandCmif(200)] // 3.0.0+
        // IsStandardNetworkSystemClockAccuracySufficient() -> bool
        public ResultCode IsStandardNetworkSystemClockAccuracySufficient(ServiceCtx context)
        {
            ITickSource tickSource = context.Device.System.TickSource;

            context.ResponseData.Write(_timeManager.StandardNetworkSystemClock.IsStandardNetworkSystemClockAccuracySufficient(tickSource));

            return ResultCode.Success;
        }

        [CommandCmif(201)] // 6.0.0+
        // GetStandardUserSystemClockAutomaticCorrectionUpdatedTime() -> nn::time::SteadyClockTimePoint
        public ResultCode GetStandardUserSystemClockAutomaticCorrectionUpdatedTime(ServiceCtx context)
        {
            StandardUserSystemClockCore userClock = _timeManager.StandardUserSystemClock;

            if (!userClock.IsInitialized())
            {
                return ResultCode.UninitializedClock;
            }

            context.ResponseData.WriteStruct(userClock.GetAutomaticCorrectionUpdatedTime());

            return ResultCode.Success;
        }

        [CommandCmif(300)] // 4.0.0+
        // CalculateMonotonicSystemClockBaseTimePoint(nn::time::SystemClockContext) -> s64
        public ResultCode CalculateMonotonicSystemClockBaseTimePoint(ServiceCtx context)
        {
            SteadyClockCore steadyClock = _timeManager.StandardSteadyClock;

            if (!steadyClock.IsInitialized())
            {
                return ResultCode.UninitializedClock;
            }

            ITickSource tickSource = context.Device.System.TickSource;

            SystemClockContext otherContext = context.RequestData.ReadStruct<SystemClockContext>();
            SteadyClockTimePoint currentTimePoint = steadyClock.GetCurrentTimePoint(tickSource);

            ResultCode result = ResultCode.TimeMismatch;

            if (currentTimePoint.ClockSourceId == otherContext.SteadyTimePoint.ClockSourceId)
            {
                TimeSpanType ticksTimeSpan = TimeSpanType.FromTicks(tickSource.Counter, tickSource.Frequency);
                long baseTimePoint = otherContext.Offset + currentTimePoint.TimePoint - ticksTimeSpan.ToSeconds();

                context.ResponseData.Write(baseTimePoint);

                result = ResultCode.Success;
            }

            return result;
        }

        [CommandCmif(400)] // 4.0.0+
        // GetClockSnapshot(u8) -> buffer<nn::time::sf::ClockSnapshot, 0x1a>
        public ResultCode GetClockSnapshot(ServiceCtx context)
        {
            byte type = context.RequestData.ReadByte();

            context.Response.PtrBuff[0] = context.Response.PtrBuff[0].WithSize((uint)Marshal.SizeOf<ClockSnapshot>());

            ITickSource tickSource = context.Device.System.TickSource;

            ResultCode result = _timeManager.StandardUserSystemClock.GetClockContext(tickSource, out SystemClockContext userContext);

            if (result == ResultCode.Success)
            {
                result = _timeManager.StandardNetworkSystemClock.GetClockContext(tickSource, out SystemClockContext networkContext);

                if (result == ResultCode.Success)
                {
                    result = GetClockSnapshotFromSystemClockContextInternal(tickSource, userContext, networkContext, type, out ClockSnapshot clockSnapshot);

                    if (result == ResultCode.Success)
                    {
                        WriteClockSnapshotFromBuffer(context, context.Request.RecvListBuff[0], clockSnapshot);
                    }
                }
            }

            return result;
        }

        [CommandCmif(401)] // 4.0.0+
        // GetClockSnapshotFromSystemClockContext(u8, nn::time::SystemClockContext, nn::time::SystemClockContext) -> buffer<nn::time::sf::ClockSnapshot, 0x1a>
        public ResultCode GetClockSnapshotFromSystemClockContext(ServiceCtx context)
        {
            byte type = context.RequestData.ReadByte();

            context.Response.PtrBuff[0] = context.Response.PtrBuff[0].WithSize((uint)Unsafe.SizeOf<ClockSnapshot>());

            context.RequestData.BaseStream.Position += 7;

            SystemClockContext userContext = context.RequestData.ReadStruct<SystemClockContext>();
            SystemClockContext networkContext = context.RequestData.ReadStruct<SystemClockContext>();

            ITickSource tickSource = context.Device.System.TickSource;

            ResultCode result = GetClockSnapshotFromSystemClockContextInternal(tickSource, userContext, networkContext, type, out ClockSnapshot clockSnapshot);

            if (result == ResultCode.Success)
            {
                WriteClockSnapshotFromBuffer(context, context.Request.RecvListBuff[0], clockSnapshot);
            }

            return result;
        }

        [CommandCmif(500)] // 4.0.0+
        // CalculateStandardUserSystemClockDifferenceByUser(buffer<nn::time::sf::ClockSnapshot, 0x19>, buffer<nn::time::sf::ClockSnapshot, 0x19>) -> nn::TimeSpanType
        public ResultCode CalculateStandardUserSystemClockDifferenceByUser(ServiceCtx context)
        {
            ClockSnapshot clockSnapshotA = ReadClockSnapshotFromBuffer(context, context.Request.PtrBuff[0]);
            ClockSnapshot clockSnapshotB = ReadClockSnapshotFromBuffer(context, context.Request.PtrBuff[1]);
            TimeSpanType difference = TimeSpanType.FromSeconds(clockSnapshotB.UserContext.Offset - clockSnapshotA.UserContext.Offset);

            if (clockSnapshotB.UserContext.SteadyTimePoint.ClockSourceId != clockSnapshotA.UserContext.SteadyTimePoint.ClockSourceId || (clockSnapshotB.IsAutomaticCorrectionEnabled && clockSnapshotA.IsAutomaticCorrectionEnabled))
            {
                difference = new TimeSpanType(0);
            }

            context.ResponseData.Write(difference.NanoSeconds);

            return ResultCode.Success;
        }

        [CommandCmif(501)] // 4.0.0+
        // CalculateSpanBetween(buffer<nn::time::sf::ClockSnapshot, 0x19>, buffer<nn::time::sf::ClockSnapshot, 0x19>) -> nn::TimeSpanType
        public ResultCode CalculateSpanBetween(ServiceCtx context)
        {
            ClockSnapshot clockSnapshotA = ReadClockSnapshotFromBuffer(context, context.Request.PtrBuff[0]);
            ClockSnapshot clockSnapshotB = ReadClockSnapshotFromBuffer(context, context.Request.PtrBuff[1]);

            TimeSpanType result;

            ResultCode resultCode = clockSnapshotA.SteadyClockTimePoint.GetSpanBetween(clockSnapshotB.SteadyClockTimePoint, out long timeSpan);

            if (resultCode != ResultCode.Success)
            {
                resultCode = ResultCode.TimeNotFound;

                if (clockSnapshotA.NetworkTime != 0 && clockSnapshotB.NetworkTime != 0)
                {
                    result = TimeSpanType.FromSeconds(clockSnapshotB.NetworkTime - clockSnapshotA.NetworkTime);
                    resultCode = ResultCode.Success;
                }
                else
                {
                    return resultCode;
                }
            }
            else
            {
                result = TimeSpanType.FromSeconds(timeSpan);
            }

            context.ResponseData.Write(result.NanoSeconds);

            return resultCode;
        }

        private ResultCode GetClockSnapshotFromSystemClockContextInternal(ITickSource tickSource, SystemClockContext userContext, SystemClockContext networkContext, byte type, out ClockSnapshot clockSnapshot)
        {
            clockSnapshot = new ClockSnapshot();

            SteadyClockCore steadyClockCore = _timeManager.StandardSteadyClock;
            SteadyClockTimePoint currentTimePoint = steadyClockCore.GetCurrentTimePoint(tickSource);

            clockSnapshot.IsAutomaticCorrectionEnabled = _timeManager.StandardUserSystemClock.IsAutomaticCorrectionEnabled();
            clockSnapshot.UserContext = userContext;
            clockSnapshot.NetworkContext = networkContext;
            clockSnapshot.SteadyClockTimePoint = currentTimePoint;

            ResultCode result = _timeManager.TimeZone.Manager.GetDeviceLocationName(out string deviceLocationName);

            if (result != ResultCode.Success)
            {
                return result;
            }

            ReadOnlySpan<byte> tzName = Encoding.ASCII.GetBytes(deviceLocationName);

            tzName.CopyTo(clockSnapshot.LocationName);

            result = ClockSnapshot.GetCurrentTime(out clockSnapshot.UserTime, currentTimePoint, clockSnapshot.UserContext);

            if (result == ResultCode.Success)
            {
                result = _timeManager.TimeZone.Manager.ToCalendarTimeWithMyRules(clockSnapshot.UserTime, out CalendarInfo userCalendarInfo);

                if (result == ResultCode.Success)
                {
                    clockSnapshot.UserCalendarTime = userCalendarInfo.Time;
                    clockSnapshot.UserCalendarAdditionalTime = userCalendarInfo.AdditionalInfo;

                    if (ClockSnapshot.GetCurrentTime(out clockSnapshot.NetworkTime, currentTimePoint, clockSnapshot.NetworkContext) != ResultCode.Success)
                    {
                        clockSnapshot.NetworkTime = 0;
                    }

                    result = _timeManager.TimeZone.Manager.ToCalendarTimeWithMyRules(clockSnapshot.NetworkTime, out CalendarInfo networkCalendarInfo);

                    if (result == ResultCode.Success)
                    {
                        clockSnapshot.NetworkCalendarTime = networkCalendarInfo.Time;
                        clockSnapshot.NetworkCalendarAdditionalTime = networkCalendarInfo.AdditionalInfo;
                        clockSnapshot.Type = type;

                        // Probably a version field?
                        clockSnapshot.Unknown = 0;
                    }
                }
            }

            return result;
        }

        private ClockSnapshot ReadClockSnapshotFromBuffer(ServiceCtx context, IpcPtrBuffDesc ipcDesc)
        {
            Debug.Assert(ipcDesc.Size == (ulong)Unsafe.SizeOf<ClockSnapshot>());

            byte[] temp = new byte[ipcDesc.Size];

            context.Memory.Read(ipcDesc.Position, temp);

            using BinaryReader bufferReader = new(new MemoryStream(temp));

            return bufferReader.ReadStruct<ClockSnapshot>();
        }

        private void WriteClockSnapshotFromBuffer(ServiceCtx context, IpcRecvListBuffDesc ipcDesc, ClockSnapshot clockSnapshot)
        {
            MemoryHelper.Write(context.Memory, ipcDesc.Position, clockSnapshot);
        }
    }
}
