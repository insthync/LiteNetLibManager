using System;
using LiteNetLib.Utils;

namespace LiteNetLibHighLevel
{
    [Serializable]
    public class SyncFieldShort : LiteNetLibSyncField<NetFieldShort, short>
    {
        public override bool IsValueChanged(short newValue)
        {
            return newValue != Value;
        }
    }
}
