using System;
using System.Net.Sockets;
using System.Threading;

namespace NetFrame.NewServer
{
    public static class ThreadFunctions
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
        
        public static bool ReadMessageBlocking(NetworkStream stream, int MaxMessageSize, byte[] headerBuffer, byte[] payloadBuffer, out int size)
        {
            size = 0;

            if (payloadBuffer.Length != 4 + MaxMessageSize)
            {
                //TODO 
                //Log.Error($"[Telepathy] ReadMessageBlocking: payloadBuffer needs to be of size 4 + MaxMessageSize = {4 + MaxMessageSize} instead of {payloadBuffer.Length}");
                return false;
            }

            // read exactly 4 bytes for header (blocking)
            if (!stream.ReadExactly(headerBuffer, 4))
                return false;
            
            size = Utils.BytesToIntBigEndian(headerBuffer);

            if (size > 0 && size <= MaxMessageSize)
            {
                return stream.ReadExactly(payloadBuffer, size);
            }
            //Log.Warning("[Telepathy] ReadMessageBlocking: possible header attack with a header of: " + size + " bytes.");
            return false;
        }

        public static void ReceiveLoop(int connectionId, TcpClient client, int MaxMessageSize, MagnificentReceivePipe receivePipe, int QueueLimit)
        {
            // get NetworkStream from client
            NetworkStream stream = client.GetStream();
            
            byte[] receiveBuffer = new byte[4 + MaxMessageSize];

            byte[] headerBuffer = new byte[4];
            
            try
            {
                receivePipe.Enqueue(connectionId, EventType.Connected, default);
                
                while (true)
                {
                    if (!ReadMessageBlocking(stream, MaxMessageSize, headerBuffer, receiveBuffer, out int size))
                    {
                        break;
                    }

                    ArraySegment<byte> message = new ArraySegment<byte>(receiveBuffer, 0, size);
                    
                    receivePipe.Enqueue(connectionId, EventType.Data, message);

                    if (receivePipe.Count(connectionId) >= QueueLimit)
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
                receivePipe.Enqueue(connectionId, EventType.Disconnected, default);
            }
        }
        
        public static void SendLoop(int connectionId, TcpClient client, MagnificentSendPipe sendPipe, ManualResetEvent sendPending)
        {
            NetworkStream stream = client.GetStream();

            byte[] payload = null;

            try
            {
                while (client.Connected)
                {
                    sendPending.Reset();

                    if (sendPipe.DequeueAndSerializeAll(ref payload, out int packetSize))
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
                //Log.Info("[Telepathy] SendLoop Exception: connectionId=" + connectionId + " reason: " + exception); //TODO 
            }
            finally
            {
                stream.Close();
                client.Close();
            }
        }
    }
}