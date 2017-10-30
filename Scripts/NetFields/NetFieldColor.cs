using LiteNetLib.Utils;
using UnityEngine;

namespace LiteNetLibHighLevel
{
    public class NetFieldColor : LiteNetLibNetField<Color>
    {
        public override void Deserialize(NetDataReader reader)
        {
            Value = new Color(reader.GetFloat(), reader.GetFloat(), reader.GetFloat(), reader.GetFloat());
        }

        public override void Serialize(NetDataWriter writer)
        {
            writer.Put(Value.r);
            writer.Put(Value.g);
            writer.Put(Value.b);
            writer.Put(Value.a);
        }
    }
}
