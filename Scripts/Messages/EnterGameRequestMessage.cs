using LiteNetLib.Utils;

namespace LiteNetLibManager
{
    public struct EnterGameRequestMessage : INetSerializable
    {
        public uint packetVersion;

        public void Deserialize(NetDataReader reader)
        {
            packetVersion = reader.GetPackedUInt();
        }

        public void Serialize(NetDataWriter writer)
        {
            writer.PutPackedUInt(packetVersion);
        }
    }
}
