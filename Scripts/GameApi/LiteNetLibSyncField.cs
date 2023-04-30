using System;
using UnityEngine;
using LiteNetLib;
using LiteNetLib.Utils;
using System.Reflection;

namespace LiteNetLibManager
{
    public abstract class LiteNetLibSyncField : LiteNetLibElement
    {
        protected readonly static NetDataWriter ServerWriter = new NetDataWriter();
        protected readonly static NetDataWriter ClientWriter = new NetDataWriter();

        public enum SyncMode : byte
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

        [Flags]
        public enum SyncBehaviour : byte
        {
            Default = 0,
            AlwaysSync = 1 << 0,
            DoNotSyncInitialDataImmediately = 1 << 1,
            DoNotSyncUpdate = 1 << 2,
        }

        [Header("Generic Settings")]
        [Tooltip("How it will sync data. If always sync, it will sync data although it has no changes. If don't sync initial data immediately, it will not sync initial data immdediately with networked object spawning message, it will sync later after spanwed. If don't sync update, it will not sync when data has changes.")]
        public SyncBehaviour syncBehaviour;
        [Tooltip("Who can sync data and sync to whom")]
        public SyncMode syncMode;
        [Tooltip("Interval to send networking data")]
        [Range(0.01f, 2f)]
        public float sendInterval = 0.1f;

        [Header("Server Sync Settings")]
        [Tooltip("Sending data channel from server to clients")]
        public byte dataChannel = 0;
        [Tooltip("Sending method type from server to clients, default is `Sequenced`")]
        public DeliveryMethod deliveryMethod = DeliveryMethod.Sequenced;

        [Header("Client Sync Settings (`syncMode` is `ClientMulticast` only)")]
        [Tooltip("Sending data channel from clients to server (`syncMode` is `ClientMulticast` only)")]
        public byte clientDataChannel = 0;
        [Tooltip("Sending method type from clients to server (`syncMode` is `ClientMulticast` only), default is `Sequenced`")]
        public DeliveryMethod clientDeliveryMethod = DeliveryMethod.Sequenced;

        private float nextSyncTime;
        private bool onChangeCalled;
        protected object defaultValue;

        public abstract Type GetFieldType();
        public abstract object GetValue();
        public abstract void SetValue(object value);
        internal abstract void OnChange(bool initial);
        internal abstract bool HasUpdate();
        internal abstract void Updated();

        public bool HasSyncBehaviourFlag(SyncBehaviour flag)
        {
            return (syncBehaviour & flag) == flag;
        }

        internal void Reset()
        {
            SetValue(defaultValue);
        }

        internal override sealed void Setup(LiteNetLibBehaviour behaviour, int elementId)
        {
            base.Setup(behaviour, elementId);
            defaultValue = GetValue();
            // Invoke on change function with initial state = true
            switch (syncMode)
            {
                case SyncMode.ServerToClients:
                case SyncMode.ServerToOwnerClient:
                    if (IsServer)
                        OnChange(true);
                    break;
                case SyncMode.ClientMulticast:
                    if (IsOwnerClient)
                        OnChange(true);
                    break;
            }
        }

        internal void NetworkUpdate(float currentTime)
        {
            if (!CanSync())
                return;

            // Won't update
            if (HasSyncBehaviourFlag(SyncBehaviour.DoNotSyncUpdate))
                return;

            // No update
            if (!HasSyncBehaviourFlag(SyncBehaviour.AlwaysSync) && !HasUpdate())
                return;

            // Call `OnChange` if it's not called yet.
            if ((HasSyncBehaviourFlag(SyncBehaviour.AlwaysSync) || HasUpdate()) && !onChangeCalled)
            {
                // Invoke on change function with initial state = false
                switch (syncMode)
                {
                    case SyncMode.ServerToClients:
                    case SyncMode.ServerToOwnerClient:
                        if (IsServer)
                            OnChange(false);
                        break;
                    case SyncMode.ClientMulticast:
                        if (IsOwnerClient)
                            OnChange(false);
                        break;
                }
                onChangeCalled = true;
            }

            // Is it time to sync?
            if (currentTime < nextSyncTime)
                return;

            // Set next sync time
            nextSyncTime = currentTime + sendInterval;

            // Send the update
            SendUpdate(false);

            // Update already sent
            Updated();

            // Reset on change called state to call `OnChange` later when has update
            onChangeCalled = false;
        }

        public void UpdateImmediately()
        {
            float currentTime = Time.fixedTime;
            nextSyncTime = currentTime;
            NetworkUpdate(currentTime);
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
            SetValue(reader.GetValue(GetFieldType()));
        }

        internal virtual void SerializeValue(NetDataWriter writer)
        {
            writer.PutValue(GetFieldType(), GetValue());
        }

        internal void SendUpdate(bool isInitial, long connectionId)
        {
            if (!CanSync() || !IsServer)
                return;
            LiteNetLibGameManager manager = Manager;
            LiteNetLibServer server = manager.Server;
            TransportHandler.WritePacket(ServerWriter, isInitial ? GameMsgTypes.InitialSyncField : GameMsgTypes.UpdateSyncField);
            SerializeForSend(ServerWriter);
            if (manager.ContainsConnectionId(connectionId))
                server.SendMessage(connectionId, dataChannel, deliveryMethod, ServerWriter);
        }

