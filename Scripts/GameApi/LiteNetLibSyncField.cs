using System;
using UnityEngine;
using LiteNetLib;
using LiteNetLib.Utils;

namespace LiteNetLibHighLevel
{
    public abstract class LiteNetLibSyncField : LiteNetLibElement
    {
        public SendOptions sendOptions;
        [Tooltip("How many time of network updates per second, 0 => updates every frames")]
        [Range(0, 30)]
        public int sendRate = 5;
        [Tooltip("If this is TRUE, this will update for owner object only")]
        public bool forOwnerOnly;
        public bool hasUpdate { get; protected set; }
        protected float lastSentTime;

        public float SendInterval
        {
            get
            {
                if (sendRate == 0)
                    return 0;
                return 1f / sendRate;
            }
        }

        internal void NetworkUpdate()
        {
            if (!ValidateBeforeAccess())
                return;

            if (Time.realtimeSinceStartup - lastSentTime < SendInterval)
                return;

            lastSentTime = Time.realtimeSinceStartup;
            SendUpdate();
        }

        public abstract void SendUpdate();
        public abstract void SendUpdate(NetPeer peer);
        public abstract void DeserializeValue(NetDataReader reader);
        public abstract void SerializeValue(NetDataWriter writer);
    }
    
    public class LiteNetLibSyncField<TField, TFieldType> : LiteNetLibSyncField
        where TField : LiteNetLibNetField<TFieldType>, new()
    {
        protected TField field;
        public TField Field
        {
            get
            {
                if (field == null)
                    field = new TField();
                return field;
            }
        }

        [ReadOnly, SerializeField]
        protected TFieldType value;
        public TFieldType Value
        {
            get { return value; }
            set
            {
                if (!ValidateBeforeAccess())
                    return;

                if (Field.IsValueChanged(value))
                {
                    this.value = value;
                    hasUpdate = true;
                }
            }
        }

        public static implicit operator TFieldType(LiteNetLibSyncField<TField, TFieldType> field)
        {
            return field.Value;
        }

        public override sealed void SendUpdate()
        {
            if (!hasUpdate)
                return;

            if (!ValidateBeforeAccess())
                return;

            var manager = Manager;
            if (!manager.IsServer)
                return;

            hasUpdate = false;
            var peers = manager.Peers;
            if (forOwnerOnly)
            {
                var connectId = Behaviour.ConnectId;
                if (peers.ContainsKey(connectId))
                    SendUpdate(peers[connectId]);
            }
            else
            {
                foreach (var peer in peers.Values)
                {
                    SendUpdate(peer);
                }
            }
        }

        public override sealed void SendUpdate(NetPeer peer)
        {
            if (!ValidateBeforeAccess())
                return;

            var manager = Manager;
            if (!manager.IsServer)
                return;

            manager.SendPacket(sendOptions, peer, LiteNetLibGameManager.GameMsgTypes.ServerUpdateSyncField, SerializeForSend);
        }

        protected void SerializeForSend(NetDataWriter writer)
        {
            LiteNetLibElementInfo.SerializeInfo(GetInfo(), writer);
            SerializeValue(writer);
        }

        public override sealed void DeserializeValue(NetDataReader reader)
        {
            Field.Deserialize(reader);
            value = Field.Value;
        }

        public override sealed void SerializeValue(NetDataWriter writer)
        {
            Field.Value = value;
            Field.Serialize(writer);
        }
    }

    #region Implement for general usages and serializable
    [Serializable]
    public class SyncFieldBool : LiteNetLibSyncField<NetFieldBool, bool>
    {
    }

    [Serializable]
    public class SyncFieldByte : LiteNetLibSyncField<NetFieldByte, byte>
    {
    }

    [Serializable]
    public class SyncFieldChar : LiteNetLibSyncField<NetFieldChar, char>
    {
    }

    [Serializable]
    public class SyncFieldColor : LiteNetLibSyncField<NetFieldColor, Color>
    {
    }

    [Serializable]
    public class SyncFieldDouble : LiteNetLibSyncField<NetFieldDouble, double>
    {
    }

    [Serializable]
    public class SyncFieldFloat : LiteNetLibSyncField<NetFieldFloat, float>
    {
    }

    [Serializable]
    public class SyncFieldInt : LiteNetLibSyncField<NetFieldInt, int>
    {
    }

    [Serializable]
    public class SyncFieldLong : LiteNetLibSyncField<NetFieldLong, long>
    {
    }

    [Serializable]
    public class SyncFieldQuaternion : LiteNetLibSyncField<NetFieldQuaternion, Quaternion>
    {
    }

    [Serializable]
    public class SyncFieldSByte : LiteNetLibSyncField<NetFieldSByte, sbyte>
    {
    }

    [Serializable]
    public class SyncFieldShort : LiteNetLibSyncField<NetFieldShort, short>
    {
    }

    [Serializable]
    public class SyncFieldString : LiteNetLibSyncField<NetFieldString, string>
    {
    }

    [Serializable]
    public class SyncFieldUInt : LiteNetLibSyncField<NetFieldUInt, uint>
    {
    }

    [Serializable]
    public class SyncFieldULong : LiteNetLibSyncField<NetFieldULong, ulong>
    {
    }

    [Serializable]
    public class SyncFieldUShort : LiteNetLibSyncField<NetFieldUShort, ushort>
    {
    }

    [Serializable]
    public class SyncFieldVector2 : LiteNetLibSyncField<NetFieldVector2, Vector2>
    {
    }

    [Serializable]
    public class SyncFieldVector2Int : LiteNetLibSyncField<NetFieldVector2Int, Vector2Int>
    {
    }

    [Serializable]
    public class SyncFieldVector3 : LiteNetLibSyncField<NetFieldVector3, Vector3>
    {
    }

    [Serializable]
    public class SyncFieldVector3Int : LiteNetLibSyncField<NetFieldVector3Int, Vector3Int>
    {
    }

    [Serializable]
    public class SyncFieldVector4 : LiteNetLibSyncField<NetFieldVector4, Vector4>
    {
    }
    #endregion
}
