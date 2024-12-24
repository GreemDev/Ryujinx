using Avalonia.Media;
using Ryujinx.Ava.UI.Windows;
using Ryujinx.HLE.UI;
using System;

namespace Ryujinx.Ava.UI.Applet
{
    class AvaloniaHostUITheme(MainWindow parent) : IHostUITheme
    {
        public string FontFamily { get; } = OperatingSystem.IsWindows() && OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000) ? "Segoe UI Variable" : parent.FontFamily.Name;

        public ThemeColor DefaultBackgroundColor { get; } = BrushToThemeColor(parent.Background);
        public ThemeColor DefaultForegroundColor { get; } = BrushToThemeColor(parent.Foreground);
        public ThemeColor DefaultBorderColor { get; } = BrushToThemeColor(parent.BorderBrush);
        public ThemeColor SelectionBackgroundColor { get; } = BrushToThemeColor(parent.ViewControls.SearchBox.SelectionBrush);
        public ThemeColor SelectionForegroundColor { get; } = BrushToThemeColor(parent.ViewControls.SearchBox.SelectionForegroundBrush);

        private static ThemeColor BrushToThemeColor(IBrush brush)
        {
            if (brush is SolidColorBrush solidColor)
            {
                return new ThemeColor((float)solidColor.Color.A / 255,
                    (float)solidColor.Color.R / 255,
                    (float)solidColor.Color.G / 255,
                    (float)solidColor.Color.B / 255);
            }

            return new ThemeColor();
        }
    }
}
