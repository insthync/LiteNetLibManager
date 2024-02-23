using LiteNetLib.Utils;

namespace LiteNetLibManager
{
    public struct PongMessage : INetSerializable
    {
        public long pingTime;
        public long pongTime;

        public void Deserialize(NetDataReader reader)
        {
            pingTime = reader.GetPackedLong();
            pongTime = reader.GetPackedLong();
        }

        public void Serialize(NetDataWriter writer)
        {
            writer.PutPackedLong(pingTime);
            writer.PutPackedLong(pongTime);
        }
    }
}
