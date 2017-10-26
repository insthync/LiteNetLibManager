using System;
using LiteNetLib.Utils;
using UnityEngine;

namespace LiteNetLibHighLevel
{
    [Serializable]
    public class SyncFieldVector4 : LiteNetLibSyncFieldBase<Vector4>
    {
        public override bool IsValueChanged(Vector4 newValue)
        {
            return !newValue.Equals(value);
        }

        public override void Deserialize(NetDataReader reader)
        {
            value = new Vector4(reader.GetFloat(), reader.GetFloat(), reader.GetFloat(), reader.GetFloat());
        }

        public override void Serialize(NetDataWriter writer)
        {
            writer.Put(value.x);
            writer.Put(value.y);
            writer.Put(value.z);
            writer.Put(value.w);
        }
    }
}
