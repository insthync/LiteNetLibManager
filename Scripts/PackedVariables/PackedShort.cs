using LiteNetLib.Utils;

namespace LiteNetLibManager
{
    public struct PackedShort : INetSerializable
    {
        public static implicit operator PackedShort(short value) { return new PackedShort(value); }
        public static implicit operator short(PackedShort value) { return value.value; }
        private short value;
        public PackedShort(short value)
        {
            this.value = value;
        }

        public void Serialize(NetDataWriter writer)
        {
            writer.PutPackedShort(value);
        }

        public void Deserialize(NetDataReader reader)
        {
            value = reader.GetPackedShort();
        }
    }
}
