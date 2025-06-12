using LiteNetLib.Utils;

namespace LiteNetLibManager
{
    public struct PongMessage : INetSerializable
    {
        public long pingTime;
        public long pongTime;
        public uint tick;

        public void Deserialize(NetDataReader reader)
        {
            pingTime = reader.GetPackedLong();
            pongTime = reader.GetPackedLong();
            tick = reader.GetPackedUInt();
        }

        public void Serialize(NetDataWriter writer)
        {
            writer.PutPackedLong(pingTime);
            writer.PutPackedLong(pongTime);
            writer.PutPackedUInt(tick);
        }
    }
}
