using NetFrame.Interpolation;
using UnityEngine;

namespace NetFrame
{
    public interface INetworkDataframeTransform : ISnapshot, INetworkDataframe
    {
        Vector3 Position { get; set; }
        Quaternion Rotation { get; set; }
    }
}