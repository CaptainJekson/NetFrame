using NetFrame;
using NetFrame.WriteAndRead;

namespace Samples.Datagrams
{
    public struct TestByteDatagram : INetFrameDatagram
    {
        public byte Value1;
        public byte Value2;
        public byte Value3;
        
        public void Write(NetFrameWriter writer)
        {
            writer.WriteByte(Value1);
            writer.WriteByte(Value2);
            writer.WriteByte(Value3);
        }
        
        public void Read(NetFrameReader reader)
        {
            Value1 = reader.ReadByte();
            Value2 = reader.ReadByte();
            Value3 = reader.ReadByte();
        }
    }
}