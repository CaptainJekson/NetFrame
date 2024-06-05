using System.IO;
using System.Security.Cryptography;
using System.Xml.Serialization;

namespace NetFrame.Encryption
{
    public class NetFrameRsaKeyGenerator
    {
        private const string PrivateRsaKeyFileName = "privateRSAKey.xml";
        private const string PublicRsaKeyFileName = "publicRSAKey.xml";
        
        public void GenerateKeys(string fullPath) //todo сделаем отдельное консольное приложение генератора этих ключей
        {
            using var rsa = new RSACryptoServiceProvider();
            WriteToXml(rsa, fullPath, PrivateRsaKeyFileName, true);
            WriteToXml(rsa, fullPath, PublicRsaKeyFileName, false);
        }
        
        private void WriteToXml(RSA rsa, string fullPath, string fileName, bool includePrivateParameters)
        {
            var privateKey = rsa.ExportParameters(includePrivateParameters);
            var xmlPrivateKey = ExportParametersToXml(privateKey);
            
            File.WriteAllText(fullPath + fileName, xmlPrivateKey);
        }

        private string ExportParametersToXml(RSAParameters parameters)
        {
            var sw = new StringWriter();
            var xs = new XmlSerializer(typeof(RSAParameters));
            xs.Serialize(sw, parameters);
            return sw.ToString();
        }
    }
}