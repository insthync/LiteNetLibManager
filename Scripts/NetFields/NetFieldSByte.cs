using LiteNetLib.Utils;
using UnityEngine;

namespace LiteNetLibHighLevel
{
    public class NetFieldSByte : LiteNetLibNetField<sbyte>
    {
        public override void Deserialize(NetDataReader reader)
        {
            Value = reader.GetSByte();
        }

        public override void Serialize(NetDataWriter writer)
        {
            writer.Put(Value);
        }

        public override bool IsValueChanged(sbyte newValue)
        {
            return newValue != Value;
        }
    }
}
