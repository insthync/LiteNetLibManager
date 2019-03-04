using System;
using UnityEngine;
using LiteNetLib;
using LiteNetLib.Utils;

namespace LiteNetLibManager
{
    public abstract class LiteNetLibSyncField : LiteNetLibElement
    {
        public DeliveryMethod deliveryMethod;
        [Tooltip("Interval to send network data")]
        [Range(0.01f, 2f)]
        public float sendInterval = 0.1f;
        [Tooltip("If this is TRUE it will syncing although no changes")]
        public bool alwaysSync;
        [Tooltip("If this is TRUE it will not sync initial data immdediately with spawn message (it will sync later)")]
        public bool doNotSyncInitialDataImmediately;
        [Tooltip("If this is TRUE, this will update to owner client only")]
        public bool forOwnerOnly;
        public bool hasUpdate { get; protected set; }
        protected float lastSentTime;

        internal void NetworkUpdate()
        {
            if (!ValidateBeforeAccess())
                return;

            if (!alwaysSync && !hasUpdate)
                return;

            if (Time.unscaledTime - lastSentTime < sendInterval)
                return;

            lastSentTime = Time.unscaledTime;
            hasUpdate = false;

            SendUpdate();
        }

        public abstract object Get();
        public abstract void Set(object value);
        internal abstract void SendUpdate();
        internal abstract void SendUpdate(long connectionId);
        internal abstract void SendUpdate(long connectionId, DeliveryMethod deliveryMethod);
        internal abstract void Deserialize(NetDataReader reader);
        internal abstract void Serialize(NetDataWriter writer);
    }
    
    public class LiteNetLibSyncField<TType> : LiteNetLibSyncField
    {
        public Action<TType> onChange;

        [SerializeField]
        protected TType value;
        public TType Value
        {
            get { return value; }
            set
            {
                if (!ValidateBeforeAccess())
                {
                    // Set intial values
                    this.value = value;
                    return;
                }

                if (IsValueChanged(value))
                {
                    this.value = value;
                    hasUpdate = true;
                    if (onChange != null)
                        onChange.Invoke(value);
                }
            }
        }

        public override object Get()
        {
            return Value;
        }

        public override void Set(object value)
        {
            Value = (TType)value;
        }

        protected virtual bool IsValueChanged(TType newValue)
        {
            return value == null || !value.Equals(newValue);
        }

        public static implicit operator TType(LiteNetLibSyncField<TType> field)
        {
            return field.Value;
        }

        internal override void Setup(LiteNetLibBehaviour behaviour, byte elementId)
        {
            base.Setup(behaviour, elementId);
            if (onChange != null)
                onChange.Invoke(Value);
        }

        protected override bool ValidateBeforeAccess()
        {
            return Behaviour != null && Behaviour.IsServer;
        }

        internal override sealed void SendUpdate()
        {
            if (!ValidateBeforeAccess())
            {
                Debug.LogError("[LiteNetLibSyncField] Error while set value, behaviour is empty or not the server");
                return;
            }

            if (forOwnerOnly)
            {
                long connectionId = Behaviour.ConnectionId;
                if (Manager.ContainsConnectionId(connectionId))
                    SendUpdate(connectionId);
            }
            else
            {
                foreach (long connectionId in Manager.GetConnectionIds())
                {
                    if (Behaviour.Identity.IsSubscribedOrOwning(connectionId))
                        SendUpdate(connectionId);
                }
            }
        }

        internal override sealed void SendUpdate(long connectionId)
        {
            SendUpdate(connectionId, deliveryMethod);
        }

        internal override sealed void SendUpdate(long connectionId, DeliveryMethod deliveryMethod)
        {
            if (!ValidateBeforeAccess())
                return;

            Manager.ServerSendPacket(connectionId, deliveryMethod, LiteNetLibGameManager.GameMsgTypes.ServerUpdateSyncField, SerializeForSend);
        }

        protected void SerializeForSend(NetDataWriter writer)
        {
            LiteNetLibElementInfo.SerializeInfo(GetInfo(), writer);
            Serialize(writer);
        }

        internal override sealed void Deserialize(NetDataReader reader)
        {
            DeserializeValue(reader);
            if (onChange != null)
                onChange.Invoke(value);
        }

