using System;

namespace Ryujinx.Headless.SDL2
{
    class StatusUpdatedEventArgs(
        bool vSyncEnabled,
        string dockedMode,
        string aspectRatio,
        string gameStatus,
        string fifoStatus,
        string gpuName)
        : EventArgs
    {
        public bool VSyncEnabled = vSyncEnabled;
        public string DockedMode = dockedMode;
        public string AspectRatio = aspectRatio;
        public string GameStatus = gameStatus;
        public string FifoStatus = fifoStatus;
        public string GpuName = gpuName;
    }
}
