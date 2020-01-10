using System;
using UnityEngine;
using LiteNetLib;
using LiteNetLib.Utils;
using System.Reflection;

namespace LiteNetLibManager
{
    public enum SyncFieldMode : byte
    {
        /// <summary>
        /// Changes handle by server
        /// Will send to connected clients when changes occurs on server
        /// </summary>
        ServerToClients,
        /// <summary>
        /// Changes handle by server
        /// Will send to owner-client when changes occurs on server
        /// </summary>
        ServerToOwnerClient,
        /// <summary>
        /// Changes handle by owner-client
        /// Will send to server then server multicast to other clients when changes occurs on owner-client
        /// </summary>
        ClientMulticast
    }

    public abstract class LiteNetLibSyncField : LiteNetLibElement
    {
        [Tooltip("Sending method type")]
        public DeliveryMethod deliveryMethod;
        [Tooltip("Interval to send network data")]
        [Range(0.01f, 2f)]
        public float sendInterval = 0.1f;
        [Tooltip("If this is `TRUE` it will syncing although no changes")]
        public bool alwaysSync;
        [Tooltip("If this is `TRUE` it will not sync initial data immdediately with spawn message (it will sync later)")]
        public bool doNotSyncInitialDataImmediately;
        [Tooltip("How data changes handle and sync")]
        public SyncFieldMode syncMode;
        protected float lastSentTime;

        private bool checkedAbleToSetElement;
        private bool isAbleToSetElement;
        protected bool IsAbleToSetElement
        {
            get
            {
                if (!checkedAbleToSetElement)
                {
                    checkedAbleToSetElement = true;
                    isAbleToSetElement = typeof(INetSerializableWithElement).IsAssignableFrom(GetFieldType());
                }
                return isAbleToSetElement;
            }
        }

        private bool onChangeCalled;

        internal void NetworkUpdate(float time)
        {
            if (!ValidateBeforeAccess())
                return;

            // No update
            if (!alwaysSync && !HasUpdate())
                return;

            // Call `OnChange` if it's not called yet.
            if (HasUpdate() && !onChangeCalled)
            {
                OnChange(false);
                onChangeCalled = true;
            }

            // It's time to send update?
            if (time - lastSentTime < sendInterval)
                return;

            // Set last send update time
            lastSentTime = time;

            // Send the update
            SendUpdate(false);

            // Update already sent
            Updated();

            // Reset on change called state to call `OnChange` later when has update
            onChangeCalled = false;
        }

        public abstract Type GetFieldType();
        public abstract object GetValue();
        public abstract void SetValue(object value);
        internal abstract void OnChange(bool initial);
        internal abstract bool HasUpdate();
        internal abstract void Updated();

        internal override sealed void Setup(LiteNetLibBehaviour behaviour, int elementId)
        {
            base.Setup(behaviour, elementId);
            OnChange(true);
        }

        internal void Deserialize(NetDataReader reader, bool isInitial)
        {
            DeserializeValue(reader);
            OnChange(isInitial);
        }

        internal void Serialize(NetDataWriter writer)
        {
            SerializeValue(writer);
        }

        internal virtual void DeserializeValue(NetDataReader reader)
        {
            if (IsAbleToSetElement)
            {
                object instance = Activator.CreateInstance(GetFieldType());
                (instance as INetSerializableWithElement).Element = this;
                (instance as INetSerializableWithElement).Deserialize(reader);
                SetValue(instance);
                return;
            }
            SetValue(reader.GetValue(GetFieldType()));
        }

        internal virtual void SerializeValue(NetDataWriter writer)
        {
            if (IsAbleToSetElement)
                (GetValue() as INetSerializableWithElement).Element = this;
            writer.PutValue(GetValue());
        }

        internal void SendUpdate(bool isInitial)
        {
            if (!ValidateBeforeAccess())
            {
                Debug.LogError("[LiteNetLibSyncField] Error while set value, behaviour is empty");
                return;
            }

            switch (syncMode)
            {
                case SyncFieldMode.ServerToClients:
                    foreach (long connectionId in Manager.GetConnectionIds())
                    {
                        if (Identity.IsSubscribedOrOwning(connectionId))
                            SendUpdate(isInitial, connectionId);
                    }
                    break;
                case SyncFieldMode.ServerToOwnerClient:
                    if (Manager.ContainsConnectionId(ConnectionId))
                        SendUpdate(isInitial, ConnectionId);
                    break;
                case SyncFieldMode.ClientMulticast:
                    if (IsOwnerClient)
                    {
                        // Client send data to server, it should reliable-ordered
                        Manager.ClientSendPacket(DeliveryMethod.ReliableOrdered,
                            (isInitial ?
                            LiteNetLibGameManager.GameMsgTypes.InitialSyncField :
                            LiteNetLibGameManager.GameMsgTypes.UpdateSyncField),
                            SerializeForSend);
                    }
                    break;
            }
        }

        internal void SendUpdate(bool isInitial, long connectionId)
        {
            SendUpdate(isInitial, connectionId, deliveryMethod);
        }

