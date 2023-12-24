using NetFrame;
using NetFrame.UnityTypesWriteAndRead;
using NetFrame.WriteAndRead;
using UnityEngine;

namespace Samples.DataframesForRealtime
{
    public struct PlayerSpawnDataframe : INetworkDataframe
    {
        public Vector3 StartPosition;
        public Quaternion StartRotation;
        
        public void Write(NetFrameWriter writer)
        {
            writer.WriteVector3(StartPosition);
            writer.WriteQuaternion(StartRotation);
        }

        public void Read(NetFrameReader reader)
        {
            StartPosition = reader.ReadVector3();
            StartRotation = reader.ReadQuaternion();
        }
    }
}