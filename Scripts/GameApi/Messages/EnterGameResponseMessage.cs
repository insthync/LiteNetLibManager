using LiteNetLib.Utils;

namespace LiteNetLibManager
{
    public struct EnterGameResponseMessage : INetSerializable
    {
        public long connectionId;
        public ServerSceneInfo serverSceneInfo;

        public void Deserialize(NetDataReader reader)
        {
            connectionId = reader.GetPackedLong();
            serverSceneInfo = reader.Get<ServerSceneInfo>();
        }

        public void Serialize(NetDataWriter writer)
        {
            writer.PutPackedLong(connectionId);
            writer.Put(serverSceneInfo);
        }
    }
}
