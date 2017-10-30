using System;
using LiteNetLib.Utils;

namespace LiteNetLibHighLevel
{
    [Serializable]
    public class SyncFieldSByte : LiteNetLibSyncField<NetFieldSByte, sbyte>
    {
        public override bool IsValueChanged(sbyte newValue)
        {
            return newValue != Value;
        }
    }
}
