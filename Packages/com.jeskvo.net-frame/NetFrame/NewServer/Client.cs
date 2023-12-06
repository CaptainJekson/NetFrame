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
    //todo проверить буфера чтения и записи, посмотреть как все работает и перетосовать методы
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
        public event Action<LogType, string> LogCall;

        public Client(int MaxMessageSize)
        {
            this.MaxMessageSize = MaxMessageSize;
            _writer = new NetFrameWriter(); //todo что с размером ??? он будет увеличиваться???
            _byteConverter = new NetFrameByteConverter();
            _handlers = new ConcurrentDictionary<Type, List<Delegate>>();
        }
        
        private void ReceiveThreadFunction(ClientConnectionState state, string ip, int port, int MaxMessageSize, bool NoDelay, int SendTimeout, int ReceiveTimeout, int ReceiveQueueLimit)
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
                LogCall?.Invoke(LogType.Error, "[NetFrameClient.ReceiveThreadFunction]: failed to connect to ip=" + ip + " port=" + port + " reason=" + exception);
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
                LogCall?.Invoke(LogType.Error, "[NetFrameClient.ReceiveThreadFunction] Exception: " + exception);
            }
            state.receivePipe.Enqueue(0, EventType.Disconnected, default);
            sendThread?.Interrupt();
            
            state.Connecting = false;
            state.tcpClient?.Close();
        }

        public void Connect(string ip, int port)
        {
            if (Connecting || Connected)
            {
                LogCall?.Invoke(LogType.Warning, "[NetFrameClient.Connect] Client can not create connection because an existing connection is connecting or connected");
                return;
            }
            
            _state = new ClientConnectionState(MaxMessageSize);
            _state.Connecting = true;
            _state.tcpClient.Client = null;
            
            _state.receiveThread = new Thread(() => 
            {
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

        private void Send(ArraySegment<byte> message)
        {
            if (Connected)
            {
                if (message.Count <= MaxMessageSize)
                {
                    if (_state.sendPipe.Count < SendQueueLimit)
                    {
                        _state.sendPipe.Enqueue(message);
                        _state.sendPending.Set();
                    }
                    else
                    {
                        LogCall?.Invoke(LogType.Warning, $"[NetFrameClient.Send] Client.Send: sendPipe reached limit of {SendQueueLimit}. This can happen if we call send faster than the network can process messages. Disconnecting to avoid ever growing memory & latency.");
                        
                        _state.tcpClient.Close();
                    }
                }
        
                LogCall?.Invoke(LogType.Error, "[NetFrameClient.Send] Client.Send: message too big: " + message.Count + ". Limit: " + MaxMessageSize);
            }
            
            LogCall?.Invoke(LogType.Warning, "[Telepathy] Client.Send: not connected!");
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
        
        private void BeginReadDataframe(ArraySegment<byte> receiveBytes)
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
                Disconnect();
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