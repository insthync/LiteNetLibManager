using System;
using LiteNetLib.Utils;
using UnityEngine;

namespace LiteNetLibHighLevel
{
    [Serializable]
    public class SyncFieldQuaternion : LiteNetLibSyncField<NetFieldQuaternion, Quaternion>
    {
        public override bool IsValueChanged(Quaternion newValue)
        {
            return !newValue.Equals(Value);
        }
    }
}
