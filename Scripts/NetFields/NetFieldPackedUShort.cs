using LiteNetLib.Utils;

namespace LiteNetLibManager
{
    public class NetFieldPackedUShort : LiteNetLibNetField<ushort>
    {
        public override void Deserialize(NetDataReader reader)
        {
            Value = reader.GetPackedUShort();
        }

        public override void Serialize(NetDataWriter writer)
        {
            writer.PutPackedUShort(Value);
        }

        public override bool IsValueChanged(ushort newValue)
        {
            return newValue != Value;
        }
    }
}
