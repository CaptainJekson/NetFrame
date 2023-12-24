using System;
using System.Collections.Generic;
using NetFrame.Enums;
using NetFrame.Utils;

namespace NetFrame.Queues
{
    public class ReceiveQueue
    {
        struct Entry
        {
            public readonly int ConnectionId;
            
            public NetworkEventType NetworkEventType;
            public ArraySegment<byte> data;
            public Entry(int connectionId, NetworkEventType networkEventType, ArraySegment<byte> data)
            {
                this.ConnectionId = connectionId;
                this.NetworkEventType = networkEventType;
                this.data = data;
            }
        }
        
        readonly Queue<Entry> queue = new Queue<Entry>();
        Pool<byte[]> pool;
        Dictionary<int, int> queueCounter = new Dictionary<int, int>();
        
        public ReceiveQueue(int MaxMessageSize)
        {
            pool = new Pool<byte[]>(() => new byte[MaxMessageSize]);
        }
        
        public int Count(int connectionId)
        {
            lock (this)
            {
                return queueCounter.TryGetValue(connectionId, out int count)
                       ? count
                       : 0;
            }
        }
        
        public int TotalCount
        {
            get { lock (this) { return queue.Count; } }
        }
        
        public int PoolCount
        {
            get { lock (this) { return pool.Count(); } }
        }
        
        public void Enqueue(int connectionId, NetworkEventType networkEventType, ArraySegment<byte> message)
        {
            lock (this)
            {
                ArraySegment<byte> segment = default;
                if (message != default)
                {
                    byte[] bytes = pool.Take();

                    Buffer.BlockCopy(message.Array, message.Offset, bytes, 0, message.Count);

                    segment = new ArraySegment<byte>(bytes, 0, message.Count);
                }

                Entry entry = new Entry(connectionId, networkEventType, segment);
                queue.Enqueue(entry);

                int oldCount = Count(connectionId);
                queueCounter[connectionId] = oldCount + 1;
            }
        }

        public bool TryPeek(out int connectionId, out NetworkEventType networkEventType, out ArraySegment<byte> data)
        {
            connectionId = 0;
            networkEventType = NetworkEventType.Disconnected;
            data = default;

            lock (this)
            {
                if (queue.Count > 0)
                {
                    Entry entry = queue.Peek();
                    connectionId = entry.ConnectionId;
                    networkEventType = entry.NetworkEventType;
                    data = entry.data;
                    return true;
                }
                return false;
            }
        }
        
        public bool TryDequeue()
        {
            lock (this)
            {
                if (queue.Count > 0)
                {
                    Entry entry = queue.Dequeue();

                    if (entry.data != default)
                    {
                        pool.Return(entry.data.Array);
                    }

                    queueCounter[entry.ConnectionId]--;

                    if (queueCounter[entry.ConnectionId] == 0)
                        queueCounter.Remove(entry.ConnectionId);

                    return true;
                }
                return false;
            }
        }

        public void Clear()
        {
            lock (this)
            {
                while (queue.Count > 0)
                {
                    Entry entry = queue.Dequeue();
                    
                    if (entry.data != default)
                    {
                        pool.Return(entry.data.Array);
                    }
                }

                queueCounter.Clear();
            }
        }
    }
}