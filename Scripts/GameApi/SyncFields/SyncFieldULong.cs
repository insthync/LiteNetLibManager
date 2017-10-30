using System;
using LiteNetLib.Utils;

namespace LiteNetLibHighLevel
{
    [Serializable]
    public class SyncFieldULong : LiteNetLibSyncField<NetFieldULong, ulong>
    {
        public override bool IsValueChanged(ulong newValue)
        {
            return newValue != Value;
        }
    }
}
