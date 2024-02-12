using LiteNetLib.Utils;

namespace LiteNetLibManager
{
    public struct ServerSceneChangeMessage : INetSerializable
    {
        public ServerSceneInfo serverSceneInfo;

        public void Deserialize(NetDataReader reader)
        {
            serverSceneInfo = reader.Get<ServerSceneInfo>();
        }

        public void Serialize(NetDataWriter writer)
        {
            writer.Put(serverSceneInfo);
        }
    }
}
