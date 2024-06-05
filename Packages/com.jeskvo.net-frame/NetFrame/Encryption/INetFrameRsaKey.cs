using System.Security.Cryptography;

namespace NetFrame.Encryption
{
    public interface INetFrameRsaKey
    {
        RSAParameters LoadKey(string fullPath);
    }
}