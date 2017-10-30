using System;
using LiteNetLib.Utils;
using UnityEngine;

namespace LiteNetLibHighLevel
{
    [Serializable]
    public class SyncFieldVector2Int : LiteNetLibSyncField<NetFieldVector2Int, Vector2Int>
    {
        public override bool IsValueChanged(Vector2Int newValue)
        {
            return !newValue.Equals(Value);
        }
    }
}