        internal void SendUpdate(bool isInitial)
        {
            if (!CanSync())
            {
                Logging.LogError(LogTag, "Error while set value, behaviour is empty");
                return;
            }
            LiteNetLibGameManager manager = Manager;
            LiteNetLibServer server = manager.Server;
            LiteNetLibClient client = manager.Client;
            switch (syncMode)
            {
                case SyncMode.ServerToClients:
                    TransportHandler.WritePacket(ServerWriter, isInitial ? GameMsgTypes.InitialSyncField : GameMsgTypes.UpdateSyncField);
                    SerializeForSend(ServerWriter);
                    foreach (long connectionId in manager.GetConnectionIds())
                    {
                        if (Identity.HasSubscriberOrIsOwning(connectionId))
                            server.SendMessage(connectionId, dataChannel, deliveryMethod, ServerWriter);
                    }
                    break;
                case SyncMode.ServerToOwnerClient:
                    if (manager.ContainsConnectionId(ConnectionId))
                    {
                        TransportHandler.WritePacket(ServerWriter, isInitial ? GameMsgTypes.InitialSyncField : GameMsgTypes.UpdateSyncField);
                        SerializeForSend(ServerWriter);
                        server.SendMessage(ConnectionId, dataChannel, deliveryMethod, ServerWriter);
                    }
                    break;
                case SyncMode.ClientMulticast:
                    if (IsOwnerClient)
                    {
                        TransportHandler.WritePacket(ClientWriter, isInitial ? GameMsgTypes.InitialSyncField : GameMsgTypes.UpdateSyncField);
                        SerializeForSend(ClientWriter);
                        // Client send data to server, then server send to other clients, it should be reliable-ordered
                        client.SendMessage(clientDataChannel, clientDeliveryMethod, ClientWriter);
                    }
                    else if (IsServer)
                    {
                        TransportHandler.WritePacket(ServerWriter, isInitial ? GameMsgTypes.InitialSyncField : GameMsgTypes.UpdateSyncField);
                        SerializeForSend(ServerWriter);
                        foreach (long connectionId in manager.GetConnectionIds())
                        {
                            if (Identity.HasSubscriberOrIsOwning(connectionId))
                                server.SendMessage(connectionId, dataChannel, deliveryMethod, ServerWriter);
                        }
                    }
                    break;
            }
        }

        private void SerializeForSend(NetDataWriter writer)
        {
            LiteNetLibElementInfo.SerializeInfo(GetInfo(), writer);
            Serialize(writer);
        }

        public override string ToString()
        {
            return GetValue().ToString();
        }
    }

    public class LiteNetLibSyncFieldContainer : LiteNetLibSyncField
    {
        /// <summary>
        /// The field which going to sync its value
        /// </summary>
        private FieldInfo field;

        /// <summary>
        /// The class which contain the field
        /// </summary>
        private object instance;

        /// <summary>
        /// Use this variable to tell that it has to update after value changed
        /// </summary>
        private bool hasUpdate;

        /// <summary>
        /// This method will be invoked after value changed
        /// </summary>
        private MethodInfo onChangeMethod;

        /// <summary>
        /// This method will be invoked after data sent
        /// </summary>
        private MethodInfo onUpdateMethod;

        /// <summary>
        /// Use this value to check field's value changes
        /// </summary>
        private object value;

        public LiteNetLibSyncFieldContainer(FieldInfo field, object instance, MethodInfo onChangeMethod, MethodInfo onUpdateMethod)
        {
            this.field = field;
            this.instance = instance;
            this.onChangeMethod = onChangeMethod;
            this.onUpdateMethod = onUpdateMethod;
            value = field.GetValue(instance);
        }

        public override sealed Type GetFieldType()
        {
            return field.FieldType;
        }

        public override sealed object GetValue()
        {
            return field.GetValue(instance);
        }

        public override sealed void SetValue(object value)
        {
            field.SetValue(instance, value);
        }

        internal override sealed bool HasUpdate()
        {
            if (hasUpdate)
                return true;

            if (value == null || !value.Equals(field.GetValue(instance)))
            {
                // Set value because it's going to sync later
                value = field.GetValue(instance);
                // Set `hasUpdate` to `TRUE` this value will turn to `FALSE` when `Updated()` called
                hasUpdate = true;
            }

            return hasUpdate;
        }

        internal override void Updated()
        {
            if (onUpdateMethod != null)
                onUpdateMethod.Invoke(instance, new object[0]);
            hasUpdate = false;
        }

        internal override sealed void OnChange(bool initial)
        {
            if (onChangeMethod != null)
                onChangeMethod.Invoke(instance, new object[] { GetValue() });
        }
    }

