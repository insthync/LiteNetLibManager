using System;
using LiteNetLib.Utils;
using UnityEngine;

namespace LiteNetLibHighLevel
{
    [Serializable]
    public class SyncFieldColor : LiteNetLibSyncField<Color>
    {
        public override bool IsValueChanged(Color newValue)
        {
            return !newValue.Equals(value);
        }

        public override void Deserialize(NetDataReader reader)
        {
            value = new Color(reader.GetFloat(), reader.GetFloat(), reader.GetFloat(), reader.GetFloat());
        }

        public override void Serialize(NetDataWriter writer)
        {
            writer.Put(value.r);
            writer.Put(value.g);
            writer.Put(value.b);
            writer.Put(value.a);
        }
    }
}
