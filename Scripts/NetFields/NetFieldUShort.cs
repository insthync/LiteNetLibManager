using LiteNetLib.Utils;

namespace LiteNetLibManager
{
    public class NetFieldUShort : LiteNetLibNetField<ushort>
    {
        public override void Deserialize(NetDataReader reader)
        {
            Value = reader.GetUShort();
        }

        public override void Serialize(NetDataWriter writer)
        {
            writer.Put(Value);
        }

        public override bool IsValueChanged(ushort newValue)
        {
            return newValue != Value;
        }
    }
}
