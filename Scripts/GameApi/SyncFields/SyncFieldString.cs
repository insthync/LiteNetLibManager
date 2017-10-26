using System;
using LiteNetLib.Utils;

namespace LiteNetLibHighLevel
{
    [Serializable]
    public class SyncFieldString : LiteNetLibSyncFieldBase<string>
    {
        public override bool IsValueChanged(string newValue)
        {
            return value == null || (newValue != null && !newValue.Equals(value));
        }

        public override void Deserialize(NetDataReader reader)
        {
            value = reader.GetString();
        }

        public override void Serialize(NetDataWriter writer)
        {
            writer.Put(value);
        }
    }
}
