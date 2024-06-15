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

        private ClientConnectionState _clientConnectionState;
        private readonly ConcurrentDictionary<Type, List<Delegate>> _handlers;
        private readonly NetFrameWriter _writer;
        private NetFrameReader _reader;

        private bool Connected => _clientConnectionState != null && _clientConnectionState.Connected;
        private bool Connecting => _clientConnectionState != null && _clientConnectionState.Connecting;
        
        public int ReceivePipeCount => _clientConnectionState != null ? _clientConnectionState.ReceiveQueue.TotalCount : 0;

        public event Action<int> ConnectionSuccessful;
        public event Action Disconnected;
        public event Action<NetworkLogType, string> LogCall;
        public event Action ConnectionFailed;
        
        private ConcurrentQueue<Action> Tasks = new();

        public NetFrameClient(int maxMessageSize)
        {
            NetFrameContainer.SetClient(this);
            
            _maxMessageSize = maxMessageSize;
            _writer = new NetFrameWriter();
            _handlers = new ConcurrentDictionary<Type, List<Delegate>>();
        }

        public void Connect(string ip, int port)
        {
            if (Connecting || Connected)
            {
                LogCall?.Invoke(NetworkLogType.Warning, "[NetFrameClient.Connect] Client can not create connection because an existing connection is connecting or connected");
                return;
            }
            
            _clientConnectionState = new ClientConnectionState(_maxMessageSize);
            _clientConnectionState.Connecting = true;
            _clientConnectionState.TcpClient.Client = null;
            
            _clientConnectionState.ReceiveTcpThread = new Thread(() => 
            {
                ReceiveTcpThreadFunction(_clientConnectionState, ip, port, _maxMessageSize, _noDelay, _sendTimeout, _receiveTimeout, _receiveQueueLimit);
            });
            
            _clientConnectionState.ReceiveTcpThread.IsBackground = true;
            _clientConnectionState.ReceiveTcpThread.Start();
        }

        public void Disconnect()
        {
            if (Connecting || Connected)
            {
                _clientConnectionState.Dispose();
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
            if (_clientConnectionState == null)
            {
                return 0;
            }
            
            for (int i = 0; i < processLimit; ++i)
            {
                if (checkEnabled != null && !checkEnabled())
                {
                    break;
                }

                if (_clientConnectionState.ReceiveQueue.TryPeek(out int _, out NetworkEventType eventType, out ArraySegment<byte> message))
                {
                    switch (eventType)
                    {
                        case NetworkEventType.Connected:
                            break;
                        case NetworkEventType.Data:
                            BeginReadDataframe(message);
                            break;
                        case NetworkEventType.Disconnected:
                            Disconnected?.Invoke();
                            break;
                    }
                    
                    _clientConnectionState.ReceiveQueue.TryDequeue();
                }
                else
                {
                    break;
                }
            }

            if (Tasks.Count > 0)
            {
                if (Tasks.TryDequeue(out var action))
                {
                    action?.Invoke();
                }
            }

            return _clientConnectionState.ReceiveQueue.TotalCount;
        }
        
        private void ReceiveTcpThreadFunction(ClientConnectionState state, string ip, int port, int maxMessageSize, 
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
                
                sendThread = new Thread(() => { ThreadTcpFunctions.SendLoop(0, state.TcpClient, state.SendQueue, state.SendPending); });
                sendThread.IsBackground = true;
                sendThread.Start();
                
                ThreadTcpFunctions.ReceiveTcpLoop(state.LocalConnectionId, state.TcpClient, maxMessageSize, state.ReceiveQueue, receiveQueueLimit);
            }
            catch (SocketException exception)
            {
                MainThreadRun(() =>
                {
                    ConnectionFailed?.Invoke();
                });
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
                LogCall?.Invoke(NetworkLogType.Error, "[NetFrameClient.ReceiveTcpThreadFunction] Exception: " + exception);
            }
            
            //state.receivePipe.Enqueue(0, EventType.Disconnected, default);// из за этого событие дисконнекта срабатывает два раза
            sendThread?.Interrupt();
            
            state.Connecting = false;
            state.TcpClient?.Close();
        }
        
        private void MainThreadRun(Action task)
        {
            Tasks.Enqueue(task);
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
                    if (_clientConnectionState.SendQueue.Count < _sendQueueLimit)
                    {
                        _clientConnectionState.SendQueue.Enqueue(message);
                        _clientConnectionState.SendPending.Set();
                        return true;
                    }

                    LogCall?.Invoke(NetworkLogType.Warning, $"[NetFrameClient.Send] Client.Send: sendPipe reached limit of {_sendQueueLimit}. This can happen if we call send faster than the network can process messages. Disconnecting to avoid ever growing memory & latency.");
                    _clientConnectionState.TcpClient.Close();
                    return false;
                }
        
                LogCall?.Invoke(NetworkLogType.Error, "[NetFrameClient.Send] Client.Send: message too big: " + message.Count + ". Limit: " + _maxMessageSize);
                return false;
            }
            
            LogCall?.Invoke(NetworkLogType.Warning, "[Telepathy] Client.Send: not connected!");
            return false;
        }

        private void BeginReadDataframe(ArraySegment<byte> receiveBytes)
        {
            var allBytes = receiveBytes.Array;

            if (allBytes == null)
            {
                return;
            }

            var firstByte = allBytes[0];
            
            if (firstByte == '#')
            {
                ReadLocalConnectionId(allBytes);
                return;
            }

            if (firstByte == '@')
            {
                //SendSecurityToken();
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
                LogCall?.Invoke(NetworkLogType.Error, $"[NetFrameClientOnServer.BeginReadBytesCallback] no datagram: {headerDataframe}");
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

        private void ReadLocalConnectionId(byte[] allBytes)
        {
            var localConnectionId = BitConverter.ToInt32(allBytes, 1);
            ConnectionSuccessful?.Invoke(localConnectionId);
            
            _clientConnectionState.LocalConnectionId = localConnectionId;
        }
    }
}