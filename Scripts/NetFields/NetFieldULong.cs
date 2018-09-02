using LiteNetLib.Utils;

namespace LiteNetLibManager
{
    public class NetFieldULong : LiteNetLibNetField<ulong>
    {
        public override void Deserialize(NetDataReader reader)
        {
            Value = reader.GetULong();
        }

        public override void Serialize(NetDataWriter writer)
        {
            writer.Put(Value);
        }

        public override bool IsValueChanged(ulong newValue)
        {
            return newValue != Value;
        }
    }
}
