using System;
using LiteNetLib.Utils;

namespace LiteNetLibHighLevel
{
    [Serializable]
    public class SyncFieldLong : LiteNetLibSyncFieldBase<long>
    {
        public override bool IsValueChanged(long newValue)
        {
            return newValue != value;
        }

        public override void Deserialize(NetDataReader reader)
        {
            value = reader.GetLong();
        }

        public override void Serialize(NetDataWriter writer)
        {
            writer.Put(value);
        }
    }
}
