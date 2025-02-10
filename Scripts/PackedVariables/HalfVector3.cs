using UnityEngine;
using LiteNetLib.Utils;

public struct HalfVector3 : INetSerializable
{
    public static implicit operator HalfVector3(Vector3 value) { return new HalfVector3(value); }
    public static implicit operator Vector3(HalfVector3 value) { return new Vector3(Mathf.HalfToFloat(value.x), Mathf.HalfToFloat(value.y), Mathf.HalfToFloat(value.z)); }

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
}
