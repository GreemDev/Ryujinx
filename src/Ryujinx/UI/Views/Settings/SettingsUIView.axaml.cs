using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using Ryujinx.Ava.Common.Locale;
using Ryujinx.Ava.UI.ViewModels;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Ryujinx.Ava.UI.Views.Settings
{
    public partial class SettingsUiView : UserControl
    {
        public SettingsViewModel ViewModel;

        public SettingsUiView()
        {
            InitializeComponent();
        }

        private async void AddGameDirButton_OnClick(object sender, RoutedEventArgs e)
        {
            string path = GameDirPathBox.Text;

            if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path) && !ViewModel.GameDirectories.Contains(path))
            {
                ViewModel.GameDirectories.Add(path);
                ViewModel.GameDirectoryChanged = true;
            }
            else
            {
                if (this.GetVisualRoot() is Window window)
                {
                    var result = await window.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
                    {
                        AllowMultiple = false,
                    });

                    if (result.Count > 0)
                    {
                        ViewModel.GameDirectories.Add(result[0].Path.LocalPath);
                        ViewModel.GameDirectoryChanged = true;
                    }
                }
            }
        }

        private void RemoveGameDirButton_OnClick(object sender, RoutedEventArgs e)
        {
            int oldIndex = GameDirsList.SelectedIndex;

            foreach (string path in new List<string>(GameDirsList.SelectedItems.Cast<string>()))
            {
                ViewModel.GameDirectories.Remove(path);
                ViewModel.GameDirectoryChanged = true;
            }

            if (GameDirsList.ItemCount > 0)
            {
                GameDirsList.SelectedIndex = oldIndex < GameDirsList.ItemCount ? oldIndex : 0;
            }
        }

        private async void AddAutoloadDirButton_OnClick(object sender, RoutedEventArgs e)
        {
            string path = AutoloadDirPathBox.Text;

            if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path) && !ViewModel.AutoloadDirectories.Contains(path))
            {
                ViewModel.AutoloadDirectories.Add(path);
                ViewModel.AutoloadDirectoryChanged = true;
            }
            else
            {
                if (this.GetVisualRoot() is Window window)
                {
                    var result = await window.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
                    {
                        AllowMultiple = false,
                    });

                    if (result.Count > 0)
                    {
                        ViewModel.AutoloadDirectories.Add(result[0].Path.LocalPath);
                        ViewModel.AutoloadDirectoryChanged = true;
                    }
                }
            }
        }

        private void RemoveAutoloadDirButton_OnClick(object sender, RoutedEventArgs e)
        {
            int oldIndex = AutoloadDirsList.SelectedIndex;

            foreach (string path in new List<string>(AutoloadDirsList.SelectedItems.Cast<string>()))
            {
                ViewModel.AutoloadDirectories.Remove(path);
                ViewModel.AutoloadDirectoryChanged = true;
            }

            if (AutoloadDirsList.ItemCount > 0)
            {
                AutoloadDirsList.SelectedIndex = oldIndex < AutoloadDirsList.ItemCount ? oldIndex : 0;
            }
        }
    }
}
