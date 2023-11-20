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
    public class NetFrameServer
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

        public NetFrameServer()
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
                var clientId = response.NewClientId;
                var newClient = response.NewClient;
                
                _clients.Add(clientId, newClient);
                ClientConnection?.Invoke(clientId);
                _clients.Last().Value.IsCanRead = true;
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
            var client = listener.EndAcceptTcpClient(result);
            
            if (_clients.Count == _maxClient)
            {
                Console.WriteLine("Maximum number of clients exceeded");
                return;
            }
             
            var clientId = _clientMaxId;
            _clientMaxId++;
             
            var netFrameClientOnServer = new NetFrameClientOnServer(clientId, client, _handlers, _receiveBufferSize);

            _clientConnectionSafeContainer.Add(new ClientConnectionSafeContainer
            {
                NewClientId =  clientId,
                NewClient = netFrameClientOnServer,
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
            foreach (var client in _clients.ToList())
            {
                if (!client.Value.TcpSocket.Connected)
                {
                    ClientDisconnect?.Invoke(client.Key);
                    _clients.Remove(client.Key);
                    continue;
                }
                
                if (!client.Value.TcpSocket.Client.Poll(0, SelectMode.SelectRead))
                {
                    continue;
                }

                var buff = new byte[1];

                if (client.Value.TcpSocket.Client.Receive(buff, SocketFlags.Peek) != 0)
                {
                    continue;
                }

                ClientDisconnect?.Invoke(client.Key);
                client.Value.Disconnect();
                _clients.Remove(client.Key);
            }
        }
    }
}