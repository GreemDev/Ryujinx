using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Ryujinx.HLE.HOS.Services.Nfc.Bin
{
    public class AmiiboMasterKey
    {
        private const int DataLength = 80;
        private const int CombinedLength = 160;
        public byte[] HmacKey { get; private set; }      // 16 bytes
        public byte[] TypeString { get; private set; }  // 14 bytes
        public byte Rfu { get; private set; }           // 1 byte reserved
        public byte MagicSize { get; private set; }     // 1 byte
        public byte[] MagicBytes { get; private set; }  // 16 bytes
        public byte[] XorPad { get; private set; }      // 32 bytes

        private AmiiboMasterKey(byte[] data)
        {
            if (data.Length != DataLength)
                throw new ArgumentException($"Data is {data.Length} bytes (should be {DataLength}).");


            // Unpack the data
            HmacKey = data[..16];
            TypeString = data[16..30];
            Rfu = data[30];
            MagicSize = data[31];
            MagicBytes = data[32..48];
            XorPad = data[48..];
        }

        public static (AmiiboMasterKey DataKey, AmiiboMasterKey TagKey) FromSeparateBin(byte[] dataBin, byte[] tagBin)
        {
            var dataKey = new AmiiboMasterKey(dataBin);
            var tagKey = new AmiiboMasterKey(tagBin);
            return (dataKey, tagKey);
        }

        public static (AmiiboMasterKey DataKey, AmiiboMasterKey TagKey) FromSeparateHex(string dataHex, string tagHex)
        {
            return FromSeparateBin(HexToBytes(dataHex), HexToBytes(tagHex));
        }

        public static (AmiiboMasterKey DataKey, AmiiboMasterKey TagKey) FromCombinedBin(byte[] combinedBin)
        {
            if (combinedBin.Length != CombinedLength)
                throw new ArgumentException($"Data is {combinedBin.Length} bytes (should be {CombinedLength}).");

            byte[] dataBin = combinedBin[..DataLength];
            byte[] tagBin = combinedBin[DataLength..];
            return FromSeparateBin(dataBin, tagBin);
        }

        private static byte[] HexToBytes(string hex)
        {
            int length = hex.Length / 2;
            byte[] bytes = new byte[length];
            for (int i = 0; i < length; i++)
            {
                bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            }
            return bytes;
        }
    }
}
