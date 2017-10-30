using System;
using LiteNetLib.Utils;

namespace LiteNetLibHighLevel
{
    [Serializable]
    public class SyncFieldLong : LiteNetLibSyncField<NetFieldLong, long>
    {
        public override bool IsValueChanged(long newValue)
        {
            return newValue != Value;
        }
    }
}
