using LibHac.Tools.FsSystem.NcaUtils;
using Ryujinx.HLE.HOS.Services.Mii.Types;
using Ryujinx.HLE.HOS.Services.Nfc.Nfp.NfpManager;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Ryujinx.HLE.HOS.Services.Nfc.Bin
{
    public class AmiiboDecrypter
    {
        public readonly byte[] _hmacKey; // HMAC key
        public readonly byte[] _aesKey;  // AES key

        public AmiiboDecrypter(string keyRetailBinPath)
        {
            var keys = AmiiboMasterKey.FromCombinedBin(File.ReadAllBytes(keyRetailBinPath));
            _hmacKey = keys.DataKey.HmacKey;
            _aesKey = keys.DataKey.XorPad;
        }

        public byte[] DecryptAmiiboData(byte[] encryptedData, byte[] counter)
        {
            // Ensure the counter length matches the block size
            if (counter.Length != 16)
            {
                throw new ArgumentException("Counter must be 16 bytes long for AES block size.");
            }

            byte[] decryptedData = new byte[encryptedData.Length];

            using (Aes aesAlg = Aes.Create())
            {
                aesAlg.Key = _aesKey;
                aesAlg.Mode = CipherMode.ECB; // Use ECB mode to handle the counter encryption
                aesAlg.Padding = PaddingMode.None;

                using (var encryptor = aesAlg.CreateEncryptor())
                {
                    int blockSize = 16;
                    byte[] encryptedCounter = new byte[blockSize];
                    byte[] currentCounter = (byte[])counter.Clone();

                    for (int i = 0; i < encryptedData.Length; i += blockSize)
                    {
                        // Encrypt the current counter block
                        encryptor.TransformBlock(currentCounter, 0, blockSize, encryptedCounter, 0);

                        // XOR the encrypted counter with the ciphertext to get the decrypted data
                        for (int j = 0; j < blockSize && i + j < encryptedData.Length; j++)
                        {
                            decryptedData[i + j] = (byte)(encryptedData[i + j] ^ encryptedCounter[j]);
                        }

                        // Increment the counter for the next block
                        IncrementCounter(currentCounter);
                    }
                }
            }

            return decryptedData;
        }

        public byte[] CalculateHMAC(byte[] data)
        {
            using (var hmac = new HMACSHA256(_hmacKey))
            {
                return hmac.ComputeHash(data);
            }
        }

        public void IncrementCounter(byte[] counter)
        {
            for (int i = counter.Length - 1; i >= 0; i--)
            {
                if (++counter[i] != 0)
                    break; // Stop if no overflow
            }
        }

        public DateTime ParseDate(byte[] data, int offset)
        {
            ushort year = BitConverter.ToUInt16(data, offset);
            byte month = data[offset + 2];
            byte day = data[offset + 3];
            byte hour = data[offset + 4];
            byte minute = data[offset + 5];
            byte second = data[offset + 6];

            return new DateTime(year, month, day, hour, minute, second);
        }

        public List<object> ParseApplicationAreas(byte[] data, int startOffset, int areaSize)
        {
            var areas = new List<object>();
            for (int i = 0; i < 8; i++) // Assuming 8 areas
            {
                int offset = startOffset + (i * areaSize);
                string applicationId = BitConverter.ToString(data[offset..(offset + 4)]).Replace("-", "");
                byte[] areaData = data[(offset + 4)..(offset + areaSize)];
                areas.Add(new VirtualAmiiboApplicationArea
                {
                    ApplicationAreaId = uint.Parse(applicationId),
                    ApplicationArea = areaData
                });
            }

            return areas;
        }
    }
}
