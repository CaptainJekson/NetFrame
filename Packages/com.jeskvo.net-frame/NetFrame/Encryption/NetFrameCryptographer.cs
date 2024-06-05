using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace NetFrame.Encryption
{
    public class NetFrameCryptographer
    {
        private UnicodeEncoding _byteConverter;
        
        public NetFrameCryptographer()
        {
            _byteConverter = new UnicodeEncoding();
        }
        
        public void TestRun()
        {
            byte[] dataToEncrypt = _byteConverter.GetBytes("Привет это я Жендос");

            using (RSACryptoServiceProvider RSA = new RSACryptoServiceProvider())
            {
                var encryptedData = Encrypt(dataToEncrypt, RSA.ExportParameters(false), false);
                Debug.Log($"Encrypt plaintext: {_byteConverter.GetString(encryptedData)}");
                
                var decryptedData = Decrypt(encryptedData, RSA.ExportParameters(true), false);
                Debug.Log($"Decrypted plaintext: {_byteConverter.GetString(decryptedData)}");
            }
        }
        
        private byte[] Encrypt(byte[] DataToEncrypt, RSAParameters RSAKeyInfo, bool DoOAEPPadding)
        {
            byte[] encryptedData;
            using (RSACryptoServiceProvider RSA = new RSACryptoServiceProvider())
            {
                
                RSA.ImportParameters(RSAKeyInfo);
                
                encryptedData = RSA.Encrypt(DataToEncrypt, DoOAEPPadding);
            }
            return encryptedData;
        }
        
        private byte[] Decrypt(byte[] DataToDecrypt, RSAParameters RSAKeyInfo, bool DoOAEPPadding)
        {
            byte[] decryptedData;

            using (RSACryptoServiceProvider RSA = new RSACryptoServiceProvider())
            {
                RSA.ImportParameters(RSAKeyInfo);
                
                decryptedData = RSA.Decrypt(DataToDecrypt, DoOAEPPadding);
            }
            return decryptedData;
        }
        
        private void Export()
        {
            using (RSACryptoServiceProvider RSA = new RSACryptoServiceProvider())
            {
                RSAParameters parameters = RSA.ExportParameters(false);
            }
        }
    }
}