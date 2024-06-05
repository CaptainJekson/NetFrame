using System.Security.Cryptography;

namespace NetFrame.Encryption
{
    public interface INetFrameEncryptor
    {
        byte[] EncryptToken(RSAParameters publicParameters, string token);
    }
}