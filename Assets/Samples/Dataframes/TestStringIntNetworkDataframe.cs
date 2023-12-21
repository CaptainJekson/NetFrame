using NetFrame;
using NetFrame.UnityTypesWriteAndRead;
using NetFrame.WriteAndRead;
using UnityEngine;

namespace Samples.Dataframes
{
    public struct TestStringIntNetworkDataframe : INetworkDataframe
    {
        public string Name;
        public int Age;
        public Vector3 TestVector3;
        
        public void Write(NetFrameWriter writer)
        {
            writer.WriteString(Name);
            writer.WriteInt(Age);
            writer.WriteVector3(TestVector3);
        }
    
        public void Read(NetFrameReader reader)
        {
            Name = reader.ReadString();
            Age = reader.ReadInt();
            TestVector3 = reader.ReadVector3();
        }
    }
}