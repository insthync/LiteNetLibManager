using LiteNetLib.Utils;
using UnityEngine;

namespace LiteNetLibHighLevel
{
    public class NetFieldULong : LiteNetLibNetField<ulong>
    {
        public override void Deserialize(NetDataReader reader)
        {
            Value = reader.GetULong();
        }

        public override void Serialize(NetDataWriter writer)
        {
            writer.Put(Value);
        }
    }
}
