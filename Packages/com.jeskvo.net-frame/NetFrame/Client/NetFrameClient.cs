using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using NetFrame.Constants;
using NetFrame.Enums;
using NetFrame.ThreadSafeContainers;
using NetFrame.Utils;
using NetFrame.WriteAndRead;

namespace NetFrame.Client
{
    public class NetFrameClient//
    {
        private readonly NetFrameByteConverter _byteConverter;
        private readonly ConcurrentDictionary<Type, List<Delegate>> _handlers;
        
        private TcpClient _tcpSocket;
        private NetworkStream _networkStream;

        private NetFrameWriter _writer;
        private NetFrameReader _reader;
        
        private byte[] _receiveBuffer;
        private byte[] _receiveBufferOversize;

        private int _receiveBufferSize;
        private int _writeBufferSize;
        
        private bool _isCanRead = true;
        private bool _isOversizeReceiveBuffer;

        private readonly ThreadSafeContainer<ConnectedFailedSafeContainer> _connectedFailedSafeContainer;
        private readonly ThreadSafeContainer<ConnectionSuccessfulSafeContainer> _connectionSuccessfulSafeContainer;
        private readonly ThreadSafeContainer<DynamicInvokeForClientSafeContainer> _dynamicInvokeForClientSafeContainer;
        private readonly ThreadSafeContainer<DisconnectSafeContainer> _disconnectSafeContainer;

        public event Action<ReasonServerConnectionFailed, string> ConnectedFailed;
        public event Action ConnectionSuccessful;
        public event Action Disconnected;

        public NetFrameClient()
        {
            _handlers = new ConcurrentDictionary<Type, List<Delegate>>();
            _byteConverter = new NetFrameByteConverter();
            
            _connectedFailedSafeContainer = new ThreadSafeContainer<ConnectedFailedSafeContainer>();
            _connectionSuccessfulSafeContainer = new ThreadSafeContainer<ConnectionSuccessfulSafeContainer>();
            _dynamicInvokeForClientSafeContainer = new ThreadSafeContainer<DynamicInvokeForClientSafeContainer>();
            _disconnectSafeContainer = new ThreadSafeContainer<DisconnectSafeContainer>();
        }