        internal override sealed void Serialize(NetDataWriter writer)
        {
            SerializeValue(writer);
        }

        protected virtual void DeserializeValue(NetDataReader reader)
        {
            value = (TType)reader.GetValue(typeof(TType));
        }

        protected virtual void SerializeValue(NetDataWriter writer)
        {
            writer.PutValue(value);
        }
    }

    #region Implement for general usages and serializable
    [Serializable]
    public class SyncFieldBool : LiteNetLibSyncField<bool>
    {
    }

    [Serializable]
    public class SyncFieldBoolArray : LiteNetLibSyncField<bool[]>
    {
    }

    [Serializable]
    public class SyncFieldByte : LiteNetLibSyncField<byte>
    {
    }

    [Serializable]
    public class SyncFieldByteArray : LiteNetLibSyncField<byte[]>
    {
    }

    [Serializable]
    public class SyncFieldChar : LiteNetLibSyncField<char>
    {
    }

    [Serializable]
    public class SyncFieldDouble : LiteNetLibSyncField<double>
    {
    }

    [Serializable]
    public class SyncFieldDoubleArray : LiteNetLibSyncField<double[]>
    {
    }

    [Serializable]
    public class SyncFieldFloat : LiteNetLibSyncField<float>
    {
    }

    [Serializable]
    public class SyncFieldFloatArray : LiteNetLibSyncField<float[]>
    {
    }

    [Serializable]
    public class SyncFieldInt : LiteNetLibSyncField<int>
    {
    }

    [Serializable]
    public class SyncFieldIntArray : LiteNetLibSyncField<int[]>
    {
    }

    [Serializable]
    public class SyncFieldLong : LiteNetLibSyncField<long>
    {
    }

    [Serializable]
    public class SyncFieldLongArray : LiteNetLibSyncField<long[]>
    {
    }

    [Serializable]
    public class SyncFieldSByte : LiteNetLibSyncField<sbyte>
    {
    }

    [Serializable]
    public class SyncFieldShort : LiteNetLibSyncField<short>
    {
    }

    [Serializable]
    public class SyncFieldShortArray : LiteNetLibSyncField<short[]>
    {
    }

    [Serializable]
    public class SyncFieldString : LiteNetLibSyncField<string>
    {
    }

    [Serializable]
    public class SyncFieldUInt : LiteNetLibSyncField<uint>
    {
    }

    [Serializable]
    public class SyncFieldUIntArray : LiteNetLibSyncField<uint[]>
    {
    }

    [Serializable]
    public class SyncFieldULong : LiteNetLibSyncField<ulong>
    {
    }

    [Serializable]
    public class SyncFieldULongArray : LiteNetLibSyncField<ulong[]>
    {
    }

    [Serializable]
    public class SyncFieldUShort : LiteNetLibSyncField<ushort>
    {
    }

    [Serializable]
    public class SyncFieldUShortArray : LiteNetLibSyncField<ushort[]>
    {
    }

    [Serializable]
    public class SyncFieldColor : LiteNetLibSyncField<Color>
    {
    }

    [Serializable]
    public class SyncFieldQuaternion : LiteNetLibSyncField<Quaternion>
    {
    }

    [Serializable]
    public class SyncFieldVector2 : LiteNetLibSyncField<Vector2>
    {
    }

    [Serializable]
    public class SyncFieldVector2Int : LiteNetLibSyncField<Vector2Int>
    {
    }

    [Serializable]
    public class SyncFieldVector3 : LiteNetLibSyncField<Vector3>
    {
    }

    [Serializable]
    public class SyncFieldVector3Int : LiteNetLibSyncField<Vector3Int>
    {
    }

    [Serializable]
    public class SyncFieldVector4 : LiteNetLibSyncField<Vector4>
    {
    }

    [Serializable]
    public class SyncFieldPackedUShort : LiteNetLibSyncField<PackedUShort>
    {
    }

    [Serializable]
    public class SyncFieldPackedUInt : LiteNetLibSyncField<PackedUInt>
    {
    }

    [Serializable]
    public class SyncFieldPackedULong : LiteNetLibSyncField<PackedULong>
    {
    }
    #endregion
}
