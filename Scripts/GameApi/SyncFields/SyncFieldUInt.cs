using System;
using LiteNetLib.Utils;

namespace LiteNetLibHighLevel
{
    [Serializable]
    public class SyncFieldUInt : LiteNetLibSyncField<NetFieldUInt, uint>
    {
        public override bool IsValueChanged(uint newValue)
        {
            return newValue != Value;
        }
    }
}