        public void Connect(string host, int port, int receiveBufferSize = 4096, int writeBufferSize = 4096)
        {
            if (_tcpSocket != null && _tcpSocket.Connected)
            {
                ConnectedFailed?.Invoke(ReasonServerConnectionFailed.AlreadyConnected, $"host: {host} port: {port}");
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

            foreach (var response in _connectedFailedSafeContainer)
            {
                ConnectedFailed?.Invoke(response.Reason, response.Parameters);
            }

            foreach (var response in _connectionSuccessfulSafeContainer)
            {
                ConnectionSuccessful?.Invoke();
            }

            foreach (var response in _disconnectSafeContainer)
            {
                Disconnect();
            }

            foreach (var response in _dynamicInvokeForClientSafeContainer)
            {
                foreach (var handler in response.Handlers)
                {
                    handler.DynamicInvoke(response.Dataframe);
                }
            }
        }

        private void BeginConnectCallback(IAsyncResult result)
        {
            var tcpSocket = (TcpClient) result.AsyncState;

            if (!tcpSocket.Connected)
            {
                _connectedFailedSafeContainer.Add(new ConnectedFailedSafeContainer
                {
                    Reason = ReasonServerConnectionFailed.ImpossibleToConnect,
                    Parameters = string.Empty,
                });
                
                return;
            }

            _networkStream = tcpSocket.GetStream();
            
            _connectionSuccessfulSafeContainer.Add(new ConnectionSuccessfulSafeContainer());
        }
        
        private void CheckAvailableBytes()
        {
            if (_networkStream != null && _networkStream.CanRead && _networkStream.DataAvailable && _isCanRead)
            {
                var availableBytes = _tcpSocket.Available;

                if (availableBytes > _receiveBufferSize)
                {
                    _receiveBufferOversize = new byte[availableBytes];
                    _reader = new NetFrameReader(new byte[availableBytes]);
                    _isOversizeReceiveBuffer = true;
                    
                    _isCanRead = false;
                }
                else if (_isOversizeReceiveBuffer)
                {
                    _isOversizeReceiveBuffer = false;
                    _reader = new NetFrameReader(new byte[_receiveBufferSize]);
                    
                    _isCanRead = false;
                }

                if (_isOversizeReceiveBuffer)
                {
                    _networkStream.BeginRead(_receiveBufferOversize, 0, availableBytes, BeginReadBytesCallback, null);
                }
                else
                {
                    _networkStream.BeginRead(_receiveBuffer, 0, _receiveBufferSize, BeginReadBytesCallback, null);
                }
            }
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
                _isCanRead = true;

                if (byteReadLength <= 0)
                {
                    return;
                }

                var allBytes = new byte[byteReadLength];

                Array.Copy( _isOversizeReceiveBuffer ? _receiveBufferOversize : _receiveBuffer, 
                    allBytes, byteReadLength);
                
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
                    var headerDataframe = Encoding.UTF8.GetString(headerSegment);

                    readBytesCompleteCount += packageSize;

                    if (!NetFrameDataframeCollection.TryGetByKey(headerDataframe, out var dataframe))
                    {
                        ConnectedFailed?.Invoke(ReasonServerConnectionFailed.NoDataframe, $"headerDataframe: {headerDataframe}");
                        _disconnectSafeContainer.Add(new DisconnectSafeContainer());
                        continue;
                    }
                    
                    var targetType = dataframe.GetType();
                    
                    _reader.SetBuffer(contentSegment);
                    dataframe.Read(_reader);
                    
                    if (_handlers.TryGetValue(targetType, out var handler))
                    {
                        _dynamicInvokeForClientSafeContainer.Add(new DynamicInvokeForClientSafeContainer
                        {
                            Handlers = handler,
                            Dataframe = dataframe,
                        });
                    }
                } 
                while (readBytesCompleteCount < allBytes.Length);
            }
            catch (Exception e)
            {
                Console.WriteLine($"[NetFrameClient.BeginReadBytesCallback] Error receive TCP Client {e.Message}");
                //Debug.LogError($"[NetFrameClient.BeginReadBytesCallback] Error receive TCP Client {e.Message}");
                
                _disconnectSafeContainer.Add(new DisconnectSafeContainer());
            }
        }

        public void Disconnect()
        {
            if (_tcpSocket != null && _tcpSocket.Connected)
            {
                _tcpSocket.Close();

                Disconnected?.Invoke();
            }
        }

        public void Send<T>(ref T dataframe) where T : struct, INetworkDataframe
        {
            _writer.Reset();
            dataframe.Write(_writer);

            var separator = '\n';
            var headerDataframe = GetByTypeName(dataframe) + separator;

            var heaterDataframe = Encoding.UTF8.GetBytes(headerDataframe);
            var dataDataframe = _writer.ToArraySegment();
            var allData = heaterDataframe.Concat(dataDataframe).ToArray();

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

        public void Subscribe<T>(Action<T> handler) where T : struct, INetworkDataframe
        {
            _handlers.AddOrUpdate(typeof(T), new List<Delegate> { handler }, (_, currentHandlers) => 
            {
                currentHandlers ??= new List<Delegate>();
                currentHandlers.Add(handler);
                return currentHandlers;
            });
        }

        public void Unsubscribe<T>(Action<T> handler) where T : struct, INetworkDataframe
        {
            if (_handlers.TryGetValue(typeof(T), out var handlers))
            {
                handlers.Remove(handler);
                
                if (handlers.Count == 0)
                {
                    _handlers.TryRemove(typeof(T), out _);
                }
            }
        }

        private string GetByTypeName<T>(T dataframe) where T : struct, INetworkDataframe
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
            
            ConnectedFailed?.Invoke(ReasonServerConnectionFailed.ConnectionLost, string.Empty);
        }
    }
}