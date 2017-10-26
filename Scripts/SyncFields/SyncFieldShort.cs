using System;
using LiteNetLib.Utils;

namespace LiteNetLibHighLevel
{
    [Serializable]
    public class SyncFieldShort : LiteNetLibSyncFieldBase<short>
    {
        public override bool IsValueChanged(short newValue)
        {
            return newValue != value;
        }

        public override void Deserialize(NetDataReader reader)
        {
            value = reader.GetShort();
        }

        public override void Serialize(NetDataWriter writer)
        {
            writer.Put(value);
        }
    }
}
