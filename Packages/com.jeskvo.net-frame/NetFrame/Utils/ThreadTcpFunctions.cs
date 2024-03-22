using System;
using System.Net.Sockets;
using System.Threading;
using NetFrame.Enums;
using NetFrame.Queues;

namespace NetFrame.Utils
{
    public static class ThreadTcpFunctions
    {
        public static bool SendMessagesBlocking(NetworkStream stream, byte[] payload, int packetSize)
        {
            try
            {
                stream.Write(payload, 0, packetSize);
                return true;
            }
            catch (Exception exception)
            {
                return false;
            }
        }

        private static bool ReadMessageBlocking(NetworkStream stream, int maxMessageSize, byte[] headerBuffer, byte[] payloadBuffer, out int size)
        {
            size = 0;

            if (payloadBuffer.Length != 4 + maxMessageSize)
            {
                //TODO 
                //Log.Error($"[Telepathy] ReadMessageBlocking: payloadBuffer needs to be of size 4 + MaxMessageSize = {4 + MaxMessageSize} instead of {payloadBuffer.Length}");
                return false;
            }

            // read exactly 4 bytes for header (blocking)
            if (!stream.ReadExactly(headerBuffer, 4))
            {
                return false;
            }
            
            size = Utils.BytesToIntBigEndian(headerBuffer);

            if (size > 0 && size <= maxMessageSize)
            {
                return stream.ReadExactly(payloadBuffer, size);
            }
            //Log.Warning("[Telepathy] ReadMessageBlocking: possible header attack with a header of: " + size + " bytes.");
            return false;
        }

        public static void ReceiveTcpLoop(int connectionId, TcpClient client, int maxMessageSize, ReceiveQueue receiveQueue, int queueLimit)
        {
            NetworkStream stream = client.GetStream();
            
            byte[] receiveBuffer = new byte[4 + maxMessageSize];
            byte[] headerBuffer = new byte[4];
            
            try
            {
                receiveQueue.Enqueue(connectionId, NetworkEventType.Connected, default);
                
                while (true)
                {
                    if (!ReadMessageBlocking(stream, maxMessageSize, headerBuffer, receiveBuffer, out int size))
                    {
                        break;
                    }

                    ArraySegment<byte> message = new ArraySegment<byte>(receiveBuffer, 0, size);
                    
                    receiveQueue.Enqueue(connectionId, NetworkEventType.Data, message);

                    if (receiveQueue.Count(connectionId) >= queueLimit)
                    {
                        //TODO
                        //Log.Warning($"[Telepathy] ReceivePipe reached limit of {QueueLimit} for connectionId {connectionId}. This can happen if network messages come in way faster than we manage to process them. Disconnecting this connection for load balancing.");
                        break;
                    }
                }
            }
            catch (Exception exception)
            {
                //Log.Info("[Telepathy] ReceiveLoop finished receive function for connectionId=" + connectionId + " reason: " + exception); //TODO
            }
            finally
            {
                stream.Close();
                client.Close();
                receiveQueue.Enqueue(connectionId, NetworkEventType.Disconnected, default);
            }
        }
        
        public static void SendLoop(int connectionId, TcpClient client, SendQueue sendQueue, ManualResetEvent sendPending)
        {
            NetworkStream stream = client.GetStream();

            byte[] payload = null;

            try
            {
                while (client.Connected)
                {
                    sendPending.Reset();

                    if (sendQueue.DequeueAndSerializeAll(ref payload, out int packetSize))
                    {
                        if (!SendMessagesBlocking(stream, payload, packetSize))
                        {
                            break;
                        }
                    }
                    
                    sendPending.WaitOne();
                }
            }
            catch (ThreadAbortException)
            {
                // happens on stop. don't log anything.
            }
            catch (ThreadInterruptedException)
            {
                // happens if receive thread interrupts send thread.
            }
            catch (Exception exception)
            {
                //Log.Info("[ThreadFunction.SendLoop] SendLoop Exception: connectionId=" + connectionId + " reason: " + exception); //TODO 
            }
            finally
            {
                stream.Close();
                client.Close();
            }
        }
    }
}