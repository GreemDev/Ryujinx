using Gommon;
using Ryujinx.Ava.Utilities.Configuration.System;
using Ryujinx.Ava.Utilities.Configuration.UI;
using Ryujinx.Common.Configuration;
using Ryujinx.Common.Configuration.Hid;
using Ryujinx.Common.Configuration.Hid.Controller;
using Ryujinx.Common.Configuration.Hid.Keyboard;
using Ryujinx.Common.Configuration.Multiplayer;
using Ryujinx.Common.Logging;
using Ryujinx.HLE;
using System;
using System.Linq;

namespace Ryujinx.Ava.Utilities.Configuration
{
    public partial class ConfigurationState
    {
        public void Load(ConfigurationFileFormat configurationFileFormat, string configurationFilePath)
        {
            // referenced by Migrate
            bool configurationFileUpdated = false;

            if (configurationFileFormat.Version is < 0 or > ConfigurationFileFormat.CurrentVersion)
            {
                Ryujinx.Common.Logging.Logger.Warning?.Print(LogClass.Application, $"Unsupported configuration version {configurationFileFormat.Version}, loading default.");

                LoadDefault();
            }
            
            Migrate(2, static cff => cff.SystemRegion = Region.USA);
            Migrate(3, static cff => cff.SystemTimeZone = "UTC");
            Migrate(4, static cff => cff.MaxAnisotropy = -1);
            Migrate(5, static cff => cff.SystemTimeOffset = 0);
            Migrate(8, static cff => cff.EnablePtc = true);
            Migrate(9, static cff =>
            {
                cff.ColumnSort = new ColumnSort
                {
                    SortColumnId = 0,
                    SortAscending = false
                };

                cff.Hotkeys = new KeyboardHotkeys { ToggleVSyncMode = Key.F1 };
            });
            Migrate(10, static cff => cff.AudioBackend = AudioBackend.OpenAl);
            Migrate(11, static cff =>
            {
                cff.ResScale = 1;
                cff.ResScaleCustom = 1.0f;
            });
            Migrate(12, static cff => cff.LoggingGraphicsDebugLevel = GraphicsDebugLevel.None);
            // 13 -> LDN1
            Migrate(14, static cff => cff.CheckUpdatesOnStart = true);
            Migrate(16, static cff => cff.EnableShaderCache = true);
            Migrate(17, static cff => cff.StartFullscreen = false);
            Migrate(18, static cff => cff.AspectRatio = AspectRatio.Fixed16x9);
            // 19 -> LDN2
            Migrate(20, static cff => cff.ShowConfirmExit = true);
            Migrate(21, static cff =>
            {
                // Initialize network config.
                
                cff.MultiplayerMode = MultiplayerMode.Disabled;
                cff.MultiplayerLanInterfaceId = "0";
            });
            Migrate(22, static cff => cff.HideCursor = HideCursorMode.Never);
            Migrate(24, static cff =>
            {
                cff.InputConfig =
                [
                    new StandardKeyboardInputConfig
                    {
                        Version = InputConfig.CurrentVersion,
                        Backend = InputBackendType.WindowKeyboard,
                        Id = "0",
                        PlayerIndex = PlayerIndex.Player1,
                        ControllerType = ControllerType.ProController,
                        LeftJoycon = new LeftJoyconCommonConfig<Key>
                        {
                            DpadUp = Key.Up,
                            DpadDown = Key.Down,
                            DpadLeft = Key.Left,
                            DpadRight = Key.Right,
                            ButtonMinus = Key.Minus,
                            ButtonL = Key.E,
                            ButtonZl = Key.Q,
                            ButtonSl = Key.Unbound,
                            ButtonSr = Key.Unbound,
                        },
                        LeftJoyconStick = new JoyconConfigKeyboardStick<Key>
                        {
                            StickUp = Key.W,
                            StickDown = Key.S,
                            StickLeft = Key.A,
                            StickRight = Key.D,
                            StickButton = Key.F,
                        },
                        RightJoycon = new RightJoyconCommonConfig<Key>
                        {
                            ButtonA = Key.Z,
                            ButtonB = Key.X,
                            ButtonX = Key.C,
                            ButtonY = Key.V,
                            ButtonPlus = Key.Plus,
                            ButtonR = Key.U,
                            ButtonZr = Key.O,
                            ButtonSl = Key.Unbound,
                            ButtonSr = Key.Unbound,
                        },
                        RightJoyconStick = new JoyconConfigKeyboardStick<Key>
                        {
                            StickUp = Key.I,
                            StickDown = Key.K,
                            StickLeft = Key.J,
                            StickRight = Key.L,
                            StickButton = Key.H,
                        },
                    }

                ];
            });
            Migrate(26, static cff => cff.MemoryManagerMode = MemoryManagerMode.HostMappedUnsafe);
            Migrate(27, static cff => cff.EnableMouse = false);
            Migrate(29, static cff => cff.Hotkeys = new KeyboardHotkeys
            {
                ToggleVSyncMode = Key.F1,
                Screenshot = Key.F8,
                ShowUI = Key.F4
            });
            Migrate(30, static cff => 
            {
                foreach (InputConfig config in cff.InputConfig)
                {
                    if (config is StandardControllerInputConfig controllerConfig)
                    {
                        controllerConfig.Rumble = new RumbleConfigController
                        {
                            EnableRumble = false,
                            StrongRumble = 1f,
                            WeakRumble = 1f,
                        };
                    }
                }
            });
            Migrate(31, static cff => cff.BackendThreading = BackendThreading.Auto);
            Migrate(32, static cff => cff.Hotkeys = new KeyboardHotkeys
            {
                ToggleVSyncMode = cff.Hotkeys.ToggleVSyncMode,
                Screenshot = cff.Hotkeys.Screenshot,
                ShowUI = cff.Hotkeys.ShowUI,
                Pause = Key.F5,
            });
            Migrate(33, static cff =>
            {
                cff.Hotkeys = new KeyboardHotkeys
                {
                    ToggleVSyncMode = cff.Hotkeys.ToggleVSyncMode,
                    Screenshot = cff.Hotkeys.Screenshot,
                    ShowUI = cff.Hotkeys.ShowUI,
                    Pause = cff.Hotkeys.Pause,
                    ToggleMute = Key.F2,
                };

                cff.AudioVolume = 1;
            });
            Migrate(34, static cff => cff.EnableInternetAccess = false);
            Migrate(35, static cff =>
            {
                foreach (StandardControllerInputConfig config in cff.InputConfig.OfType<StandardControllerInputConfig>())
                {
                    config.RangeLeft = 1.0f;
                    config.RangeRight = 1.0f;
                }
            });
            
            Migrate(36, static cff => cff.LoggingEnableTrace = false);
            Migrate(37, static cff => cff.ShowConsole = true);
            Migrate(38, static cff =>
            {
                cff.BaseStyle = "Dark";
                cff.GameListViewMode = 0;
                cff.ShowNames = true;
                cff.GridSize = 2;
                cff.LanguageCode = "en_US";
            });
            Migrate(39, static cff => cff.Hotkeys = new KeyboardHotkeys
            {
                ToggleVSyncMode = cff.Hotkeys.ToggleVSyncMode,
                Screenshot = cff.Hotkeys.Screenshot,
                ShowUI = cff.Hotkeys.ShowUI,
                Pause = cff.Hotkeys.Pause,
                ToggleMute = cff.Hotkeys.ToggleMute,
                ResScaleUp = Key.Unbound,
                ResScaleDown = Key.Unbound
            });
            Migrate(40, static cff => cff.GraphicsBackend = GraphicsBackend.OpenGl);
            Migrate(41, static cff => cff.Hotkeys = new KeyboardHotkeys
            {
                ToggleVSyncMode = cff.Hotkeys.ToggleVSyncMode,
                Screenshot = cff.Hotkeys.Screenshot,
                ShowUI = cff.Hotkeys.ShowUI,
                Pause = cff.Hotkeys.Pause,
                ToggleMute = cff.Hotkeys.ToggleMute,
                ResScaleUp = cff.Hotkeys.ResScaleUp,
                ResScaleDown = cff.Hotkeys.ResScaleDown,
                VolumeUp = Key.Unbound,
                VolumeDown = Key.Unbound
            });
            Migrate(42, static cff => cff.EnableMacroHLE = true);
            Migrate(43, static cff => cff.UseHypervisor = true);
            Migrate(44, static cff =>
            {
                cff.AntiAliasing = AntiAliasing.None;
                cff.ScalingFilter = ScalingFilter.Bilinear;
                cff.ScalingFilterLevel = 80;
            });
            Migrate(45, static cff => cff.ShownFileTypes = new ShownFileTypes
            {
                NSP = true,
                PFS0 = true,
                XCI = true,
                NCA = true,
                NRO = true,
                NSO = true
            });
            Migrate(46, static cff => cff.UseHypervisor = OperatingSystem.IsMacOS());
            Migrate(47, static cff => cff.WindowStartup = new WindowStartup
            {
                WindowPositionX = 0,
                WindowPositionY = 0,
                WindowSizeHeight = 760,
                WindowSizeWidth = 1280,
                WindowMaximized = false
            });
            Migrate(48, static cff => cff.EnableColorSpacePassthrough = false);
            Migrate(49, static _ =>
            {
                if (OperatingSystem.IsMacOS())
                {
                    AppDataManager.FixMacOSConfigurationFolders();
                }
            });
            Migrate(50, static cff => cff.EnableHardwareAcceleration = true);
            Migrate(51, static cff => cff.RememberWindowState = true);
            Migrate(52, static cff => cff.AutoloadDirs = []);
            Migrate(53, static cff => cff.EnableLowPowerPtc = false);
            Migrate(54, static cff => cff.DramSize = MemoryConfiguration.MemoryConfiguration4GiB);
            Migrate(55, static cff => cff.IgnoreApplet = false);
            Migrate(56, static cff => cff.ShowTitleBar = !OperatingSystem.IsWindows());
            Migrate(57, static cff =>
            {
                cff.VSyncMode = VSyncMode.Switch;
                cff.EnableCustomVSyncInterval = false;

                cff.Hotkeys = new KeyboardHotkeys
                {
                    ToggleVSyncMode = Key.F1,
                    Screenshot = cff.Hotkeys.Screenshot,
                    ShowUI = cff.Hotkeys.ShowUI,
                    Pause = cff.Hotkeys.Pause,
                    ToggleMute = cff.Hotkeys.ToggleMute,
                    ResScaleUp = cff.Hotkeys.ResScaleUp,
                    ResScaleDown = cff.Hotkeys.ResScaleDown,
                    VolumeUp = cff.Hotkeys.VolumeUp,
                    VolumeDown = cff.Hotkeys.VolumeDown,
                    CustomVSyncIntervalIncrement = Key.Unbound,
                    CustomVSyncIntervalDecrement = Key.Unbound,
                };

                cff.CustomVSyncInterval = 120;
            });
            // 58 migration accidentally got skipped but it worked with no issues somehow lol
            Migrate(59, static cff =>
            {
                cff.ShowDirtyHacks = false;
                cff.DirtyHacks = [];

                // This was accidentally enabled by default when it was PRed. That is not what we want,
                // so as a compromise users who want to use it will simply need to re-enable it once after updating.
                cff.IgnoreApplet = false;
            });

            Logger.EnableFileLog.Value = configurationFileFormat.EnableFileLog;
            Graphics.ResScale.Value = configurationFileFormat.ResScale;
            Graphics.ResScaleCustom.Value = configurationFileFormat.ResScaleCustom;
            Graphics.MaxAnisotropy.Value = configurationFileFormat.MaxAnisotropy;
            Graphics.AspectRatio.Value = configurationFileFormat.AspectRatio;
            Graphics.ShadersDumpPath.Value = configurationFileFormat.GraphicsShadersDumpPath;
            Graphics.BackendThreading.Value = configurationFileFormat.BackendThreading;
            Graphics.GraphicsBackend.Value = configurationFileFormat.GraphicsBackend;
            Graphics.PreferredGpu.Value = configurationFileFormat.PreferredGpu;
            Graphics.AntiAliasing.Value = configurationFileFormat.AntiAliasing;
            Graphics.ScalingFilter.Value = configurationFileFormat.ScalingFilter;
            Graphics.ScalingFilterLevel.Value = configurationFileFormat.ScalingFilterLevel;
            Logger.EnableDebug.Value = configurationFileFormat.LoggingEnableDebug;
            Logger.EnableStub.Value = configurationFileFormat.LoggingEnableStub;
            Logger.EnableInfo.Value = configurationFileFormat.LoggingEnableInfo;
            Logger.EnableWarn.Value = configurationFileFormat.LoggingEnableWarn;
            Logger.EnableError.Value = configurationFileFormat.LoggingEnableError;
            Logger.EnableTrace.Value = configurationFileFormat.LoggingEnableTrace;
            Logger.EnableGuest.Value = configurationFileFormat.LoggingEnableGuest;
            Logger.EnableFsAccessLog.Value = configurationFileFormat.LoggingEnableFsAccessLog;
            Logger.FilteredClasses.Value = configurationFileFormat.LoggingFilteredClasses;
            Logger.GraphicsDebugLevel.Value = configurationFileFormat.LoggingGraphicsDebugLevel;
            System.Language.Value = configurationFileFormat.SystemLanguage;
            System.Region.Value = configurationFileFormat.SystemRegion;
            System.TimeZone.Value = configurationFileFormat.SystemTimeZone;
            System.SystemTimeOffset.Value = configurationFileFormat.SystemTimeOffset;
            System.EnableDockedMode.Value = configurationFileFormat.DockedMode;
            EnableDiscordIntegration.Value = configurationFileFormat.EnableDiscordIntegration;
            CheckUpdatesOnStart.Value = configurationFileFormat.CheckUpdatesOnStart;
            ShowConfirmExit.Value = configurationFileFormat.ShowConfirmExit;
            IgnoreApplet.Value = configurationFileFormat.IgnoreApplet;
            RememberWindowState.Value = configurationFileFormat.RememberWindowState;
            ShowTitleBar.Value = configurationFileFormat.ShowTitleBar;
            EnableHardwareAcceleration.Value = configurationFileFormat.EnableHardwareAcceleration;
            HideCursor.Value = configurationFileFormat.HideCursor;
            Graphics.VSyncMode.Value = configurationFileFormat.VSyncMode;
            Graphics.EnableCustomVSyncInterval.Value = configurationFileFormat.EnableCustomVSyncInterval;
            Graphics.CustomVSyncInterval.Value = configurationFileFormat.CustomVSyncInterval;
            Graphics.EnableShaderCache.Value = configurationFileFormat.EnableShaderCache;
            Graphics.EnableTextureRecompression.Value = configurationFileFormat.EnableTextureRecompression;
            Graphics.EnableMacroHLE.Value = configurationFileFormat.EnableMacroHLE;
            Graphics.EnableColorSpacePassthrough.Value = configurationFileFormat.EnableColorSpacePassthrough;
            System.EnablePtc.Value = configurationFileFormat.EnablePtc;
            System.EnableLowPowerPtc.Value = configurationFileFormat.EnableLowPowerPtc;
            System.EnableInternetAccess.Value = configurationFileFormat.EnableInternetAccess;
            System.EnableFsIntegrityChecks.Value = configurationFileFormat.EnableFsIntegrityChecks;
            System.FsGlobalAccessLogMode.Value = configurationFileFormat.FsGlobalAccessLogMode;
            System.AudioBackend.Value = configurationFileFormat.AudioBackend;
            System.AudioVolume.Value = configurationFileFormat.AudioVolume;
            System.MemoryManagerMode.Value = configurationFileFormat.MemoryManagerMode;
            System.DramSize.Value = configurationFileFormat.DramSize;
            System.IgnoreMissingServices.Value = configurationFileFormat.IgnoreMissingServices;
            System.UseHypervisor.Value = configurationFileFormat.UseHypervisor;
            UI.GuiColumns.FavColumn.Value = configurationFileFormat.GuiColumns.FavColumn;
            UI.GuiColumns.IconColumn.Value = configurationFileFormat.GuiColumns.IconColumn;
            UI.GuiColumns.AppColumn.Value = configurationFileFormat.GuiColumns.AppColumn;
            UI.GuiColumns.DevColumn.Value = configurationFileFormat.GuiColumns.DevColumn;
            UI.GuiColumns.VersionColumn.Value = configurationFileFormat.GuiColumns.VersionColumn;
            UI.GuiColumns.TimePlayedColumn.Value = configurationFileFormat.GuiColumns.TimePlayedColumn;
            UI.GuiColumns.LastPlayedColumn.Value = configurationFileFormat.GuiColumns.LastPlayedColumn;
            UI.GuiColumns.FileExtColumn.Value = configurationFileFormat.GuiColumns.FileExtColumn;
            UI.GuiColumns.FileSizeColumn.Value = configurationFileFormat.GuiColumns.FileSizeColumn;
            UI.GuiColumns.PathColumn.Value = configurationFileFormat.GuiColumns.PathColumn;
            UI.ColumnSort.SortColumnId.Value = configurationFileFormat.ColumnSort.SortColumnId;
            UI.ColumnSort.SortAscending.Value = configurationFileFormat.ColumnSort.SortAscending;
            UI.GameDirs.Value = configurationFileFormat.GameDirs;
            UI.AutoloadDirs.Value = configurationFileFormat.AutoloadDirs ?? [];
            UI.ShownFileTypes.NSP.Value = configurationFileFormat.ShownFileTypes.NSP;
            UI.ShownFileTypes.PFS0.Value = configurationFileFormat.ShownFileTypes.PFS0;
            UI.ShownFileTypes.XCI.Value = configurationFileFormat.ShownFileTypes.XCI;
            UI.ShownFileTypes.NCA.Value = configurationFileFormat.ShownFileTypes.NCA;
            UI.ShownFileTypes.NRO.Value = configurationFileFormat.ShownFileTypes.NRO;
            UI.ShownFileTypes.NSO.Value = configurationFileFormat.ShownFileTypes.NSO;
            UI.LanguageCode.Value = configurationFileFormat.LanguageCode;
            UI.BaseStyle.Value = configurationFileFormat.BaseStyle;
            UI.GameListViewMode.Value = configurationFileFormat.GameListViewMode;
            UI.ShowNames.Value = configurationFileFormat.ShowNames;
            UI.IsAscendingOrder.Value = configurationFileFormat.IsAscendingOrder;
            UI.GridSize.Value = configurationFileFormat.GridSize;
            UI.ApplicationSort.Value = configurationFileFormat.ApplicationSort;
            UI.StartFullscreen.Value = configurationFileFormat.StartFullscreen;
            UI.ShowConsole.Value = configurationFileFormat.ShowConsole;
            UI.WindowStartup.WindowSizeWidth.Value = configurationFileFormat.WindowStartup.WindowSizeWidth;
            UI.WindowStartup.WindowSizeHeight.Value = configurationFileFormat.WindowStartup.WindowSizeHeight;
            UI.WindowStartup.WindowPositionX.Value = configurationFileFormat.WindowStartup.WindowPositionX;
            UI.WindowStartup.WindowPositionY.Value = configurationFileFormat.WindowStartup.WindowPositionY;
            UI.WindowStartup.WindowMaximized.Value = configurationFileFormat.WindowStartup.WindowMaximized;
            Hid.EnableKeyboard.Value = configurationFileFormat.EnableKeyboard;
            Hid.EnableMouse.Value = configurationFileFormat.EnableMouse;
            Hid.Hotkeys.Value = configurationFileFormat.Hotkeys;
            Hid.InputConfig.Value = configurationFileFormat.InputConfig ?? [];

            Multiplayer.LanInterfaceId.Value = configurationFileFormat.MultiplayerLanInterfaceId;
            Multiplayer.Mode.Value = configurationFileFormat.MultiplayerMode;
            Multiplayer.DisableP2p.Value = configurationFileFormat.MultiplayerDisableP2p;
            Multiplayer.LdnPassphrase.Value = configurationFileFormat.MultiplayerLdnPassphrase;
            Multiplayer.LdnServer.Value = configurationFileFormat.LdnServer;
            
            Hacks.ShowDirtyHacks.Value = configurationFileFormat.ShowDirtyHacks;

            {
                DirtyHacks hacks = new (configurationFileFormat.DirtyHacks ?? []);

                Hacks.Xc2MenuSoftlockFix.Value = hacks.IsEnabled(DirtyHack.Xc2MenuSoftlockFix);

                Hacks.EnableShaderTranslationDelay.Value = hacks.IsEnabled(DirtyHack.ShaderTranslationDelay);
                Hacks.ShaderTranslationDelay.Value = hacks[DirtyHack.ShaderTranslationDelay].CoerceAtLeast(0);
            }

            if (configurationFileUpdated)
            {
                ToFileFormat().SaveConfig(configurationFilePath);

                Ryujinx.Common.Logging.Logger.Notice.Print(LogClass.Application, $"Configuration file updated to version {ConfigurationFileFormat.CurrentVersion}");
            }

            return;
            
            void Migrate(int newVer, Action<ConfigurationFileFormat> migrator)
            {
                if (configurationFileFormat.Version >= newVer) return;

                Ryujinx.Common.Logging.Logger.Warning?.Print(LogClass.Application, $"Outdated configuration version {configurationFileFormat.Version}, migrating to version {newVer}.");
                
                migrator(configurationFileFormat);

                configurationFileUpdated = true;
            }
        }
    }
}
