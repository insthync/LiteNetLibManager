using LiteNetLib.Utils;
using UnityEngine;

namespace LiteNetLibHighLevel
{
    public class NetFieldUInt : LiteNetLibNetField<uint>
    {
        public override void Deserialize(NetDataReader reader)
        {
            Value = reader.GetUInt();
        }

        public override void Serialize(NetDataWriter writer)
        {
            writer.Put(Value);
        }

        public override bool IsValueChanged(uint newValue)
        {
            return newValue != Value;
        }
    }
}
