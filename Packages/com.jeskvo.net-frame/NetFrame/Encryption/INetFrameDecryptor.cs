using System.Security.Cryptography;

namespace NetFrame.Encryption
{
    public interface INetFrameDecryptor
    {
        string DecryptToken(RSAParameters privateParameters, byte[] encryptedData);
    }
}