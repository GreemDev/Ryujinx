﻿using ARMeilleure;
using Ryujinx.Common;
using Ryujinx.Common.Configuration;
using Ryujinx.Common.Configuration.Hid;
using Ryujinx.Common.Configuration.Multiplayer;
using Ryujinx.Common.Logging;
using Ryujinx.HLE;
using Ryujinx.UI.Common.Configuration.System;
using Ryujinx.UI.Common.Helper;
using System.Collections.Generic;

namespace Ryujinx.UI.Common.Configuration
{
    public partial class ConfigurationState
    {
                /// <summary>
        /// UI configuration section
        /// </summary>
        public class UISection
        {
            public class Columns
            {
                public ReactiveObject<bool> FavColumn { get; private set; }
                public ReactiveObject<bool> IconColumn { get; private set; }
                public ReactiveObject<bool> AppColumn { get; private set; }
                public ReactiveObject<bool> DevColumn { get; private set; }
                public ReactiveObject<bool> VersionColumn { get; private set; }
                public ReactiveObject<bool> LdnInfoColumn { get; private set; }
                public ReactiveObject<bool> TimePlayedColumn { get; private set; }
                public ReactiveObject<bool> LastPlayedColumn { get; private set; }
                public ReactiveObject<bool> FileExtColumn { get; private set; }
                public ReactiveObject<bool> FileSizeColumn { get; private set; }
                public ReactiveObject<bool> PathColumn { get; private set; }

                public Columns()
                {
                    FavColumn = new ReactiveObject<bool>();
                    IconColumn = new ReactiveObject<bool>();
                    AppColumn = new ReactiveObject<bool>();
                    DevColumn = new ReactiveObject<bool>();
                    VersionColumn = new ReactiveObject<bool>();
                    LdnInfoColumn = new ReactiveObject<bool>();
                    TimePlayedColumn = new ReactiveObject<bool>();
                    LastPlayedColumn = new ReactiveObject<bool>();
                    FileExtColumn = new ReactiveObject<bool>();
                    FileSizeColumn = new ReactiveObject<bool>();
                    PathColumn = new ReactiveObject<bool>();
                }
            }

            public class ColumnSortSettings
            {
                public ReactiveObject<int> SortColumnId { get; private set; }
                public ReactiveObject<bool> SortAscending { get; private set; }

                public ColumnSortSettings()
                {
                    SortColumnId = new ReactiveObject<int>();
                    SortAscending = new ReactiveObject<bool>();
                }
            }

            /// <summary>
            /// Used to toggle which file types are shown in the UI
            /// </summary>
            public class ShownFileTypeSettings
            {
                public ReactiveObject<bool> NSP { get; private set; }
                public ReactiveObject<bool> PFS0 { get; private set; }
                public ReactiveObject<bool> XCI { get; private set; }
                public ReactiveObject<bool> NCA { get; private set; }
                public ReactiveObject<bool> NRO { get; private set; }
                public ReactiveObject<bool> NSO { get; private set; }

                public ShownFileTypeSettings()
                {
                    NSP = new ReactiveObject<bool>();
                    PFS0 = new ReactiveObject<bool>();
                    XCI = new ReactiveObject<bool>();
                    NCA = new ReactiveObject<bool>();
                    NRO = new ReactiveObject<bool>();
                    NSO = new ReactiveObject<bool>();
                }
            }

            // <summary>
            /// Determines main window start-up position, size and state
            ///<summary>
            public class WindowStartupSettings
            {
                public ReactiveObject<int> WindowSizeWidth { get; private set; }
                public ReactiveObject<int> WindowSizeHeight { get; private set; }
                public ReactiveObject<int> WindowPositionX { get; private set; }
                public ReactiveObject<int> WindowPositionY { get; private set; }
                public ReactiveObject<bool> WindowMaximized { get; private set; }

                public WindowStartupSettings()
                {
                    WindowSizeWidth = new ReactiveObject<int>();
                    WindowSizeHeight = new ReactiveObject<int>();
                    WindowPositionX = new ReactiveObject<int>();
                    WindowPositionY = new ReactiveObject<int>();
                    WindowMaximized = new ReactiveObject<bool>();
                }
            }

            /// <summary>
            /// Used to toggle columns in the GUI
            /// </summary>
            public Columns GuiColumns { get; private set; }

