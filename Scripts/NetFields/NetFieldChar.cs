using LiteNetLib.Utils;

namespace LiteNetLibHighLevel
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
    }
}
