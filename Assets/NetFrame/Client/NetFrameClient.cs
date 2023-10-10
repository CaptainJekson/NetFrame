using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using NetFrame.Constants;
using NetFrame.Enums;
using NetFrame.Utils;
using NetFrame.WriteAndRead;
using UnityEngine;

namespace NetFrame.Client
{
    public class NetFrameClient
    {
        private readonly NetFrameByteConverter _byteConverter;
        private readonly ConcurrentDictionary<Type, Delegate> _handlers;
        private readonly NetFrameDatagramCollection _datagramCollection;
        
        private TcpClient _tcpSocket;
        private NetworkStream _networkStream;

        private NetFrameWriter _writer;
        private NetFrameReader _reader;
        private byte[] _receiveBuffer;

        private int _receiveBufferSize;
        private int _writeBufferSize;
        
        private bool _canRead;

        public event Action<ReasonServerConnectionFailed> ConnectedFailed;
        public event Action ConnectionSuccessful;
        public event Action Disconnected;

        public NetFrameClient()
        {
            _handlers = new ConcurrentDictionary<Type, Delegate>();
            _byteConverter = new NetFrameByteConverter();
            _datagramCollection = new NetFrameDatagramCollection();
        }

        public void Connect(string host, int port, int receiveBufferSize = 1024, int writeBufferSize = 1024)
        {
            if (_tcpSocket != null && _tcpSocket.Connected)
            {
                ConnectedFailed?.Invoke(ReasonServerConnectionFailed.AlreadyConnected);
                return;
            }

            _tcpSocket = new TcpClient();

            _receiveBufferSize = receiveBufferSize;
            _writeBufferSize = writeBufferSize;
            _receiveBuffer = new byte[receiveBufferSize];

            _writer = new NetFrameWriter(_writeBufferSize);
            _reader = new NetFrameReader(new byte[_receiveBufferSize]);

            _tcpSocket.BeginConnect(host, port, BeginConnectCallback, _tcpSocket);
        }

        public void Run()
        {
            CheckDisconnectToServer();
            CheckAvailableBytes();
        }

        private void BeginConnectCallback(IAsyncResult result)
        {
            var tcpSocket = (TcpClient) result.AsyncState;

            if (!tcpSocket.Connected)
            {
                ConnectedFailed?.Invoke(ReasonServerConnectionFailed.ImpossibleToConnect);
                return;
            }

            _networkStream = tcpSocket.GetStream();
            
            ConnectionSuccessful?.Invoke();
        }
        
        private void CheckAvailableBytes()
        {
            if (_networkStream != null && _networkStream.DataAvailable && !_canRead)
            {
                var availableBytes = _tcpSocket.Available;

                if (availableBytes > _receiveBufferSize) //todo нужно возвращать назад дефолтный размер буфера
                {
                    _receiveBufferSize = availableBytes;
                    _receiveBuffer = new byte[_receiveBufferSize];
                    _reader = new NetFrameReader(new byte[_receiveBufferSize]);
                }

                BeginReadBytes();
                
                _canRead = true;
            }
        }

        private void BeginReadBytes() //todo перенесен ва Run для динамического расширения буфера для чтения, тоже самое нажо сделать на сервере
        {
            _tcpSocket.ReceiveBufferSize = _receiveBufferSize;
            _tcpSocket.SendBufferSize = _writeBufferSize;

            _networkStream.BeginRead(_receiveBuffer, 0, _receiveBufferSize, BeginReadBytesCallback, null);
        }

        private void BeginReadBytesCallback(IAsyncResult result)
        {
            try
            {
                if (!_networkStream.CanRead)
                {
                    return;
                }
                
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

                _networkStream.BeginRead(_receiveBuffer, 0, _receiveBufferSize, BeginReadBytesCallback, null);
            }
            catch (Exception e)
            {
                //Console.WriteLine($"Error receive TCP Client {e.Message}");
                Debug.LogError($"Error receive TCP Client {e.Message}");
                Disconnect();
            }
        }

        public void Disconnect()
        {
            if (_tcpSocket != null && _tcpSocket.Connected)
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

            Task.Run(async () =>
            {
                await SendAsync(_networkStream, allPackage);
            });
        }
        
        private async Task SendAsync(NetworkStream networkStream, ArraySegment<byte> data)
        {
            await networkStream.WriteAsync(data);
        }

        public void Subscribe<T>(Action<T> handler) where T : struct, INetFrameDatagram
        {
            _handlers.AddOrUpdate(typeof(T), handler, (_, currentHandler) => (Action<T>)currentHandler + handler);
        }

        public void Unsubscribe<T>(Action<T> handler) where T : struct, INetFrameDatagram
        {
            _handlers.TryRemove(typeof(T), out var currentHandler);
        }

        private string GetDatagramTypeName<T>(T datagram) where T : struct, INetFrameDatagram
        {
            return typeof(T).Name;
        }
        
        private void CheckDisconnectToServer()
        {
            if (_tcpSocket == null || !_tcpSocket.Connected)
            {
                return;
            }
            
            if (!_tcpSocket.Client.Poll(0, SelectMode.SelectRead))
            {
                return;
            }
            
            var buff = new byte[1];
            
            
            if (_tcpSocket.Client.Receive(buff, SocketFlags.Peek) != 0)
            {
                return;
            }
            
            _tcpSocket.Client.Disconnect(false);
            
            ConnectedFailed?.Invoke(ReasonServerConnectionFailed.ConnectionLost);
        }
    }
}