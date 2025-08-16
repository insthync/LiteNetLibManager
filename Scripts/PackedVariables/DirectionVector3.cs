using UnityEngine;
using LiteNetLib.Utils;

namespace LiteNetLibManager
{
    public struct DirectionVector3 : INetSerializable
    {
        public static implicit operator DirectionVector3(Vector3 value) => new DirectionVector3(value);
        public static implicit operator Vector3(DirectionVector3 value) => value.ToVector3();
        public Vector3 ToVector3() => new Vector3((float)x / 100f, (float)y / 100f, (float)z / 100f);
        public sbyte x;
        public sbyte y;
        public sbyte z;

        public DirectionVector3(Vector3 vector3)
        {
            x = (sbyte)(vector3.x * 100);
            y = (sbyte)(vector3.y * 100);
            z = (sbyte)(vector3.z * 100);
        }

        public void Serialize(NetDataWriter writer)
        {
            writer.Put(x);
            writer.Put(y);
            writer.Put(z);
        }

        public void Deserialize(NetDataReader reader)
        {
            x = reader.GetSByte();
            y = reader.GetSByte();
            z = reader.GetSByte();
        }

        public override string ToString()
        {
            return ToVector3().ToString();
        }
    }
}
