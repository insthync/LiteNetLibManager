using LiteNetLib.Utils;

public struct PackedULong : INetSerializable
{
    public static implicit operator PackedULong(ulong value) { return new PackedULong(value); }
    public static implicit operator ulong(PackedULong value) { return value.value; }
    private ulong value;
    public PackedULong(ulong value)
    {
        this.value = value;
    }

    public void Serialize(NetDataWriter writer)
    {
        writer.PutPackedULong(value);
    }

    public void Deserialize(NetDataReader reader)
    {
        value = reader.GetPackedULong();
    }
}
