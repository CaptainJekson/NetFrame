using System;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using NetFrame.Server;

namespace NetFrame.Encryption
{
    public class NetFrameConnectionValidator
    {
        private const int AuthenticatePending = 10000;

        private readonly INetFrameDecryptor _netFrameDecryptor;
        private RSAParameters _rsaParameters;
        private string _securityToken;

        public NetFrameConnectionValidator(string rsaKeyFullPath, string securityToken)
        {
            _netFrameDecryptor = new NetFrameCryptographer();
            _rsaParameters = _netFrameDecryptor.LoadKey(rsaKeyFullPath);
            _securityToken = securityToken;
        }
        
        public async Task<bool> TryValidateConnectionAsync(ConnectionState connectionState)
        {
            var cancelTokenSource = new CancellationTokenSource(); 
            var token = cancelTokenSource.Token;
            connectionState.ValidatePendingCancellationToken = token;

            try
            {
                await Task.Delay(AuthenticatePending, token);
            }
            catch (AggregateException aggregateException)
            {
                foreach (var exception in aggregateException.InnerExceptions)
                {
                    if (exception is TaskCanceledException)
                    {
                        return true;
                    }
                    
                    throw exception;
                }
            }
            return false;
        }
    }
}