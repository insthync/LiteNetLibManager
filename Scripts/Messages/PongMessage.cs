using LiteNetLib.Utils;

namespace LiteNetLibManager
{
    public struct PongMessage : INetSerializable
    {
        public long pingTime;
        public long serverTime;

        public void Deserialize(NetDataReader reader)
        {
            pingTime = reader.GetPackedLong();
            serverTime = reader.GetPackedLong();
        }

        public void Serialize(NetDataWriter writer)
        {
            writer.PutPackedLong(pingTime);
            writer.PutPackedLong(serverTime);
        }
    }
}
