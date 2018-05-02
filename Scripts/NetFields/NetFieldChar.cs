using LiteNetLib.Utils;

namespace LiteNetLibManager
{
    public class NetFieldChar : LiteNetLibNetField<char>
    {
        public override void Deserialize(NetDataReader reader)
        {
            Value = reader.GetChar();
        }

        public override void Serialize(NetDataWriter writer)
        {
            writer.Put(Value);
        }

        public override bool IsValueChanged(char newValue)
        {
            return newValue != Value;
        }
    }
}
