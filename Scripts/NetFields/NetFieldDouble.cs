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

        public override bool IsValueChanged(double newValue)
        {
            return newValue != Value;
        }
    }
}
