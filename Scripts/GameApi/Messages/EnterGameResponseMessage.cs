using LiteNetLib.Utils;

namespace LiteNetLibManager
{
    public struct EnterGameResponseMessage : INetSerializable
    {
        public long connectionId;
        public string serverSceneName;

        public void Deserialize(NetDataReader reader)
        {
            connectionId = reader.GetPackedLong();
            serverSceneName = reader.GetString();
        }

        public void Serialize(NetDataWriter writer)
        {
            writer.PutPackedLong(connectionId);
            writer.Put(serverSceneName);
        }
    }
}
