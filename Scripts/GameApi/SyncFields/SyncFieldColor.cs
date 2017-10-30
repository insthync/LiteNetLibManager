using System;
using LiteNetLib.Utils;
using UnityEngine;

namespace LiteNetLibHighLevel
{
    [Serializable]
    public class SyncFieldColor : LiteNetLibSyncField<NetFieldColor, Color>
    {
        public override bool IsValueChanged(Color newValue)
        {
            return !newValue.Equals(Value);
        }
    }
}
