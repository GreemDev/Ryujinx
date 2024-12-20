using System.IO;

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

        public AmiiboDump EncryptAmiiboDump(byte[] decryptedDumpData)
        {
            // Initialize AmiiboDump with decrypted data
            AmiiboDump amiiboDump = new AmiiboDump(decryptedDumpData, DataKey, TagKey, isLocked: false);

            // Lock (encrypt) the dump
            amiiboDump.Lock();

            return amiiboDump;
        }
    }
}
