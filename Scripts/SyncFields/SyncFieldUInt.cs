using System;
using LiteNetLib.Utils;

namespace LiteNetLibHighLevel
{
    [Serializable]
    public class SyncFieldUInt : LiteNetLibSyncFieldBase<uint>
    {
        public override bool IsValueChanged(uint newValue)
        {
            return newValue != value;
        }

        public override void Deserialize(NetDataReader reader)
        {
            value = reader.GetUInt();
        }

        public override void Serialize(NetDataWriter writer)
        {
            writer.Put(value);
        }
    }
}
