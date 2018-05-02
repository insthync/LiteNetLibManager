using LiteNetLib.Utils;

namespace LiteNetLibManager
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

        public override bool IsValueChanged(long newValue)
        {
            return newValue != Value;
        }
    }
}
