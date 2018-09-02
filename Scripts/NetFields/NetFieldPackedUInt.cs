using LiteNetLib.Utils;

namespace LiteNetLibManager
{
    public class NetFieldPackedUInt : LiteNetLibNetField<uint>
    {
        public override void Deserialize(NetDataReader reader)
        {
            Value = reader.GetPackedUInt();
        }

        public override void Serialize(NetDataWriter writer)
        {
            writer.PutPackedUInt(Value);
        }

        public override bool IsValueChanged(uint newValue)
        {
            return newValue != Value;
        }
    }
}
