using System;
using LiteNetLib.Utils;

namespace LiteNetLibHighLevel
{
    [Serializable]
    public class SyncFieldChar : LiteNetLibSyncFieldBase<char>
    {
        public override bool IsValueChanged(char newValue)
        {
            return newValue != value;
        }

        public override void Deserialize(NetDataReader reader)
        {
            value = reader.GetChar();
        }

        public override void Serialize(NetDataWriter writer)
        {
            writer.Put(value);
        }
    }
}
