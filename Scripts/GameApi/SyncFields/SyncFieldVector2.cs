using System;
using LiteNetLib.Utils;
using UnityEngine;

namespace LiteNetLibHighLevel
{
    [Serializable]
    public class SyncFieldVector2 : LiteNetLibSyncField<NetFieldVector2, Vector2>
    {
        public override bool IsValueChanged(Vector2 newValue)
        {
            return !newValue.Equals(Value);
        }
    }
}
