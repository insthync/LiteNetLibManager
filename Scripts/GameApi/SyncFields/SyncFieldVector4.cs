using System;
using LiteNetLib.Utils;
using UnityEngine;

namespace LiteNetLibHighLevel
{
    [Serializable]
    public class SyncFieldVector4 : LiteNetLibSyncField<NetFieldVector4, Vector4>
    {
        public override bool IsValueChanged(Vector4 newValue)
        {
            return !newValue.Equals(Value);
        }
    }
}
