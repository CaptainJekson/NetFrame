using NetFrame;
using NetFrame.WriteAndRead;

namespace Samples.Dataframes.Collections
{
    public struct UserNetworkModel : IWriteable, IReadable
    {
        public string FirstName;
        public string LastName;
        public uint Age;
        public bool IsLeader;
        
        public void Write(NetFrameWriter writer)
        {
            writer.WriteString(FirstName);
            writer.WriteString(LastName);
            writer.WriteUInt(Age);
            writer.WriteBool(IsLeader);
        }
    
        public void Read(NetFrameReader reader)
        {
            FirstName = reader.ReadString();
            LastName = reader.ReadString();
            Age = reader.ReadUInt();
            IsLeader = reader.ReadBool();
        }
    }
}