using LiteNetLib.Utils;
using UnityEngine;

namespace LiteNetLibManager
{
    public class NetFieldVector3Int : LiteNetLibNetField<Vector3Int>
    {
        public override void Deserialize(NetDataReader reader)
        {
            Value = new Vector3Int(reader.GetInt(), reader.GetInt(), reader.GetInt());
        }

        public override void Serialize(NetDataWriter writer)
        {
            writer.Put(Value.x);
            writer.Put(Value.y);
            writer.Put(Value.z);
        }

        public override bool IsValueChanged(Vector3Int newValue)
        {
            return !newValue.Equals(Value);
        }
    }
}
