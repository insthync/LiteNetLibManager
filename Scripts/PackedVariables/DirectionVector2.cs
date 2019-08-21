using UnityEngine;
using LiteNetLib.Utils;

public struct DirectionVector2 : INetSerializable
{
    public static implicit operator DirectionVector2(Vector2 value) { return new DirectionVector2(value); }
    public static implicit operator Vector2(DirectionVector2 value) { return new Vector2(value.x / 100, value.y / 100); }

    private sbyte x;
    private sbyte y;

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
