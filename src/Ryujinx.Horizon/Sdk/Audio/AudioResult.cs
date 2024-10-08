using Ryujinx.Horizon.Common;

namespace Ryujinx.Horizon.Sdk.Audio
{
    static class AudioResult
    {
        private const int ModuleId = 153;

        public static Result DeviceNotFound => new(ModuleId, 1);
        public static Result UnsupportedRevision => new(ModuleId, 2);
        public static Result NotImplemented => new(ModuleId, 513);
    }
}
