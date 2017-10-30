using System;
using LiteNetLib.Utils;

namespace LiteNetLibHighLevel
{
    [Serializable]
    public class SyncFieldUShort : LiteNetLibSyncField<NetFieldUShort, ushort>
    {
        public override bool IsValueChanged(ushort newValue)
        {
            return newValue != Value;
        }
    }
}
