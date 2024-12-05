using Ryujinx.Common.Configuration;
using Ryujinx.HLE.HOS.Services.Nfc.Nfp;
using Ryujinx.HLE.HOS.Services.Nfc.Nfp.NfpManager;
using Ryujinx.HLE.HOS.Tamper;
using System;
using System.IO;
using System.Text;

namespace Ryujinx.HLE.HOS.Services.Nfc.Bin
{
    public class AmiiboBinReader
    {
        private static byte CalculateBCC0(byte[] uid)
        {
            return (byte)(uid[0] ^ uid[1] ^ uid[2] ^ 0x88);
        }

        private static byte CalculateBCC1(byte[] uid)
        {
            return (byte)(uid[3] ^ uid[4] ^ uid[5] ^ uid[6]);
        }

        public static VirtualAmiiboFile ReadBinFile(byte[] fileBytes)
        {
            string keyRetailBinPath = GetKeyRetailBinPath();
            if (string.IsNullOrEmpty(keyRetailBinPath))
            {
                return new VirtualAmiiboFile();
            }

            byte[] initialCounter = new byte[16];

            const int totalPages = 135;
            const int pageSize = 4;
            const int totalBytes = totalPages * pageSize;

            if (fileBytes.Length < totalBytes)
            {
                return new VirtualAmiiboFile();
            }

            AmiiboDecrypter amiiboDecryptor = new AmiiboDecrypter(keyRetailBinPath);
            byte[] decryptedFileBytes = amiiboDecryptor.DecryptAmiiboData(fileBytes, initialCounter);

            if (decryptedFileBytes.Length != totalBytes)
            {
                Array.Resize(ref decryptedFileBytes, totalBytes);
            }

            byte[] uid = new byte[7];
            Array.Copy(fileBytes, 0, uid, 0, 7);

            byte bcc0 = CalculateBCC0(uid);
            byte bcc1 = CalculateBCC1(uid);

            LogDebugData(uid, bcc0, bcc1);

            byte[] nickNameBytes = new byte[20];
            byte[] titleId = new byte[8];
            byte[] usedCharacter = new byte[2];
            byte[] variation = new byte[2];
            byte[] amiiboID = new byte[2];
            byte[] setID = new byte[1];
            byte[] initDate = new byte[2];
            byte[] writeDate = new byte[2];
            byte[] writeCounter = new byte[2];
            byte[] appId = new byte[8];
            byte[] settingsBytes = new byte[2];
            byte formData = 0;
            byte[] applicationAreas = new byte[216];

            for (int page = 0; page < totalPages; page++)
            {
                int pageStartIdx = page * pageSize;
                byte[] pageData = new byte[4];
                bool isEncrypted = IsPageEncrypted(page);
                byte[] sourceBytes = isEncrypted ? decryptedFileBytes : fileBytes;
                if (pageStartIdx + pageSize > sourceBytes.Length)
                {
                    break;
                }
                Array.Copy(sourceBytes, pageStartIdx, pageData, 0, 4);

                switch (page)
                {
                    case 0:
                        break;

                    case 2:
                        byte internalValue = pageData[1];
                        break;

                    case 5:
                        Array.Copy(pageData, 0, settingsBytes, 0, 2);
                        break;

                    case 6:
                        Array.Copy(pageData, 0, initDate, 0, 2);
                        Array.Copy(pageData, 2, writeDate, 0, 2);
                        break;

                    case >= 8 and <= 12:
                        int nickNameOffset = (page - 8) * 4;
                        Array.Copy(pageData, 0, nickNameBytes, nickNameOffset, 4);
                        break;

                    case 21:
                        Array.Copy(pageData, 0, usedCharacter, 0, 2);
                        Array.Copy(pageData, 2, variation, 0, 2);
                        break;

                    case 22:
                        Array.Copy(pageData, 0, amiiboID, 0, 2);
                        setID[0] = pageData[2];
                        formData = pageData[3];
                        break;

                    case 40:
                    case 41:
                        int appIdOffset = (page - 40) * 4;
                        Array.Copy(decryptedFileBytes, pageStartIdx, appId, appIdOffset, 4);
                        break;

                    case 64:
                    case 65:
                        int titleIdOffset = (page - 64) * 4;
                        Array.Copy(sourceBytes, pageStartIdx, titleId, titleIdOffset, 4);
                        break;

                    case 66:
                        Array.Copy(pageData, 0, writeCounter, 0, 2);
                        break;

                    case >= 76 and <= 129:
                        int appAreaOffset = (page - 76) * 4;
                        if (appAreaOffset + 4 <= applicationAreas.Length)
                        {
                            Array.Copy(pageData, 0, applicationAreas, appAreaOffset, 4);
                        }
                        break;
                }
            }

            string usedCharacterStr = BitConverter.ToString(usedCharacter).Replace("-", "");
            string variationStr = BitConverter.ToString(variation).Replace("-", "");
            string amiiboIDStr = BitConverter.ToString(amiiboID).Replace("-", "");
            string setIDStr = BitConverter.ToString(setID).Replace("-", "");
            string head = usedCharacterStr + variationStr;
            string tail = amiiboIDStr + setIDStr + "02";
            string finalID = head + tail;

            ushort settingsValue = BitConverter.ToUInt16(settingsBytes, 0);
            string nickName = Encoding.BigEndianUnicode.GetString(nickNameBytes).TrimEnd('\0');
            ushort initDateValue = BitConverter.ToUInt16(initDate, 0);
            ushort writeDateValue = BitConverter.ToUInt16(writeDate, 0);
            DateTime initDateTime = DateTimeFromTag(initDateValue);
            DateTime writeDateTime = DateTimeFromTag(writeDateValue);
            ushort writeCounterValue = BitConverter.ToUInt16(writeCounter, 0);

            LogFinalData(titleId, appId, head, tail, finalID, nickName, initDateTime, writeDateTime, settingsValue, writeCounterValue, applicationAreas);

            VirtualAmiiboFile virtualAmiiboFile = new VirtualAmiiboFile
            {
                FileVersion = 1,
                TagUuid = uid,
                AmiiboId = finalID,
                FirstWriteDate = initDateTime,
                LastWriteDate = writeDateTime,
                WriteCounter = writeCounterValue,
            };
            VirtualAmiibo.applicationBytes = applicationAreas;

            return virtualAmiiboFile;
        }

