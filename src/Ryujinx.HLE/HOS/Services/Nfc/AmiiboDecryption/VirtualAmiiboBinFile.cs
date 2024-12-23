using Ryujinx.Common.Logging;
using Ryujinx.Cpu;
using Ryujinx.HLE.HOS.Applets;
using Ryujinx.HLE.HOS.Services.Mii;
using Ryujinx.HLE.HOS.Services.Mii.Types;
using Ryujinx.HLE.HOS.Services.Nfc.Nfp;
using Ryujinx.HLE.HOS.Services.Nfc.Nfp.NfpManager;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Ryujinx.HLE.HOS.Services.Nfc.AmiiboDecryption
{
    public class VirtualAmiiboBinFile
    {
        public byte[] ApplicationBytes { get; set; }
        public byte[] MiiBytes { get; set; }
        public string InputBin { get; set; }
        public string NickName { get; set; }
        public int WriteCounter { get; set; }
        public DateTime LastWriteDate { get; set; }
        public byte[] TagUuid { get; set; }

        internal RegisterInfo GetRegisterInfo(ITickSource tickSource)
        {
            RegisterInfo info = new RegisterInfo();
            info.FirstWriteYear = (ushort)LastWriteDate.Year;
            info.FirstWriteMonth = (byte)LastWriteDate.Month;
            info.FirstWriteDay = (byte)LastWriteDate.Day;
            byte[] nicknameBytes = Encoding.UTF8.GetBytes(NickName);
            nicknameBytes.CopyTo(info.Nickname.AsSpan());
            byte[] newMiiBytes = new byte[92];
            Array.Copy(MiiBytes, 0, newMiiBytes, 0, 92);
            CharInfoBin charInfoBin = CharInfoBin.Parse(newMiiBytes);
            UtilityImpl utilityImpl = new UtilityImpl(tickSource);
            CharInfo Info = new();
            Info.SetFromStoreData(StoreData.BuildDefault(utilityImpl, 0));
            CharInfo charInfo = charInfoBin.ConvertToCharInfo(Info);
            info.MiiCharInfo = charInfo;
            return info;
        }
        public void UpdateApplicationArea(byte[] applicationAreaData)
        {
            ApplicationBytes = applicationAreaData;
            WriteCounter++;
            LastWriteDate = DateTime.Now;
            SaveFile();
        }

        public void UpdateNickName(string newNickName)
        {
            NickName = newNickName;
            SaveFile();
        }

        public bool SaveFile()
        {
            try
            {
                AmiiboBinReader.SaveBinFile(InputBin, this);
                VirtualAmiibo.VirtualAmiiboBinFile = null;
            }
            catch (Exception)
            {
                return false;
            }
            return true;
        }
    }
}
