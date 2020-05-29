using LiteNetLib.Utils;

namespace LiteNetLibManager
{
    public interface INetSerializableWithElement : INetSerializable
    {
        void Deserialize(NetDataReader reader, LiteNetLibElement element);
        void Serialize(NetDataWriter writer, LiteNetLibElement element);
    }
}
