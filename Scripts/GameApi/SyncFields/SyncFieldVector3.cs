using System;
using LiteNetLib.Utils;
using UnityEngine;

namespace LiteNetLibHighLevel
{
    [Serializable]
    public class SyncFieldVector3 : LiteNetLibSyncField<NetFieldVector3, Vector3>
    {
        public override bool IsValueChanged(Vector3 newValue)
        {
            return !newValue.Equals(Value);
        }
    }
}
