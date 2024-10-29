using Avalonia.Threading;
using Ryujinx.Ava.UI.ViewModels;

namespace Ryujinx.Ava.Common
{
    internal class XCIFileTrimmerMainWindowLog : Ryujinx.Common.Logging.XCIFileTrimmerLog
    {
        private readonly MainWindowViewModel _viewModel;

        public XCIFileTrimmerMainWindowLog(MainWindowViewModel viewModel)
        {
            _viewModel = viewModel;
        }

        public override void Progress(long current, long total, string text, bool complete)
        {
            Dispatcher.UIThread.Post(() =>
            {
                _viewModel.StatusBarProgressMaximum = (int)(total);
                _viewModel.StatusBarProgressValue = (int)(current);
            });
        }
    }
}
