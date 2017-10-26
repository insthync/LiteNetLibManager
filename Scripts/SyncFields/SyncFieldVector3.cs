using System;
using LiteNetLib.Utils;
using UnityEngine;

namespace LiteNetLibHighLevel
{
    [Serializable]
    public class SyncFieldVector3 : LiteNetLibSyncFieldBase<Vector3>
    {
        public override bool IsValueChanged(Vector3 newValue)
        {
            return !newValue.Equals(value);
        }

        public override void Deserialize(NetDataReader reader)
        {
            value = new Vector3(reader.GetFloat(), reader.GetFloat(), reader.GetFloat());
        }

        public override void Serialize(NetDataWriter writer)
        {
            writer.Put(value.x);
            writer.Put(value.y);
            writer.Put(value.z);
        }
    }
}
