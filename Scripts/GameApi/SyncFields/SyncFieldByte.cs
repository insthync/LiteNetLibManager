using System;
using LiteNetLib.Utils;

namespace LiteNetLibHighLevel
{
    [Serializable]
    public class SyncFieldByte : LiteNetLibSyncField<NetFieldByte, byte>
    {
        public override bool IsValueChanged(byte newValue)
        {
            return newValue != Value;
        }
    }
}
