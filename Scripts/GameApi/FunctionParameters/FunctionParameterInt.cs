using LiteNetLib.Utils;

namespace LiteNetLibHighLevel
{
    public class FunctionParameterInt : LiteNetLibFunctionParameter<int>
    {
        public override void Deserialize(NetDataReader reader)
        {
            Value = reader.GetInt();
        }

        public override void Serialize(NetDataWriter writer)
        {
            writer.Put((int)Value);
        }
    }
}
