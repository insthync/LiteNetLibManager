using LiteNetLib.Utils;

namespace LiteNetLibManager
{
    public struct PackedUShort : INetSerializable
    {
        public static implicit operator PackedUShort(ushort value) { return new PackedUShort(value); }
        public static implicit operator ushort(PackedUShort value) { return value.value; }
        private ushort value;
        public PackedUShort(ushort value)
        {
            this.value = value;
        }

        public void Serialize(NetDataWriter writer)
        {
            writer.PutPackedUShort(value);
        }

        public void Deserialize(NetDataReader reader)
        {
            value = reader.GetPackedUShort();
        }
    }
}
