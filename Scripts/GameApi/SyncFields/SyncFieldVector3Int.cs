using System;
using LiteNetLib.Utils;
using UnityEngine;

namespace LiteNetLibHighLevel
{
    [Serializable]
    public class SyncFieldVector3Int : LiteNetLibSyncFieldBase<Vector3Int>
    {
        public override bool IsValueChanged(Vector3Int newValue)
        {
            return !newValue.Equals(value);
        }

        public override void Deserialize(NetDataReader reader)
        {
            value = new Vector3Int(reader.GetInt(), reader.GetInt(), reader.GetInt());
        }

        public override void Serialize(NetDataWriter writer)
        {
            writer.Put(value.x);
            writer.Put(value.y);
            writer.Put(value.z);
        }
    }
}
