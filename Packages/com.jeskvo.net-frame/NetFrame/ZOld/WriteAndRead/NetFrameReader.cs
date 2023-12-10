using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;

namespace NetFrame.WriteAndRead
{
    public class NetFrameReader
    {
        internal ArraySegment<byte> buffer;
        
        public int Position;
        public int Remaining => buffer.Count - Position;

        public int Capacity => buffer.Count;
        
        internal readonly UTF8Encoding encoding = new UTF8Encoding(false, true);

        public NetFrameReader(ArraySegment<byte> segment)
        {
            buffer = segment;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetBuffer(ArraySegment<byte> segment)
        {
            buffer = segment;
            Position = 0;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal unsafe T ReadBlittable<T>() where T : unmanaged
        {
            var size = sizeof(T);
            
            if (Remaining < size)
            {
                throw new EndOfStreamException($"ReadBlittable<{typeof(T)}> not enough data in buffer to read {size} bytes: {ToString()}");
            }
            
            T value;
            fixed (byte* ptr = &buffer.Array[buffer.Offset + Position])
            {
                value = *(T*)ptr;
            }
            Position += size;
            return value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal T? ReadBlittableNullable<T>() where T : unmanaged
        {
            return ReadByte() != 0 ? ReadBlittable<T>() : default(T?);
        }

        public byte ReadByte()
        {
            return ReadBlittable<byte>();
        }

        public byte[] ReadBytes(byte[] bytes, int count)
        {
            if (count < 0) throw new ArgumentOutOfRangeException("ReadBytes requires count >= 0");
            
            if (count > bytes.Length)
            {
                throw new EndOfStreamException($"ReadBytes can't read {count} + bytes because the passed byte[] only has length {bytes.Length}");
            }
            
            if (Remaining < count)
            {
                throw new EndOfStreamException($"ReadBytesSegment can't read {count} bytes because it would read past the end of the stream. {ToString()}");
            }

            Array.Copy(buffer.Array, buffer.Offset + Position, bytes, 0, count);
            Position += count;
            return bytes;
        }
        
        public ArraySegment<byte> ReadBytesSegment(int count)
        {
            if (count < 0) throw new ArgumentOutOfRangeException("ReadBytesSegment requires count >= 0");
            
            if (Remaining < count)
            {
                throw new EndOfStreamException($"ReadBytesSegment can't read {count} bytes because it would read past the end of the stream. {ToString()}");
            }
            
            ArraySegment<byte> result = new ArraySegment<byte>(buffer.Array, buffer.Offset + Position, count);
            Position += count;
            return result;
        }

        public T Read<T>() where T : struct, IReadable
        {
            var value = new T();
            value.Read(this);
            return value;
        }

        public override string ToString()
        {
            var text = BitConverter.ToString(buffer.Array, buffer.Offset, buffer.Count);
            return $"[{text} @ {Position}/{Capacity}]";
        }
    }
    
    public static class Reader<T> where T : IReadable
    {
        public static Func<NetFrameReader, T> read;
    }
}