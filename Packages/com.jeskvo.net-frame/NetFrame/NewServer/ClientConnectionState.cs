using System.Net.Sockets;
using System.Threading;

namespace NetFrame.NewServer
{
    public class ClientConnectionState : ConnectionState
    {
        public Thread receiveThread;

        public bool Connected => tcpClient != null &&
                                 tcpClient.Client != null &&
                                 tcpClient.Client.Connected;
        
        public volatile bool Connecting;
        public readonly MagnificentReceivePipe receivePipe;
        
        public ClientConnectionState(int MaxMessageSize) : base(new TcpClient(), MaxMessageSize)
        {
            receivePipe = new MagnificentReceivePipe(MaxMessageSize);
        }
        
        public void Dispose()
        {
            tcpClient.Close();
            receiveThread?.Interrupt();
            Connecting = false;
            sendPipe.Clear();
            tcpClient = null;
        }
    }
}