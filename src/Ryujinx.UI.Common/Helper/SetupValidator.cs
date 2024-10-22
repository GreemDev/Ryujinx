using Ryujinx.Common.Logging;
using Ryujinx.HLE.FileSystem;
using System;
using System.IO;

namespace Ryujinx.UI.Common.Helper
{
    /// <summary>
    /// Ensure installation validity
    /// </summary>
    public static class SetupValidator
    {
        public static bool IsFirmwareValid(ContentManager contentManager, out UserError error)
        {
            bool hasFirmware = contentManager.GetCurrentFirmwareVersion() != null;

            if (hasFirmware)
            {
                error = UserError.Success;

                return true;
            }

            error = UserError.NoFirmware;

            return false;
        }

        public static bool CanFixStartApplication(ContentManager contentManager, string baseApplicationPath, UserError error, out SystemVersion firmwareVersion)
        {
            try
            {
                firmwareVersion = contentManager.VerifyFirmwarePackage(baseApplicationPath);
            }
            catch (Exception)
            {
                firmwareVersion = null;
            }

            return error == UserError.NoFirmware && Path.GetExtension(baseApplicationPath).ToLowerInvariant() == ".xci" && firmwareVersion != null;
        }

        public static bool TryFixStartApplication(ContentManager contentManager, string baseApplicationPath, UserError error, out UserError outError)
        {
            if (error == UserError.NoFirmware)
            {
                string baseApplicationExtension = Path.GetExtension(baseApplicationPath).ToLowerInvariant();

                // If the target app to start is a XCI, try to install firmware from it
                if (baseApplicationExtension == ".xci")
                {
                    SystemVersion firmwareVersion;

                    try
                    {
                        firmwareVersion = contentManager.VerifyFirmwarePackage(baseApplicationPath);
                    }
                    catch (Exception)
                    {
                        firmwareVersion = null;
                    }

                    // The XCI is a valid firmware package, try to install the firmware from it!
                    if (firmwareVersion != null)
                    {
                        try
                        {
                            Logger.Info?.Print(LogClass.Application, $"Installing firmware {firmwareVersion.VersionString}");

                            contentManager.InstallFirmware(baseApplicationPath);

                            Logger.Info?.Print(LogClass.Application, $"System version {firmwareVersion.VersionString} successfully installed.");

                            outError = UserError.Success;

                            return true;
                        }
                        catch
                        {
                            // ignored
                        }
                    }
                }
            }

            outError = error;

            return false;
        }

        public static bool CanStartApplication(ContentManager contentManager, string baseApplicationPath, out UserError error)
        {
            if (Directory.Exists(baseApplicationPath) || File.Exists(baseApplicationPath))
            {
                string baseApplicationExtension = Path.GetExtension(baseApplicationPath).ToLowerInvariant();

                // NOTE: We don't force homebrew developers to install a system firmware.
                if (baseApplicationExtension is not (".nro" or ".nso"))
                    return IsFirmwareValid(contentManager, out error);

            }

            error = UserError.ApplicationNotFound;

            return error is UserError.Success;
        }
    }
}
