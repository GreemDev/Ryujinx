using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Ryujinx.Ava.Common.Locale;
using Ryujinx.Ava.UI.Windows;
using Ryujinx.Common;
using Ryujinx.Common.Configuration;
using Ryujinx.Common.Logging;
using Ryujinx.UI.Common.Configuration;
using System;

namespace Ryujinx.Ava.UI.Views.Main
{
    public partial class MainStatusBarView : UserControl
    {
        public MainWindow Window;

        public MainStatusBarView()
        {
            InitializeComponent();
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);

            if (VisualRoot is MainWindow window)
            {
                Window = window;
                DataContext = window.ViewModel;
                LocaleManager.Instance.LocaleChanged += () => Dispatcher.UIThread.Post(() =>
                {
                    if (Window.ViewModel.EnableNonGameRunningControls)
                        Window.LoadApplications();
                });
            }
        }

        private void VSyncMode_PointerReleased(object sender, PointerReleasedEventArgs e)
        {
            Window.ViewModel.ToggleVSyncMode();
            Logger.Info?.Print(LogClass.Application, $"VSync Mode toggled to: {Window.ViewModel.AppHost.Device.VSyncMode}");
        }

        private void DockedStatus_PointerReleased(object sender, PointerReleasedEventArgs e)
        {
            ConfigurationState.Instance.System.EnableDockedMode.Toggle();
        }

        private void AspectRatioStatus_OnClick(object sender, RoutedEventArgs e)
        {
            AspectRatio aspectRatio = ConfigurationState.Instance.Graphics.AspectRatio.Value;
            ConfigurationState.Instance.Graphics.AspectRatio.Value = (int)aspectRatio + 1 > Enum.GetNames(typeof(AspectRatio)).Length - 1 ? AspectRatio.Fixed4x3 : aspectRatio + 1;
        }

        private void Refresh_OnClick(object sender, RoutedEventArgs e) => Window.LoadApplications();

        private void VolumeStatus_OnPointerWheelChanged(object sender, PointerWheelEventArgs e)
        {
            // Change the volume by 5% at a time
            float newValue = Window.ViewModel.Volume + (float)e.Delta.Y * 0.05f;

            Window.ViewModel.Volume = newValue switch
            {
                < 0 => 0,
                > 1 => 1,
                _ => newValue,
            };

            e.Handled = true;
        }
    }
}
