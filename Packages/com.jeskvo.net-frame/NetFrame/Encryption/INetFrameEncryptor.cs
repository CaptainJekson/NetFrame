using System.Security.Cryptography;

namespace NetFrame.Encryption
{
    public interface INetFrameEncryptor : INetFrameRsaKey
    {
        byte[] EncryptToken(RSAParameters publicParameters, string token);
    }
}