using System.Security.Cryptography;

namespace NetFrame.Encryption
{
    public interface INetFrameDecryptor : INetFrameRsaKey
    {
        string DecryptToken(RSAParameters privateParameters, byte[] encryptedData);
    }
}