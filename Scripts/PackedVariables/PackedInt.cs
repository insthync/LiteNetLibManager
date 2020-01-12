using LiteNetLib.Utils;

public struct PackedInt : INetSerializable
{
    public static implicit operator PackedInt(int value) { return new PackedInt(value); }
    public static implicit operator int(PackedInt value) { return value.value; }
    private int value;
    public PackedInt(int value)
    {
        this.value = value;
    }

    public void Serialize(NetDataWriter writer)
    {
        writer.PutPackedInt(value);
    }

    public void Deserialize(NetDataReader reader)
    {
        value = reader.GetPackedInt();
    }
}
