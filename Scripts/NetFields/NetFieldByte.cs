using LiteNetLib.Utils;

namespace LiteNetLibManager
{
    public class NetFieldByte : LiteNetLibNetField<byte>
    {
        public override void Deserialize(NetDataReader reader)
        {
            Value = reader.GetByte();
        }

        public override void Serialize(NetDataWriter writer)
        {
            writer.Put(Value);
        }

        public override bool IsValueChanged(byte newValue)
        {
            return newValue != Value;
        }
    }
}
