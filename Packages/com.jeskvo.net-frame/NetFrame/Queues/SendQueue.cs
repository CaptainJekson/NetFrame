using System;
using System.Collections.Generic;
using NetFrame.Utils;

namespace NetFrame.Queues
{
   public class SendQueue
    {
        readonly Queue<ArraySegment<byte>> queue = new Queue<ArraySegment<byte>>();
        
        Pool<byte[]> pool;
        
        public SendQueue(int MaxMessageSize)
        {
            pool = new Pool<byte[]>(() => new byte[MaxMessageSize]);
        }
        
        public int Count
        {
            get { lock (this) { return queue.Count; } }
        }
        
        public int PoolCount
        {
            get { lock (this) { return pool.Count(); } }
        }
        
        public void Enqueue(ArraySegment<byte> message)
        {
            lock (this)
            {
                byte[] bytes = pool.Take();
                
                Buffer.BlockCopy(message.Array, message.Offset, bytes, 0, message.Count);
                
                ArraySegment<byte> segment = new ArraySegment<byte>(bytes, 0, message.Count);
                
                queue.Enqueue(segment);
            }
        }
        
        public bool DequeueAndSerializeAll(ref byte[] payload, out int packetSize)
        {
            lock (this)
            {
                packetSize = 0;
                
                if (queue.Count == 0)
                {
                    return false;
                }
                
                packetSize = 0;
                
                foreach (ArraySegment<byte> message in queue)
                {
                    packetSize += 4 + message.Count;
                }

                if (payload == null || payload.Length < packetSize)
                {
                    payload = new byte[packetSize];
                }
                
                int position = 0;
                
                while (queue.Count > 0)
                {
                    ArraySegment<byte> message = queue.Dequeue();
                    
                    Utils.Utils.IntToBytesBigEndianNonAlloc(message.Count, payload, position);
                    position += 4;
                    
                    Buffer.BlockCopy(message.Array, message.Offset, payload, position, message.Count);
                    position += message.Count;
                    
                    pool.Return(message.Array);
                }
                
                return true;
            }
        }

        public void Clear()
        {
            lock (this)
            {
                while (queue.Count > 0)
                {
                    pool.Return(queue.Dequeue().Array);
                }
            }
        }
    }
}