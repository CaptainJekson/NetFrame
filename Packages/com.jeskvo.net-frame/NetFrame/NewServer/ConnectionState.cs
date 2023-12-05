using System.Net.Sockets;
using System.Threading;

namespace NetFrame.NewServer
{
    public class ConnectionState
    {
        public TcpClient tcpClient;
        
        public readonly MagnificentSendPipe sendPipe;
        
        public ManualResetEvent sendPending = new ManualResetEvent(false);

        public ConnectionState(TcpClient tcpClient, int MaxMessageSize)
        {
            this.tcpClient = tcpClient;
            sendPipe = new MagnificentSendPipe(MaxMessageSize);
        }
    }
}