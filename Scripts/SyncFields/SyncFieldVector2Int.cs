using System;
using LiteNetLib.Utils;
using UnityEngine;

namespace LiteNetLibHighLevel
{
    [Serializable]
    public class SyncFieldVector2Int : LiteNetLibSyncFieldBase<Vector2Int>
    {
        public override bool IsValueChanged(Vector2Int newValue)
        {
            return !newValue.Equals(value);
        }

        public override void Deserialize(NetDataReader reader)
        {
            value = new Vector2Int(reader.GetInt(), reader.GetInt());
        }

        public override void Serialize(NetDataWriter writer)
        {
            writer.Put(value.x);
            writer.Put(value.y);
        }
    }
}
