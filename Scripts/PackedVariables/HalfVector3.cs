using UnityEngine;
using LiteNetLib.Utils;

namespace LiteNetLibManager
{
    public struct HalfVector3 : INetSerializable
    {
        public static implicit operator HalfVector3(Vector3 value) => new HalfVector3(value);
        public static implicit operator Vector3(HalfVector3 value) => value.ToVector3();
        public Vector3 ToVector3() => new Vector3(Mathf.HalfToFloat(x), Mathf.HalfToFloat(y), Mathf.HalfToFloat(z));
        public ushort x;
        public ushort y;
        public ushort z;

        public HalfVector3(Vector3 vector3)
        {
            x = Mathf.FloatToHalf(vector3.x);
            y = Mathf.FloatToHalf(vector3.y);
            z = Mathf.FloatToHalf(vector3.z);
        }

        public void Serialize(NetDataWriter writer)
        {
            writer.PutPackedUShort(x);
            writer.PutPackedUShort(y);
            writer.PutPackedUShort(z);
        }

        public void Deserialize(NetDataReader reader)
        {
            x = reader.GetPackedUShort();
            y = reader.GetPackedUShort();
            z = reader.GetPackedUShort();
        }

        public override string ToString()
        {
            return ToVector3().ToString();
        }
    }
}