            /// <summary>
            /// Used to configure column sort settings in the GUI
            /// </summary>
            public ColumnSortSettings ColumnSort { get; private set; }

            /// <summary>
            /// A list of directories containing games to be used to load games into the games list
            /// </summary>
            public ReactiveObject<List<string>> GameDirs { get; private set; }

            /// <summary>
            /// A list of directories containing DLC/updates the user wants to autoload during library refreshes
            /// </summary>
            public ReactiveObject<List<string>> AutoloadDirs { get; private set; }

            /// <summary>
            /// A list of file types to be hidden in the games List
            /// </summary>
            public ShownFileTypeSettings ShownFileTypes { get; private set; }

            /// <summary>
            /// Determines main window start-up position, size and state
            /// </summary>
            public WindowStartupSettings WindowStartup { get; private set; }

            /// <summary>
            /// Language Code for the UI
            /// </summary>
            public ReactiveObject<string> LanguageCode { get; private set; }

            /// <summary>
            /// Selects the base style
            /// </summary>
            public ReactiveObject<string> BaseStyle { get; private set; }

            /// <summary>
            /// Start games in fullscreen mode
            /// </summary>
            public ReactiveObject<bool> StartFullscreen { get; private set; }

            /// <summary>
            /// Hide / Show Console Window
            /// </summary>
            public ReactiveObject<bool> ShowConsole { get; private set; }

            /// <summary>
            /// View Mode of the Game list
            /// </summary>
            public ReactiveObject<int> GameListViewMode { get; private set; }

            /// <summary>
            /// Show application name in Grid Mode
            /// </summary>
            public ReactiveObject<bool> ShowNames { get; private set; }

            /// <summary>
            /// Sets App Icon Size in Grid Mode
            /// </summary>
            public ReactiveObject<int> GridSize { get; private set; }

            /// <summary>
            /// Sorts Apps in Grid Mode
            /// </summary>
            public ReactiveObject<int> ApplicationSort { get; private set; }

            /// <summary>
            /// Sets if Grid is ordered in Ascending Order
            /// </summary>
            public ReactiveObject<bool> IsAscendingOrder { get; private set; }

            public UISection()
            {
                GuiColumns = new Columns();
                ColumnSort = new ColumnSortSettings();
                GameDirs = new ReactiveObject<List<string>>();
                AutoloadDirs = new ReactiveObject<List<string>>();
                ShownFileTypes = new ShownFileTypeSettings();
                WindowStartup = new WindowStartupSettings();
                BaseStyle = new ReactiveObject<string>();
                StartFullscreen = new ReactiveObject<bool>();
                GameListViewMode = new ReactiveObject<int>();
                ShowNames = new ReactiveObject<bool>();
                GridSize = new ReactiveObject<int>();
                ApplicationSort = new ReactiveObject<int>();
                IsAscendingOrder = new ReactiveObject<bool>();
                LanguageCode = new ReactiveObject<string>();
                ShowConsole = new ReactiveObject<bool>();
                ShowConsole.Event += static (_, e) => ConsoleHelper.SetConsoleWindowState(e.NewValue);
            }
        }

        /// <summary>
        /// Logger configuration section
        /// </summary>
        public class LoggerSection
        {
            /// <summary>
            /// Enables printing debug log messages
            /// </summary>
            public ReactiveObject<bool> EnableDebug { get; private set; }

            /// <summary>
            /// Enables printing stub log messages
            /// </summary>
            public ReactiveObject<bool> EnableStub { get; private set; }

            /// <summary>
            /// Enables printing info log messages
            /// </summary>
            public ReactiveObject<bool> EnableInfo { get; private set; }

            /// <summary>
            /// Enables printing warning log messages
            /// </summary>
            public ReactiveObject<bool> EnableWarn { get; private set; }

            /// <summary>
            /// Enables printing error log messages
            /// </summary>
            public ReactiveObject<bool> EnableError { get; private set; }

            /// <summary>
            /// Enables printing trace log messages
            /// </summary>
            public ReactiveObject<bool> EnableTrace { get; private set; }

            /// <summary>
            /// Enables printing guest log messages
            /// </summary>
            public ReactiveObject<bool> EnableGuest { get; private set; }

            /// <summary>
            /// Enables printing FS access log messages
            /// </summary>
            public ReactiveObject<bool> EnableFsAccessLog { get; private set; }

