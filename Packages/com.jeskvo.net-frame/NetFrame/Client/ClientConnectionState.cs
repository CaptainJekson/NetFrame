using System.Net.Sockets;
using System.Threading;
using NetFrame.Queues;
using NetFrame.Server;

namespace NetFrame.Client
{
    public class ClientConnectionState : ConnectionState
    {
        public Thread ReceiveTcpThread;
        //public Thread ReceiveUpdThread;
        public int LocalConnectionId;
        
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
            TcpClient.Dispose();
            ReceiveTcpThread?.Interrupt();
            //ReceiveUpdThread?.Interrupt();
            Connecting = false;
            SendQueue.Clear();
            TcpClient = null;
        }
    }
}