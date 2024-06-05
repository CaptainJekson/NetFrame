using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Serialization;

namespace NetFrame.Encryption
{
    public class NetFrameCryptographer : INetFrameEncryptor, INetFrameDecryptor
    {
        private UnicodeEncoding _byteConverter; 
        
        public NetFrameCryptographer()
        {
            _byteConverter = new UnicodeEncoding();
        }

        /// <summary>
        /// Encrypts a token using a public key
        /// </summary>
        /// <param name="publicParameters">RSA parameters containing public key</param>
        /// <param name="token">Application token</param>
        /// <returns></returns>
        public byte[] EncryptToken(RSAParameters publicParameters, string token)
        {
            byte[] tokenToClient = _byteConverter.GetBytes(token);

            using var rsaClient = new RSACryptoServiceProvider();
            rsaClient.ImportParameters(publicParameters);

            //шифруем используя публичный ключ на КЛИЕНТЕ
            var encryptedData = Encrypt(tokenToClient, rsaClient.ExportParameters(false), false);
            return encryptedData;
        }
        
        /// <summary>
        /// Decrypt the token using the public and private RSA key on the server
        /// </summary>
        /// <param name="privateParameters">RSA parameters containing private and public key</param>
        /// <param name="encryptedData">Encrypted data from the client</param>
        /// <returns></returns>
        public string DecryptToken(RSAParameters privateParameters, byte[] encryptedData)
        {
            using var rsaServer = new RSACryptoServiceProvider();
            rsaServer.ImportParameters(privateParameters);
            
            var decryptedData = Decrypt(encryptedData, rsaServer.ExportParameters(true), false);
            var tokenFromClient = _byteConverter.GetString(decryptedData);
            return tokenFromClient;
        }

        public RSAParameters LoadKey(string fullPath)
        {
            var xmlParameters = File.ReadAllText(fullPath);
            var parameters = ImportParametersFromXml(xmlParameters);
            return parameters;
        }

        private byte[] Encrypt(byte[] DataToEncrypt, RSAParameters RSAKeyInfo, bool DoOAEPPadding)
        {
            try
            {
                using var rsa = new RSACryptoServiceProvider();
                rsa.ImportParameters(RSAKeyInfo);
                
                var encryptedData = rsa.Encrypt(DataToEncrypt, DoOAEPPadding);
                return encryptedData;
            }
            catch (Exception e)
            {
                return Array.Empty<byte>();
            }
        }
        
        private byte[] Decrypt(byte[] DataToDecrypt, RSAParameters RSAKeyInfo, bool DoOAEPPadding)
        {
            try
            {
                using var rsa = new RSACryptoServiceProvider();
                rsa.ImportParameters(RSAKeyInfo);
                
                var decryptedData = rsa.Decrypt(DataToDecrypt, DoOAEPPadding);
                return decryptedData;
            }
            catch (Exception e)
            {
                return Array.Empty<byte>();
            }
        }

        private static RSAParameters ImportParametersFromXml(string xml)
        {
            var stringReader = new StringReader(xml);
            var xmlSerializer = new XmlSerializer(typeof(RSAParameters));
            return (RSAParameters)xmlSerializer.Deserialize(stringReader);
        }
    }
}