using System.Net.Sockets;
using System.Threading;
using NetFrame.Queues;

namespace NetFrame.Server
{
    public class ConnectionState
    {
        public TcpClient TcpClient;
        public UdpClient UdpClient;
        public readonly SendQueue SendQueue;
        
        public readonly ManualResetEvent SendPending;

        public ConnectionState(TcpClient tcpClient, int maxMessageSize)
        {
            TcpClient = tcpClient;
            SendPending = new ManualResetEvent(false);
            SendQueue = new SendQueue(maxMessageSize);
        }
    }
}