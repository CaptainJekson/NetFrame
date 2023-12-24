using NetFrame;
using NetFrame.WriteAndRead;

namespace Samples.Dataframes
{
    public struct TestByteNetworkDataframe : INetworkDataframe
    {
        public byte Value1;
        public byte Value2;
        public byte Value3;
        public decimal D;
        
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