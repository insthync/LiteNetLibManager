using LiteNetLib.Utils;

namespace LiteNetLibManager
{
    public struct PongMessage : INetSerializable
    {
        public long clientTime;
        public long serverUnixTime;

        public void Deserialize(NetDataReader reader)
        {
            clientTime = reader.GetPackedLong();
            serverUnixTime = reader.GetPackedLong();
        }

        public void Serialize(NetDataWriter writer)
        {
            writer.PutPackedLong(clientTime);
            writer.PutPackedLong(serverUnixTime);
        }
    }
}
