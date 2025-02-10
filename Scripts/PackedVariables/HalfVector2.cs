using UnityEngine;
using LiteNetLib.Utils;

public struct HalfVector2 : INetSerializable
{
    public static implicit operator HalfVector2(Vector2 value) { return new HalfVector2(value); }
    public static implicit operator Vector2(HalfVector2 value) { return new Vector2(Mathf.HalfToFloat(value.x), Mathf.HalfToFloat(value.y)); }

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
}
