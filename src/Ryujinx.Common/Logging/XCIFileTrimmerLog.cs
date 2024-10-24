using Ryujinx.Common.Utilities;

namespace Ryujinx.Common.Logging
{
    public class XCIFileTrimmerLog : XCIFileTrimmer.ILog
    {
        public virtual void Progress(long current, long total, string text, bool complete)
        {
        }

        public void Write(XCIFileTrimmer.LogType logType, string text)
        {
            switch (logType)
            {
                case XCIFileTrimmer.LogType.Info:
                    Logger.Notice.Print(LogClass.XCIFileTrimmer, text);
                    break;
                case XCIFileTrimmer.LogType.Warn:
                    Logger.Warning?.Print(LogClass.XCIFileTrimmer, text);
                    break;
                case XCIFileTrimmer.LogType.Error:
                    Logger.Error?.Print(LogClass.XCIFileTrimmer, text);
                    break;
                case XCIFileTrimmer.LogType.Progress:
                    Logger.Info?.Print(LogClass.XCIFileTrimmer, text);
                    break;
            }
        }
    }
}
