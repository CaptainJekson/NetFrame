using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using NetFrame.Constants;
using NetFrame.Utils;
using NetFrame.WriteAndRead;

namespace NetFrame.NewServer
{
    //todo теперь надо сделать отправку сообщений
    public class Server
    {
        private readonly NetFrameByteConverter _byteConverter = new NetFrameByteConverter();
        
        public int SendTimeout = 5000;

        public int ReceiveTimeout = 0;
        public bool NoDelay = true;
        public readonly int MaxMessageSize;

        public event Action<int> ClientConnection;
        public event Action<int> ClientDisconnect;
        
        public TcpListener listener;
        Thread listenerThread;
        
        public int SendQueueLimit = 10000;
        public int ReceiveQueueLimit = 10000;

        protected MagnificentReceivePipe receivePipe;
        
        public int ReceivePipeTotalCount => receivePipe.TotalCount;
        
        readonly ConcurrentDictionary<int, ConnectionState> _clients = new ConcurrentDictionary<int, ConnectionState>();
        private ConcurrentDictionary<Type, List<Delegate>> _handlers = new ConcurrentDictionary<Type, List<Delegate>>();
        
        int counter;
        
        private bool Active => listenerThread != null && listenerThread.IsAlive;
        
        public int NextConnectionId()
        {
            int id = Interlocked.Increment(ref counter);
            
            if (id == int.MaxValue)
            {
                throw new Exception("connection id limit reached: " + id);
            }

            return id;
        }
        
        
        public Server(int maxMessageSize)
        {
            MaxMessageSize = maxMessageSize;

            _clients = new ConcurrentDictionary<int, ConnectionState>();
            _handlers = new ConcurrentDictionary<Type, List<Delegate>>();
        }
        
        private void Listen(int port)
        {
            try
            {
                listener = TcpListener.Create(port);
                listener.Server.NoDelay = NoDelay;
                listener.Start();
                //Log.Info($"[Telepathy] Starting server on port {port}"); //TODO !!!!

                // keep accepting new clients
                while (true)
                {
                    TcpClient client = listener.AcceptTcpClient();
                    
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
                            //Log.Error("[Telepathy] Server send thread exception: " + exception); //TODO!!!!!
                        }
                    });
                    sendThread.IsBackground = true;
                    sendThread.Start();

                    Thread receiveThread = new Thread(() =>
                    {
                        try
                        {
                            ThreadFunctions.ReceiveLoop(connectionId, client, MaxMessageSize, receivePipe, ReceiveQueueLimit);
                            sendThread.Interrupt();
                        }
                        catch (Exception exception)
                        {
                            //Log.Error("[Telepathy] Server client thread exception: " + exception); //TODO !!!!!
                        }
                    });
                    receiveThread.IsBackground = true;
                    receiveThread.Start();
                }
            }
            catch (ThreadAbortException exception)
            {
                //Log.Info("[Telepathy] Server thread aborted. That's okay. " + exception); ////TODO !!!!!
            }
            catch (SocketException exception)
            {
                //Log.Info("[Telepathy] Server Thread stopped. That's okay. " + exception); //TODO !!!!!
            }
            catch (Exception exception)
            {
                //Log.Error("[Telepathy] Server Exception: " + exception); //TODO !!!!!
            }
        }
        
        public bool Start(int port, int maxClients) //todo сделать функционал с maxClients
        {
            if (Active) return false;
            
            receivePipe = new MagnificentReceivePipe(MaxMessageSize);
            
            //Log.Info($"[Telepathy] Starting server on port {port}"); //TODO !!!!!

            listenerThread = new Thread(() => { Listen(port); });
            listenerThread.IsBackground = true;
            listenerThread.Priority = ThreadPriority.BelowNormal;
            listenerThread.Start();
            return true;
        }

        public void Stop()
        {
            if (!Active) return;

            //Log.Info("[Telepathy] Server: stopping...");  //TODO !!!!!
            
            listener?.Stop();
            
            listenerThread?.Interrupt();
            listenerThread = null;
            
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
            
            counter = 0;
        }
        
        public bool Send(int connectionId, ArraySegment<byte> message)
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
                    else
                    {
                        //TODO 
                        //Log.Warning($"[Telepathy] Server.Send: sendPipe for connection {connectionId} reached limit of {SendQueueLimit}. This can happen if we call send faster than the network can process messages. Disconnecting this connection for load balancing.");
                        connection.tcpClient.Close();
                        return false;
                    }
                }
                return false;
            }
            //Log.Error("[Telepathy] Server.Send: message too big: " + message.Count + ". Limit: " + MaxMessageSize); //TODO
            return false;
        }

        // ip-адрес клиента иногда требуется серверу, например, для банов
        public string GetClientAddress(int connectionId)
        {
            if (_clients.TryGetValue(connectionId, out ConnectionState connection))
            {
                return ((IPEndPoint)connection.tcpClient.Client.RemoteEndPoint).Address.ToString();
            }
            return "";
        }

        // disconnect (kick) a client
        public bool Disconnect(int connectionId)
        {
            // find the connection
            if (_clients.TryGetValue(connectionId, out ConnectionState connection))
            {
                // just close it. send thread will take care of the rest.
                connection.tcpClient.Close();
                // Log.Info("[Telepathy] Server.Disconnect connectionId:" + connectionId); //TODO 
                return true;
            }
            return false;
        }
        
        public int Run(int processLimit, Func<bool> checkEnabled = null)
        {
            if (receivePipe == null)
                return 0;

            for (int i = 0; i < processLimit; ++i)
            {
                if (checkEnabled != null && !checkEnabled())
                    break;
                
                if (receivePipe.TryPeek(out int connectionId, out EventType eventType, out ArraySegment<byte> message))
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
                    
                    receivePipe.TryDequeue();
                }
                else
                {
                    break;
                }
            }
            
            return receivePipe.TotalCount;
        }

        private void BeginReadDataframe(int clientId, ArraySegment<byte> receiveBytes) //TODO метод который конвертит датафрейм
        {
            var allBytes = receiveBytes.Array;

            if (allBytes == null)
            {
                return;
            }
            
            var packageSizeSegment = new ArraySegment<byte>(allBytes, 0, NetFrameConstants.SizeByteCount);
            var packageSize = _byteConverter.GetUIntFromByteArray(packageSizeSegment.ToArray());
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
                Console.WriteLine($"[NetFrameClientOnServer.BeginReadBytesCallback] no datagram: {headerDataframe}");
                //Debug.LogError($"[NetFrameClientOnServer.BeginReadBytesCallback] no datagram: {headerDataframe}");
                //TODO тут надо принудительно отключить такого клиента ---------->
                return;
            }
            
            var targetType = dataframe.GetType();

            var availableBytes = 1024;
            var _reader = new NetFrameReader(new byte[availableBytes]); //TODO проверить расширение бефера
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
        
        //TODO из NetFrameServer
        public void Subscribe<T>(Action<T, int> handler) where T : struct, INetworkDataframe
        {
            _handlers.AddOrUpdate(typeof(T), new List<Delegate> { handler }, (_, currentHandlers) => 
            {
                currentHandlers ??= new List<Delegate>();
                currentHandlers.Add(handler);
                return currentHandlers;
            });
        }

        //TODO из NetFrameServer
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