using LiteNetLib.Utils;
using UnityEngine;

namespace LiteNetLibHighLevel
{
    public class NetFieldQuaternion : LiteNetLibNetField<Quaternion>
    {
        public override void Deserialize(NetDataReader reader)
        {
            Value = Quaternion.Euler(reader.GetFloat(), reader.GetFloat(), reader.GetFloat());
        }

        public override void Serialize(NetDataWriter writer)
        {
            var euler = Value.eulerAngles;
            writer.Put(euler.x);
            writer.Put(euler.y);
            writer.Put(euler.z);
        }

        public override bool IsValueChanged(Quaternion newValue)
        {
            return !newValue.Equals(Value);
        }
    }
}
