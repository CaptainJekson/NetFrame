using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using NetFrame.Constants;
using NetFrame.ThreadSafeContainers;
using NetFrame.Utils;
using NetFrame.WriteAndRead;

namespace NetFrame.Server
{
    public class OldNetFrameServer
    {
        private TcpListener _tcpServer;
        private int _maxClient;
        private int _receiveBufferSize;
        private int _writeBufferSize;
        private int _clientMaxId;
        
        private Dictionary<int, NetFrameClientOnServer> _clients;
        private NetFrameWriter _writer;
        private NetFrameByteConverter _byteConverter;
        private ConcurrentDictionary<Type, List<Delegate>> _handlers;

        private readonly ThreadSafeContainer<ClientConnectionSafeContainer> _clientConnectionSafeContainer;

        public event Action<int> ClientConnection;
        public event Action<int> ClientDisconnect;

        public OldNetFrameServer()
        {
            _byteConverter = new NetFrameByteConverter();
            _handlers = new ConcurrentDictionary<Type, List<Delegate>>();
            
            _clientConnectionSafeContainer = new ThreadSafeContainer<ClientConnectionSafeContainer>();
        }

        public void Start(int port, int maxClient, int receiveBufferSize = 4096, int writeBufferSize = 4096)
        {
            _tcpServer = new TcpListener(IPAddress.Any, port);
            _maxClient = maxClient;
            _clients = new Dictionary<int, NetFrameClientOnServer>();
            
            _receiveBufferSize = receiveBufferSize;
            _writeBufferSize = writeBufferSize;
            
            _writer = new NetFrameWriter(_writeBufferSize);

            _tcpServer.Start();
            
            _tcpServer.BeginAcceptTcpClient(ConnectedClientCallback, _tcpServer);
        }
        
        public void Run()
        {
            foreach (var response in _clientConnectionSafeContainer)
            {
                _clientMaxId++;
             
                var netFrameClientOnServer = new NetFrameClientOnServer(_clientMaxId, response.TcpClient, _handlers, _receiveBufferSize);

                _clients.Add(_clientMaxId, netFrameClientOnServer);
                _clients[_clientMaxId].IsCanRead = true;
                
                ClientConnection?.Invoke(_clientMaxId);
            }
            
            CheckDisconnectClients();
            CheckAvailableBytesForClientsAndHandlerSafeContainer();
        }

        public void Stop()
        {
            foreach (var client in _clients)
            {
                client.Value.Disconnect();
            }
            
            _clients.Clear();
            
            _tcpServer.Stop();
            _tcpServer.Server.Disconnect(false);
            _tcpServer.Server.Dispose();
        }

        private void ConnectedClientCallback(IAsyncResult result)
        {
            var listener = (TcpListener) result.AsyncState;
            var tcpClient = listener.EndAcceptTcpClient(result);
            
            if (_clients.Count == _maxClient)
            {
                Console.WriteLine("Maximum number of clients exceeded");
                return;
            }

            _clientConnectionSafeContainer.Add(new ClientConnectionSafeContainer
            {
                TcpClient =  tcpClient,
            });

            _tcpServer.BeginAcceptTcpClient(ConnectedClientCallback, _tcpServer);
        }

        public void Send<T>(ref T dataframe, int clientId) where T : struct, INetworkDataframe
        {
            var client = _clients[clientId];
            var clientStream = client.TcpSocket.GetStream();

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
                await SendAsync(clientStream, allPackage);
            });
        }

        public void SendAll<T>(ref T dataframe) where T : struct, INetworkDataframe
        {
            foreach (var clientId in _clients.Keys)
            {
                Send(ref dataframe, clientId);
            }
        }

        public void Subscribe<T>(Action<T, int> handler) where T : struct, INetworkDataframe
        {
            _handlers.AddOrUpdate(typeof(T), new List<Delegate> { handler }, (_, currentHandlers) => 
            {
                currentHandlers ??= new List<Delegate>();
                currentHandlers.Add(handler);
                return currentHandlers;
            });
        }

        public void Unsubscribe<T>(Action<T, int> handler) where T : struct, INetworkDataframe
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

        private async Task SendAsync(NetworkStream networkStream, ArraySegment<byte> data)
        {
            await networkStream.WriteAsync(data);
        }

        private string GetByTypeName<T>(T dataframe) where T : struct, INetworkDataframe
        {
            return typeof(T).Name;
        }
        
        private void CheckAvailableBytesForClientsAndHandlerSafeContainer()
        {
            foreach (var client in _clients)
            {
                client.Value.CheckAvailableBytes();
                client.Value.RunHandlerSafeContainer();
            }
        }

        private void CheckDisconnectClients()
        {
            foreach (var clientEntry in _clients.ToList())
            {
                var client = clientEntry.Value;
                if (!client.TcpSocket.Connected || client.TcpSocket.Client.Poll(0, SelectMode.SelectRead) && client.TcpSocket.Available == 0)
                {
                    ClientDisconnect?.Invoke(clientEntry.Key);
                    client.Disconnect();
                    _clients.Remove(clientEntry.Key);
                }
            }
        }
    }
}