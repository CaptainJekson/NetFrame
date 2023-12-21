using NetFrame.WriteAndRead;
using UnityEngine;

namespace NetFrame.UnityTypesWriteAndRead
{
    public static class NetFrameUnityReaderExtension
    {
        public static Vector2 ReadVector2(this NetFrameReader reader)
        {
            return reader.ReadBlittable<Vector2>();
        }
        
        public static Vector3 ReadVector3(this NetFrameReader reader)
        {
            return reader.ReadBlittable<Vector3>();
        }
        
        public static Vector4 ReadVector4(this NetFrameReader reader)
        {
            return reader.ReadBlittable<Vector4>();
        }
        
        public static Quaternion ReadQuaternion(this NetFrameReader reader)
        {
            return reader.ReadBlittable<Quaternion>();
        }
        
        public static Color ReadColor(this NetFrameReader reader)
        {
            return reader.ReadBlittable<Color>();
        }
    }
}