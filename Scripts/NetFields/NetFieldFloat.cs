using LiteNetLib.Utils;

namespace LiteNetLibHighLevel
{
    public class NetFieldFloat : LiteNetLibNetField<float>
    {
        public override void Deserialize(NetDataReader reader)
        {
            Value = reader.GetFloat();
        }

        public override void Serialize(NetDataWriter writer)
        {
            writer.Put(Value);
        }
    }
}
