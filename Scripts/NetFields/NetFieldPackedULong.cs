using LiteNetLib.Utils;
using UnityEngine;

namespace LiteNetLibManager
{
    public class NetFieldPackedULong : LiteNetLibNetField<ulong>
    {
        public override void Deserialize(NetDataReader reader)
        {
            Value = reader.GetPackedULong();
        }

        public override void Serialize(NetDataWriter writer)
        {
            writer.PutPackedULong(Value);
        }

        public override bool IsValueChanged(ulong newValue)
        {
            return newValue != Value;
        }
    }
}
