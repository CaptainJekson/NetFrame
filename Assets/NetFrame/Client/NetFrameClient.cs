using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using NetFrame.Constants;
using NetFrame.Utils;
using NetFrame.WriteAndRead;

namespace NetFrame.Client
{
    public class NetFrameClient
    {
        private TcpClient _tcpSocket;
        private NetworkStream _networkStream;

        private NetFrameWriter _writer;
        private NetFrameReader _reader;
        private NetFrameByteConverter _byteConverter;
        private byte[] _receiveBuffer;

        private int _receiveBufferSize;
        private int _writeBufferSize;

        private ConcurrentDictionary<Type, Delegate> _handlers;
        private NetFrameDatagramCollection _datagramCollection;

        public event Action<string> ConnectedFailed;
        public event Action Disconnected;

        public void Connect(string host, int port, int receiveBufferSize = 1024, int writeBufferSize = 1024)
        {
            try
            {
                _tcpSocket = new TcpClient(host, port);
                _networkStream = _tcpSocket.GetStream();
            }
            catch (Exception e)
            {
                ConnectedFailed?.Invoke(e.Message);
                return;
            }

            _receiveBufferSize = receiveBufferSize;
            _writeBufferSize = writeBufferSize;
            _receiveBuffer = new byte[receiveBufferSize];

            _writer = new NetFrameWriter(_writeBufferSize);
            _reader = new NetFrameReader(new byte[_receiveBufferSize]);
            _handlers = new ConcurrentDictionary<Type, Delegate>();
            _byteConverter = new NetFrameByteConverter();
            _datagramCollection = new NetFrameDatagramCollection();
            
            BeginReadBytes();
        }
        
        public void ChangeReceiveBufferSize(int newSize)
        {
            _receiveBufferSize = newSize;
        }

        public void ChangeWriteBufferSize(int newSize)
        {
            _writeBufferSize = newSize;
            _writer = new NetFrameWriter(_writeBufferSize);
        }

        private void BeginReadBytes()
        {
            _tcpSocket.ReceiveBufferSize = _receiveBufferSize;
            _tcpSocket.SendBufferSize = _receiveBufferSize;

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
                        handler.DynamicInvoke(datagram);
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
            if (_tcpSocket != null)
            {
                _tcpSocket.Close();
                _tcpSocket = null;

                Disconnected?.Invoke();
            }
        }

        public void Send<T>(ref T datagram) where T : struct, INetFrameDatagram
        {
            _writer.Reset();
            datagram.Write(_writer);

            var separator = '\n';
            var headerDatagram = GetDatagramTypeName(datagram) + separator;

            var heaterDatagram = Encoding.UTF8.GetBytes(headerDatagram);
            var dataDatagram = _writer.ToArraySegment();
            var allData = heaterDatagram.Concat(dataDatagram).ToArray();

            var allPackageSize = (uint)allData.Length + NetFrameConstants.SizeByteCount;
            var sizeBytes = _byteConverter.GetByteArrayFromUInt(allPackageSize);
            var allPackage = sizeBytes.Concat(allData).ToArray();

            Task.Run(async () => { await SendAsync(_networkStream, allPackage); });
        }
        

        public void Subscribe<T>(Action<T> handler) where T : struct, INetFrameDatagram
        {
            _handlers.AddOrUpdate(typeof(T), handler, (_, currentHandler) => (Action<T>)currentHandler + handler);
        }

        public void Unsubscribe<T>(Action<T> handler) where T : struct, INetFrameDatagram
        {
            _handlers.TryRemove(typeof(T), out var currentHandler);
        }

        private async Task SendAsync(NetworkStream networkStream, ArraySegment<byte> data)
        {
            await networkStream.WriteAsync(data);
        }

        private string GetDatagramTypeName<T>(T datagram) where T : struct, INetFrameDatagram
        {
            return typeof(T).Name;
        }
    }
}