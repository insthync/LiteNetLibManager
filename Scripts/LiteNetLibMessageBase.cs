using LiteNetLib.Utils;

namespace LiteNetLibHighLevel
{
    public abstract class LiteNetLibMessageBase
    {
        public abstract void Deserialize(NetDataReader reader);
        public abstract void Serialize(NetDataWriter writer);
    }
}
