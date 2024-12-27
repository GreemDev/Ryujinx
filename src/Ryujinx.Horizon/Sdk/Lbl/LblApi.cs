using Ryujinx.Horizon.Common;
using Ryujinx.Horizon.Sdk.Sm;
using System;

namespace Ryujinx.Horizon.Sdk.Lbl
{
    public class LblApi : IDisposable
    {
        private const string LblName = "lbl";

        private int _sessionHandle;

        public LblApi()
        {
            using var smApi = new SmApi();

            smApi.Initialize();
            smApi.GetServiceHandle(out _sessionHandle, ServiceName.Encode(LblName)).AbortOnFailure();
        }

        public Result EnableVrMode()
        {
            return ServiceUtil.SendRequest(out _, _sessionHandle, 26, sendPid: false, []);
        }

        public Result DisableVrMode()
        {
            return ServiceUtil.SendRequest(out _, _sessionHandle, 27, sendPid: false, []);
        }

        public void Dispose()
        {
            if (_sessionHandle != 0)
            {
                HorizonStatic.Syscall.CloseHandle(_sessionHandle);

                _sessionHandle = 0;
            }

            GC.SuppressFinalize(this);
        }
    }
}
