using NetFrame;
using NetFrame.UnityTypesWriteAndRead;
using NetFrame.WriteAndRead;
using UnityEngine;

namespace Samples.DataframesForRealtime
{
    public struct PlayerMoveDataframe : INetworkDataframeTransform
    {
        public double RemoteTime { get; set; }
        public double LocalTime { get; set; }
        public Vector3 Position { get; set; }
        public Quaternion Rotation { get; set; }
        
        
        public void Write(NetFrameWriter writer)
        {
            writer.WriteDouble(RemoteTime);
            writer.WriteDouble(LocalTime);
            writer.WriteVector3(Position);
            writer.WriteQuaternion(Rotation);
        }

        public void Read(NetFrameReader reader)
        {
            RemoteTime = reader.ReadDouble();
            LocalTime = reader.ReadDouble();
            Position = reader.ReadVector3();
            Rotation = reader.ReadQuaternion();
        }
    }
}