    public class LiteNetLibSyncField<TType> : LiteNetLibSyncField
    {
        public delegate void OnChangeDelegate(bool initial, TType value);
        public delegate void OnUpdatedDelegate();
        /// <summary>
        /// Action with initial state and value, this will be invoked after data changed
        /// </summary>
        public OnChangeDelegate onChange;

        /// <summary>
        /// This will be invoked after data sent
        /// </summary>
        public OnUpdatedDelegate onUpdated;

        /// <summary>
        /// Use this variable to tell that it has to update after value changed
        /// </summary>
        protected bool hasUpdate;

        [SerializeField]
        protected TType value;
        public TType Value
        {
            get { return value; }
            set
            {
                if (IsValueChanged(value))
                {
                    this.value = value;
                    if (CanSync())
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
            if (onUpdated != null)
                onUpdated.Invoke();
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
            if (onChange != null)
                onChange.Invoke(initial, Value);
        }

        public static implicit operator TType(LiteNetLibSyncField<TType> field)
        {
            return field.Value;
        }
    }

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

        internal override void DeserializeValue(NetDataReader reader)
        {
            Value = reader.GetArray<TType>();
        }

        internal override void SerializeValue(NetDataWriter writer)
        {
            writer.PutArray(Value);
        }

        public int Length { get { return Value.Length; } }
    }

    #region Implement for general usages and serializable
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
        [Tooltip("If angle between new value and old value >= this value, it will be determined that the value is changing")]
        public float valueChangeAngle = 1f;

        protected override bool IsValueChanged(Quaternion newValue)
        {
            return Quaternion.Angle(value, newValue) >= valueChangeAngle;
        }
    }

    [Serializable]
    public class SyncFieldVector2 : LiteNetLibSyncField<Vector2>
    {
        [Tooltip("If distance between new value and old value >= this value, it will be determined that the value is changing")]
        public float valueChangeDistance = 0.01f;

        protected override bool IsValueChanged(Vector2 newValue)
        {
            return Vector2.Distance(value, newValue) >= valueChangeDistance;
        }
    }

    [Serializable]
    public class SyncFieldVector2Int : LiteNetLibSyncField<Vector2Int>
    {
        [Tooltip("If distance between new value and old value >= this value, it will be determined that the value is changing")]
        public float valueChangeDistance = 0.01f;

        protected override bool IsValueChanged(Vector2Int newValue)
        {
            return Vector2Int.Distance(value, newValue) >= valueChangeDistance;
        }
    }

    [Serializable]
    public class SyncFieldVector3 : LiteNetLibSyncField<Vector3>
    {
        [Tooltip("If distance between new value and old value >= this value, it will be determined that the value is changing")]
        public float valueChangeDistance = 0.01f;

        protected override bool IsValueChanged(Vector3 newValue)
        {
            return Vector3.Distance(value, newValue) >= valueChangeDistance;
        }
    }

    [Serializable]
    public class SyncFieldVector3Int : LiteNetLibSyncField<Vector3Int>
    {
        [Tooltip("If distance between new value and old value >= this value, it will be determined that the value is changing")]
        public float valueChangeDistance = 0.01f;

        protected override bool IsValueChanged(Vector3Int newValue)
        {
            return Vector3Int.Distance(value, newValue) >= valueChangeDistance;
        }
    }

    [Serializable]
    public class SyncFieldVector4 : LiteNetLibSyncField<Vector4>
    {
        [Tooltip("If distance between new value and old value >= this value, it will be determined that the value is changing")]
        public float valueChangeDistance = 0.01f;

        protected override bool IsValueChanged(Vector4 newValue)
        {
            return Vector4.Distance(value, newValue) >= valueChangeDistance;
        }
    }

    [Serializable]
    [Obsolete("SyncField<Int,Short,Long,UInt,UShort,ULong> already packed. So you don't have to use this class")]
    public class SyncFieldPackedUShort : LiteNetLibSyncField<PackedUShort>
    {
    }

    [Serializable]
    [Obsolete("SyncField<Int,Short,Long,UInt,UShort,ULong> already packed. So you don't have to use this class")]
    public class SyncFieldPackedUInt : LiteNetLibSyncField<PackedUInt>
    {
    }

    [Serializable]
    [Obsolete("SyncField<Int,Short,Long,UInt,UShort,ULong> already packed. So you don't have to use this class")]
    public class SyncFieldPackedULong : LiteNetLibSyncField<PackedULong>
    {
    }

    [Serializable]
    [Obsolete("SyncField<Int,Short,Long,UInt,UShort,ULong> already packed. So you don't have to use this class")]
    public class SyncFieldPackedShort : LiteNetLibSyncField<PackedShort>
    {
    }

    [Serializable]
    [Obsolete("SyncField<Int,Short,Long,UInt,UShort,ULong> already packed. So you don't have to use this class")]
    public class SyncFieldPackedInt : LiteNetLibSyncField<PackedInt>
    {
    }

    [Serializable]
    [Obsolete("SyncField<Int,Short,Long,UInt,UShort,ULong> already packed. So you don't have to use this class")]
    public class SyncFieldPackedLong : LiteNetLibSyncField<PackedLong>
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
