using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using NetFrame.Constants;
using NetFrame.Utils;
using NetFrame.WriteAndRead;

namespace NetFrame.NewServer
{
    public class Client
    {
        public int SendQueueLimit = 10000;
        public int ReceiveQueueLimit = 10000;
        public bool NoDelay = true;
        public readonly int MaxMessageSize;
        public int SendTimeout = 5000;
        public int ReceiveTimeout = 0;
        
        private NetFrameWriter _writer;
        private NetFrameReader _reader;
        private ClientConnectionState _state;
        private readonly NetFrameByteConverter _byteConverter;
        private readonly ConcurrentDictionary<Type, List<Delegate>> _handlers;
        
        public bool Connected => _state != null && _state.Connected;
        public bool Connecting => _state != null && _state.Connecting;
        
        public int ReceivePipeCount => _state != null ? _state.receivePipe.TotalCount : 0;

        public event Action ConnectionSuccessful;
        public event Action Disconnected;
        
        public Client(int MaxMessageSize)
        {
            this.MaxMessageSize = MaxMessageSize;
            _writer = new NetFrameWriter(); //todo что с размером ??? он будет увеличиваться???
            _byteConverter = new NetFrameByteConverter();
            _handlers = new ConcurrentDictionary<Type, List<Delegate>>();
        }
        
        private static void ReceiveThreadFunction(ClientConnectionState state, string ip, int port, int MaxMessageSize, bool NoDelay, int SendTimeout, int ReceiveTimeout, int ReceiveQueueLimit)
        {
            Thread sendThread = null;
            try
            {
                state.tcpClient.Connect(ip, port);
                state.Connecting = false;
                
                state.tcpClient.NoDelay = NoDelay;
                state.tcpClient.SendTimeout = SendTimeout;
                state.tcpClient.ReceiveTimeout = ReceiveTimeout;
                
                sendThread = new Thread(() => { ThreadFunctions.SendLoop(0, state.tcpClient, state.sendPipe, state.sendPending); });
                sendThread.IsBackground = true;
                sendThread.Start();
                
                ThreadFunctions.ReceiveLoop(0, state.tcpClient, MaxMessageSize, state.receivePipe, ReceiveQueueLimit);
            }
            catch (SocketException exception)
            {
                //Log.Info("[Telepathy] Client Recv: failed to connect to ip=" + ip + " port=" + port + " reason=" + exception); //TODO
            }
            catch (ThreadInterruptedException)
            {
                // expected if Disconnect() aborts it
            }
            catch (ThreadAbortException)
            {
                
            }
            catch (ObjectDisposedException)
            {
          
            }
            catch (Exception exception)
            {
                //Log.Error("[Telepathy] Client Recv Exception: " + exception); //TODO
            }
            state.receivePipe.Enqueue(0, EventType.Disconnected, default);
            sendThread?.Interrupt();
            
            state.Connecting = false;
            state.tcpClient?.Close();
        }

        public void Connect(string ip, int port)
        {
            // not if already started
            if (Connecting || Connected)
            {
                //Log.Warning("[Telepathy] Client can not create connection because an existing connection is connecting or connected"); //TODO
                return;
            }
            
            _state = new ClientConnectionState(MaxMessageSize);
            
            _state.Connecting = true;
            
            _state.tcpClient.Client = null;
            
            _state.receiveThread = new Thread(() => {
                ReceiveThreadFunction(_state, ip, port, MaxMessageSize, NoDelay, SendTimeout, ReceiveTimeout, ReceiveQueueLimit);
            });
            _state.receiveThread.IsBackground = true;
            _state.receiveThread.Start();
        }

        public void Disconnect()
        {
            if (Connecting || Connected)
            {
                _state.Dispose();
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

            Send(allPackage);
        }
        
        private string GetByTypeName<T>(T dataframe) where T : struct, INetworkDataframe
        {
            return typeof(T).Name;
        }
        
        public bool Send(ArraySegment<byte> message)
        {
            if (Connected)
            {
                if (message.Count <= MaxMessageSize)
                {
                    // check send pipe limit
                    if (_state.sendPipe.Count < SendQueueLimit)
                    {
                        _state.sendPipe.Enqueue(message);
                        _state.sendPending.Set();
                        return true;
                    }
                    else
                    {
                        // log the reason
                        //TODO
                        //Log.Warning($"[Telepathy] Client.Send: sendPipe reached limit of {SendQueueLimit}. This can happen if we call send faster than the network can process messages. Disconnecting to avoid ever growing memory & latency.");

                        // just close it. send thread will take care of the rest.
                        _state.tcpClient.Close();
                        return false;
                    }
                }
                //TODO
                //Log.Error("[Telepathy] Client.Send: message too big: " + message.Count + ". Limit: " + MaxMessageSize);
                return false;
            }
            //TODO
            //Log.Warning("[Telepathy] Client.Send: not connected!");
            return false;
        }

        public int Run(int processLimit, Func<bool> checkEnabled = null)
        {
            if (_state == null)
            {
                return 0;
            }
            
            for (int i = 0; i < processLimit; ++i)
            {
                if (checkEnabled != null && !checkEnabled())
                {
                    break;
                }

                if (_state.receivePipe.TryPeek(out int _, out EventType eventType, out ArraySegment<byte> message))
                {
                    switch (eventType)
                    {
                        case EventType.Connected:
                            ConnectionSuccessful?.Invoke();
                            break;
                        case EventType.Data:
                            BeginReadDataframe(message);
                            break;
                        case EventType.Disconnected:
                            Disconnected?.Invoke();
                            break;
                    }
                    
                    _state.receivePipe.TryDequeue();
                }
                else
                {
                    break;
                }
            }
            
            return _state.receivePipe.TotalCount;
        }
        
        private void BeginReadDataframe(ArraySegment<byte> receiveBytes) //TODO метод который конвертит датафрейм
        {
            var allBytes = receiveBytes.Array;

            if (allBytes == null)
            {
                return;
            }
            
            var packageSizeSegment = new ArraySegment<byte>(allBytes, 0, NetFrameConstants.SizeByteCount);
            var packageSize = _byteConverter.GetUIntFromByteArray(packageSizeSegment.ToArray()); //todo GetUIntFromByteArray allocate use
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

            _reader = new NetFrameReader(new byte[packageSize]); //TODO точно packageSize ???? 
            _reader.SetBuffer(contentSegment);
            
            dataframe.Read(_reader);

            if (_handlers.TryGetValue(targetType, out var handlers))
            {
                foreach (var handler in handlers)
                {
                    handler.DynamicInvoke(dataframe);
                }
            }
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
    }
}