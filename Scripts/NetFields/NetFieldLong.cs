using LiteNetLib.Utils;

namespace LiteNetLibHighLevel
{
    public class NetFieldLong : LiteNetLibNetField<long>
    {
        public override void Deserialize(NetDataReader reader)
        {
            Value = reader.GetLong();
        }

        public override void Serialize(NetDataWriter writer)
        {
            writer.Put(Value);
        }
    }
}
