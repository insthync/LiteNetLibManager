using System;
using LiteNetLib.Utils;

namespace LiteNetLibHighLevel
{
    [Serializable]
    public class SyncFieldBool : LiteNetLibSyncFieldBase<bool>
    {
        public override bool IsValueChanged(bool newValue)
        {
            return newValue != value;
        }

        public override void Deserialize(NetDataReader reader)
        {
            value = reader.GetBool();
        }

        public override void Serialize(NetDataWriter writer)
        {
            writer.Put(value);
        }
    }
}
