using UnityEngine;
using LiteNetLib.Utils;

public struct DirectionVector3 : INetSerializable
{
    public static implicit operator DirectionVector3(Vector3 value) { return new DirectionVector3(value); }
    public static implicit operator Vector3(DirectionVector3 value) { return new Vector3((float)value.x / 10000f, (float)value.y / 10000f, (float)value.z / 10000f); }

    public short x;
    public short y;
    public short z;

    public DirectionVector3(Vector3 vector3)
    {
        x = (short)(vector3.x * 10000);
        y = (short)(vector3.y * 10000);
        z = (short)(vector3.z * 10000);
    }

    public void Serialize(NetDataWriter writer)
    {
        writer.PutPackedShort(x);
        writer.PutPackedShort(y);
        writer.PutPackedShort(z);
    }

    public void Deserialize(NetDataReader reader)
    {
        x = reader.GetPackedShort();
        y = reader.GetPackedShort();
        z = reader.GetPackedShort();
    }
}
