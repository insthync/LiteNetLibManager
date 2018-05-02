using LiteNetLib.Utils;

namespace LiteNetLibManager
{
    public interface ILiteNetLibMessage
    {
        void Deserialize(NetDataReader reader);
        void Serialize(NetDataWriter writer);
    }
}
