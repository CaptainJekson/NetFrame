using System.Net.Sockets;
using System.Threading;
using NetFrame.Queues;
using NetFrame.Server;

namespace NetFrame.Client
{
    public class ClientConnectionState : ConnectionState
    {
        public Thread ReceiveThread;

        public bool Connected => TcpClient != null &&
                                 TcpClient.Client != null &&
                                 TcpClient.Client.Connected;
        
        public volatile bool Connecting;
        public readonly ReceiveQueue ReceiveQueue;
        
        public ClientConnectionState(int maxMessageSize) : base(new TcpClient(), maxMessageSize)
        {
            ReceiveQueue = new ReceiveQueue(maxMessageSize);
        }
        
        public void Dispose()
        {
            TcpClient.Close();
            ReceiveThread?.Interrupt();
            Connecting = false;
            SendQueue.Clear();
            TcpClient = null;
        }
    }
}