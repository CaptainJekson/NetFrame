using System;
using System.Collections.Generic;

namespace NetFrame.NewServer
{
    public class MagnificentReceivePipe
    {
        struct Entry
        {
            public int connectionId;
            public EventType eventType;
            public ArraySegment<byte> data;
            public Entry(int connectionId, EventType eventType, ArraySegment<byte> data)
            {
                this.connectionId = connectionId;
                this.eventType = eventType;
                this.data = data;
            }
        }
        
        readonly Queue<Entry> queue = new Queue<Entry>();
        Pool<byte[]> pool;
        Dictionary<int, int> queueCounter = new Dictionary<int, int>();
        
        public MagnificentReceivePipe(int MaxMessageSize)
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
        
        public void Enqueue(int connectionId, EventType eventType, ArraySegment<byte> message)
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

                Entry entry = new Entry(connectionId, eventType, segment);
                queue.Enqueue(entry);

                int oldCount = Count(connectionId);
                queueCounter[connectionId] = oldCount + 1;
            }
        }

        public bool TryPeek(out int connectionId, out EventType eventType, out ArraySegment<byte> data)
        {
            connectionId = 0;
            eventType = EventType.Disconnected;
            data = default;

            lock (this)
            {
                if (queue.Count > 0)
                {
                    Entry entry = queue.Peek();
                    connectionId = entry.connectionId;
                    eventType = entry.eventType;
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

                    queueCounter[entry.connectionId]--;

                    if (queueCounter[entry.connectionId] == 0)
                        queueCounter.Remove(entry.connectionId);

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