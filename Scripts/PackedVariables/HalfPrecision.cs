using UnityEngine;
using LiteNetLib.Utils;

public struct HalfPrecision : INetSerializable
{
    public static implicit operator HalfPrecision(float value) { return new HalfPrecision(value); }
    public static implicit operator float(HalfPrecision value) { return Mathf.HalfToFloat(value.halfValue); }

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
}
