using NetFrame.WriteAndRead;
using UnityEngine;

namespace NetFrame.UnityTypesWriteAndRead
{
    public static class NetFrameUnityWriterExtension
    {
        public static void WriteVector2(this NetFrameWriter writer, Vector2 value)
        {
            writer.WriteBlittable(value);
        }
        
        public static void WriteVector3(this NetFrameWriter writer, Vector3 value)
        {
            writer.WriteBlittable(value);
        }
        
        public static void WriteVector4(this NetFrameWriter writer, Vector4 value)
        {
            writer.WriteBlittable(value);
        }
        
        public static void WriteQuaternion(this NetFrameWriter writer, Quaternion value)
        {
            writer.WriteBlittable(value);
        }
        
        public static void WriteColor(this NetFrameWriter writer, Color value)
        {
            writer.WriteBlittable(value);
        }
    }
}