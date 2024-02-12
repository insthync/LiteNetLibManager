using LiteNetLib.Utils;

namespace LiteNetLibManager
{
    [System.Serializable]
    public struct ServerSceneInfo : INetSerializable
    {
        public bool isAddressable;
        public string sceneNameOrKey;

        public void Deserialize(NetDataReader reader)
        {
            isAddressable = reader.GetBool();
            sceneNameOrKey = reader.GetString();
        }

        public void Serialize(NetDataWriter writer)
        {
            writer.Put(isAddressable);
            writer.Put(sceneNameOrKey);
        }

        public bool Equals(ServerSceneInfo another)
        {
            return isAddressable == another.isAddressable && string.Equals(sceneNameOrKey, another.sceneNameOrKey);
        }
    }
}