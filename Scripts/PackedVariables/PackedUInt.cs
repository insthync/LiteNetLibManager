using LiteNetLib.Utils;

namespace LiteNetLibManager
{
    public struct PackedUInt : INetSerializable
    {
        public static implicit operator PackedUInt(uint value) { return new PackedUInt(value); }
        public static implicit operator uint(PackedUInt value) { return value.value; }
        private uint value;
        public PackedUInt(uint value)
        {
            this.value = value;
        }

        public void Serialize(NetDataWriter writer)
        {
            writer.PutPackedUInt(value);
        }

        public void Deserialize(NetDataReader reader)
        {
            value = reader.GetPackedUInt();
        }
    }
}
