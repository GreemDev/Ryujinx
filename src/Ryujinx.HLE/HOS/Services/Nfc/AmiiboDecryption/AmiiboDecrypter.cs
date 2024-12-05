using System;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using System.Security.Cryptography;

namespace Ryujinx.HLE.HOS.Services.Nfc.AmiiboDecryption
{
    public class AmiiboDecrypter
    {
        public AmiiboMasterKey DataKey { get; private set; }
        public AmiiboMasterKey TagKey { get; private set; }

        public AmiiboDecrypter(string keyRetailBinPath)
        {
            var combinedKeys = File.ReadAllBytes(keyRetailBinPath);
            var keys = AmiiboMasterKey.FromCombinedBin(combinedKeys);
            DataKey = keys.DataKey;
            TagKey = keys.TagKey;
        }

        public AmiiboDump DecryptAmiiboDump(byte[] encryptedDumpData)
        {
            // Initialize AmiiboDump with encrypted data
            AmiiboDump amiiboDump = new AmiiboDump(encryptedDumpData, DataKey, TagKey, isLocked: true);

            // Unlock (decrypt) the dump
            amiiboDump.Unlock();

            // Optional: Verify HMACs
            amiiboDump.VerifyHMACs();

            return amiiboDump;
        }
    }
}
