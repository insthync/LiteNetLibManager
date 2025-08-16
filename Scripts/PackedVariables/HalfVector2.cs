using UnityEngine;
using LiteNetLib.Utils;

namespace LiteNetLibManager
{
    public struct HalfVector2 : INetSerializable
    {
        public static implicit operator HalfVector2(Vector2 value) => new HalfVector2(value);
        public static implicit operator HalfVector2(Vector3 value) => new HalfVector2(value);
        public static implicit operator Vector2(HalfVector2 value) => value.ToVector2();
        public static implicit operator Vector3(HalfVector2 value) => value.ToVector2();
        public Vector2 ToVector2() => new Vector2(Mathf.HalfToFloat(x), Mathf.HalfToFloat(y));
        public ushort x;
        public ushort y;

        public HalfVector2(Vector2 vector2)
        {
            x = Mathf.FloatToHalf(vector2.x);
            y = Mathf.FloatToHalf(vector2.y);
        }

        public void Serialize(NetDataWriter writer)
        {
            writer.PutPackedUShort(x);
            writer.PutPackedUShort(y);
        }

        public void Deserialize(NetDataReader reader)
        {
            x = reader.GetPackedUShort();
            y = reader.GetPackedUShort();
        }

        public override string ToString()
        {
            return ToVector2().ToString();
        }
    }
}
