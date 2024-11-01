using Ryujinx.Common.Logging;
using System.Text;

namespace Ryujinx.HLE.HOS.Services.Ngct
{
    static class NgctServer
    {
        public static ResultCode Match(ServiceCtx context)
        {
            // NOTE: Service load the values of sys:set ngc.t!functionality_override_enabled and ngc.t!auto_reload_enabled in internal fields.
            //       Then it checks if ngc.t!functionality_override_enabled is enabled and if sys:set GetT is == 2.
            //       If both conditions are true, it does this following code. Since we currently stub it, it's fine to don't check settings service values.

            ulong bufferPosition = context.Request.PtrBuff[0].Position;
            ulong bufferSize = context.Request.PtrBuff[0].Size;

            bool isMatch = false;
            string text = string.Empty;

            if (bufferSize != 0)
            {
                if (bufferSize > 1024)
                {
                    isMatch = true;
                }
                else
                {
                    byte[] buffer = new byte[bufferSize];

                    context.Memory.Read(bufferPosition, buffer);

                    text = Encoding.ASCII.GetString(buffer);

                    // NOTE: Ngct use the archive 0100000000001034 which contains a words table. This is pushed on Chinese Switchs using Bcat service.
                    //       This call check if the string match with entries in the table and return the result if there is one (or more).
                    //       Since we don't want to hide bad words. It's fine to returns false here.

                    isMatch = false;
                }
            }

            Logger.Stub?.PrintStub(LogClass.ServiceNgct, new { isMatch, text });

            context.ResponseData.Write(isMatch);

            return ResultCode.Success;
        }

        public static ResultCode Filter(ServiceCtx context)
        {
            // NOTE: Service load the values of sys:set ngc.t!functionality_override_enabled and ngc.t!auto_reload_enabled in internal fields.
            //       Then it checks if ngc.t!functionality_override_enabled is enabled and if sys:set GetT is == 2.
            //       If both conditions are true, it does this following code. Since we currently stub it, it's fine to don't check settings service values.

            ulong bufferPosition = context.Request.PtrBuff[0].Position;
            ulong bufferSize = context.Request.PtrBuff[0].Size;

            ulong bufferFilteredPosition = context.Request.RecvListBuff[0].Position;

            string text = string.Empty;
            string textFiltered = string.Empty;

            if (bufferSize != 0)
            {
                if (bufferSize > 1024)
                {
                    textFiltered = new string('*', text.Length);

                    context.Memory.Write(bufferFilteredPosition, Encoding.ASCII.GetBytes(textFiltered));
                }
                else
                {
                    byte[] buffer = new byte[bufferSize];

                    context.Memory.Read(bufferPosition, buffer);

                    // NOTE: Ngct use the archive 0100000000001034 which contains a words table. This is pushed on Chinese Switchs using Bcat service.
                    //       This call check if the string contains words which are in the table then returns the same string with each matched words replaced by '*'.
                    //       Since we don't want to hide bad words. It's fine to returns the same string.

                    textFiltered = text = Encoding.ASCII.GetString(buffer);

                    context.Memory.Write(bufferFilteredPosition, buffer);
                }
            }

            Logger.Stub?.PrintStub(LogClass.ServiceNgct, new { text, textFiltered });

            return ResultCode.Success;
        }
    }
}
