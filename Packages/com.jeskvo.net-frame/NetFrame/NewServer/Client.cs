using System;
using System.Net.Sockets;
using System.Threading;

namespace NetFrame.NewServer
{
    public class Client
    {
        public Action OnConnected;
        public Action<ArraySegment<byte>> OnData;
        public Action OnDisconnected;
        
        public int SendQueueLimit = 10000;
        public int ReceiveQueueLimit = 10000;
        public bool NoDelay = true;
        public readonly int MaxMessageSize;
        public int SendTimeout = 5000;
        public int ReceiveTimeout = 0;
        
        ClientConnectionState state;
        
        public bool Connected => state != null && state.Connected;
        public bool Connecting => state != null && state.Connecting;
        
        public int ReceivePipeCount => state != null ? state.receivePipe.TotalCount : 0;
        
        public Client(int MaxMessageSize)
        {
            this.MaxMessageSize = MaxMessageSize;
        }
        static void ReceiveThreadFunction(ClientConnectionState state, string ip, int port, int MaxMessageSize, bool NoDelay, int SendTimeout, int ReceiveTimeout, int ReceiveQueueLimit)
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
            
            state = new ClientConnectionState(MaxMessageSize);
            
            state.Connecting = true;
            
            state.tcpClient.Client = null;
            
            state.receiveThread = new Thread(() => {
                ReceiveThreadFunction(state, ip, port, MaxMessageSize, NoDelay, SendTimeout, ReceiveTimeout, ReceiveQueueLimit);
            });
            state.receiveThread.IsBackground = true;
            state.receiveThread.Start();
        }

        public void Disconnect()
        {
            if (Connecting || Connected)
            {
                state.Dispose();
            }
        }
        
        public bool Send(ArraySegment<byte> message)
        {
            if (Connected)
            {
                if (message.Count <= MaxMessageSize)
                {
                    // check send pipe limit
                    if (state.sendPipe.Count < SendQueueLimit)
                    {
                        state.sendPipe.Enqueue(message);
                        state.sendPending.Set();
                        return true;
                    }
                    else
                    {
                        // log the reason
                        //TODO
                        //Log.Warning($"[Telepathy] Client.Send: sendPipe reached limit of {SendQueueLimit}. This can happen if we call send faster than the network can process messages. Disconnecting to avoid ever growing memory & latency.");

                        // just close it. send thread will take care of the rest.
                        state.tcpClient.Close();
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

        public int Tick(int processLimit, Func<bool> checkEnabled = null)
        {
            if (state == null)
            {
                return 0;
            }
            
            for (int i = 0; i < processLimit; ++i)
            {
                if (checkEnabled != null && !checkEnabled())
                {
                    break;
                }

                if (state.receivePipe.TryPeek(out int _, out EventType eventType, out ArraySegment<byte> message))
                {
                    switch (eventType)
                    {
                        case EventType.Connected:
                            OnConnected?.Invoke();
                            break;
                        case EventType.Data:
                            OnData?.Invoke(message);
                            break;
                        case EventType.Disconnected:
                            OnDisconnected?.Invoke();
                            break;
                    }
                    
                    state.receivePipe.TryDequeue();
                }
                else
                {
                    break;
                }
            }
            
            return state.receivePipe.TotalCount;
        }
    }
}