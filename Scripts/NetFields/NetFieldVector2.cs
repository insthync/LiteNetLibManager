using LiteNetLib.Utils;
using UnityEngine;

namespace LiteNetLibHighLevel
{
    public class NetFieldVector2 : LiteNetLibNetField<Vector2>
    {
        public override void Deserialize(NetDataReader reader)
        {
            Value = new Vector2(reader.GetFloat(), reader.GetFloat());
        }

        public override void Serialize(NetDataWriter writer)
        {
            writer.Put(Value.x);
            writer.Put(Value.y);
        }

        public override bool IsValueChanged(Vector2 newValue)
        {
            return !newValue.Equals(Value);
        }
    }
}
