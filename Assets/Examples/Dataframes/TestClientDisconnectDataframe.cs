using NetFrame;
using NetFrame.WriteAndRead;

namespace Samples.Dataframes
{
    public struct TestClientDisconnectDataframe : INetworkDataframe
    {
        public int ClientId;
    
        public void Write(NetFrameWriter writer)
        {
            writer.WriteInt(ClientId);
        }
    
        public void Read(NetFrameReader reader)
        {
            ClientId = reader.ReadInt();
        }
    }
}