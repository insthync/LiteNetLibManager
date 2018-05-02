using LiteNetLib.Utils;

namespace LiteNetLibManager
{
    public class NetFieldFloat : LiteNetLibNetField<float>
    {
        public override void Deserialize(NetDataReader reader)
        {
            Value = reader.GetFloat();
        }

        public override void Serialize(NetDataWriter writer)
        {
            writer.Put(Value);
        }

        public override bool IsValueChanged(float newValue)
        {
            return newValue != Value;
        }
    }
}
