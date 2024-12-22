using Ryujinx.HLE.HOS.Ipc;
using Ryujinx.HLE.HOS.Services.Pcv.Clkrst.ClkrstManager;
using Ryujinx.HLE.HOS.Services.Pcv.Types;
using Ryujinx.Horizon.Common;
using System;

namespace Ryujinx.HLE.HOS.Services.Pcv.Clkrst
{
    [Service("clkrst")]   // 8.0.0+
    [Service("clkrst:i")] // 8.0.0+
    class IClkrstManager : IpcService
    {
        private int _moduleStateTableEventHandle;

        public IClkrstManager(ServiceCtx context) { }

        [CommandCmif(0)]
        // OpenSession(u32 device_code, u32 unk) -> object<nn::clkrst::IClkrstSession>
        public ResultCode OpenSession(ServiceCtx context)
        {
            DeviceCode deviceCode = (DeviceCode)context.RequestData.ReadUInt32();
            uint unknown = context.RequestData.ReadUInt32();

            // TODO: Service checks the deviceCode and the unk value.

            MakeObject(context, new IClkrstSession(deviceCode, unknown));

            return ResultCode.Success;
        }

        [CommandCmif(4)]
        // GetModuleStateTableEvent() -> handle<copy>
        public ResultCode GetModuleStateTableEvent(ServiceCtx context)
        {
            if (_moduleStateTableEventHandle == 0)
            {
                if (context.Process.HandleTable.GenerateHandle(context.Device.System.IirsSharedMem, out _moduleStateTableEventHandle) != Result.Success)
                {
                    throw new InvalidOperationException("Out of handles!");
                }
            }

            context.Response.HandleDesc = IpcHandleDesc.MakeCopy(_moduleStateTableEventHandle);

            return ResultCode.Success;
        }

        [CommandCmif(5)]
        // GetModuleStateTableMaxCount() -> u32 max_count
        public ResultCode GetModuleStateTableMaxCount(ServiceCtx context)
        {
            context.ResponseData.Write(26u);

            return ResultCode.Success;
        }
    }
}
