using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Platform;
using Avalonia.Threading;
using DynamicData;
using FluentAvalonia.UI.Controls;
using FluentAvalonia.UI.Windowing;
using Gommon;
using LibHac.Tools.FsSystem;
using Ryujinx.Ava.Common;
using Ryujinx.Ava.Common.Locale;
using Ryujinx.Ava.Input;
using Ryujinx.Ava.UI.Applet;
using Ryujinx.Ava.UI.Helpers;
using Ryujinx.Ava.UI.ViewModels;
using Ryujinx.Common;
using Ryujinx.Common.Logging;
using Ryujinx.Graphics.Gpu;
using Ryujinx.HLE.FileSystem;
using Ryujinx.HLE.HOS;
using Ryujinx.HLE.HOS.Services.Account.Acc;
using Ryujinx.Input.HLE;
using Ryujinx.Input.SDL2;
using Ryujinx.UI.App.Common;
using Ryujinx.UI.Common;
using Ryujinx.UI.Common.Configuration;
using Ryujinx.UI.Common.Helper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;

namespace Ryujinx.Ava.UI.Windows
{
    public partial class MainWindow : StyleableAppWindow
    {
        internal static MainWindowViewModel MainWindowViewModel { get; private set; }
        
        public MainWindowViewModel ViewModel { get; }

        internal readonly AvaHostUIHandler UiHandler;

        private bool _isLoading;
        private bool _applicationsLoadedOnce;

        private UserChannelPersistence _userChannelPersistence;
        private static bool _deferLoad;
        private static string _launchPath;
        private static string _launchApplicationId;
        private static bool _startFullscreen;
        private IDisposable _appLibraryAppsSubscription;

        public VirtualFileSystem VirtualFileSystem { get; private set; }
        public ContentManager ContentManager { get; private set; }
        public AccountManager AccountManager { get; private set; }

        public LibHacHorizonManager LibHacHorizonManager { get; private set; }

        public InputManager InputManager { get; private set; }

        public SettingsWindow SettingsWindow { get; set; }

        public static bool ShowKeyErrorOnLoad { get; set; }
        public ApplicationLibrary ApplicationLibrary { get; set; }

        // Correctly size window when 'TitleBar' is enabled (Nov. 14, 2024)
        public readonly double TitleBarHeight;

        public readonly double StatusBarHeight;
        public readonly double MenuBarHeight;

        public MainWindow()
        {
            DataContext = ViewModel = MainWindowViewModel = new MainWindowViewModel
            {
                Window = this
            };

            InitializeComponent();
            Load();

            UiHandler = new AvaHostUIHandler(this);

            ViewModel.Title = App.FormatTitle();

            TitleBar.ExtendsContentIntoTitleBar = !ConfigurationState.Instance.ShowTitleBar;
            TitleBar.TitleBarHitTestType = (ConfigurationState.Instance.ShowTitleBar) ? TitleBarHitTestType.Simple : TitleBarHitTestType.Complex;

            // Correctly size window when 'TitleBar' is enabled (Nov. 14, 2024)
            TitleBarHeight = (ConfigurationState.Instance.ShowTitleBar ? TitleBar.Height : 0);

            // NOTE: Height of MenuBar and StatusBar is not usable here, since it would still be 0 at this point.
            StatusBarHeight = StatusBarView.StatusBar.MinHeight;
            MenuBarHeight = MenuBar.MinHeight;

            SetWindowSizePosition();

            if (Program.PreviewerDetached)
            {
                InputManager = new InputManager(new AvaloniaKeyboardDriver(this), new SDL2GamepadDriver());

                _ = this.GetObservable(IsActiveProperty).Subscribe(it => ViewModel.IsActive = it);
                this.ScalingChanged += OnScalingChanged;
            }
        }

