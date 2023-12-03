using System;
using System.IO;

namespace NetFrame.WriteAndRead
{
    public static class NetFrameReaderExtensions
    {
        public static byte ReadByte(this NetFrameReader reader)
        {
            return reader.ReadBlittable<byte>();
        }

        public static byte? ReadByteNullable(this NetFrameReader reader)
        {
            return reader.ReadBlittableNullable<byte>();
        }

        public static sbyte ReadSByte(this NetFrameReader reader)
        {
            return reader.ReadBlittable<sbyte>();
        }

        public static sbyte? ReadSByteNullable(this NetFrameReader reader)
        {
            return reader.ReadBlittableNullable<sbyte>();
        }
        
        public static char ReadChar(this NetFrameReader reader)
        {
            return (char)reader.ReadBlittable<ushort>();
        }

        public static char? ReadCharNullable(this NetFrameReader reader)
        {
            return (char?)reader.ReadBlittableNullable<ushort>();
        }
        
        public static bool ReadBool(this NetFrameReader reader)
        {
            return reader.ReadBlittable<byte>() != 0;
        }

        public static bool? ReadBoolNullable(this NetFrameReader reader)
        {
            byte? value = reader.ReadBlittableNullable<byte>();
            return value.HasValue ? (value.Value != 0) : default(bool?);
        }

        public static short ReadShort(this NetFrameReader reader)
        {
            return (short)reader.ReadUShort();
        }

        public static short? ReadShortNullable(this NetFrameReader reader)
        {
            return reader.ReadBlittableNullable<short>();
        }

        public static ushort ReadUShort(this NetFrameReader reader)
        {
            return reader.ReadBlittable<ushort>();
        }

        public static ushort? ReadUShortNullable(this NetFrameReader reader)
        {
            return reader.ReadBlittableNullable<ushort>();
        }

        public static int ReadInt(this NetFrameReader reader)
        {
            return reader.ReadBlittable<int>();
        }

        public static int? ReadIntNullable(this NetFrameReader reader)
        {
            return reader.ReadBlittableNullable<int>();
        }

        public static uint ReadUInt(this NetFrameReader reader)
        {
            return reader.ReadBlittable<uint>();
        }

        public static uint? ReadUIntNullable(this NetFrameReader reader)
        {
            return reader.ReadBlittableNullable<uint>();
        }

        public static long ReadLong(this NetFrameReader reader)
        {
            return reader.ReadBlittable<long>();
        }

        public static long? ReadLongNullable(this NetFrameReader reader)
        {
            return reader.ReadBlittableNullable<long>();
        }

        public static ulong ReadULong(this NetFrameReader reader)
        {
            return reader.ReadBlittable<ulong>();
        }

        public static ulong? ReadULongNullable(this NetFrameReader reader)
        {
            return reader.ReadBlittableNullable<ulong>();
        }

        public static float ReadFloat(this NetFrameReader reader)
        {
            return reader.ReadBlittable<float>();
        }

        public static float? ReadFloatNullable(this NetFrameReader reader)
        {
            return reader.ReadBlittableNullable<float>();
        }

        public static double ReadDouble(this NetFrameReader reader)
        {
            return reader.ReadBlittable<double>();
        }

        public static double? ReadDoubleNullable(this NetFrameReader reader)
        {
            return reader.ReadBlittableNullable<double>();
        }

        public static decimal ReadDecimal(this NetFrameReader reader)
        {
            return reader.ReadBlittable<decimal>();
        }

        public static decimal? ReadDecimalNullable(this NetFrameReader reader)
        {
            return reader.ReadBlittableNullable<decimal>();
        }

        public static string ReadString(this NetFrameReader reader)
        {
            ushort size = reader.ReadUShort();

            if (size == 0)
                return null;

            ushort realSize = (ushort)(size - 1);
            
            if (realSize > NetFrameWriter.MaxStringLength)
            {
                throw new EndOfStreamException($"NetworkReader.ReadString - Value too long: {realSize} bytes. Limit is: {NetFrameWriter.MaxStringLength} bytes");
            }

            ArraySegment<byte> data = reader.ReadBytesSegment(realSize);
            return reader.encoding.GetString(data.Array, data.Offset, data.Count);
        }
        
        public static byte[] ReadBytesAndSize(this NetFrameReader reader)
        {
            uint count = reader.ReadUInt();
            return count == 0 ? null : reader.ReadBytes(checked((int)(count - 1u)));
        }

        public static byte[] ReadBytes(this NetFrameReader reader, int count)
        {
            byte[] bytes = new byte[count];
            reader.ReadBytes(bytes, count);
            return bytes;
        }
        
        public static ArraySegment<byte> ReadBytesAndSizeSegment(this NetFrameReader reader)
        {
            uint count = reader.ReadUInt();
            return count == 0 ? default : reader.ReadBytesSegment(checked((int)(count - 1u)));
        }
        
        public static Guid ReadGuid(this NetFrameReader reader)
        {
            if (reader.Remaining >= 16)
            {
                ReadOnlySpan<byte> span = new ReadOnlySpan<byte>(reader.buffer.Array, reader.buffer.Offset + reader.Position, 16);
                reader.Position += 16;
                return new Guid(span);
            }
            throw new EndOfStreamException($"ReadGuid out of range: {reader}");
        }
        
        public static Guid? ReadGuidNullable(this NetFrameReader reader)
        {
            return reader.ReadBool() ? ReadGuid(reader) : default(Guid?);
        }

        public static Uri ReadUri(this NetFrameReader reader)
        {
            string uriString = reader.ReadString();
            return (string.IsNullOrWhiteSpace(uriString) ? null : new Uri(uriString));
        }
        
        public static DateTime ReadDateTime(this NetFrameReader reader)
        {
            return DateTime.FromOADate(reader.ReadDouble());
        }

        public static DateTime? ReadDateTimeNullable(this NetFrameReader reader)
        {
            return reader.ReadBool() ? ReadDateTime(reader) : default(DateTime?);
        }
    }
}