using UnityEngine;
using LiteNetLib.Utils;

namespace LiteNetLibManager
{
    public struct DirectionVector2 : INetSerializable
    {
        public static implicit operator DirectionVector2(Vector2 value) => new DirectionVector2(value);
        public static implicit operator DirectionVector2(Vector3 value) => new DirectionVector2(value);
        public static implicit operator Vector2(DirectionVector2 value) => value.ToVector2();
        public static implicit operator Vector3(DirectionVector2 value) => value.ToVector2();
        public Vector2 ToVector2() => new Vector2((float)x / 100f, (float)y / 100f);
        public sbyte x;
        public sbyte y;

        public DirectionVector2(Vector2 vector2)
        {
            x = (sbyte)(vector2.x * 100);
            y = (sbyte)(vector2.y * 100);
        }

        public void Serialize(NetDataWriter writer)
        {
            writer.Put(x);
            writer.Put(y);
        }

        public void Deserialize(NetDataReader reader)
        {
            x = reader.GetSByte();
            y = reader.GetSByte();
        }
    }
}
