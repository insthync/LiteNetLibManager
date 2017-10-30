using System;
using LiteNetLib.Utils;

namespace LiteNetLibHighLevel
{
    [Serializable]
    public class SyncFieldString : LiteNetLibSyncField<NetFieldString, string>
    {
        public override bool IsValueChanged(string newValue)
        {
            return Value == null || (newValue != null && !newValue.Equals(Value));
        }
    }
}
