using Avalonia.Controls;
using Avalonia.Interactivity;
using Ryujinx.Ava.UI.ViewModels;
using Ryujinx.UI.Common.Configuration;

namespace Ryujinx.Ava.UI.Views.Settings
{
    public partial class SettingsHacksView : UserControl
    {
        public SettingsViewModel ViewModel;
        
        public SettingsHacksView()
        {
            InitializeComponent();
        }
    }
}