        private static void LogDebugData(byte[] uid, byte bcc0, byte bcc1)
        {
            Console.WriteLine($"UID: {BitConverter.ToString(uid)}");
            Console.WriteLine($"BCC0: 0x{bcc0:X2}, BCC1: 0x{bcc1:X2}");
        }

        private static void LogFinalData(byte[] titleId, byte[] appId, string head, string tail, string finalID, string nickName, DateTime initDateTime, DateTime writeDateTime, ushort settingsValue, ushort writeCounterValue, byte[] applicationAreas)
        {
            Console.WriteLine($"Title ID: 0x{BitConverter.ToString(titleId).Replace("-", "")}");
            Console.WriteLine($"Application Program ID: 0x{BitConverter.ToString(appId).Replace("-", "")}");
            Console.WriteLine($"Head: {head}");
            Console.WriteLine($"Tail: {tail}");
            Console.WriteLine($"Final ID: {finalID}");
            Console.WriteLine($"Nickname: {nickName}");
            Console.WriteLine($"Init Date: {initDateTime}");
            Console.WriteLine($"Write Date: {writeDateTime}");
            Console.WriteLine($"Settings: 0x{settingsValue:X4}");
            Console.WriteLine($"Write Counter: {writeCounterValue}");
            Console.WriteLine("Length of Application Areas: " + applicationAreas.Length);
        }

        private static uint CalculateCRC32(byte[] input)
        {
            uint[] table = new uint[256];
            uint polynomial = 0xEDB88320;
            for (uint i = 0; i < table.Length; ++i)
            {
                uint crc = i;
                for (int j = 0; j < 8; ++j)
                {
                    if ((crc & 1) != 0)
                        crc = (crc >> 1) ^ polynomial;
                    else
                        crc >>= 1;
                }
                table[i] = crc;
            }

            uint result = 0xFFFFFFFF;
            foreach (byte b in input)
            {
                byte index = (byte)((result & 0xFF) ^ b);
                result = (result >> 8) ^ table[index];
            }
            return ~result;
        }

        private static string GetKeyRetailBinPath()
        {
            return Path.Combine(AppDataManager.KeysDirPath, "key_retail.bin");
        }

        public static bool IsPageEncrypted(int page)
        {
            return (page >= 5 && page <= 12) || (page >= 40 && page <= 129);
        }

        public static DateTime DateTimeFromTag(ushort value)
        {
            try
            {
                int day = value & 0x1F;
                int month = (value >> 5) & 0x0F;
                int year = (value >> 9) & 0x7F;

                if (day == 0 || month == 0 || month > 12 || day > DateTime.DaysInMonth(2000 + year, month))
                    throw new ArgumentOutOfRangeException();

                return new DateTime(2000 + year, month, day);
            }
            catch
            {
                return DateTime.Now;
            }
        }
    }
}
