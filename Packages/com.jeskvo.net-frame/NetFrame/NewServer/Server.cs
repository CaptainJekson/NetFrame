using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using NetFrame.Constants;
using NetFrame.Utils;
using NetFrame.WriteAndRead;

namespace NetFrame.NewServer
{
    //todo привести код в порядок и понять как все работает
    public class Server
    {
        public int SendTimeout = 5000;
        public int SendQueueLimit = 10000;
        public int ReceiveQueueLimit = 10000;

        public int ReceiveTimeout = 0;
        public bool NoDelay = true;
        public readonly int MaxMessageSize;

        private TcpListener _tcpListener;
        private Thread _listenerThread;
        private MagnificentReceivePipe _receivePipe;
        
        private readonly ConcurrentDictionary<int, ConnectionState> _clients;
        private readonly ConcurrentDictionary<Type, List<Delegate>> _handlers;
        private readonly NetFrameByteConverter _byteConverter;
        private NetFrameReader _reader;
        private NetFrameWriter _writer;
        
        public int ReceivePipeTotalCount => _receivePipe.TotalCount;
        
        private int _clientIdCounter;
        
        private bool Active => _listenerThread != null && _listenerThread.IsAlive;
        
        public event Action<int> ClientConnection;
        public event Action<int> ClientDisconnect;
        public event Action<LogType, string> LogCall;
        
        public Server(int maxMessageSize)
        {
            MaxMessageSize = maxMessageSize;

            _clients = new ConcurrentDictionary<int, ConnectionState>();
            _handlers = new ConcurrentDictionary<Type, List<Delegate>>();
            _byteConverter = new NetFrameByteConverter();
            _writer = new NetFrameWriter(); //todo что с размером ??? он будет увеличиваться???
        }

        private int NextConnectionId()
        {
            int id = Interlocked.Increment(ref _clientIdCounter);
            
            if (id == int.MaxValue)
            {
                throw new Exception("connection id limit reached: " + id);
            }

            return id;
        }

        private void Listen(int port)
        {
            try
            {
                _tcpListener = TcpListener.Create(port);
                _tcpListener.Server.NoDelay = NoDelay;
                _tcpListener.Start();
                
                LogCall?.Invoke(LogType.Info, $"[NetFrameServer.Listen] Starting server on port {port}");
                
                while (true)
                {
                    TcpClient client = _tcpListener.AcceptTcpClient();
                    
                    client.NoDelay = NoDelay;
                    client.SendTimeout = SendTimeout;
                    client.ReceiveTimeout = ReceiveTimeout;
                    
                    int connectionId = NextConnectionId();
                    
                    ConnectionState connection = new ConnectionState(client, MaxMessageSize);
                    _clients[connectionId] = connection;

                    Thread sendThread = new Thread(() =>
                    {
                        try
                        {
                            ThreadFunctions.SendLoop(connectionId, client, connection.sendPipe, connection.sendPending);
                        }
                        catch (ThreadAbortException)
                        {
                            
                        }
                        catch (Exception exception)
                        {
                            LogCall?.Invoke(LogType.Error, "[NetFrameServer.Listen] Server send thread exception: " + exception);
                        }
                    });
                    sendThread.IsBackground = true;
                    sendThread.Start();

                    Thread receiveThread = new Thread(() =>
                    {
                        try
                        {
                            ThreadFunctions.ReceiveLoop(connectionId, client, MaxMessageSize, _receivePipe, ReceiveQueueLimit);
                            sendThread.Interrupt();
                        }
                        catch (Exception exception)
                        {
                            LogCall?.Invoke(LogType.Error, "[Telepathy] Server client thread exception: " + exception);
                        }
                    });
                    receiveThread.IsBackground = true;
                    receiveThread.Start();
                }
            }
            catch (ThreadAbortException exception)
            {
                LogCall?.Invoke(LogType.Info, "[NetFrameServer.Listen] Server thread aborted. That's okay. " + exception);
            }
            catch (SocketException exception)
            {
                LogCall?.Invoke(LogType.Info, "[NetFrameServer.Listen] Server Thread stopped. That's okay. " + exception);
            }
            catch (Exception exception)
            {
                LogCall?.Invoke(LogType.Error, "[NetFrameServer.Listen] Server Exception: " + exception);
            }
        }
        
        public bool Start(int port, int maxClients) //todo сделать функционал с maxClients
        {
            if (Active) return false;
            
            _receivePipe = new MagnificentReceivePipe(MaxMessageSize);
            
            LogCall?.Invoke(LogType.Info, $"[NetFrameServer.Start] Starting server on port {port}");

            _listenerThread = new Thread(() => { Listen(port); });
            _listenerThread.IsBackground = true;
            _listenerThread.Priority = ThreadPriority.BelowNormal;
            _listenerThread.Start();
            return true;
        }

