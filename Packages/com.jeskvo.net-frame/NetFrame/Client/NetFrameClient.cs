using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using NetFrame.Enums;
using NetFrame.Utils;
using NetFrame.WriteAndRead;

namespace NetFrame.Client
{
    public class NetFrameClient
    {
        private readonly int _sendQueueLimit = 10000;
        private readonly int _receiveQueueLimit = 10000;
        private readonly bool _noDelay = true;
        private readonly int _maxMessageSize;
        private readonly int _sendTimeout = 5000;
        private readonly int _receiveTimeout = 0;
        
        private ClientConnectionState _state;
        
        private readonly NetFrameByteConverter _byteConverter;
        private readonly ConcurrentDictionary<Type, List<Delegate>> _handlers;
        private readonly NetFrameWriter _writer;
        private NetFrameReader _reader;

        private bool Connected => _state != null && _state.Connected;
        private bool Connecting => _state != null && _state.Connecting;
        
        public int ReceivePipeCount => _state != null ? _state.ReceiveQueue.TotalCount : 0;

        public event Action ConnectionSuccessful;
        public event Action Disconnected;
        public event Action<LogType, string> LogCall;

        public NetFrameClient(int maxMessageSize)
        {
            _maxMessageSize = maxMessageSize;
            _writer = new NetFrameWriter();
            _byteConverter = new NetFrameByteConverter();
            _handlers = new ConcurrentDictionary<Type, List<Delegate>>();
        }
        
        public void Connect(string ip, int port)
        {
            if (Connecting || Connected)
            {
                LogCall?.Invoke(LogType.Warning, "[NetFrameClient.Connect] Client can not create connection because an existing connection is connecting or connected");
                return;
            }
            
            _state = new ClientConnectionState(_maxMessageSize);
            _state.Connecting = true;
            _state.TcpClient.Client = null;
            
            _state.ReceiveThread = new Thread(() => 
            {
                ReceiveThreadFunction(_state, ip, port, _maxMessageSize, _noDelay, _sendTimeout, _receiveTimeout, _receiveQueueLimit);
            });
            
            _state.ReceiveThread.IsBackground = true;
            _state.ReceiveThread.Start();
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

            Send(allData);
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

                if (_state.ReceiveQueue.TryPeek(out int _, out EventType eventType, out ArraySegment<byte> message))
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
                    
                    _state.ReceiveQueue.TryDequeue();
                }
                else
                {
                    break;
                }
            }
            
            return _state.ReceiveQueue.TotalCount;
        }
        
        private void ReceiveThreadFunction(ClientConnectionState state, string ip, int port, int maxMessageSize, 
            bool noDelay, int sendTimeout, int receiveTimeout, int receiveQueueLimit)
        {
            Thread sendThread = null;
            try
            {
                state.TcpClient.Connect(ip, port);
                state.Connecting = false;
                
                state.TcpClient.NoDelay = noDelay;
                state.TcpClient.SendTimeout = sendTimeout;
                state.TcpClient.ReceiveTimeout = receiveTimeout;
                
                sendThread = new Thread(() => { ThreadFunctions.SendLoop(0, state.TcpClient, state.SendQueue, state.SendPending); });
                sendThread.IsBackground = true;
                sendThread.Start();
                
                ThreadFunctions.ReceiveLoop(0, state.TcpClient, maxMessageSize, state.ReceiveQueue, receiveQueueLimit);
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
            
            //state.receivePipe.Enqueue(0, EventType.Disconnected, default);// из за этого событие дисконнекта срабатывает два раза
            sendThread?.Interrupt();
            
            state.Connecting = false;
            state.TcpClient?.Close();
        }

        private string GetByTypeName<T>(T dataframe) where T : struct, INetworkDataframe
        {
            return typeof(T).Name;
        }

        private bool Send(ArraySegment<byte> message)
        {
            if (Connected)
            {
                if (message.Count <= _maxMessageSize)
                {
                    if (_state.SendQueue.Count < _sendQueueLimit)
                    {
                        _state.SendQueue.Enqueue(message);
                        _state.SendPending.Set();
                        return true;
                    }
                    else
                    {
                        LogCall?.Invoke(LogType.Warning, $"[NetFrameClient.Send] Client.Send: sendPipe reached limit of {_sendQueueLimit}. This can happen if we call send faster than the network can process messages. Disconnecting to avoid ever growing memory & latency.");
                        _state.TcpClient.Close();
                        return false;
                    }
                }
        
                LogCall?.Invoke(LogType.Error, "[NetFrameClient.Send] Client.Send: message too big: " + message.Count + ". Limit: " + _maxMessageSize);
                return false;
            }
            
            LogCall?.Invoke(LogType.Warning, "[Telepathy] Client.Send: not connected!");
            return false;
        }

        private void BeginReadDataframe(ArraySegment<byte> receiveBytes)
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
                Disconnect();
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
                    handler.DynamicInvoke(dataframe);
                }
            }
        }
    }
}