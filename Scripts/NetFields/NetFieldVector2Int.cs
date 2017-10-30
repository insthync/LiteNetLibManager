using LiteNetLib.Utils;
using UnityEngine;

namespace LiteNetLibHighLevel
{
    public class NetFieldVector2Int : LiteNetLibNetField<Vector2Int>
    {
        public override void Deserialize(NetDataReader reader)
        {
            Value = new Vector2Int(reader.GetInt(), reader.GetInt());
        }

        public override void Serialize(NetDataWriter writer)
        {
            writer.Put(Value.x);
            writer.Put(Value.y);
        }
    }
}