        /// <summary>
        /// Event handler for detecting OS theme change when using "Follow OS theme" option
        /// </summary>
        private static void OnPlatformColorValuesChanged(object sender, PlatformColorValues e)
        {
            if (Application.Current is App app)
                app.ApplyConfiguredTheme(ConfigurationState.Instance.UI.BaseStyle);
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            if (PlatformSettings != null)
            {
                PlatformSettings.ColorValuesChanged -= OnPlatformColorValuesChanged;
            }
        }

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);

            NotificationHelper.SetNotificationManager(this);
        }

        private void OnScalingChanged(object sender, EventArgs e)
        {
            Program.DesktopScaleFactor = this.RenderScaling;
        }

        private void ApplicationLibrary_ApplicationCountUpdated(object sender, ApplicationCountUpdatedEventArgs e)
        {
            LocaleManager.Instance.UpdateAndGetDynamicValue(LocaleKeys.StatusBarGamesLoaded, e.NumAppsLoaded, e.NumAppsFound);

            Dispatcher.UIThread.Post(() =>
            {
                ViewModel.StatusBarProgressValue = e.NumAppsLoaded;
                ViewModel.StatusBarProgressMaximum = e.NumAppsFound;

                if (e.NumAppsFound == 0)
                {
                    StatusBarView.LoadProgressBar.IsVisible = false;
                }

                if (e.NumAppsLoaded == e.NumAppsFound)
                {
                    StatusBarView.LoadProgressBar.IsVisible = false;
                }
            });
        }

        private void ApplicationLibrary_LdnGameDataReceived(object sender, LdnGameDataReceivedEventArgs e)
        {
            Dispatcher.UIThread.Post(() =>
            {
                var ldnGameDataArray = e.LdnData;
                ViewModel.LastLdnGameData = ldnGameDataArray;
                foreach (var application in ViewModel.Applications)
                {
                    UpdateApplicationWithLdnData(application);
                }
                ViewModel.RefreshView();
            });
        }

        private void UpdateApplicationWithLdnData(ApplicationData application)
        {
            if (application.ControlHolder.ByteSpan.Length > 0 && ViewModel.LastLdnGameData != null)
            {
                IEnumerable<LdnGameData> ldnGameData = ViewModel.LastLdnGameData.Where(game => application.ControlHolder.Value.LocalCommunicationId.Items.Contains(Convert.ToUInt64(game.TitleId, 16)));

                application.PlayerCount = ldnGameData.Sum(game => game.PlayerCount);
                application.GameCount = ldnGameData.Count();
            }
            else
            {
                application.PlayerCount = 0;
                application.GameCount = 0;
            }
        }

        public void Application_Opened(object sender, ApplicationOpenedEventArgs args)
        {
            if (args.Application != null)
            {
                ViewModel.SelectedIcon = args.Application.Icon;

                ViewModel.LoadApplication(args.Application).Wait();
            }

            args.Handled = true;
        }

        internal static void DeferLoadApplication(string launchPathArg, string launchApplicationId, bool startFullscreenArg)
        {
            _deferLoad = true;
            _launchPath = launchPathArg;
            _launchApplicationId = launchApplicationId;
            _startFullscreen = startFullscreenArg;
        }

        public void SwitchToGameControl(bool startFullscreen = false)
        {
            ViewModel.ShowLoadProgress = false;
            ViewModel.ShowContent = true;
            ViewModel.IsLoadingIndeterminate = false;

            if (startFullscreen && ViewModel.WindowState is not WindowState.FullScreen)
            {
                ViewModel.ToggleFullscreen();
            }
        }

        public void ShowLoading(bool startFullscreen = false)
        {
            ViewModel.ShowContent = false;
            ViewModel.ShowLoadProgress = true;
            ViewModel.IsLoadingIndeterminate = true;

            if (startFullscreen && ViewModel.WindowState is not WindowState.FullScreen)
            {
                ViewModel.ToggleFullscreen();
            }
        }

        private void Initialize()
        {
            _userChannelPersistence = new UserChannelPersistence();
            VirtualFileSystem = VirtualFileSystem.CreateInstance();
            LibHacHorizonManager = new LibHacHorizonManager();
            ContentManager = new ContentManager(VirtualFileSystem);

            LibHacHorizonManager.InitializeFsServer(VirtualFileSystem);
            LibHacHorizonManager.InitializeArpServer();
            LibHacHorizonManager.InitializeBcatServer();
            LibHacHorizonManager.InitializeSystemClients();

            IntegrityCheckLevel checkLevel = ConfigurationState.Instance.System.EnableFsIntegrityChecks
                ? IntegrityCheckLevel.ErrorOnInvalid
                : IntegrityCheckLevel.None;

            ApplicationLibrary = new ApplicationLibrary(VirtualFileSystem, checkLevel)
            {
                DesiredLanguage = ConfigurationState.Instance.System.Language,
            };

            // Save data created before we supported extra data in directory save data will not work properly if
            // given empty extra data. Luckily some of that extra data can be created using the data from the
            // save data indexer, which should be enough to check access permissions for user saves.
            // Every single save data's extra data will be checked and fixed if needed each time the emulator is opened.
            // Consider removing this at some point in the future when we don't need to worry about old saves.
            VirtualFileSystem.FixExtraData(LibHacHorizonManager.RyujinxClient);

            AccountManager = new AccountManager(LibHacHorizonManager.RyujinxClient, CommandLineState.Profile);

            VirtualFileSystem.ReloadKeySet();

            ApplicationHelper.Initialize(VirtualFileSystem, AccountManager, LibHacHorizonManager.RyujinxClient);
        }

        [SupportedOSPlatform("linux")]
        private static async Task ShowVmMaxMapCountWarning()
        {
            LocaleManager.Instance.UpdateAndGetDynamicValue(LocaleKeys.LinuxVmMaxMapCountWarningTextSecondary,
                LinuxHelper.VmMaxMapCount, LinuxHelper.RecommendedVmMaxMapCount);

            await ContentDialogHelper.CreateWarningDialog(
                LocaleManager.Instance[LocaleKeys.LinuxVmMaxMapCountWarningTextPrimary],
                LocaleManager.Instance[LocaleKeys.LinuxVmMaxMapCountWarningTextSecondary]
            );
        }

        [SupportedOSPlatform("linux")]
        private static async Task ShowVmMaxMapCountDialog()
        {
            LocaleManager.Instance.UpdateAndGetDynamicValue(LocaleKeys.LinuxVmMaxMapCountDialogTextPrimary,
                LinuxHelper.RecommendedVmMaxMapCount);

            UserResult response = await ContentDialogHelper.ShowTextDialog(
                $"Ryujinx - {LocaleManager.Instance[LocaleKeys.LinuxVmMaxMapCountDialogTitle]}",
                LocaleManager.Instance[LocaleKeys.LinuxVmMaxMapCountDialogTextPrimary],
                LocaleManager.Instance[LocaleKeys.LinuxVmMaxMapCountDialogTextSecondary],
                LocaleManager.Instance[LocaleKeys.LinuxVmMaxMapCountDialogButtonUntilRestart],
                LocaleManager.Instance[LocaleKeys.LinuxVmMaxMapCountDialogButtonPersistent],
                LocaleManager.Instance[LocaleKeys.InputDialogNo],
                (int)Symbol.Help
            );

            int rc;

            switch (response)
            {
                case UserResult.Ok:
                    rc = LinuxHelper.RunPkExec($"echo {LinuxHelper.RecommendedVmMaxMapCount} > {LinuxHelper.VmMaxMapCountPath}");
                    if (rc == 0)
                    {
                        Logger.Info?.Print(LogClass.Application, $"vm.max_map_count set to {LinuxHelper.VmMaxMapCount} until the next restart.");
                    }
                    else
                    {
                        Logger.Error?.Print(LogClass.Application, $"Unable to change vm.max_map_count. Process exited with code: {rc}");
                    }
                    break;
                case UserResult.No:
                    rc = LinuxHelper.RunPkExec($"echo \"vm.max_map_count = {LinuxHelper.RecommendedVmMaxMapCount}\" > {LinuxHelper.SysCtlConfigPath} && sysctl -p {LinuxHelper.SysCtlConfigPath}");
                    if (rc == 0)
                    {
                        Logger.Info?.Print(LogClass.Application, $"vm.max_map_count set to {LinuxHelper.VmMaxMapCount}. Written to config: {LinuxHelper.SysCtlConfigPath}");
                    }
                    else
                    {
                        Logger.Error?.Print(LogClass.Application, $"Unable to write new value for vm.max_map_count to config. Process exited with code: {rc}");
                    }
                    break;
            }
        }

        private async Task CheckLaunchState()
        {
            if (OperatingSystem.IsLinux() && LinuxHelper.VmMaxMapCount < LinuxHelper.RecommendedVmMaxMapCount)
            {
                Logger.Warning?.Print(LogClass.Application, $"The value of vm.max_map_count is lower than {LinuxHelper.RecommendedVmMaxMapCount}. ({LinuxHelper.VmMaxMapCount})");

                if (LinuxHelper.PkExecPath is not null)
                {
                    await Dispatcher.UIThread.InvokeAsync(ShowVmMaxMapCountDialog);
                }
                else
                {
                    await Dispatcher.UIThread.InvokeAsync(ShowVmMaxMapCountWarning);
                }
            }

            if (!ShowKeyErrorOnLoad)
            {
                if (_deferLoad)
                {
                    _deferLoad = false;

                    if (ApplicationLibrary.TryGetApplicationsFromFile(_launchPath, out List<ApplicationData> applications))
                    {
                        ApplicationData applicationData;

                        if (_launchApplicationId != null)
                        {
                            applicationData = applications.Find(application => application.IdString == _launchApplicationId);

                            if (applicationData != null)
                            {
                                await ViewModel.LoadApplication(applicationData, _startFullscreen);
                            }
                            else
                            {
                                Logger.Error?.Print(LogClass.Application, $"Couldn't find requested application id '{_launchApplicationId}' in '{_launchPath}'.");
                                await Dispatcher.UIThread.InvokeAsync(async () => await UserErrorDialog.ShowUserErrorDialog(UserError.ApplicationNotFound));
                            }
                        }
                        else
                        {
                            applicationData = applications[0];
                            await ViewModel.LoadApplication(applicationData, _startFullscreen);
                        }
                    }
                    else
                    {
                        Logger.Error?.Print(LogClass.Application, $"Couldn't find any application in '{_launchPath}'.");
                        await Dispatcher.UIThread.InvokeAsync(async () => await UserErrorDialog.ShowUserErrorDialog(UserError.ApplicationNotFound));
                    }
                }
            }
            else
            {
                ShowKeyErrorOnLoad = false;

                await Dispatcher.UIThread.InvokeAsync(async () => await UserErrorDialog.ShowUserErrorDialog(UserError.NoKeys));
            }

            if (ConfigurationState.Instance.CheckUpdatesOnStart && !CommandLineState.HideAvailableUpdates && Updater.CanUpdate())
            {
                await Updater.BeginUpdateAsync()
                    .Catch(task => Logger.Error?.Print(LogClass.Application, $"Updater Error: {task.Exception}"));
            }
        }

        private void Load()
        {
            StatusBarView.VolumeStatus.Click += VolumeStatus_CheckedChanged;

            ApplicationGrid.ApplicationOpened += Application_Opened;

            ApplicationGrid.DataContext = ViewModel;

            ApplicationList.ApplicationOpened += Application_Opened;

            ApplicationList.DataContext = ViewModel;
        }

        private void SetWindowSizePosition()
        {
            if (!ConfigurationState.Instance.RememberWindowState)
            {
                // Correctly size window when 'TitleBar' is enabled (Nov. 14, 2024)
                ViewModel.WindowHeight = (720 + StatusBarHeight + MenuBarHeight + TitleBarHeight) * Program.WindowScaleFactor;
                ViewModel.WindowWidth = 1280 * Program.WindowScaleFactor;

                WindowState = WindowState.Normal;
                WindowStartupLocation = WindowStartupLocation.CenterScreen;

                return;
            }

            PixelPoint savedPoint = new(ConfigurationState.Instance.UI.WindowStartup.WindowPositionX,
                                        ConfigurationState.Instance.UI.WindowStartup.WindowPositionY);

            ViewModel.WindowHeight = ConfigurationState.Instance.UI.WindowStartup.WindowSizeHeight * Program.WindowScaleFactor;
            ViewModel.WindowWidth = ConfigurationState.Instance.UI.WindowStartup.WindowSizeWidth * Program.WindowScaleFactor;

            ViewModel.WindowState = ConfigurationState.Instance.UI.WindowStartup.WindowMaximized.Value ? WindowState.Maximized : WindowState.Normal;

            if (Screens.All.Any(screen => screen.Bounds.Contains(savedPoint)))
            {
                Position = savedPoint;
            }
            else
            {
                Logger.Warning?.Print(LogClass.Application, "Failed to find valid start-up coordinates. Defaulting to primary monitor center.");
                WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }
        }

        private void SaveWindowSizePosition()
        {
            ConfigurationState.Instance.UI.WindowStartup.WindowMaximized.Value = WindowState == WindowState.Maximized;

            // Only save rectangle properties if the window is not in a maximized state.
            if (WindowState != WindowState.Maximized)
            {
                // Since scaling is being applied to the loaded settings from disk (see SetWindowSizePosition() above), scaling should be removed from width/height before saving out to disk
                // as well - otherwise anyone not using a 1.0 scale factor their window will increase in size with every subsequent launch of the program when scaling is applied (Nov. 14, 2024)
                ConfigurationState.Instance.UI.WindowStartup.WindowSizeHeight.Value = (int)(Height / Program.WindowScaleFactor);
                ConfigurationState.Instance.UI.WindowStartup.WindowSizeWidth.Value = (int)(Width / Program.WindowScaleFactor);

                ConfigurationState.Instance.UI.WindowStartup.WindowPositionX.Value = Position.X;
                ConfigurationState.Instance.UI.WindowStartup.WindowPositionY.Value = Position.Y;
            }

            MainWindowViewModel.SaveConfig();
        }

        protected override void OnOpened(EventArgs e)
        {
            base.OnOpened(e);

            Initialize();

            PlatformSettings!.ColorValuesChanged += OnPlatformColorValuesChanged;

            ViewModel.Initialize(
                ContentManager,
                StorageProvider,
                ApplicationLibrary,
                VirtualFileSystem,
                AccountManager,
                InputManager,
                _userChannelPersistence,
                LibHacHorizonManager,
                UiHandler,
                ShowLoading,
                SwitchToGameControl,
                SetMainContent,
                this);

            ApplicationLibrary.ApplicationCountUpdated += ApplicationLibrary_ApplicationCountUpdated;
            _appLibraryAppsSubscription?.Dispose();
            _appLibraryAppsSubscription = ApplicationLibrary.Applications
                    .Connect()
                    .ObserveOn(SynchronizationContext.Current!)
                    .Bind(ViewModel.Applications)
                    .OnItemAdded(UpdateApplicationWithLdnData)
                    .Subscribe();
            ApplicationLibrary.LdnGameDataReceived += ApplicationLibrary_LdnGameDataReceived;

            ConfigurationState.Instance.Multiplayer.Mode.Event += (sender, evt) =>
            {
                _ = Task.Run(ViewModel.ApplicationLibrary.RefreshLdn);
            };

            ConfigurationState.Instance.Multiplayer.LdnServer.Event += (sender, evt) =>
            {
                _ = Task.Run(ViewModel.ApplicationLibrary.RefreshLdn);
            };
            _ = Task.Run(ViewModel.ApplicationLibrary.RefreshLdn);

            ViewModel.RefreshFirmwareStatus();

            // Load applications if no application was requested by the command line
            if (!_deferLoad)
            {
                LoadApplications();
            }

            _ = CheckLaunchState();
        }

        private void SetMainContent(Control content = null)
        {
            content ??= GameLibrary;

            if (MainContent.Content != content)
            {
                // Load applications while switching to the GameLibrary if we haven't done that yet
                if (!_applicationsLoadedOnce && content == GameLibrary)
                {
                    LoadApplications();
                }

                MainContent.Content = content;
            }
        }

        public static void UpdateGraphicsConfig()
        {
#pragma warning disable IDE0055 // Disable formatting
            GraphicsConfig.ResScale                   = ConfigurationState.Instance.Graphics.ResScale == -1 
                ? ConfigurationState.Instance.Graphics.ResScaleCustom 
                : ConfigurationState.Instance.Graphics.ResScale;
            GraphicsConfig.MaxAnisotropy              = ConfigurationState.Instance.Graphics.MaxAnisotropy;
            GraphicsConfig.ShadersDumpPath            = ConfigurationState.Instance.Graphics.ShadersDumpPath;
            GraphicsConfig.EnableShaderCache          = ConfigurationState.Instance.Graphics.EnableShaderCache;
            GraphicsConfig.EnableTextureRecompression = ConfigurationState.Instance.Graphics.EnableTextureRecompression;
            GraphicsConfig.EnableMacroHLE             = ConfigurationState.Instance.Graphics.EnableMacroHLE;
#pragma warning restore IDE0055
        }

        private void VolumeStatus_CheckedChanged(object sender, RoutedEventArgs e)
        {
            if (ViewModel.IsGameRunning && sender is ToggleSplitButton volumeSplitButton)
            {
                if (!volumeSplitButton.IsChecked)
                {
                    ViewModel.AppHost.Device.SetVolume(ViewModel.VolumeBeforeMute);
                }
                else
                {
                    ViewModel.VolumeBeforeMute = ViewModel.AppHost.Device.GetVolume();
                    ViewModel.AppHost.Device.SetVolume(0);
                }

                ViewModel.Volume = ViewModel.AppHost.Device.GetVolume();
            }
        }

        protected override void OnClosing(WindowClosingEventArgs e)
        {
            if (!ViewModel.IsClosing && ViewModel.AppHost != null && ConfigurationState.Instance.ShowConfirmExit)
            {
                e.Cancel = true;

                ConfirmExit();

                return;
            }

            ViewModel.IsClosing = true;

            if (ViewModel.AppHost != null)
            {
                ViewModel.AppHost.AppExit -= ViewModel.AppHost_AppExit;
                ViewModel.AppHost.AppExit += (_, _) =>
                {
                    ViewModel.AppHost = null;

                    Dispatcher.UIThread.Post(() =>
                    {
                        MainContent = null;

                        Close();
                    });
                };
                ViewModel.AppHost?.Stop();

                e.Cancel = true;

                return;
            }

            if (ConfigurationState.Instance.RememberWindowState)
            {
                SaveWindowSizePosition();
            }

            ApplicationLibrary.CancelLoading();
            InputManager.Dispose();
            _appLibraryAppsSubscription?.Dispose();
            Program.Exit();

            base.OnClosing(e);
        }

        private void ConfirmExit()
        {
            Dispatcher.UIThread.InvokeAsync(async () =>
            {
                ViewModel.IsClosing = await ContentDialogHelper.CreateExitDialog();

                if (ViewModel.IsClosing)
                {
                    Close();
                }
            });
        }

        public void LoadApplications()
        {
            _applicationsLoadedOnce = true;

            StatusBarView.LoadProgressBar.IsVisible = true;
            ViewModel.StatusBarProgressMaximum = 0;
            ViewModel.StatusBarProgressValue = 0;

            LocaleManager.Instance.UpdateAndGetDynamicValue(LocaleKeys.StatusBarGamesLoaded, 0, 0);

            ReloadGameList();
        }

        public void ToggleFileType(string fileType)
        {
            switch (fileType)
            {
                case "NSP":
                    ConfigurationState.Instance.UI.ShownFileTypes.NSP.Toggle();
                    break;
                case "PFS0":
                    ConfigurationState.Instance.UI.ShownFileTypes.PFS0.Toggle();
                    break;
                case "XCI":
                    ConfigurationState.Instance.UI.ShownFileTypes.XCI.Toggle();
                    break;
                case "NCA":
                    ConfigurationState.Instance.UI.ShownFileTypes.NCA.Toggle();
                    break;
                case "NRO":
                    ConfigurationState.Instance.UI.ShownFileTypes.NRO.Toggle();
                    break;
                case "NSO":
                    ConfigurationState.Instance.UI.ShownFileTypes.NSO.Toggle();
                    break;
                default:
                    throw new ArgumentOutOfRangeException(fileType);
            }

            ConfigurationState.Instance.ToFileFormat().SaveConfig(Program.ConfigurationPath);
            LoadApplications();
        }

        private void ReloadGameList()
        {
            if (_isLoading)
            {
                return;
            }

            _isLoading = true;

            Thread applicationLibraryThread = new(() =>
            {
                ApplicationLibrary.DesiredLanguage = ConfigurationState.Instance.System.Language;

                ApplicationLibrary.LoadApplications(ConfigurationState.Instance.UI.GameDirs);

                var autoloadDirs = ConfigurationState.Instance.UI.AutoloadDirs.Value;
                if (autoloadDirs.Count > 0)
                {
                    var updatesLoaded = ApplicationLibrary.AutoLoadTitleUpdates(autoloadDirs, out int updatesRemoved);
                    var dlcLoaded = ApplicationLibrary.AutoLoadDownloadableContents(autoloadDirs, out int dlcRemoved);

                    ShowNewContentAddedDialog(dlcLoaded, dlcRemoved, updatesLoaded, updatesRemoved);
                }

                _isLoading = false;
            })
            {
                Name = "GUI.ApplicationLibraryThread",
                IsBackground = true,
            };
            applicationLibraryThread.Start();
        }

        private void ShowNewContentAddedDialog(int numDlcAdded, int numDlcRemoved, int numUpdatesAdded, int numUpdatesRemoved)
        {
            string[] messages = {
                numDlcRemoved > 0 ? string.Format(LocaleManager.Instance[LocaleKeys.AutoloadDlcRemovedMessage], numDlcRemoved): null,
                numDlcAdded > 0 ? string.Format(LocaleManager.Instance[LocaleKeys.AutoloadDlcAddedMessage], numDlcAdded): null,
                numUpdatesRemoved > 0 ? string.Format(LocaleManager.Instance[LocaleKeys.AutoloadUpdateRemovedMessage], numUpdatesRemoved): null,
                numUpdatesAdded > 0 ? string.Format(LocaleManager.Instance[LocaleKeys.AutoloadUpdateAddedMessage], numUpdatesAdded) : null
            };

            string msg = String.Join("\r\n", messages);

            if (String.IsNullOrWhiteSpace(msg))
                return;

            Dispatcher.UIThread.InvokeAsync(async () =>
            {
                await ContentDialogHelper.ShowTextDialog(
                    LocaleManager.Instance[LocaleKeys.DialogConfirmationTitle],
                    msg,
                    string.Empty, 
                    string.Empty, 
                    string.Empty, 
                    LocaleManager.Instance[LocaleKeys.InputDialogOk], 
                    (int)Symbol.Checkmark);
            });
        }
    }
}
