using LiteNetLib.Utils;
using UnityEngine;

namespace LiteNetLibHighLevel
{
    public class NetFieldUShort : LiteNetLibNetField<ushort>
    {
        public override void Deserialize(NetDataReader reader)
        {
            Value = reader.GetUShort();
        }

        public override void Serialize(NetDataWriter writer)
        {
            writer.Put(Value);
        }
    }
}
