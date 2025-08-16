using UnityEngine;
using LiteNetLib.Utils;

namespace LiteNetLibManager
{
    public struct HalfPrecision : INetSerializable
    {
        public static implicit operator HalfPrecision(float value) => new HalfPrecision(value);
        public static implicit operator float(HalfPrecision value) => value.ToFloat();
        public float ToFloat() => Mathf.HalfToFloat(halfValue);
        public ushort halfValue;

        public HalfPrecision(float value)
        {
            halfValue = Mathf.FloatToHalf(value);
        }

        public void Serialize(NetDataWriter writer)
        {
            writer.PutPackedUShort(halfValue);
        }

        public void Deserialize(NetDataReader reader)
        {
            halfValue = reader.GetPackedUShort();
        }

        public override string ToString()
        {
            return ToFloat().ToString();
        }
    }
}
