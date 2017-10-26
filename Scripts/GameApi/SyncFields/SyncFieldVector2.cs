using System;
using LiteNetLib.Utils;
using UnityEngine;

namespace LiteNetLibHighLevel
{
    [Serializable]
    public class SyncFieldVector2 : LiteNetLibSyncField<Vector2>
    {
        public override bool IsValueChanged(Vector2 newValue)
        {
            return !newValue.Equals(value);
        }

        public override void Deserialize(NetDataReader reader)
        {
            value = new Vector2(reader.GetFloat(), reader.GetFloat());
        }

        public override void Serialize(NetDataWriter writer)
        {
            writer.Put(value.x);
            writer.Put(value.y);
        }
    }
}
