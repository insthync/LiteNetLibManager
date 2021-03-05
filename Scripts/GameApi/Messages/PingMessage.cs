using LiteNetLib.Utils;

namespace LiteNetLibManager
{
    public struct PingMessage : INetSerializable
    {
        public long clientTime;

        public void Deserialize(NetDataReader reader)
        {
            clientTime = reader.GetPackedLong();
        }

        public void Serialize(NetDataWriter writer)
        {
            writer.PutPackedLong(clientTime);
        }
    }
}
