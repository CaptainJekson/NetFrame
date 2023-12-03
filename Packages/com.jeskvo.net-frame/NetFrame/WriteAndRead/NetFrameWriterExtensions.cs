using System;
using System.Runtime.InteropServices;

namespace NetFrame.WriteAndRead
{
    public static class NetFrameWriterExtensions
    {
        public static void WriteByte(this NetFrameWriter writer, byte value)
        {
            writer.WriteBlittable(value);
        }

        public static void WriteByteNullable(this NetFrameWriter writer, byte? value)
        {
            writer.WriteBlittableNullable(value);
        }

        public static void WriteSByte(this NetFrameWriter writer, sbyte value)
        {
            writer.WriteBlittable(value);
        }

        public static void WriteSByteNullable(this NetFrameWriter writer, sbyte? value)
        {
            writer.WriteBlittableNullable(value);
        }
        
        public static void WriteChar(this NetFrameWriter writer, char value)
        {
            writer.WriteBlittable((ushort)value);
        }

        public static void WriteCharNullable(this NetFrameWriter writer, char? value)
        {
            writer.WriteBlittableNullable((ushort?)value);
        }
        
        public static void WriteBool(this NetFrameWriter writer, bool value)
        {
            writer.WriteBlittable((byte)(value ? 1 : 0));
        }

        public static void WriteBoolNullable(this NetFrameWriter writer, bool? value)
        {
            writer.WriteBlittableNullable(value.HasValue ? ((byte)(value.Value ? 1 : 0)) : new byte?());
        }

        public static void WriteShort(this NetFrameWriter writer, short value)
        {
            writer.WriteBlittable(value);
        }

        public static void WriteShortNullable(this NetFrameWriter writer, short? value)
        {
            writer.WriteBlittableNullable(value);
        }

        public static void WriteUShort(this NetFrameWriter writer, ushort value)
        {
            writer.WriteBlittable(value);
        }

        public static void WriteUShortNullable(this NetFrameWriter writer, ushort? value)
        {
            writer.WriteBlittableNullable(value);
        }

        public static void WriteInt(this NetFrameWriter writer, int value)
        {
            writer.WriteBlittable(value);
        }

        public static void WriteIntNullable(this NetFrameWriter writer, int? value)
        {
            writer.WriteBlittableNullable(value);
        }

        public static void WriteUInt(this NetFrameWriter writer, uint value)
        {
            writer.WriteBlittable(value);
        }

        public static void WriteUIntNullable(this NetFrameWriter writer, uint? value)
        {
            writer.WriteBlittableNullable(value);
        }

        public static void WriteLong(this NetFrameWriter writer, long value)
        {
            writer.WriteBlittable(value);
        }

        public static void WriteLongNullable(this NetFrameWriter writer, long? value)
        {
            writer.WriteBlittableNullable(value);
        }

        public static void WriteULong(this NetFrameWriter writer, ulong value)
        {
            writer.WriteBlittable(value);
        }

        public static void WriteULongNullable(this NetFrameWriter writer, ulong? value)
        {
            writer.WriteBlittableNullable(value);
        }

        public static void WriteFloat(this NetFrameWriter writer, float value)
        {
            writer.WriteBlittable(value);
        }

        public static void WriteFloatNullable(this NetFrameWriter writer, float? value)
        {
            writer.WriteBlittableNullable(value);
        }

        [StructLayout(LayoutKind.Explicit)]
        internal struct UIntDouble
        {
            [FieldOffset(0)]
            public double doubleValue;

            [FieldOffset(0)]
            public ulong longValue;
        }

        public static void WriteDouble(this NetFrameWriter writer, double value)
        {
            writer.WriteBlittable(value);
        }
        
        public static void WriteDoubleNullable(this NetFrameWriter writer, double? value)
        {
            writer.WriteBlittableNullable(value);
        }

        public static void WriteDecimal(this NetFrameWriter writer, decimal value)
        {
            writer.WriteBlittable(value);
        }

        public static void WriteDecimalNullable(this NetFrameWriter writer, decimal? value)
        {
            writer.WriteBlittableNullable(value);
        }

        public static void WriteString(this NetFrameWriter writer, string value)
        {
            if (value == null)
            {
                writer.WriteUShort(0);
                return;
            }
            
            int maxSize = writer.encoding.GetMaxByteCount(value.Length);
            writer.EnsureCapacity(writer.Position + 2 + maxSize);
            
            int written = writer.encoding.GetBytes(value, 0, value.Length, writer.buffer, writer.Position + 2);

            if (written > NetFrameWriter.MaxStringLength)
            {
                throw new IndexOutOfRangeException($"NetworkWriter.WriteString - Value too long: {written} bytes. Limit: {NetFrameWriter.MaxStringLength} bytes");
            }
            
            writer.WriteUShort(checked((ushort)(written + 1)));
            
            writer.Position += written;
        }

        public static void WriteBytesAndSizeSegment(this NetFrameWriter writer, ArraySegment<byte> buffer)
        {
            writer.WriteBytesAndSize(buffer.Array, buffer.Offset, buffer.Count);
        }

        public static void WriteBytesAndSize(this NetFrameWriter writer, byte[] buffer)
        {
            writer.WriteBytesAndSize(buffer, 0, buffer != null ? buffer.Length : 0);
        }

        public static void WriteBytesAndSize(this NetFrameWriter writer, byte[] buffer, int offset, int count)
        {
            if (buffer == null)
            {
                writer.WriteUInt(0u);
                return;
            }
            writer.WriteUInt(checked((uint)count) + 1u);
            writer.WriteBytes(buffer, offset, count);
        }

        public static void WriteGuid(this NetFrameWriter writer, Guid value)
        {
            writer.EnsureCapacity(writer.Position + 16);
            value.TryWriteBytes(new Span<byte>(writer.buffer, writer.Position, 16));
            writer.Position += 16;
        }
        
        public static void WriteGuidNullable(this NetFrameWriter writer, Guid? value)
        {
            writer.WriteBool(value.HasValue);
            
            if (value.HasValue)
            {
                writer.WriteGuid(value.Value);
            }
        }

        public static void WriteUri(this NetFrameWriter writer, Uri uri)
        {
            writer.WriteString(uri?.ToString());
        }

        public static void WriteDateTime(this NetFrameWriter writer, DateTime dateTime)
        {
            writer.WriteDouble(dateTime.ToOADate());
        }

        public static void WriteDateTimeNullable(this NetFrameWriter writer, DateTime? dateTime)
        {
            writer.WriteBool(dateTime.HasValue);
            if (dateTime.HasValue)
                writer.WriteDouble(dateTime.Value.ToOADate());
        }
    }
}