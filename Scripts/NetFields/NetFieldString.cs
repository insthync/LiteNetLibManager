using LiteNetLib.Utils;
using UnityEngine;

namespace LiteNetLibManager
{
    public class NetFieldString : LiteNetLibNetField<string>
    {
        public override void Deserialize(NetDataReader reader)
        {
            Value = reader.GetString();
        }

        public override void Serialize(NetDataWriter writer)
        {
            writer.Put(Value);
        }

        public override bool IsValueChanged(string newValue)
        {
            return Value == null || (newValue != null && !newValue.Equals(Value));
        }
    }
}
