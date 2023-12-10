using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using NetFrame.Queues;
using NetFrame.Utils;
using NetFrame.WriteAndRead;
using NetFrame.Enums;

namespace NetFrame.Server
{
    public class NetFrameServer
    {
        private readonly int _sendTimeout = 5000;
        private readonly int _sendQueueLimit = 10000;
        private readonly int _receiveQueueLimit = 10000;

        private readonly int _receiveTimeout = 0;
        private readonly bool _noDelay = true;
        private readonly int _maxMessageSize;

        private TcpListener _tcpListener;
        private Thread _listenerThread;
        private ReceiveQueue _receiveQueue;
        
        private readonly ConcurrentDictionary<int, ConnectionState> _clients;
        private readonly ConcurrentDictionary<Type, List<Delegate>> _handlers;
        private readonly NetFrameByteConverter _byteConverter;
        private readonly NetFrameWriter _writer;
        private NetFrameReader _reader;
        
        public int ReceivePipeTotalCount => _receiveQueue.TotalCount;
        
        private int _clientIdCounter;
        private int _maxClients;
        
        private bool Active => _listenerThread != null && _listenerThread.IsAlive;
        
        public event Action<int> ClientConnection;
        public event Action<int> ClientDisconnect;
        public event Action<LogType, string> LogCall;
        
        public NetFrameServer(int maxMessageSize)
        {
            _maxMessageSize = maxMessageSize;

            _clients = new ConcurrentDictionary<int, ConnectionState>();
            _handlers = new ConcurrentDictionary<Type, List<Delegate>>();
            _byteConverter = new NetFrameByteConverter();
            _writer = new NetFrameWriter();
        }
        
        public bool Start(int port, int maxClients)
        {
            if (Active)
            {
                return false;
            }

            _receiveQueue = new ReceiveQueue(_maxMessageSize);
            _maxClients = maxClients;
            
            LogCall?.Invoke(LogType.Info, $"[NetFrameServer.Start] Starting server on port {port}");

            _listenerThread = new Thread(() =>
            {
                ListenConnectionClients(port);
            });
            
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
                TcpClient tcpClient = keyValuePair.Value.TcpClient;
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

            Send(clientId, allData);
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
        
        public int Run(int processLimit, Func<bool> checkEnabled = null)
        {
            if (_receiveQueue == null)
                return 0;

            for (int i = 0; i < processLimit; ++i)
            {
                if (checkEnabled != null && !checkEnabled())
                    break;
                
                if (_receiveQueue.TryPeek(out int connectionId, out EventType eventType, out ArraySegment<byte> message))
                {
                    switch (eventType)
                    {
                        case EventType.Connected:
                            ClientConnection?.Invoke(connectionId);
                            break;
                        case EventType.Data:
                            BeginReadDataframe(connectionId, message);
                            break;
                        case EventType.Disconnected:
                            ClientDisconnect?.Invoke(connectionId);
                            _clients.TryRemove(connectionId, out ConnectionState _);
                            break;
                    }
                    
                    _receiveQueue.TryDequeue();
                }
                else
                {
                    break;
                }
            }
            
            return _receiveQueue.TotalCount;
        }
        
        //the client's IP address is sometimes required by the server, for example, for bans
        public string GetClientAddress(int connectionId)
        {
            if (_clients.TryGetValue(connectionId, out ConnectionState connection))
            {
                return ((IPEndPoint)connection.TcpClient.Client.RemoteEndPoint).Address.ToString();
            }
            return "";
        }

        // disconnect (kick) a client
        public bool Disconnect(int clientId)
        {
            if (_clients.TryGetValue(clientId, out ConnectionState connection))
            {
                connection.TcpClient.Close();
                LogCall?.Invoke(LogType.Info, "[NetFrameClient.Send] Server.Disconnect connectionId:" + clientId);
                return true;
            }
            return false;
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

        private void ListenConnectionClients(int port)
        {
            try
            {
                _tcpListener = TcpListener.Create(port);
                _tcpListener.Server.NoDelay = _noDelay;
                _tcpListener.Start();
                
                LogCall?.Invoke(LogType.Info, $"[NetFrameServer.Listen] Starting server on port {port}");
                
                while (true)
                {
                    TcpClient tcpClient = _tcpListener.AcceptTcpClient();

                    if (_clients.Count >= _maxClients)
                    {
                        try
                        {
                            tcpClient.GetStream().Close();
                        }
                        catch
                        {
                    
                        }
                        
                        tcpClient.Close();
                        
                        LogCall?.Invoke(LogType.Warning, $"[NetFrameServer.Listen] The customer limit has been reached: {_clients.Count}");
                        
                        continue;
                    }
                    
                    tcpClient.NoDelay = _noDelay;
                    tcpClient.SendTimeout = _sendTimeout;
                    tcpClient.ReceiveTimeout = _receiveTimeout;
                    
                    int connectionId = NextConnectionId();
                    
                    ConnectionState connection = new ConnectionState(tcpClient, _maxMessageSize);
                    _clients[connectionId] = connection;

                    Thread sendThread = new Thread(() =>
                    {
                        try
                        {
                            ThreadFunctions.SendLoop(connectionId, tcpClient, connection.SendQueue, connection.SendPending);
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
                            ThreadFunctions.ReceiveLoop(connectionId, tcpClient, _maxMessageSize, _receiveQueue, _receiveQueueLimit);
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

        private string GetByTypeName<T>(T dataframe) where T : struct, INetworkDataframe
        {
            return typeof(T).Name;
        }

        private bool Send(int connectionId, ArraySegment<byte> message)
        {
            if (message.Count <= _maxMessageSize)
            {
                if (_clients.TryGetValue(connectionId, out ConnectionState connection))
                {
                    if (connection.SendQueue.Count < _sendQueueLimit)
                    {
                        connection.SendQueue.Enqueue(message);
                        connection.SendPending.Set();
                        return true;
                    }

                    LogCall?.Invoke(LogType.Warning, $"[NetFrameClient.Send] Server.Send: sendPipe for connection {connectionId} reached limit of {_sendQueueLimit}. This can happen if we call send faster than the network can process messages. Disconnecting this connection for load balancing.");
                    connection.TcpClient.Close();
                    return false;
                }
                return false;
            }

            LogCall?.Invoke(LogType.Error, "[NetFrameClient.Send] Server.Send: message too big: " + message.Count + ". Limit: " + _maxMessageSize);
            return false;
        }

        private void BeginReadDataframe(int clientId, ArraySegment<byte> receiveBytes)
        {
            var allBytes = receiveBytes.Array;

            if (allBytes == null)
            {
                return;
            }

            var tempIndex = 0;
            for (var index = 0; index < allBytes.Length; index++)
            {
                var b = receiveBytes[index];

                if (b == '\n')
                {
                    tempIndex = index + 1;
                    break;
                }
            }
            
            var headerSegment = new ArraySegment<byte>(receiveBytes.ToArray(),0,tempIndex - 1);
            var contentSegment = new ArraySegment<byte>(receiveBytes.ToArray(), tempIndex, receiveBytes.Count - tempIndex);
            var headerDataframe = Encoding.UTF8.GetString(headerSegment);
            
            if (!NetFrameDataframeCollection.TryGetByKey(headerDataframe, out var dataframe))
            {
                LogCall?.Invoke(LogType.Error, $"[NetFrameClientOnServer.BeginReadBytesCallback] no datagram: {headerDataframe}");
                Disconnect(clientId);
                return;
            }
            
            var targetType = dataframe.GetType();

            _reader = new NetFrameReader(new byte[_maxMessageSize]);
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
    }
}