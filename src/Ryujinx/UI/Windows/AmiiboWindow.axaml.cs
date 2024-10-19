using Avalonia.Interactivity;
using Ryujinx.Ava.Common.Locale;
using Ryujinx.Ava.UI.ViewModels;
using Ryujinx.UI.Common.Models.Amiibo;

namespace Ryujinx.Ava.UI.Windows
{
    public partial class AmiiboWindow : StyleableAppWindow
    {
        public AmiiboWindow(bool showAll, string lastScannedAmiiboId, string titleId)
        {
            DataContext = ViewModel = new AmiiboWindowViewModel(this, lastScannedAmiiboId, titleId)
            {
                ShowAllAmiibo = showAll,
            };

            InitializeComponent();

            Title = App.FormatTitle(LocaleKeys.Amiibo);
        }

        public AmiiboWindow()
        {
            DataContext = ViewModel = new AmiiboWindowViewModel(this, string.Empty, string.Empty);

            InitializeComponent();

            if (Program.PreviewerDetached)
            {
                Title = App.FormatTitle(LocaleKeys.Amiibo);
            }
        }

        public bool IsScanned { get; set; }
        public AmiiboApi ScannedAmiibo { get; set; }
        public AmiiboWindowViewModel ViewModel;

        private void ScanButton_Click(object sender, RoutedEventArgs e) => ViewModel.Scan();

        private void CancelButton_Click(object sender, RoutedEventArgs e) => ViewModel.Cancel();
    }
}
