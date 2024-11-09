using Avalonia.Threading;
using Ryujinx.Ava.UI.ViewModels;

namespace Ryujinx.Ava.Common
{
    internal class XCIFileTrimmerWindowLog : Ryujinx.Common.Logging.XCIFileTrimmerLog
    {
        private readonly XCITrimmerViewModel _viewModel;

        public XCIFileTrimmerWindowLog(XCITrimmerViewModel viewModel)
        {
            _viewModel = viewModel;
        }

        public override void Progress(long current, long total, string text, bool complete)
        {
            Dispatcher.UIThread.Post(() =>
            {
                _viewModel.SetProgress((int)(current), (int)(total));
            });
        }
    }
}
