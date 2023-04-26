using LiteNetLib.Utils;

namespace LiteNetLibManager
{
    public struct PingMessage : INetSerializable
    {
        public long pingTime;

        public void Deserialize(NetDataReader reader)
        {
            pingTime = reader.GetPackedLong();
        }

        public void Serialize(NetDataWriter writer)
        {
            writer.PutPackedLong(pingTime);
        }
    }
}
