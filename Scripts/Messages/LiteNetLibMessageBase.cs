using LiteNetLib.Utils;

namespace LiteNetLibHighLevel.Messages
{
    public abstract class LiteNetLibMessageBase
    {
        public virtual void Deserialize(NetDataReader reader) { }
        public virtual void Serialize(NetDataWriter writer) { }
    }
}
