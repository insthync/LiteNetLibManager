using LiteNetLib.Utils;
using UnityEngine;

namespace LiteNetLibManager
{
    public class NetFieldQuaternion : LiteNetLibNetField<Quaternion>
    {
        public override void Deserialize(NetDataReader reader)
        {
            var vector3 = new Vector3(reader.GetFloat(), reader.GetFloat(), reader.GetFloat());
            var quaternion = vector3.magnitude <= 0 ? Quaternion.identity : Quaternion.Euler(vector3);
            Value = quaternion;
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
