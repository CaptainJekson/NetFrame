using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace NetFrame.WriteAndRead
{
    public class NetFrameWriter
    {
        public const ushort MaxStringLength = ushort.MaxValue - 1;
        public int _bufferSize;
        internal byte[] buffer;
        
        public int Position;
        
        public int Capacity => buffer.Length;
        
        internal readonly UTF8Encoding encoding = new UTF8Encoding(false, true);

        public NetFrameWriter(int bufferSize = 1500)
        {
            _bufferSize = bufferSize;
            buffer = new byte[_bufferSize];
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Reset()
        {
            Position = 0;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void EnsureCapacity(int value)
        {
            if (buffer.Length < value)
            {
                int capacity = Math.Max(value, buffer.Length * 2);
                Array.Resize(ref buffer, capacity);
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte[] ToArray()
        {
            byte[] data = new byte[Position];
            Array.ConstrainedCopy(buffer, 0, data, 0, Position);
            return data;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ArraySegment<byte> ToArraySegment()
        {
            return new ArraySegment<byte>(buffer, 0, Position);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator ArraySegment<byte>(NetFrameWriter w)
        {
            return w.ToArraySegment();
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void WriteBlittable<T>(T value) where T : unmanaged
        {
            int size = sizeof(T);
            
            EnsureCapacity(Position + size);

            fixed (byte* ptr = &buffer[Position])
            {
                *(T*)ptr = value;
            }
            Position += size;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void WriteBlittableNullable<T>(T? value)
            where T : unmanaged
        {
            WriteByte((byte)(value.HasValue ? 0x01 : 0x00));

            if (value.HasValue)
            {
                WriteBlittable(value.Value);
            }
        }

        public void WriteByte(byte value)
        {
            WriteBlittable(value);  
        } 
        
        public void WriteBytes(byte[] array, int offset, int count)
        {
            EnsureCapacity(Position + count);
            Array.ConstrainedCopy(array, offset, this.buffer, Position, count);
            Position += count;
        }
      
        public unsafe bool WriteBytes(byte* ptr, int offset, int size)
        {
            EnsureCapacity(Position + size);

            byte[] tempArray = new byte[size];
            Marshal.Copy((IntPtr)(ptr + offset), tempArray, 0, size);
            Array.Copy(tempArray, 0, buffer, Position, size);

            Position += size;
            return true;

        }

        public void Write<T>(T value) where T : IWriteable
        {
            value.Write(this);
        }

        public override string ToString()
        {
            var segment = ToArraySegment();
            var text = BitConverter.ToString(segment.Array, segment.Offset, segment.Count);
            return $"[{text} @ {Position}/{Capacity}]";
        }
    }
    
    public static class Writer<T> where T : IWriteable
    {
        public static Action<NetFrameWriter, T> write;
    }
}