using LiteNetLib.Utils;
using UnityEngine;

namespace LiteNetLibHighLevel
{
    public class NetFieldVector4 : LiteNetLibNetField<Vector4>
    {
        public override void Deserialize(NetDataReader reader)
        {
            Value = new Vector4(reader.GetFloat(), reader.GetFloat(), reader.GetFloat(), reader.GetFloat());
        }

        public override void Serialize(NetDataWriter writer)
        {
            writer.Put(Value.x);
            writer.Put(Value.y);
            writer.Put(Value.z);
            writer.Put(Value.w);
        }

        public override bool IsValueChanged(Vector4 newValue)
        {
            return !newValue.Equals(Value);
        }
    }
}