            /// <summary>
            /// Controls which log messages are written to the log targets
            /// </summary>
            public ReactiveObject<LogClass[]> FilteredClasses { get; private set; }

            /// <summary>
            /// Enables or disables logging to a file on disk
            /// </summary>
            public ReactiveObject<bool> EnableFileLog { get; private set; }

            /// <summary>
            /// Controls which OpenGL log messages are recorded in the log
            /// </summary>
            public ReactiveObject<GraphicsDebugLevel> GraphicsDebugLevel { get; private set; }

            public LoggerSection()
            {
                EnableDebug = new ReactiveObject<bool>();
                EnableDebug.LogChangesToValue(nameof(EnableDebug));
                EnableStub = new ReactiveObject<bool>();
                EnableInfo = new ReactiveObject<bool>();
                EnableWarn = new ReactiveObject<bool>();
                EnableError = new ReactiveObject<bool>();
                EnableTrace = new ReactiveObject<bool>();
                EnableGuest = new ReactiveObject<bool>();
                EnableFsAccessLog = new ReactiveObject<bool>();
                FilteredClasses = new ReactiveObject<LogClass[]>();
                EnableFileLog = new ReactiveObject<bool>();
                EnableFileLog.LogChangesToValue(nameof(EnableFileLog));
                GraphicsDebugLevel = new ReactiveObject<GraphicsDebugLevel>();
            }
        }

        /// <summary>
        /// System configuration section
        /// </summary>
        public class SystemSection
        {
            /// <summary>
            /// Change System Language
            /// </summary>
            public ReactiveObject<Language> Language { get; private set; }

            /// <summary>
            /// Change System Region
            /// </summary>
            public ReactiveObject<Region> Region { get; private set; }

            /// <summary>
            /// Change System TimeZone
            /// </summary>
            public ReactiveObject<string> TimeZone { get; private set; }

            /// <summary>
            /// System Time Offset in Seconds
            /// </summary>
            public ReactiveObject<long> SystemTimeOffset { get; private set; }

            /// <summary>
            /// Enables or disables Docked Mode
            /// </summary>
            public ReactiveObject<bool> EnableDockedMode { get; private set; }

            /// <summary>
            /// Enables or disables persistent profiled translation cache
            /// </summary>
            public ReactiveObject<bool> EnablePtc { get; private set; }

            /// <summary>
            /// Enables or disables low-power persistent profiled translation cache loading
            /// </summary>
            public ReactiveObject<bool> EnableLowPowerPtc { get; private set; }

            /// <summary>
            /// Enables or disables guest Internet access
            /// </summary>
            public ReactiveObject<bool> EnableInternetAccess { get; private set; }

            /// <summary>
            /// Enables integrity checks on Game content files
            /// </summary>
            public ReactiveObject<bool> EnableFsIntegrityChecks { get; private set; }

            /// <summary>
            /// Enables FS access log output to the console. Possible modes are 0-3
            /// </summary>
            public ReactiveObject<int> FsGlobalAccessLogMode { get; private set; }

            /// <summary>
            /// The selected audio backend
            /// </summary>
            public ReactiveObject<AudioBackend> AudioBackend { get; private set; }

            /// <summary>
            /// The audio backend volume
            /// </summary>
            public ReactiveObject<float> AudioVolume { get; private set; }

            /// <summary>
            /// The selected memory manager mode
            /// </summary>
            public ReactiveObject<MemoryManagerMode> MemoryManagerMode { get; private set; }

            /// <summary>
            /// Defines the amount of RAM available on the emulated system, and how it is distributed
            /// </summary>
            public ReactiveObject<MemoryConfiguration> DramSize { get; private set; }

            /// <summary>
            /// Enable or disable ignoring missing services
            /// </summary>
            public ReactiveObject<bool> IgnoreMissingServices { get; private set; }

            /// <summary>
            /// Uses Hypervisor over JIT if available
            /// </summary>
            public ReactiveObject<bool> UseHypervisor { get; private set; }

