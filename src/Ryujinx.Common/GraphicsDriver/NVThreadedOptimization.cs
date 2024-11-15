using Ryujinx.Common.GraphicsDriver.NVAPI;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
// ReSharper disable InconsistentNaming

namespace Ryujinx.Common.GraphicsDriver
{
    static partial class NVThreadedOptimization
    {
        private const string ProfileName = "Ryujinx Nvidia Profile";

        private const uint NvAPI_Initialize_ID = 0x0150E828;
        private const uint NvAPI_DRS_CreateSession_ID = 0x0694D52E;
        private const uint NvAPI_DRS_LoadSettings_ID = 0x375DBD6B;
        private const uint NvAPI_DRS_FindProfileByName_ID = 0x7E4A9A0B;
        private const uint NvAPI_DRS_CreateProfile_ID = 0x0CC176068;
        private const uint NvAPI_DRS_CreateApplication_ID = 0x4347A9DE;
        private const uint NvAPI_DRS_SetSetting_ID = 0x577DD202;
        private const uint NvAPI_DRS_SaveSettings_ID = 0xFCBC7E14;
        private const uint NvAPI_DRS_DestroySession_ID = 0x0DAD9CFF8;

        [LibraryImport("nvapi64")]
        private static partial nint nvapi_QueryInterface(uint id);

        private delegate int NvAPI_InitializeDelegate();
        private static NvAPI_InitializeDelegate NvAPI_Initialize;

        private delegate int NvAPI_DRS_CreateSessionDelegate(out nint handle);
        private static NvAPI_DRS_CreateSessionDelegate NvAPI_DRS_CreateSession;

        private delegate int NvAPI_DRS_LoadSettingsDelegate(nint handle);
        private static NvAPI_DRS_LoadSettingsDelegate NvAPI_DRS_LoadSettings;

        private delegate int NvAPI_DRS_FindProfileByNameDelegate(nint handle, NvapiUnicodeString profileName, out nint profileHandle);
        private static NvAPI_DRS_FindProfileByNameDelegate NvAPI_DRS_FindProfileByName;

        private delegate int NvAPI_DRS_CreateProfileDelegate(nint handle, ref NvdrsProfile profileInfo, out nint profileHandle);
        private static NvAPI_DRS_CreateProfileDelegate NvAPI_DRS_CreateProfile;

        private delegate int NvAPI_DRS_CreateApplicationDelegate(nint handle, nint profileHandle, ref NvdrsApplicationV4 app);
        private static NvAPI_DRS_CreateApplicationDelegate NvAPI_DRS_CreateApplication;

        private delegate int NvAPI_DRS_SetSettingDelegate(nint handle, nint profileHandle, ref NvdrsSetting setting);
        private static NvAPI_DRS_SetSettingDelegate NvAPI_DRS_SetSetting;

        private delegate int NvAPI_DRS_SaveSettingsDelegate(nint handle);
        private static NvAPI_DRS_SaveSettingsDelegate NvAPI_DRS_SaveSettings;

        private delegate int NvAPI_DRS_DestroySessionDelegate(nint handle);
        private static NvAPI_DRS_DestroySessionDelegate NvAPI_DRS_DestroySession;

        private static bool _initialized;

        private static void Check(int status)
        {
            if (status != 0)
            {
                throw new Exception($"NVAPI Error: {status}");
            }
        }

        private static void Initialize()
        {
            if (!_initialized)
            {
                NvAPI_Initialize = NvAPI_Delegate<NvAPI_InitializeDelegate>(NvAPI_Initialize_ID);

                Check(NvAPI_Initialize());

                NvAPI_DRS_CreateSession = NvAPI_Delegate<NvAPI_DRS_CreateSessionDelegate>(NvAPI_DRS_CreateSession_ID);
                NvAPI_DRS_LoadSettings = NvAPI_Delegate<NvAPI_DRS_LoadSettingsDelegate>(NvAPI_DRS_LoadSettings_ID);
                NvAPI_DRS_FindProfileByName = NvAPI_Delegate<NvAPI_DRS_FindProfileByNameDelegate>(NvAPI_DRS_FindProfileByName_ID);
                NvAPI_DRS_CreateProfile = NvAPI_Delegate<NvAPI_DRS_CreateProfileDelegate>(NvAPI_DRS_CreateProfile_ID);
                NvAPI_DRS_CreateApplication = NvAPI_Delegate<NvAPI_DRS_CreateApplicationDelegate>(NvAPI_DRS_CreateApplication_ID);
                NvAPI_DRS_SetSetting = NvAPI_Delegate<NvAPI_DRS_SetSettingDelegate>(NvAPI_DRS_SetSetting_ID);
                NvAPI_DRS_SaveSettings = NvAPI_Delegate<NvAPI_DRS_SaveSettingsDelegate>(NvAPI_DRS_SaveSettings_ID);
                NvAPI_DRS_DestroySession = NvAPI_Delegate<NvAPI_DRS_DestroySessionDelegate>(NvAPI_DRS_DestroySession_ID);

                _initialized = true;
            }
        }

        private static uint MakeVersion<T>(uint version) where T : unmanaged
        {
            return (uint)Unsafe.SizeOf<T>() | version << 16;
        }

        public static void SetThreadedOptimization(bool enabled)
        {
            Initialize();

            uint targetValue = (uint)(enabled ? Nvapi.OglThreadControlEnable : Nvapi.OglThreadControlDisable);

            Check(NvAPI_Initialize());

            Check(NvAPI_DRS_CreateSession(out nint handle));

            Check(NvAPI_DRS_LoadSettings(handle));

            // Check if the profile already exists.

            int status = NvAPI_DRS_FindProfileByName(handle, new NvapiUnicodeString(ProfileName), out nint profileHandle);

            if (status != 0)
            {
                NvdrsProfile profile = new()
                {
                    Version = MakeVersion<NvdrsProfile>(1),
                    IsPredefined = 0,
                    GpuSupport = uint.MaxValue,
                };
                profile.ProfileName.Set(ProfileName);
                Check(NvAPI_DRS_CreateProfile(handle, ref profile, out profileHandle));

                NvdrsApplicationV4 application = new()
                {
                    Version = MakeVersion<NvdrsApplicationV4>(4),
                    IsPredefined = 0,
                    Flags = 3, // IsMetro, IsCommandLine
                };
                application.AppName.Set("Ryujinx.exe");
                application.UserFriendlyName.Set("Ryujinx");
                application.Launcher.Set(string.Empty);
                application.FileInFolder.Set(string.Empty);

                Check(NvAPI_DRS_CreateApplication(handle, profileHandle, ref application));
            }

            NvdrsSetting setting = new()
            {
                Version = MakeVersion<NvdrsSetting>(1),
                SettingId = Nvapi.OglThreadControlId,
                SettingType = NvdrsSettingType.NvdrsDwordType,
                SettingLocation = NvdrsSettingLocation.NvdrsCurrentProfileLocation,
                IsCurrentPredefined = 0,
                IsPredefinedValid = 0,
                CurrentValue = targetValue,
                PredefinedValue = targetValue,
            };

            Check(NvAPI_DRS_SetSetting(handle, profileHandle, ref setting));

            Check(NvAPI_DRS_SaveSettings(handle));

            NvAPI_DRS_DestroySession(handle);
        }

        private static T NvAPI_Delegate<T>(uint id) where T : class
        {
            nint ptr = nvapi_QueryInterface(id);

            if (ptr != nint.Zero)
            {
                return Marshal.GetDelegateForFunctionPointer<T>(ptr);
            }

            return null;
        }
    }
}
