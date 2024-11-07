using Ryujinx.Ava.Common.Locale;
using static Ryujinx.Common.Utilities.XCIFileTrimmer;

namespace Ryujinx.Ava.UI.Helpers
{
    public static class XCIFileTrimmerOperationOutcomeExtensions
    {
        public static string ToLocalisedText(this OperationOutcome operationOutcome)
        {
            switch (operationOutcome)
            {
                case OperationOutcome.NoTrimNecessary:
                    return LocaleManager.Instance[LocaleKeys.TrimXCIFileNoTrimNecessary];
                case OperationOutcome.NoUntrimPossible:
                    return LocaleManager.Instance[LocaleKeys.TrimXCIFileNoUntrimPossible];
                case OperationOutcome.ReadOnlyFileCannotFix:
                    return LocaleManager.Instance[LocaleKeys.TrimXCIFileReadOnlyFileCannotFix];
                case OperationOutcome.FreeSpaceCheckFailed:
                    return LocaleManager.Instance[LocaleKeys.TrimXCIFileFreeSpaceCheckFailed];
                case OperationOutcome.InvalidXCIFile:
                    return LocaleManager.Instance[LocaleKeys.TrimXCIFileInvalidXCIFile];
                case OperationOutcome.FileIOWriteError:
                    return LocaleManager.Instance[LocaleKeys.TrimXCIFileFileIOWriteError];
                case OperationOutcome.FileSizeChanged:
                    return LocaleManager.Instance[LocaleKeys.TrimXCIFileFileSizeChanged];
                case OperationOutcome.Cancelled:
                    return LocaleManager.Instance[LocaleKeys.TrimXCIFileCancelled];
                case OperationOutcome.Undetermined:
                    return LocaleManager.Instance[LocaleKeys.TrimXCIFileFileUndertermined];
                case OperationOutcome.Successful:
                default:
                    return null;
            }
        }
    }
}