            public SystemSection()
            {
                Language = new ReactiveObject<Language>();
                Language.LogChangesToValue(nameof(Language));
                Region = new ReactiveObject<Region>();
                Region.LogChangesToValue(nameof(Region));
                TimeZone = new ReactiveObject<string>();
                TimeZone.LogChangesToValue(nameof(TimeZone));
                SystemTimeOffset = new ReactiveObject<long>();
                SystemTimeOffset.LogChangesToValue(nameof(SystemTimeOffset));
                EnableDockedMode = new ReactiveObject<bool>();
                EnableDockedMode.LogChangesToValue(nameof(EnableDockedMode));
                EnablePtc = new ReactiveObject<bool>();
                EnablePtc.LogChangesToValue(nameof(EnablePtc));
                EnableLowPowerPtc = new ReactiveObject<bool>();
                EnableLowPowerPtc.LogChangesToValue(nameof(EnableLowPowerPtc));
                EnableLowPowerPtc.Event += (_, evnt) 
                    => Optimizations.LowPower = evnt.NewValue;
                EnableInternetAccess = new ReactiveObject<bool>();
                EnableInternetAccess.LogChangesToValue(nameof(EnableInternetAccess));
                EnableFsIntegrityChecks = new ReactiveObject<bool>();
                EnableFsIntegrityChecks.LogChangesToValue(nameof(EnableFsIntegrityChecks));
                FsGlobalAccessLogMode = new ReactiveObject<int>();
                FsGlobalAccessLogMode.LogChangesToValue(nameof(FsGlobalAccessLogMode));
                AudioBackend = new ReactiveObject<AudioBackend>();
                AudioBackend.LogChangesToValue(nameof(AudioBackend));
                MemoryManagerMode = new ReactiveObject<MemoryManagerMode>();
                MemoryManagerMode.LogChangesToValue(nameof(MemoryManagerMode));
                DramSize = new ReactiveObject<MemoryConfiguration>();
                DramSize.LogChangesToValue(nameof(DramSize));
                IgnoreMissingServices = new ReactiveObject<bool>();
                IgnoreMissingServices.LogChangesToValue(nameof(IgnoreMissingServices));
                AudioVolume = new ReactiveObject<float>();
                AudioVolume.LogChangesToValue(nameof(AudioVolume));
                UseHypervisor = new ReactiveObject<bool>();
                UseHypervisor.LogChangesToValue(nameof(UseHypervisor));
            }
        }

        /// <summary>
        /// Hid configuration section
        /// </summary>
        public class HidSection
        {
            /// <summary>
            /// Enable or disable keyboard support (Independent from controllers binding)
            /// </summary>
            public ReactiveObject<bool> EnableKeyboard { get; private set; }

            /// <summary>
            /// Enable or disable mouse support (Independent from controllers binding)
            /// </summary>
            public ReactiveObject<bool> EnableMouse { get; private set; }

            /// <summary>
            /// Hotkey Keyboard Bindings
            /// </summary>
            public ReactiveObject<KeyboardHotkeys> Hotkeys { get; private set; }

            /// <summary>
            /// Input device configuration.
            /// NOTE: This ReactiveObject won't issue an event when the List has elements added or removed.
            /// TODO: Implement a ReactiveList class.
            /// </summary>
            public ReactiveObject<List<InputConfig>> InputConfig { get; private set; }

            public HidSection()
            {
                EnableKeyboard = new ReactiveObject<bool>();
                EnableMouse = new ReactiveObject<bool>();
                Hotkeys = new ReactiveObject<KeyboardHotkeys>();
                InputConfig = new ReactiveObject<List<InputConfig>>();
            }
        }

        /// <summary>
        /// Graphics configuration section
        /// </summary>
        public class GraphicsSection
        {
            /// <summary>
            /// Whether or not backend threading is enabled. The "Auto" setting will determine whether threading should be enabled at runtime.
            /// </summary>
            public ReactiveObject<BackendThreading> BackendThreading { get; private set; }

            /// <summary>
            /// Max Anisotropy. Values range from 0 - 16. Set to -1 to let the game decide.
            /// </summary>
            public ReactiveObject<float> MaxAnisotropy { get; private set; }

            /// <summary>
            /// Aspect Ratio applied to the renderer window.
            /// </summary>
            public ReactiveObject<AspectRatio> AspectRatio { get; private set; }

            /// <summary>
            /// Resolution Scale. An integer scale applied to applicable render targets. Values 1-4, or -1 to use a custom floating point scale instead.
            /// </summary>
            public ReactiveObject<int> ResScale { get; private set; }

