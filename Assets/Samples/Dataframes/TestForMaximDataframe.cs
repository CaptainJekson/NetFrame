using NetFrame;
using NetFrame.WriteAndRead;

namespace Samples.Dataframes
{
    public struct TestForMaximDataframe : INetworkDataframe
    {
        public string Name;
        public int Age;
        
        public void Write(NetFrameWriter writer)
        {
            writer.WriteString(Name);
            writer.WriteInt(Age);
        }

        public void Read(NetFrameReader reader)
        {
            Name = reader.ReadString();
            Age = reader.ReadInt();
        }
    }
}