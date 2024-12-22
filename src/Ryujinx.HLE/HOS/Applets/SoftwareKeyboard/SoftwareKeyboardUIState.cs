using Ryujinx.HLE.UI;

namespace Ryujinx.HLE.HOS.Applets.SoftwareKeyboard
{
    /// <summary>
    /// TODO
    /// </summary>
    internal class SoftwareKeyboardUIState
    {
        public string InputText = string.Empty;
        public int CursorBegin;
        public int CursorEnd;
        public bool AcceptPressed;
        public bool CancelPressed;
        public bool OverwriteMode;
        public bool TypingEnabled = true;
        public bool ControllerEnabled = true;
        public int TextBoxBlinkCounter;

        public RenderingSurfaceInfo SurfaceInfo;
    }
}
