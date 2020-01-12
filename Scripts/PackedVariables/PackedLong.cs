using LiteNetLib.Utils;

public struct PackedLong : INetSerializable
{
    public static implicit operator PackedLong(long value) { return new PackedLong(value); }
    public static implicit operator long(PackedLong value) { return value.value; }
    private long value;
    public PackedLong(long value)
    {
        this.value = value;
    }

    public void Serialize(NetDataWriter writer)
    {
        writer.PutPackedLong(value);
    }

    public void Deserialize(NetDataReader reader)
    {
        value = reader.GetPackedLong();
    }
}
