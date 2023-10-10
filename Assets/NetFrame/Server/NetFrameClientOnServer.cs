using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using NetFrame.Constants;
using NetFrame.Utils;
using NetFrame.WriteAndRead;

namespace NetFrame.Server
{
    public class NetFrameClientOnServer
    {
        private readonly int _id;
        private readonly TcpClient _tcpSocket;
        private readonly NetworkStream _networkStream;
        private readonly NetFrameByteConverter _byteConverter;
        private readonly ConcurrentDictionary<Type, Delegate> _handlers;
        private readonly byte[] _receiveBuffer;
        private readonly int _receiveBufferSize; 

        private readonly NetFrameReader _reader;
        private NetFrameDatagramCollection _datagramCollection;

        public TcpClient TcpSocket => _tcpSocket;

        public NetFrameClientOnServer(int id, TcpClient tcpSocket, ConcurrentDictionary<Type, Delegate> handlers, 
            int bufferSize, NetFrameDatagramCollection datagramCollection)
        {
            _id = id;
            _tcpSocket = tcpSocket;
            _handlers = handlers;
            _networkStream = tcpSocket.GetStream();
            _byteConverter = new NetFrameByteConverter();
            _datagramCollection = datagramCollection;
            _reader = new NetFrameReader(new byte[bufferSize]);
            _receiveBufferSize = bufferSize;
            _receiveBuffer = new byte[_receiveBufferSize];
        }

        public void BeginReadBytes()
        {
            _networkStream.BeginRead(_receiveBuffer, 0, _receiveBufferSize, ReceiveCallback, null);
        }

        private void ReceiveCallback(IAsyncResult result)
        {
            try
            {
                var byteReadLength = _networkStream.EndRead(result);
                if (byteReadLength <= 0)
                {
                    return;
                }

                var allBytes = new byte[byteReadLength];
                Array.Copy(_receiveBuffer, allBytes, byteReadLength);
                var readBytesCompleteCount = 0;
                
                do
                {
                    var packageSizeSegment = new ArraySegment<byte>(allBytes, readBytesCompleteCount,
                        NetFrameConstants.SizeByteCount);
                    var packageSize = _byteConverter.GetUIntFromByteArray(packageSizeSegment.ToArray());
                    var packageBytes = new ArraySegment<byte>(allBytes, readBytesCompleteCount, packageSize);

                    var tempIndex = 0;
                    for (var index = NetFrameConstants.SizeByteCount; index < packageSize; index++)
                    {
                        var b = packageBytes[index];

                        if (b == '\n')
                        {
                            tempIndex = index + 1;
                            break;
                        }
                    }

                    var headerSegment = new ArraySegment<byte>(packageBytes.ToArray(),
                        NetFrameConstants.SizeByteCount,
                        tempIndex - NetFrameConstants.SizeByteCount - 1);
                    var contentSegment =
                        new ArraySegment<byte>(packageBytes.ToArray(), tempIndex, packageSize - tempIndex);
                    var headerDatagram = Encoding.UTF8.GetString(headerSegment);
                    
                    readBytesCompleteCount += packageSize;

                    var datagram = _datagramCollection.GetDatagramByKey(headerDatagram);
                    var targetType = datagram.GetType();
                    
                    _reader.SetBuffer(contentSegment);
                    datagram.Read(_reader);
                    
                    if (_handlers.TryGetValue(targetType, out var handler))
                    {
                        handler.DynamicInvoke(datagram, _id);
                    }
                } 
                while (readBytesCompleteCount < allBytes.Length);
                
                _networkStream.BeginRead(_receiveBuffer, 0, _receiveBufferSize, ReceiveCallback, null);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error receive TCP Client {e.Message}");
                Disconnect();
            }
        }

        public void Disconnect()
        {
            _tcpSocket.Close();
        }
    }
}