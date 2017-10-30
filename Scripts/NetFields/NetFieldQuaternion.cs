using LiteNetLib.Utils;
using UnityEngine;

namespace LiteNetLibHighLevel
{
    public class NetFieldQuaternion : LiteNetLibNetField<Quaternion>
    {
        public override void Deserialize(NetDataReader reader)
        {
            Value = new Quaternion(reader.GetFloat(), reader.GetFloat(), reader.GetFloat(), reader.GetFloat());
        }

        public override void Serialize(NetDataWriter writer)
        {
            writer.Put(Value.x);
            writer.Put(Value.y);
            writer.Put(Value.z);
            writer.Put(Value.w);
        }
    }
}