            /// <summary>
            /// Custom Resolution Scale. A custom floating point scale applied to applicable render targets. Only active when Resolution Scale is -1.
            /// </summary>
            public ReactiveObject<float> ResScaleCustom { get; private set; }

            /// <summary>
            /// Dumps shaders in this local directory
            /// </summary>
            public ReactiveObject<string> ShadersDumpPath { get; private set; }

            /// <summary>
            /// Enables or disables Vertical Sync
            /// </summary>
            public ReactiveObject<bool> EnableVsync { get; private set; }

            /// <summary>
            /// Enables or disables Shader cache
            /// </summary>
            public ReactiveObject<bool> EnableShaderCache { get; private set; }

            /// <summary>
            /// Enables or disables texture recompression
            /// </summary>
            public ReactiveObject<bool> EnableTextureRecompression { get; private set; }

            /// <summary>
            /// Enables or disables Macro high-level emulation
            /// </summary>
            public ReactiveObject<bool> EnableMacroHLE { get; private set; }

            /// <summary>
            /// Enables or disables color space passthrough, if available.
            /// </summary>
            public ReactiveObject<bool> EnableColorSpacePassthrough { get; private set; }

            /// <summary>
            /// Graphics backend
            /// </summary>
            public ReactiveObject<GraphicsBackend> GraphicsBackend { get; private set; }

            /// <summary>
            /// Applies anti-aliasing to the renderer.
            /// </summary>
            public ReactiveObject<AntiAliasing> AntiAliasing { get; private set; }

            /// <summary>
            /// Sets the framebuffer upscaling type.
            /// </summary>
            public ReactiveObject<ScalingFilter> ScalingFilter { get; private set; }

            /// <summary>
            /// Sets the framebuffer upscaling level.
            /// </summary>
            public ReactiveObject<int> ScalingFilterLevel { get; private set; }

            /// <summary>
            /// Preferred GPU
            /// </summary>
            public ReactiveObject<string> PreferredGpu { get; private set; }

            public GraphicsSection()
            {
                BackendThreading = new ReactiveObject<BackendThreading>();
                BackendThreading.LogChangesToValue(nameof(BackendThreading));
                ResScale = new ReactiveObject<int>();
                ResScale.LogChangesToValue(nameof(ResScale));
                ResScaleCustom = new ReactiveObject<float>();
                ResScaleCustom.LogChangesToValue(nameof(ResScaleCustom));
                MaxAnisotropy = new ReactiveObject<float>();
                MaxAnisotropy.LogChangesToValue(nameof(MaxAnisotropy));
                AspectRatio = new ReactiveObject<AspectRatio>();
                AspectRatio.LogChangesToValue(nameof(AspectRatio));
                ShadersDumpPath = new ReactiveObject<string>();
                EnableVsync = new ReactiveObject<bool>();
                EnableVsync.LogChangesToValue(nameof(EnableVsync));
                EnableShaderCache = new ReactiveObject<bool>();
                EnableShaderCache.LogChangesToValue(nameof(EnableShaderCache));
                EnableTextureRecompression = new ReactiveObject<bool>();
                EnableTextureRecompression.LogChangesToValue(nameof(EnableTextureRecompression));
                GraphicsBackend = new ReactiveObject<GraphicsBackend>();
                GraphicsBackend.LogChangesToValue(nameof(GraphicsBackend));
                PreferredGpu = new ReactiveObject<string>();
                PreferredGpu.LogChangesToValue(nameof(PreferredGpu));
                EnableMacroHLE = new ReactiveObject<bool>();
                EnableMacroHLE.LogChangesToValue(nameof(EnableMacroHLE));
                EnableColorSpacePassthrough = new ReactiveObject<bool>();
                EnableColorSpacePassthrough.LogChangesToValue(nameof(EnableColorSpacePassthrough));
                AntiAliasing = new ReactiveObject<AntiAliasing>();
                AntiAliasing.LogChangesToValue(nameof(AntiAliasing));
                ScalingFilter = new ReactiveObject<ScalingFilter>();
                ScalingFilter.LogChangesToValue(nameof(ScalingFilter));
                ScalingFilterLevel = new ReactiveObject<int>();
                ScalingFilterLevel.LogChangesToValue(nameof(ScalingFilterLevel));
            }
        }

