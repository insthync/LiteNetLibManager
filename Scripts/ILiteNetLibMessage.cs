using LiteNetLib.Utils;

namespace LiteNetLibHighLevel
{
    public interface ILiteNetLibMessage
    {
        void Deserialize(NetDataReader reader);
        void Serialize(NetDataWriter writer);
    }
}
