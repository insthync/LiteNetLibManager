using System;
using LiteNetLib.Utils;

namespace LiteNetLibHighLevel
{
    [Serializable]
    public class SyncFieldChar : LiteNetLibSyncField<NetFieldChar, char>
    {
        public override bool IsValueChanged(char newValue)
        {
            return newValue != Value;
        }
    }
}
