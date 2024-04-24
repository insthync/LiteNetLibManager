using LiteNetLib.Utils;

namespace LiteNetLibManager
{
    [System.Serializable]
    public struct ServerSceneInfo : INetSerializable
    {
        public bool isAddressable;
        public string addressableKey;
        public string sceneName;

        public void Deserialize(NetDataReader reader)
        {
            isAddressable = reader.GetBool();
            addressableKey = reader.GetString();
            sceneName = reader.GetString();
        }

        public void Serialize(NetDataWriter writer)
        {
            writer.Put(isAddressable);
            writer.Put(addressableKey);
            writer.Put(sceneName);
        }
    }
}