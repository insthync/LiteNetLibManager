using System;
using UnityEngine;
using LiteNetLib;
using LiteNetLib.Utils;
using System.Reflection;

namespace LiteNetLibManager
{
    public abstract class LiteNetLibSyncField : LiteNetLibSyncElement
    {
        protected readonly static NetDataWriter s_ServerWriter = new NetDataWriter();
        protected readonly static NetDataWriter s_ClientWriter = new NetDataWriter();

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
        [Tooltip("Sending data channel from client to server (`syncMode` is `ClientMulticast` only)")]
        public byte clientDataChannel = 0;
        [Tooltip("Sending method type from client to server (`syncMode` is `ClientMulticast` only), default is `Sequenced`")]
        public DeliveryMethod clientDeliveryMethod = DeliveryMethod.Sequenced;

        protected float _nextSyncTime;
        protected object _defaultValue;

        public abstract Type GetFieldType();
        protected abstract object GetValue();
        protected abstract void SetValue(object value);
        internal abstract void OnChange(bool initial, object oldValue, object newValue);
        internal abstract bool HasUpdate();
        internal abstract void Updated();

        protected override bool CanSync()
        {
            switch (syncMode)
            {
                case SyncMode.ServerToClients:
                    return IsServer;
                case SyncMode.ServerToOwnerClient:
                    return IsServer;
                case SyncMode.ClientMulticast:
                    return IsOwnerClient || IsServer;
            }
            return false;
        }

        public bool HasSyncBehaviourFlag(SyncBehaviour flag)
        {
            return (syncBehaviour & flag) == flag;
        }

        internal void Reset()
        {
            SetValue(_defaultValue);
        }

        internal override sealed void Setup(LiteNetLibBehaviour behaviour, int elementId)
        {
            base.Setup(behaviour, elementId);
            _defaultValue = GetValue();
            // Invoke on change function with initial state = true
            switch (syncMode)
            {
                case SyncMode.ServerToClients:
                case SyncMode.ServerToOwnerClient:
                    if (IsServer)
                        OnChange(true, _defaultValue, _defaultValue);
                    break;
                case SyncMode.ClientMulticast:
                    if (IsOwnerClient || IsServer)
                        OnChange(true, _defaultValue, _defaultValue);
                    break;
            }
            RegisterUpdating();
        }

        public void RegisterUpdating()
        {
            if (!IsSetup)
                return;
            Manager?.RegisterServerSyncElement(this);
        }

        public void UnregisterUpdating()
        {
            Manager?.UnregisterServerSyncElement(this);
        }

        /// <summary>
        /// Return `TRUE` to determine that the update is done and unregister updating
        /// </summary>
        /// <param name="currentTime"></param>
        /// <returns></returns>
        internal virtual bool NetworkUpdate(float currentTime)
        {
            if (!CanSync())
                return false;

            // Won't update
            if (HasSyncBehaviourFlag(SyncBehaviour.DoNotSyncUpdate))
                return true;

            // No update
            if (!HasSyncBehaviourFlag(SyncBehaviour.AlwaysSync) && !HasUpdate())
                return true;

            // Is it time to sync?
            if (currentTime < _nextSyncTime)
                return false;

            // Set next sync time
            _nextSyncTime = currentTime + sendInterval;

            // Send the update
            SendUpdate(false);

            // Update already sent
            Updated();

            // Keep update next frame if it is `always sync`
            return !HasSyncBehaviourFlag(SyncBehaviour.AlwaysSync);
        }

        public void UpdateImmediately()
        {
            float currentTime = Time.unscaledTime;
            _nextSyncTime = currentTime;
            NetworkUpdate(currentTime);
        }

        // TODO: Remove this
        internal void Deserialize(NetDataReader reader, bool isInitial)
        {
            object oldValue = GetValue();
            DeserializeValue(reader);
            OnChange(isInitial, oldValue, GetValue());
        }

