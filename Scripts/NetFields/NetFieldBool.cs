using LiteNetLib.Utils;

namespace LiteNetLibManager
{
    public class NetFieldBool : LiteNetLibNetField<bool>
    {
        public override void Deserialize(NetDataReader reader)
        {
            Value = reader.GetBool();
        }

        public override void Serialize(NetDataWriter writer)
        {
            writer.Put(Value);
        }

        public override bool IsValueChanged(bool newValue)
        {
            return newValue != Value;
        }
    }
}