        public void Stop()
        {
            if (!Active) return;

            LogCall?.Invoke(LogType.Info, $"[NetFrameServer.Stop] Server: stopping...");
            
            _tcpListener?.Stop();
            
            _listenerThread?.Interrupt();
            _listenerThread = null;
            
            foreach (KeyValuePair<int, ConnectionState> keyValuePair in _clients)
            {
                TcpClient tcpClient = keyValuePair.Value.tcpClient;
                try
                {
                    tcpClient.GetStream().Close();

                }
                catch
                {
                    
                }
                tcpClient.Close();
            }
            
            _clients.Clear();
            
            _clientIdCounter = 0;
        }
        
        public void Send<T>(ref T dataframe, int clientId) where T : struct, INetworkDataframe
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

            Send(clientId, allPackage);
        }
        
        public void SendAll<T>(ref T dataframe) where T : struct, INetworkDataframe
        {
            foreach (var clientId in _clients.Keys)
            {
                Send(ref dataframe, clientId);
            }
        }
        
        private string GetByTypeName<T>(T dataframe) where T : struct, INetworkDataframe
        {
            return typeof(T).Name;
        }
        
        public bool Send(int connectionId, ArraySegment<byte> message) //TODO
        {
            if (message.Count <= MaxMessageSize)
            {
                if (_clients.TryGetValue(connectionId, out ConnectionState connection))
                {
                    if (connection.sendPipe.Count < SendQueueLimit)
                    {
                        connection.sendPipe.Enqueue(message);
                        connection.sendPending.Set();
                        return true;
                    }

                    LogCall?.Invoke(LogType.Warning, $"[NetFrameClient.Send] Server.Send: sendPipe for connection {connectionId} reached limit of {SendQueueLimit}. This can happen if we call send faster than the network can process messages. Disconnecting this connection for load balancing.");
                    connection.tcpClient.Close();
                    return false;
                }
                return false;
            }

            LogCall?.Invoke(LogType.Error, "[NetFrameClient.Send] Server.Send: message too big: " + message.Count + ". Limit: " + MaxMessageSize);
            return false;
        }

        //the client's IP address is sometimes required by the server, for example, for bans
        public string GetClientAddress(int connectionId)
        {
            if (_clients.TryGetValue(connectionId, out ConnectionState connection))
            {
                return ((IPEndPoint)connection.tcpClient.Client.RemoteEndPoint).Address.ToString();
            }
            return "";
        }

        // disconnect (kick) a client
        public bool Disconnect(int clientId)
        {
            if (_clients.TryGetValue(clientId, out ConnectionState connection))
            {
                connection.tcpClient.Close();
                LogCall?.Invoke(LogType.Info, "[NetFrameClient.Send] Server.Disconnect connectionId:" + clientId);
                return true;
            }
            return false;
        }
        
        public int Run(int processLimit, Func<bool> checkEnabled = null)
        {
            if (_receivePipe == null)
                return 0;

            for (int i = 0; i < processLimit; ++i)
            {
                if (checkEnabled != null && !checkEnabled())
                    break;
                
                if (_receivePipe.TryPeek(out int connectionId, out EventType eventType, out ArraySegment<byte> message))
                {
                    switch (eventType)
                    {
                        case EventType.Connected:
                            ClientConnection?.Invoke(connectionId);
                            break;
                        case EventType.Data:
                            BeginReadDataframe(connectionId, message); //TODO проверить как это будет работать когда будет большие пачки сообщений подряд
                            break;
                        case EventType.Disconnected:
                            ClientDisconnect?.Invoke(connectionId);
                            _clients.TryRemove(connectionId, out ConnectionState _);
                            break;
                    }
                    
                    _receivePipe.TryDequeue();
                }
                else
                {
                    break;
                }
            }
            
            return _receivePipe.TotalCount;
        }

        private void BeginReadDataframe(int clientId, ArraySegment<byte> receiveBytes)
        {
            var allBytes = receiveBytes.Array;

            if (allBytes == null)
            {
                return;
            }
            
            var packageSizeSegment = new ArraySegment<byte>(allBytes, 0, NetFrameConstants.SizeByteCount);
            var packageSize = Utils.BytesToIntBigEndian(packageSizeSegment.ToArray()); //todo GetUIntFromByteArray allocate use
            var packageBytes = new ArraySegment<byte>(allBytes, 0, packageSize);
            
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
            
            if (!NetFrameDataframeCollection.TryGetByKey(headerDataframe, out var dataframe))
            {
                LogCall?.Invoke(LogType.Error, $"[NetFrameClientOnServer.BeginReadBytesCallback] no datagram: {headerDataframe}");
                Disconnect(clientId);
                return;
            }
            
            var targetType = dataframe.GetType();

            _reader = new NetFrameReader(new byte[packageSize]);
            _reader.SetBuffer(contentSegment);
            
            dataframe.Read(_reader);

            if (_handlers.TryGetValue(targetType, out var handlers))
            {
                foreach (var handler in handlers)
                {
                    handler.DynamicInvoke(dataframe, clientId);
                }
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
    }
}