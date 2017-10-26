using LiteNetLib;
using LiteNetLib.Utils;

namespace LiteNetLibHighLevel
{
    public class LiteNetLibFunctionParameter
    {
        public object Value
        {
            get; set;
        }

        public virtual void Deserialize(NetDataReader reader) { }
        public virtual void Serialize(NetDataWriter writer) { }
    }
}
