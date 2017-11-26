using LiteNetLib.Utils;

namespace LiteNetLibHighLevel
{
    public class NetFieldInt : LiteNetLibNetField<int>
    {
        public override void Deserialize(NetDataReader reader)
        {
            Value = reader.GetInt();
        }

        public override void Serialize(NetDataWriter writer)
        {
            writer.Put(Value);
        }

        public override bool IsValueChanged(int newValue)
        {
            return newValue != Value;
        }
    }
}
