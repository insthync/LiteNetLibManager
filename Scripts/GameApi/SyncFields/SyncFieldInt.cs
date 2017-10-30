using System;
using LiteNetLib.Utils;

namespace LiteNetLibHighLevel
{
    [Serializable]
    public class SyncFieldInt : LiteNetLibSyncField<NetFieldInt, int>
    {
        public override bool IsValueChanged(int newValue)
        {
            return newValue != Value;
        }
    }
}