        /// <summary>
        /// Multiplayer configuration section
        /// </summary>
        public class MultiplayerSection
        {
            /// <summary>
            /// GUID for the network interface used by LAN (or 0 for default)
            /// </summary>
            public ReactiveObject<string> LanInterfaceId { get; private set; }

            /// <summary>
            /// Multiplayer Mode
            /// </summary>
            public ReactiveObject<MultiplayerMode> Mode { get; private set; }

            /// <summary>
            /// Disable P2P
            /// </summary>
            public ReactiveObject<bool> DisableP2p { get; private set; }

            /// <summary>
            /// LDN PassPhrase
            /// </summary>
            public ReactiveObject<string> LdnPassphrase { get; private set; }

            /// <summary>
            /// LDN Server
            /// </summary>
            public ReactiveObject<string> LdnServer { get; private set; }

            public MultiplayerSection()
            {
                LanInterfaceId = new ReactiveObject<string>();
                Mode = new ReactiveObject<MultiplayerMode>();
                Mode.LogChangesToValue(nameof(MultiplayerMode));
                DisableP2p = new ReactiveObject<bool>();
                DisableP2p.LogChangesToValue(nameof(DisableP2p));
                LdnPassphrase = new ReactiveObject<string>();
                LdnPassphrase.LogChangesToValue(nameof(LdnPassphrase));
                LdnServer = new ReactiveObject<string>();
                LdnServer.LogChangesToValue(nameof(LdnServer));
            }
        }

        /// <summary>
        /// The default configuration instance
        /// </summary>
        public static ConfigurationState Instance { get; private set; }

        /// <summary>
        /// The UI section
        /// </summary>
        public UISection UI { get; private set; }

        /// <summary>
        /// The Logger section
        /// </summary>
        public LoggerSection Logger { get; private set; }

        /// <summary>
        /// The System section
        /// </summary>
        public SystemSection System { get; private set; }

        /// <summary>
        /// The Graphics section
        /// </summary>
        public GraphicsSection Graphics { get; private set; }

        /// <summary>
        /// The Hid section
        /// </summary>
        public HidSection Hid { get; private set; }

        /// <summary>
        /// The Multiplayer section
        /// </summary>
        public MultiplayerSection Multiplayer { get; private set; }

        /// <summary>
        /// Enables or disables Discord Rich Presence
        /// </summary>
        public ReactiveObject<bool> EnableDiscordIntegration { get; private set; }

        /// <summary>
        /// Checks for updates when Ryujinx starts when enabled
        /// </summary>
        public ReactiveObject<bool> CheckUpdatesOnStart { get; private set; }

        /// <summary>
        /// Show "Confirm Exit" Dialog
        /// </summary>
        public ReactiveObject<bool> ShowConfirmExit { get; private set; }

        /// <summary>
        /// Ignore Applet
        /// </summary>
        public ReactiveObject<bool> IgnoreApplet { get; private set; }

        /// <summary>
        /// Enables or disables save window size, position and state on close.
        /// </summary>
        public ReactiveObject<bool> RememberWindowState { get; private set; }

        /// <summary>
        /// Enables or disables the redesigned title bar
        /// </summary>
        public ReactiveObject<bool> ShowTitleBar { get; private set; }

        /// <summary>
        /// Enables hardware-accelerated rendering for Avalonia
        /// </summary>
        public ReactiveObject<bool> EnableHardwareAcceleration { get; private set; }

        /// <summary>
        /// Hide Cursor on Idle
        /// </summary>
        public ReactiveObject<HideCursorMode> HideCursor { get; private set; }

        private ConfigurationState()
        {
            UI = new UISection();
            Logger = new LoggerSection();
            System = new SystemSection();
            Graphics = new GraphicsSection();
            Hid = new HidSection();
            Multiplayer = new MultiplayerSection();
            EnableDiscordIntegration = new ReactiveObject<bool>();
            CheckUpdatesOnStart = new ReactiveObject<bool>();
            ShowConfirmExit = new ReactiveObject<bool>();
            IgnoreApplet = new ReactiveObject<bool>();
            IgnoreApplet.LogChangesToValue(nameof(IgnoreApplet));
            RememberWindowState = new ReactiveObject<bool>();
            ShowTitleBar = new ReactiveObject<bool>();
            EnableHardwareAcceleration = new ReactiveObject<bool>();
            HideCursor = new ReactiveObject<HideCursorMode>();
        }
    }
}
