using UnityEngine;
using LiteNetLib.Utils;

public struct DirectionVector2 : INetSerializable
{
    public static implicit operator DirectionVector2(Vector2 value) { return new DirectionVector2(value); }
    public static implicit operator DirectionVector2(Vector3 value) { return new DirectionVector2(value); }
    public static implicit operator Vector2(DirectionVector2 value) { return new Vector2((float)value.x / 10000f, (float)value.y / 10000f); }
    public static implicit operator Vector3(DirectionVector2 value) { return new Vector2((float)value.x / 10000f, (float)value.y / 10000f); }

    public short x;
    public short y;

    public DirectionVector2(Vector2 vector2)
    {
        x = (short)(vector2.x * 10000);
        y = (short)(vector2.y * 10000);
    }

    public void Serialize(NetDataWriter writer)
    {
        writer.PutPackedShort(x);
        writer.PutPackedShort(y);
    }

    public void Deserialize(NetDataReader reader)
    {
        x = reader.GetPackedShort();
        y = reader.GetPackedShort();
    }
}
