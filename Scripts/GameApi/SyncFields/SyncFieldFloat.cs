using System;
using LiteNetLib.Utils;

namespace LiteNetLibHighLevel
{
    [Serializable]
    public class SyncFieldFloat : LiteNetLibSyncField<NetFieldFloat, float>
    {
        public override bool IsValueChanged(float newValue)
        {
            return newValue != Value;
        }
    }
}
