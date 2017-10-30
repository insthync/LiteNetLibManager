using LiteNetLib.Utils;

namespace LiteNetLibHighLevel
{
    public class NetFieldDouble : LiteNetLibNetField<double>
    {
        public override void Deserialize(NetDataReader reader)
        {
            Value = reader.GetDouble();
        }

        public override void Serialize(NetDataWriter writer)
        {
            writer.Put(Value);
        }
    }
}
