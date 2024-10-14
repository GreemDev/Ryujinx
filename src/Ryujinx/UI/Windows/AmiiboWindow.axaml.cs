using Avalonia.Interactivity;
using Ryujinx.Ava.Common.Locale;
using Ryujinx.Ava.UI.ViewModels;
using Ryujinx.UI.Common.Models.Amiibo;

namespace Ryujinx.Ava.UI.Windows
{
    public partial class AmiiboWindow : StyleableWindow
    {
        public AmiiboWindow(bool showAll, string lastScannedAmiiboId, string titleId)
        {
            ViewModel = new AmiiboWindowViewModel(this, lastScannedAmiiboId, titleId)
            {
                ShowAllAmiibo = showAll,
            };

            DataContext = ViewModel;

            InitializeComponent();

            Title = App.FormatTitle(LocaleKeys.Amiibo);
        }

        public AmiiboWindow()
        {
            ViewModel = new AmiiboWindowViewModel(this, string.Empty, string.Empty);

            DataContext = ViewModel;

            InitializeComponent();

            if (Program.PreviewerDetached)
            {
                Title = App.FormatTitle(LocaleKeys.Amiibo);
            }
        }

        public bool IsScanned { get; set; }
        public AmiiboApi ScannedAmiibo { get; set; }
        public AmiiboWindowViewModel ViewModel { get; set; }

        private void ScanButton_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.AmiiboSelectedIndex > -1)
            {
                ScannedAmiibo = ViewModel.AmiiboList[ViewModel.AmiiboSelectedIndex];
                IsScanned = true;
                Close();
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            IsScanned = false;

            Close();
        }
    }
}
