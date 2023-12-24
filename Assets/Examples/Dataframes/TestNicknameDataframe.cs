using NetFrame;
using NetFrame.WriteAndRead;

namespace Samples.Dataframes
{
    public struct TestNicknameDataframe : INetworkDataframe
    {
        public string Nickname;
        
        public void Write(NetFrameWriter writer)
        {
            writer.WriteString(Nickname);
        }
    
        public void Read(NetFrameReader reader)
        {
            Nickname = reader.ReadString();
        }
    }
}