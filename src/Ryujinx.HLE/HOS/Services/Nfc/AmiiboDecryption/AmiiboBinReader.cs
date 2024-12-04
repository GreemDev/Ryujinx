using Ryujinx.Common.Configuration;
using Ryujinx.HLE.HOS.Services.Nfc.Nfp;
using Ryujinx.HLE.HOS.Services.Nfc.Nfp.NfpManager;
using Ryujinx.HLE.HOS.Tamper;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace Ryujinx.HLE.HOS.Services.Nfc.Bin
{
    public class AmiiboBinReader
    {
        // Method to calculate BCC (XOR checksum) from UID bytes
        private static byte CalculateBCC(byte[] uid, int startIdx)
        {
            return (byte)(uid[startIdx] ^ uid[startIdx + 1] ^ uid[startIdx + 2] ^ 0x88);
        }

        // Method to read and process a .bin file
        public static VirtualAmiiboFile ReadBinFile(byte[] fileBytes)
        {
            string keyRetailBinPath = GetKeyRetailBinPath();
            if (string.IsNullOrEmpty(keyRetailBinPath))
            {
                Console.WriteLine("Key retail bin path not found.");
                return new VirtualAmiiboFile();
            }

            byte[] initialCounter = new byte[16];

            // Ensure the file is long enough
            if (fileBytes.Length < 128 * 4)  // Each page is 4 bytes, total 512 bytes
            {
                Console.WriteLine("File is too short to process.");
                return new VirtualAmiiboFile();
            }

            // Decrypt the Amiibo data
            AmiiboDecrypter amiiboDecryptor = new AmiiboDecrypter(keyRetailBinPath);
            byte[] decryptedFileBytes = amiiboDecryptor.DecryptAmiiboData(fileBytes, initialCounter);

            // Assuming the UID is stored in the first 7 bytes (NTAG215 UID length)
            byte[] uid = new byte[7];
            Array.Copy(fileBytes, 0, uid, 0, 7);

            // Calculate BCC values
            byte bcc0 = CalculateBCC(uid, 0); // BCC0 = UID0 ^ UID1 ^ UID2 ^ 0x88
            byte bcc1 = CalculateBCC(uid, 3); // BCC1 = UID3 ^ UID4 ^ UID5 ^ 0x00

            Console.WriteLine($"UID: {BitConverter.ToString(uid)}");
            Console.WriteLine($"BCC0: 0x{bcc0:X2}, BCC1: 0x{bcc1:X2}");

            // Initialize byte arrays for data extraction
            byte[] nickNameBytes = new byte[20]; // Amiibo nickname is 20 bytes
            byte[] titleId = new byte[8];
            byte[] usedCharacter = new byte[2];
            byte[] variation = new byte[2];
            byte[] amiiboID = new byte[2];
            byte[] setID = new byte[1];
            byte[] initDate = new byte[2];
            byte[] writeDate = new byte[2];
            byte[] writeCounter = new byte[2];
            byte formData = 0;
            byte[] applicationAreas = new byte[212];

            // Reading specific pages and parsing bytes
            for (int page = 0; page < 128; page++) // NTAG215 has 128 pages
            {
                int pageStartIdx = page * 4; // Each page is 4 bytes
                byte[] pageData = new byte[4];
                bool isEncrypted = IsPageEncrypted(page);
                byte[] sourceBytes = isEncrypted ? decryptedFileBytes : fileBytes;
                Array.Copy(sourceBytes, pageStartIdx, pageData, 0, 4);

                // Special handling for specific pages
                switch (page)
                {
                    case 0: // Page 0 (UID + BCC0)
                        Console.WriteLine("Page 0: UID and BCC0.");
                        break;

                    case 2: // Page 2 (BCC1 + Internal Value)
                        byte internalValue = pageData[1];
                        Console.WriteLine($"Page 2: BCC1 + Internal Value 0x{internalValue:X2} (Expected 0x48).");
                        break;

                    case 6:
                        // Bytes 0 and 1 are init date, bytes 2 and 3 are write date
                        Array.Copy(pageData, 0, initDate, 0, 2);
                        Array.Copy(pageData, 2, writeDate, 0, 2);
                        break;

                    case 8:
                    case 9:
                    case 10:
                    case 11:
                    case 12:
                        // Extract nickname bytes
                        int nickNameOffset = (page - 8) * 4;
                        Array.Copy(pageData, 0, nickNameBytes, nickNameOffset, 4);
                        break;

                    case 21:
                        // Bytes 0 and 1 are used character, bytes 2 and 3 are variation
                        Array.Copy(pageData, 0, usedCharacter, 0, 2);
                        Array.Copy(pageData, 2, variation, 0, 2);
                        break;

                    case 22:
                        // Bytes 0 and 1 are amiibo ID, byte 2 is set ID, byte 3 is form data
                        Array.Copy(pageData, 0, amiiboID, 0, 2);
                        setID[0] = pageData[2];
                        formData = pageData[3];
                        break;

                    case 64:
                    case 65:
                        // Extract title ID
                        int titleIdOffset = (page - 64) * 4;
                        Array.Copy(pageData, 0, titleId, titleIdOffset, 4);
                        break;

                    case 66:
                        // Bytes 0 and 1 are write counter
                        Array.Copy(pageData, 0, writeCounter, 0, 2);
                        break;

                    // Pages 76 to 127 are application areas
                    case >= 76 and <= 127:
                        int appAreaOffset = (page - 76) * 4;
                        Array.Copy(pageData, 0, applicationAreas, appAreaOffset, 4);
                        break;
                }
            }
            // Debugging
            string titleIdStr = BitConverter.ToString(titleId).Replace("-", "");
            string usedCharacterStr = BitConverter.ToString(usedCharacter).Replace("-", "");
            string variationStr = BitConverter.ToString(variation).Replace("-", "");
            string amiiboIDStr = BitConverter.ToString(amiiboID).Replace("-", "");
            string formDataStr = formData.ToString("X2");
            string setIDStr = BitConverter.ToString(setID).Replace("-", "");
            string nickName = Encoding.BigEndianUnicode.GetString(nickNameBytes).TrimEnd('\0');
            string head = usedCharacterStr + variationStr;
            string tail = amiiboIDStr + setIDStr + "02";
            string finalID = head + tail;
            string initDateStr = BitConverter.ToString(initDate).Replace("-", "");
            string writeDateStr = BitConverter.ToString(writeDate).Replace("-", "");

            Console.WriteLine($"Title ID: {titleIdStr}");
            Console.WriteLine($"Head: {head}");
            Console.WriteLine($"Tail: {tail}");
            Console.WriteLine($"Used Character: {usedCharacterStr}");
            Console.WriteLine($"Form Data: {formDataStr}");
            Console.WriteLine($"Variation: {variationStr}");
            Console.WriteLine($"Amiibo ID: {amiiboIDStr}");
            Console.WriteLine($"Set ID: {setIDStr}");
            Console.WriteLine($"Final ID: {finalID}");
            Console.WriteLine($"Nickname: {nickName}");
            Console.WriteLine($"Init Date: {initDateStr}");
            Console.WriteLine($"Write Date: {writeDateStr}");

            VirtualAmiiboFile virtualAmiiboFile = new VirtualAmiiboFile
            {
                FileVersion = 1,
                TagUuid = uid,
                AmiiboId = finalID
            };

            DateTime initDateTime = DateTimeFromBytes(initDate);
            DateTime writeDateTime = DateTimeFromBytes(writeDate);

            Console.WriteLine($"Parsed Init Date: {initDateTime}");
            Console.WriteLine($"Parsed Write Date: {writeDateTime}");

            virtualAmiiboFile.FirstWriteDate = initDateTime;
            virtualAmiiboFile.LastWriteDate = writeDateTime;
            virtualAmiiboFile.WriteCounter = BitConverter.ToUInt16(writeCounter, 0);

            // Parse application areas
            //List<VirtualAmiiboApplicationArea> applicationAreasList = ParseAmiiboData(applicationAreas);
            List<VirtualAmiiboApplicationArea> applicationAreasList = new List<VirtualAmiiboApplicationArea>();
            virtualAmiiboFile.ApplicationAreas = applicationAreasList;

            // Save the virtual Amiibo file
            VirtualAmiibo.SaveAmiiboFile(virtualAmiiboFile);

            return virtualAmiiboFile;
        }

        private static string GetKeyRetailBinPath()
        {
            return Path.Combine(AppDataManager.KeysDirPath, "key_retail.bin");
        }

        public static bool IsPageEncrypted(int page)
        {
            return (page >= 6 && page <= 9) || (page >= 43 && page <= 84);
        }

        public static DateTime DateTimeFromBytes(byte[] date)
        {
            if (date == null || date.Length != 2)
            {
                Console.WriteLine("Invalid date bytes.");
                return DateTime.MinValue;
            }

            ushort value = BitConverter.ToUInt16(date, 0);

            int day = value & 0x1F;
            int month = (value >> 5) & 0x0F;
            int year = (value >> 9) & 0x7F;

            try
            {
                return new DateTime(2000 + year, month, day);
            }
            catch (ArgumentOutOfRangeException)
            {
                Console.WriteLine("Invalid date values extracted.");
                return DateTime.MinValue;
            }
        }

        public static List<VirtualAmiiboApplicationArea> ParseAmiiboData(byte[] decryptedData)
        {
            return JsonSerializer.Deserialize<List<VirtualAmiiboApplicationArea>>(decryptedData);
        }
    }
}