        // TODO: Remove this
        internal void Serialize(NetDataWriter writer)
        {
            SerializeValue(writer);
        }

        // TODO: Keep this
        internal override void WriteSyncData(NetDataWriter writer, uint tick)
        {
            SerializeValue(writer);
        }

        // TODO: Keep this
        internal override void ReadSyncData(NetDataReader reader, uint tick)
        {
            object oldValue = GetValue();
            DeserializeValue(reader);
            OnChange(false, oldValue, GetValue());
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
            if (!CanSync())
                return;
            LiteNetLibGameManager manager = Manager;
            LiteNetLibServer server = manager.Server;
            TransportHandler.WritePacket(s_ServerWriter, isInitial ? GameMsgTypes.InitialSyncField : GameMsgTypes.UpdateSyncField);
            SerializeForSend(s_ServerWriter);
            if (manager.ContainsConnectionId(connectionId))
                server.SendMessage(connectionId, dataChannel, deliveryMethod, s_ServerWriter);
        }

        internal void SendUpdate(bool isInitial)
        {
            if (!CanSync())
                return;
            LiteNetLibGameManager manager = Manager;
            LiteNetLibServer server = manager.Server;
            LiteNetLibClient client = manager.Client;
            switch (syncMode)
            {
                case SyncMode.ServerToClients:
                    TransportHandler.WritePacket(s_ServerWriter, isInitial ? GameMsgTypes.InitialSyncField : GameMsgTypes.UpdateSyncField);
                    SerializeForSend(s_ServerWriter);
                    foreach (long connectionId in manager.GetConnectionIds())
                    {
                        if (Identity.HasSubscriberOrIsOwning(connectionId))
                            server.SendMessage(connectionId, dataChannel, deliveryMethod, s_ServerWriter);
                    }
                    break;
                case SyncMode.ServerToOwnerClient:
                    if (manager.ContainsConnectionId(ConnectionId))
                    {
                        TransportHandler.WritePacket(s_ServerWriter, isInitial ? GameMsgTypes.InitialSyncField : GameMsgTypes.UpdateSyncField);
                        SerializeForSend(s_ServerWriter);
                        server.SendMessage(ConnectionId, dataChannel, deliveryMethod, s_ServerWriter);
                    }
                    break;
                case SyncMode.ClientMulticast:
                    if (IsOwnerClient)
                    {
                        TransportHandler.WritePacket(s_ClientWriter, isInitial ? GameMsgTypes.InitialSyncField : GameMsgTypes.UpdateSyncField);
                        SerializeForSend(s_ClientWriter);
                        // Client send data to server, then server send to other clients, it should be reliable-ordered
                        client.SendMessage(clientDataChannel, clientDeliveryMethod, s_ClientWriter);
                    }
                    else if (IsServer)
                    {
                        TransportHandler.WritePacket(s_ServerWriter, isInitial ? GameMsgTypes.InitialSyncField : GameMsgTypes.UpdateSyncField);
                        SerializeForSend(s_ServerWriter);
                        foreach (long connectionId in manager.GetConnectionIds())
                        {
                            if (Identity.HasSubscriberOrIsOwning(connectionId))
                                server.SendMessage(connectionId, dataChannel, deliveryMethod, s_ServerWriter);
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

    // TODO: Remove this
    public class LiteNetLibSyncFieldContainer : LiteNetLibSyncField
    {
        /// <summary>
        /// The field which going to sync its value
        /// </summary>
        private FieldInfo _field;

        /// <summary>
        /// The class which contain the field
        /// </summary>
        private object _instance;

        /// <summary>
        /// Use this variable to tell that it has to update after value changed
        /// </summary>
        private bool _hasUpdate;

        /// <summary>
        /// This method will be invoked after value changed
        /// </summary>
        private MethodInfo _onChangeMethod;

        /// <summary>
        /// This method will be invoked after data sent
        /// </summary>
        private MethodInfo _onUpdateMethod;

        /// <summary>
        /// Use this value to check field's value changes
        /// </summary>
        private object _value;

        public LiteNetLibSyncFieldContainer(FieldInfo field, object instance, MethodInfo onChangeMethod, MethodInfo onUpdateMethod)
        {
            _field = field;
            _instance = instance;
            _onChangeMethod = onChangeMethod;
            _onUpdateMethod = onUpdateMethod;
            _value = field.GetValue(instance);
        }

        public override sealed Type GetFieldType()
        {
            return _field.FieldType;
        }

        protected override sealed object GetValue()
        {
            return _field.GetValue(_instance);
        }

        protected override sealed void SetValue(object value)
        {
            _field.SetValue(_instance, value);
        }

        internal override sealed bool NetworkUpdate(float currentTime)
        {
            if (CanSync() && currentTime > _nextSyncTime)
            {
                // Check for updating
                object fieldValue = _field.GetValue(_instance);
                if (_value == null || !_value.Equals(fieldValue))
                {
                    object oldValue = _value;
                    // Set value because it's going to sync later
                    _value = fieldValue;
                    // On changed
                    OnChange(false, oldValue, _value);
                    // Set `hasUpdate` to `TRUE` this value will turn to `FALSE` when `Updated()` called
                    _hasUpdate = true;
                }
            }
            base.NetworkUpdate(currentTime);
            // Always returns FALSE to keep updating
            return false;
        }

        internal override sealed bool HasUpdate()
        {
            return _hasUpdate;
        }

        internal override void Updated()
        {
            if (_onUpdateMethod != null)
                _onUpdateMethod.Invoke(_instance, new object[0]);
            _hasUpdate = false;
        }

        internal override sealed void OnChange(bool initial, object oldValue, object newValue)
        {
            if (_onChangeMethod != null)
                _onChangeMethod.Invoke(_instance, new object[] { initial, oldValue, newValue });
        }
    }

    public class LiteNetLibSyncField<TType> : LiteNetLibSyncField
    {
        public delegate void OnChangeDelegate(bool initial, TType oldValue, TType newValue);
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
        protected bool _hasUpdate;

        [SerializeField]
        protected TType _value;
        public TType Value
        {
            get { return _value; }
            set
            {
                if (IsSetup && !CanSync())
                {
                    switch (syncMode)
                    {
                        case SyncMode.ServerToClients:
                            Logging.LogError(LogTag, "Cannot access sync field from client.");
                            break;
                        case SyncMode.ServerToOwnerClient:
                            Logging.LogError(LogTag, "Cannot access sync field from client.");
                            break;
                        case SyncMode.ClientMulticast:
                            Logging.LogError(LogTag, "Cannot access sync field, client is not its owner.");
                            break;
                    }
                    return;
                }
                if (!IsValueChanged(value))
                {
                    // No changes
                    return;
                }
                TType oldValue = _value;
                _value = value;
                if (IsSetup)
                {
                    OnChange(false, oldValue, value);
                    _hasUpdate = true;
                    RegisterUpdating();
                }
            }
        }

        protected virtual bool IsValueChanged(TType newValue)
        {
            return _value == null || !_value.Equals(newValue);
        }

        internal override bool HasUpdate()
        {
            return _hasUpdate;
        }

        internal override void Updated()
        {
            if (onUpdated != null)
                onUpdated.Invoke();
            _hasUpdate = false;
        }

        public override sealed Type GetFieldType()
        {
            return typeof(TType);
        }

        protected override sealed object GetValue()
        {
            return _value;
        }

        protected override sealed void SetValue(object value)
        {
            _value = (TType)value;
        }

        internal override sealed void OnChange(bool initial, object oldValue, object newValue)
        {
            if (onChange != null)
                onChange.Invoke(initial, (TType)oldValue, (TType)newValue);
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
                if (IsSetup && !CanSync())
                {
                    switch (syncMode)
                    {
                        case SyncMode.ServerToClients:
                            Logging.LogError(LogTag, "Cannot access sync field from client.");
                            break;
                        case SyncMode.ServerToOwnerClient:
                            Logging.LogError(LogTag, "Cannot access sync field from client.");
                            break;
                        case SyncMode.ClientMulticast:
                            Logging.LogError(LogTag, "Cannot access sync field, client is not its owner.");
                            break;
                    }
                    return;
                }
                Value[i] = value;
                if (CanSync())
                {
                    _hasUpdate = true;
                    RegisterUpdating();
                }
            }
        }

        internal override void DeserializeValue(NetDataReader reader)
        {
            Value = reader.GetArrayExtension<TType>();
        }

        internal override void SerializeValue(NetDataWriter writer)
        {
            writer.PutArrayExtension(Value);
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
            return Quaternion.Angle(_value, newValue) >= valueChangeAngle;
        }
    }

    [Serializable]
    public class SyncFieldVector2 : LiteNetLibSyncField<Vector2>
    {
        [Tooltip("If distance between new value and old value >= this value, it will be determined that the value is changing")]
        public float valueChangeDistance = 0.01f;

        protected override bool IsValueChanged(Vector2 newValue)
        {
            return Vector2.Distance(_value, newValue) >= valueChangeDistance;
        }
    }

    [Serializable]
    public class SyncFieldVector2Int : LiteNetLibSyncField<Vector2Int>
    {
        [Tooltip("If distance between new value and old value >= this value, it will be determined that the value is changing")]
        public float valueChangeDistance = 0.01f;

        protected override bool IsValueChanged(Vector2Int newValue)
        {
            return Vector2Int.Distance(_value, newValue) >= valueChangeDistance;
        }
    }

    [Serializable]
    public class SyncFieldVector3 : LiteNetLibSyncField<Vector3>
    {
        [Tooltip("If distance between new value and old value >= this value, it will be determined that the value is changing")]
        public float valueChangeDistance = 0.01f;

        protected override bool IsValueChanged(Vector3 newValue)
        {
            return Vector3.Distance(_value, newValue) >= valueChangeDistance;
        }
    }

    [Serializable]
    public class SyncFieldVector3Int : LiteNetLibSyncField<Vector3Int>
    {
        [Tooltip("If distance between new value and old value >= this value, it will be determined that the value is changing")]
        public float valueChangeDistance = 0.01f;

        protected override bool IsValueChanged(Vector3Int newValue)
        {
            return Vector3Int.Distance(_value, newValue) >= valueChangeDistance;
        }
    }

    [Serializable]
    public class SyncFieldVector4 : LiteNetLibSyncField<Vector4>
    {
        [Tooltip("If distance between new value and old value >= this value, it will be determined that the value is changing")]
        public float valueChangeDistance = 0.01f;

        protected override bool IsValueChanged(Vector4 newValue)
        {
            return Vector4.Distance(_value, newValue) >= valueChangeDistance;
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

    [Serializable]
    public class SyncFieldHalfPrecision : LiteNetLibSyncField<HalfPrecision>
    {
        protected override bool IsValueChanged(HalfPrecision newValue)
        {
            return Value.halfValue != newValue.halfValue;
        }
    }

    [Serializable]
    public class SyncFieldHalfVector2 : LiteNetLibSyncField<HalfVector2>
    {
        protected override bool IsValueChanged(HalfVector2 newValue)
        {
            return Value.x != newValue.x || Value.y != newValue.y;
        }
    }

    [Serializable]
    public class SyncFieldHalfVector3 : LiteNetLibSyncField<HalfVector3>
    {
        protected override bool IsValueChanged(HalfVector3 newValue)
        {
            return Value.x != newValue.x || Value.y != newValue.y || Value.z != newValue.z;
        }
    }
    #endregion
}