        internal void SendUpdate(bool isInitial, long connectionId, DeliveryMethod deliveryMethod)
        {
            if (!ValidateBeforeAccess() || !IsServer)
                return;

            SendingConnectionId = connectionId;
            Manager.ServerSendPacket(connectionId, deliveryMethod,
                (isInitial ?
                LiteNetLibGameManager.GameMsgTypes.InitialSyncField :
                LiteNetLibGameManager.GameMsgTypes.UpdateSyncField),
                SerializeForSend);
        }

        private void SerializeForSend(NetDataWriter writer)
        {
            LiteNetLibElementInfo.SerializeInfo(GetInfo(), writer);
            Serialize(writer);
        }

        public static bool TypeCanBeSyncField(Type type)
        {
            // TODO: Implement this
            return true;
        }
    }

    public class LiteNetLibSyncFieldContainer : LiteNetLibSyncField
    {
        private FieldInfo field;
        private object instance;
        private object value;

        public LiteNetLibSyncFieldContainer(FieldInfo field, object instance)
        {
            this.field = field;
            this.instance = instance;
            this.value = field.GetValue(instance);
        }

        public override sealed Type GetFieldType()
        {
            return field.FieldType;
        }

        public override sealed object GetValue()
        {
            return value;
        }

        public override sealed void SetValue(object value)
        {
            this.value = value;
        }

        internal override sealed bool HasUpdate()
        {
            return value != field.GetValue(instance);
        }

        internal override void Updated()
        {
            value = field.GetValue(instance);
        }

        internal override sealed void OnChange(bool initial)
        {
            // TODO: Change field value then call hook function

        }
    }
    
    public class LiteNetLibSyncField<TType> : LiteNetLibSyncField
    {
        /// <summary>
        /// Action for initial state, data this will be invoked when data changes
        /// </summary>
        public Action<bool, TType> onChange;

        /// <summary>
        /// Use this variable to tell that there is update after value changed
        /// </summary>
        protected bool hasUpdate;

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
                }
            }
        }

        protected virtual bool IsValueChanged(TType newValue)
        {
            return value == null || !value.Equals(newValue);
        }

        internal override bool HasUpdate()
        {
            return hasUpdate;
        }

        internal override void Updated()
        {
            hasUpdate = false;
        }

        public override sealed Type GetFieldType()
        {
            return typeof(TType);
        }

        public override sealed object GetValue()
        {
            return Value;
        }

        public override sealed void SetValue(object value)
        {
            Value = (TType)value;
        }

        internal override sealed void OnChange(bool initial)
        {
            onChange.Invoke(initial, Value);
        }

        public static implicit operator TType(LiteNetLibSyncField<TType> field)
        {
            return field.Value;
        }
    }

    #region Implement for general usages and serializable
    [Serializable]
    public class SyncFieldArray<TType> : LiteNetLibSyncField<TType[]>
    {
        public TType this[int i]
        {
            get { return Value[i]; }
            set
            {
                Value[i] = value;
                hasUpdate = true;
            }
        }

        public int Length { get { return Value.Length; } }
    }

    [Serializable]
    public class SyncFieldBool : LiteNetLibSyncField<bool>
    {
    }

    [Serializable]
    public class SyncFieldBoolArray : SyncFieldArray<bool>
    {
    }

    [Serializable]
    public class SyncFieldByte : LiteNetLibSyncField<byte>
    {
    }

    [Serializable]
    public class SyncFieldByteArray : SyncFieldArray<byte>
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
    public class SyncFieldDoubleArray : SyncFieldArray<double>
    {
    }

    [Serializable]
    public class SyncFieldFloat : LiteNetLibSyncField<float>
    {
    }

    [Serializable]
    public class SyncFieldFloatArray : SyncFieldArray<float>
    {
    }

    [Serializable]
    public class SyncFieldInt : LiteNetLibSyncField<int>
    {
    }

    [Serializable]
    public class SyncFieldIntArray : SyncFieldArray<int>
    {
    }

    [Serializable]
    public class SyncFieldLong : LiteNetLibSyncField<long>
    {
    }

    [Serializable]
    public class SyncFieldLongArray : SyncFieldArray<long>
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
    public class SyncFieldShortArray : SyncFieldArray<short>
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
    public class SyncFieldUIntArray : SyncFieldArray<uint>
    {
    }

    [Serializable]
    public class SyncFieldULong : LiteNetLibSyncField<ulong>
    {
    }

    [Serializable]
    public class SyncFieldULongArray : SyncFieldArray<ulong>
    {
    }

    [Serializable]
    public class SyncFieldUShort : LiteNetLibSyncField<ushort>
    {
    }

    [Serializable]
    public class SyncFieldUShortArray : SyncFieldArray<ushort>
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

    [Serializable]
    public class SyncFieldDirectionVector2 : LiteNetLibSyncField<DirectionVector2>
    {
        protected override bool IsValueChanged(DirectionVector2 newValue)
        {
            return Value.x != newValue.x || Value.y != newValue.y;
        }
    }

    [Serializable]
    public class SyncFieldDirectionVector3 : LiteNetLibSyncField<DirectionVector3>
    {
        protected override bool IsValueChanged(DirectionVector3 newValue)
        {
            return Value.x != newValue.x || Value.y != newValue.y || Value.z != newValue.z;
        }
    }
    #endregion
}
