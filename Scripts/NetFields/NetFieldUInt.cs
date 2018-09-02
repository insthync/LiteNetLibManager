using LiteNetLib.Utils;

namespace LiteNetLibManager
{
    public class NetFieldUInt : LiteNetLibNetField<uint>
    {
        public override void Deserialize(NetDataReader reader)
        {
            Value = reader.GetUInt();
        }

        public override void Serialize(NetDataWriter writer)
        {
            writer.Put(Value);
        }

        public override bool IsValueChanged(uint newValue)
        {
            return newValue != Value;
        }
    }
}
