using System;
using LiteNetLib.Utils;

namespace LiteNetLibHighLevel
{
    [Serializable]
    public class SyncFieldDouble : LiteNetLibSyncField<NetFieldDouble, double>
    {
        public override bool IsValueChanged(double newValue)
        {
            return newValue != Value;
        }
    }
}
