using System;
using LiteNetLib.Utils;

namespace LiteNetLibHighLevel
{
    [Serializable]
    public class SyncFieldBool : LiteNetLibSyncField<NetFieldBool, bool>
    {
        public override bool IsValueChanged(bool newValue)
        {
            return newValue != Value;
        }
    }
}
