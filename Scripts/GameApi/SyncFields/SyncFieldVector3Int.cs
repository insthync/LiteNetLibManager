using System;
using LiteNetLib.Utils;
using UnityEngine;

namespace LiteNetLibHighLevel
{
    [Serializable]
    public class SyncFieldVector3Int : LiteNetLibSyncField<NetFieldVector3Int, Vector3Int>
    {
        public override bool IsValueChanged(Vector3Int newValue)
        {
            return !newValue.Equals(Value);
        }
    }
}
