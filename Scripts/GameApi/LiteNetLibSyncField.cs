using LiteNetLib.Utils;
using System;
using UnityEngine;

namespace LiteNetLibManager
{
    public abstract class LiteNetLibSyncField : LiteNetLibSyncElement
    {
        public override byte ElementType => SyncElementTypes.SyncField;
        public LiteNetLibSyncFieldMode SyncMode { get; set; } = LiteNetLibSyncFieldMode.ServerToClients;
        public LiteNetLibSyncFieldStep SyncFieldStep { get; protected set; } = LiteNetLibSyncFieldStep.None;

        protected object _defaultValue;
        /// <summary>
        /// 0 - No syncing
        /// 1 - Change occurs, will sync unreliably
        /// 2 - Sent unreliably already, will sync packed state reliably
        /// </summary>
        protected byte _syncStep = 0;

        public abstract Type GetFieldType();
        protected abstract object GetValue();
        protected abstract void SetValue(object value);
        internal abstract void OnChange(bool initial, object oldValue, object newValue);

        protected bool CanSync(bool isServer, bool isOwnerClient)
        {
            switch (SyncMode)
            {
                case LiteNetLibSyncFieldMode.ServerToClients:
                    return isServer;
                case LiteNetLibSyncFieldMode.ServerToOwnerClient:
                    return isServer;
                case LiteNetLibSyncFieldMode.ClientMulticast:
                    return isOwnerClient || isServer;
            }
            return false;
        }

        protected bool CanSync()
        {
            return CanSync(IsServer, IsOwnerClient);
        }

        internal override bool CanSyncFromServer(LiteNetLibPlayer player)
        {
            return CanSync(IsServer, ConnectionId == player.ConnectionId) && base.CanSyncFromServer(player);
        }

        internal override bool CanSyncFromOwnerClient()
        {
            switch (SyncMode)
            {
                case LiteNetLibSyncFieldMode.ClientMulticast:
                    return IsOwnerClient || IsServer;
            }
            return false;
        }

        internal override bool WillSyncFromServerUnreliably(LiteNetLibPlayer player)
        {
            return SyncFieldStep == LiteNetLibSyncFieldStep.Syncing;
        }

        internal override bool WillSyncFromServerReliably(LiteNetLibPlayer player)
        {
            return SyncFieldStep == LiteNetLibSyncFieldStep.Confirming;
        }

        internal override bool WillSyncFromOwnerClientUnreliably()
        {
            return SyncFieldStep == LiteNetLibSyncFieldStep.Syncing;
        }

        internal override bool WillSyncFromOwnerClientReliably()
        {
            return SyncFieldStep == LiteNetLibSyncFieldStep.Confirming;
        }

        public override void Synced()
        {
            switch (SyncFieldStep)
            {
                case LiteNetLibSyncFieldStep.Syncing:
                    SyncFieldStep = LiteNetLibSyncFieldStep.Confirming;
                    break;
                case LiteNetLibSyncFieldStep.Confirming:
                    SyncFieldStep = LiteNetLibSyncFieldStep.None;
                    UnregisterUpdating();
                    break;
                default:
                    UnregisterUpdating();
                    break;
            }
        }

        internal override void Reset()
        {
            SetValue(_defaultValue);
        }

        internal override sealed void Setup(LiteNetLibBehaviour behaviour, int elementId)
        {
            base.Setup(behaviour, elementId);
            _defaultValue = GetValue();
            // Invoke on change function with initial state = true
            switch (SyncMode)
            {
                case LiteNetLibSyncFieldMode.ServerToClients:
                case LiteNetLibSyncFieldMode.ServerToOwnerClient:
                    if (IsServer)
                        OnChange(true, _defaultValue, _defaultValue);
                    break;
                case LiteNetLibSyncFieldMode.ClientMulticast:
                    if (IsOwnerClient || IsServer)
                        OnChange(true, _defaultValue, _defaultValue);
                    break;
            }
        }

        internal override void WriteSyncData(bool initial, NetDataWriter writer)
        {
            SerializeValue(writer);
        }

        internal override void ReadSyncData(bool initial, NetDataReader reader)
        {
            object oldValue = GetValue();
            DeserializeValue(reader);
            OnChange(initial, oldValue, GetValue());
            if (SyncMode == LiteNetLibSyncFieldMode.ClientMulticast && IsServer)
            {
                SyncFieldStep = LiteNetLibSyncFieldStep.Syncing;
                RegisterUpdating();
            }
        }

        internal virtual void DeserializeValue(NetDataReader reader)
        {
            SetValue(reader.GetValue(GetFieldType()));
        }

        internal virtual void SerializeValue(NetDataWriter writer)
        {
            writer.PutValue(GetFieldType(), GetValue());
        }

        public override string ToString()
        {
            return GetValue().ToString();
        }
    }

    public class LiteNetLibSyncField<TType> : LiteNetLibSyncField
    {
        public delegate void OnChangeDelegate(bool initial, TType oldValue, TType newValue);
        /// <summary>
        /// Action with initial state and value, this will be invoked after data changed
        /// </summary>
        public OnChangeDelegate onChange;

        protected TType _value;
        public TType Value
        {
            get { return _value; }
            set
            {
                bool canSync = CanSync();
                if (IsSetup && !canSync)
                {
                    switch (SyncMode)
                    {
                        case LiteNetLibSyncFieldMode.ServerToClients:
                            Logging.LogError(LogTag, "Cannot access sync field from client.");
                            break;
                        case LiteNetLibSyncFieldMode.ServerToOwnerClient:
                            Logging.LogError(LogTag, "Cannot access sync field from client.");
                            break;
                        case LiteNetLibSyncFieldMode.ClientMulticast:
                            Logging.LogError(LogTag, "Cannot access sync field, client is not its owner.");
                            break;
                    }
                    return;
                }
                if (!IsValueChanged(_value, value))
                {
                    // No changes
                    return;
                }
                TType oldValue = _value;
                _value = value;
                if (IsSetup && canSync)
                {
                    OnChange(false, oldValue, value);
                    SyncFieldStep = LiteNetLibSyncFieldStep.Syncing;
                    RegisterUpdating();
                }
            }
        }

        protected override bool IsValueChanged(object oldValue, object newValue)
        {
            return IsValueChanged((TType)oldValue, (TType)newValue);
        }

        protected virtual bool IsValueChanged(TType oldValue, TType newValue)
        {
            return oldValue == null || !oldValue.Equals(newValue);
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
    public class SyncFieldNetSerializableStruct<TType> : LiteNetLibSyncField<TType>
        where TType : struct, INetSerializable
    {
        internal override void DeserializeValue(NetDataReader reader)
        {
            _value = reader.Get<TType>();
        }

        internal override void SerializeValue(NetDataWriter writer)
        {
            writer.Put(_value);
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
                bool canSync = CanSync();
                if (IsSetup && !canSync)
                {
                    switch (SyncMode)
                    {
                        case LiteNetLibSyncFieldMode.ServerToClients:
                            Logging.LogError(LogTag, "Cannot access sync field from client.");
                            break;
                        case LiteNetLibSyncFieldMode.ServerToOwnerClient:
                            Logging.LogError(LogTag, "Cannot access sync field from client.");
                            break;
                        case LiteNetLibSyncFieldMode.ClientMulticast:
                            Logging.LogError(LogTag, "Cannot access sync field, client is not its owner.");
                            break;
                    }
                    return;
                }
                Value[i] = value;
                if (IsSetup && canSync)
                {
                    SyncFieldStep = LiteNetLibSyncFieldStep.Syncing;
                    RegisterUpdating();
                }
            }
        }

        internal sealed override void DeserializeValue(NetDataReader reader)
        {
            int count = reader.GetPackedInt();
            TType[] result = new TType[count];
            for (int i = 0; i < count; ++i)
            {
                result[i] = DeserializeElementValue(reader);
            }
            _value = result;
        }

        internal sealed override void SerializeValue(NetDataWriter writer)
        {
            writer.PutArrayExtension(_value);
            if (_value == null)
            {
                writer.Put(0);
                return;
            }
            writer.Put(_value.Length);
            foreach (TType element in _value)
            {
                SerializeElementValue(writer, element);
            }
        }

        internal virtual TType DeserializeElementValue(NetDataReader reader)
        {
            return reader.GetValue<TType>();
        }

        internal virtual void SerializeElementValue(NetDataWriter writer, TType element)
        {
            writer.PutValue(element);
        }

        public int Length { get { return Value.Length; } }
    }

    #region Implement for general usages and serializable
    [Serializable]
    public class SyncFieldBool : LiteNetLibSyncField<bool>
    {
        internal override void DeserializeValue(NetDataReader reader)
        {
            _value = reader.GetBool();
        }

        internal override void SerializeValue(NetDataWriter writer)
        {
            writer.Put(_value);
        }
    }

    [Serializable]
    public class SyncFieldBoolArray : SyncFieldArray<bool>
    {
        internal override bool DeserializeElementValue(NetDataReader reader)
        {
            return reader.GetBool();
        }

        internal override void SerializeElementValue(NetDataWriter writer, bool element)
        {
            writer.Put(element);
        }
    }

    [Serializable]
    public class SyncFieldByte : LiteNetLibSyncField<byte>
    {
        internal override void DeserializeValue(NetDataReader reader)
        {
            _value = reader.GetByte();
        }

        internal override void SerializeValue(NetDataWriter writer)
        {
            writer.Put(_value);
        }
    }

    [Serializable]
    public class SyncFieldByteArray : SyncFieldArray<byte>
    {
        internal override byte DeserializeElementValue(NetDataReader reader)
        {
            return reader.GetByte();
        }

        internal override void SerializeElementValue(NetDataWriter writer, byte element)
        {
            writer.Put(element);
        }
    }

    [Serializable]
    public class SyncFieldChar : LiteNetLibSyncField<char>
    {
        internal override void DeserializeValue(NetDataReader reader)
        {
            _value = reader.GetChar();
        }

        internal override void SerializeValue(NetDataWriter writer)
        {
            writer.Put(_value);
        }
    }

    [Serializable]
    public class SyncFieldCharArray : SyncFieldArray<char>
    {
        internal override char DeserializeElementValue(NetDataReader reader)
        {
            return reader.GetChar();
        }

        internal override void SerializeElementValue(NetDataWriter writer, char element)
        {
            writer.Put(element);
        }
    }

    [Serializable]
    public class SyncFieldDouble : LiteNetLibSyncField<double>
    {
        internal override void DeserializeValue(NetDataReader reader)
        {
            _value = reader.GetDouble();
        }

        internal override void SerializeValue(NetDataWriter writer)
        {
            writer.Put(_value);
        }
    }

    [Serializable]
    public class SyncFieldDoubleArray : SyncFieldArray<double>
    {
        internal override double DeserializeElementValue(NetDataReader reader)
        {
            return reader.GetDouble();
        }

        internal override void SerializeElementValue(NetDataWriter writer, double element)
        {
            writer.Put(element);
        }
    }

    [Serializable]
    public class SyncFieldFloat : LiteNetLibSyncField<float>
    {
        internal override void DeserializeValue(NetDataReader reader)
        {
            _value = reader.GetFloat();
        }

        internal override void SerializeValue(NetDataWriter writer)
        {
            writer.Put(_value);
        }
    }

    [Serializable]
    public class SyncFieldFloatArray : SyncFieldArray<float>
    {
        internal override float DeserializeElementValue(NetDataReader reader)
        {
            return reader.GetFloat();
        }

        internal override void SerializeElementValue(NetDataWriter writer, float element)
        {
            writer.Put(element);
        }
    }

    [Serializable]
    public class SyncFieldInt : LiteNetLibSyncField<int>
    {
        internal override void DeserializeValue(NetDataReader reader)
        {
            _value = reader.GetPackedInt();
        }

        internal override void SerializeValue(NetDataWriter writer)
        {
            writer.PutPackedInt(_value);
        }
    }

    [Serializable]
    public class SyncFieldIntArray : SyncFieldArray<int>
    {
        internal override int DeserializeElementValue(NetDataReader reader)
        {
            return reader.GetPackedInt();
        }

        internal override void SerializeElementValue(NetDataWriter writer, int element)
        {
            writer.PutPackedInt(element);
        }
    }

    [Serializable]
    public class SyncFieldLong : LiteNetLibSyncField<long>
    {
        internal override void DeserializeValue(NetDataReader reader)
        {
            _value = reader.GetPackedLong();
        }

        internal override void SerializeValue(NetDataWriter writer)
        {
            writer.PutPackedLong(_value);
        }
    }

    [Serializable]
    public class SyncFieldLongArray : SyncFieldArray<long>
    {
        internal override long DeserializeElementValue(NetDataReader reader)
        {
            return reader.GetPackedLong();
        }

        internal override void SerializeElementValue(NetDataWriter writer, long element)
        {
            writer.PutPackedLong(element);
        }
    }

    [Serializable]
    public class SyncFieldSByte : LiteNetLibSyncField<sbyte>
    {
        internal override void DeserializeValue(NetDataReader reader)
        {
            _value = reader.GetSByte();
        }

        internal override void SerializeValue(NetDataWriter writer)
        {
            writer.Put(_value);
        }
    }

    [Serializable]
    public class SyncFieldSByteArray : SyncFieldArray<sbyte>
    {
        internal override sbyte DeserializeElementValue(NetDataReader reader)
        {
            return reader.GetSByte();
        }

        internal override void SerializeElementValue(NetDataWriter writer, sbyte element)
        {
            writer.Put(element);
        }
    }

    [Serializable]
    public class SyncFieldShort : LiteNetLibSyncField<short>
    {
        internal override void DeserializeValue(NetDataReader reader)
        {
            _value = reader.GetPackedShort();
        }

        internal override void SerializeValue(NetDataWriter writer)
        {
            writer.PutPackedShort(_value);
        }
    }

    [Serializable]
    public class SyncFieldShortArray : SyncFieldArray<short>
    {
        internal override short DeserializeElementValue(NetDataReader reader)
        {
            return reader.GetPackedShort();
        }

        internal override void SerializeElementValue(NetDataWriter writer, short element)
        {
            writer.PutPackedShort(element);
        }
    }

    [Serializable]
    public class SyncFieldString : LiteNetLibSyncField<string>
    {
        internal override void DeserializeValue(NetDataReader reader)
        {
            _value = reader.GetString();
        }

        internal override void SerializeValue(NetDataWriter writer)
        {
            writer.Put(_value);
        }
    }

    [Serializable]
    public class SyncFieldStringArray : SyncFieldArray<string>
    {
        internal override string DeserializeElementValue(NetDataReader reader)
        {
            return reader.GetString();
        }

        internal override void SerializeElementValue(NetDataWriter writer, string element)
        {
            writer.Put(element);
        }
    }

    [Serializable]
    public class SyncFieldUInt : LiteNetLibSyncField<uint>
    {
        internal override void DeserializeValue(NetDataReader reader)
        {
            _value = reader.GetPackedUInt();
        }

        internal override void SerializeValue(NetDataWriter writer)
        {
            writer.PutPackedUInt(_value);
        }
    }

    [Serializable]
    public class SyncFieldUIntArray : SyncFieldArray<uint>
    {
        internal override uint DeserializeElementValue(NetDataReader reader)
        {
            return reader.GetPackedUInt();
        }

        internal override void SerializeElementValue(NetDataWriter writer, uint element)
        {
            writer.PutPackedUInt(element);
        }
    }

    [Serializable]
    public class SyncFieldULong : LiteNetLibSyncField<ulong>
    {
        internal override void DeserializeValue(NetDataReader reader)
        {
            _value = reader.GetPackedULong();
        }

        internal override void SerializeValue(NetDataWriter writer)
        {
            writer.PutPackedULong(_value);
        }
    }

    [Serializable]
    public class SyncFieldULongArray : SyncFieldArray<ulong>
    {
        internal override ulong DeserializeElementValue(NetDataReader reader)
        {
            return reader.GetPackedULong();
        }

        internal override void SerializeElementValue(NetDataWriter writer, ulong element)
        {
            writer.PutPackedULong(element);
        }
    }

    [Serializable]
    public class SyncFieldUShort : LiteNetLibSyncField<ushort>
    {
        internal override void DeserializeValue(NetDataReader reader)
        {
            _value = reader.GetPackedUShort();
        }

        internal override void SerializeValue(NetDataWriter writer)
        {
            writer.PutPackedUShort(_value);
        }
    }

    [Serializable]
    public class SyncFieldUShortArray : SyncFieldArray<ushort>
    {
        internal override ushort DeserializeElementValue(NetDataReader reader)
        {
            return reader.GetPackedUShort();
        }

        internal override void SerializeElementValue(NetDataWriter writer, ushort element)
        {
            writer.PutPackedUShort(element);
        }
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

        protected override bool IsValueChanged(Quaternion oldValue, Quaternion newValue)
        {
            return Quaternion.Angle(oldValue, newValue) >= valueChangeAngle;
        }
    }

    [Serializable]
    public class SyncFieldVector2 : LiteNetLibSyncField<Vector2>
    {
        [Tooltip("If distance between new value and old value >= this value, it will be determined that the value is changing")]
        public float valueChangeDistance = 0.01f;

        protected override bool IsValueChanged(Vector2 oldValue, Vector2 newValue)
        {
            return Vector2.Distance(oldValue, newValue) >= valueChangeDistance;
        }
    }

    [Serializable]
    public class SyncFieldVector2Int : LiteNetLibSyncField<Vector2Int>
    {
        [Tooltip("If distance between new value and old value >= this value, it will be determined that the value is changing")]
        public float valueChangeDistance = 0.01f;

        protected override bool IsValueChanged(Vector2Int oldValue, Vector2Int newValue)
        {
            return Vector2Int.Distance(oldValue, newValue) >= valueChangeDistance;
        }
    }

    [Serializable]
    public class SyncFieldVector3 : LiteNetLibSyncField<Vector3>
    {
        [Tooltip("If distance between new value and old value >= this value, it will be determined that the value is changing")]
        public float valueChangeDistance = 0.01f;

        protected override bool IsValueChanged(Vector3 oldValue, Vector3 newValue)
        {
            return Vector3.Distance(oldValue, newValue) >= valueChangeDistance;
        }
    }

    [Serializable]
    public class SyncFieldVector3Int : LiteNetLibSyncField<Vector3Int>
    {
        [Tooltip("If distance between new value and old value >= this value, it will be determined that the value is changing")]
        public float valueChangeDistance = 0.01f;

        protected override bool IsValueChanged(Vector3Int oldValue, Vector3Int newValue)
        {
            return Vector3Int.Distance(oldValue, newValue) >= valueChangeDistance;
        }
    }

    [Serializable]
    public class SyncFieldVector4 : LiteNetLibSyncField<Vector4>
    {
        [Tooltip("If distance between new value and old value >= this value, it will be determined that the value is changing")]
        public float valueChangeDistance = 0.01f;

        protected override bool IsValueChanged(Vector4 oldValue, Vector4 newValue)
        {
            return Vector4.Distance(oldValue, newValue) >= valueChangeDistance;
        }
    }

    [Serializable]
    [Obsolete("SyncField<Int,Short,Long,UInt,UShort,ULong> already packed. So you don't have to use this class")]
    public class SyncFieldPackedUShort : SyncFieldUShort
    {
    }

    [Serializable]
    [Obsolete("SyncField<Int,Short,Long,UInt,UShort,ULong> already packed. So you don't have to use this class")]
    public class SyncFieldPackedUInt : SyncFieldUInt
    {
    }

    [Serializable]
    [Obsolete("SyncField<Int,Short,Long,UInt,UShort,ULong> already packed. So you don't have to use this class")]
    public class SyncFieldPackedULong : SyncFieldULong
    {
    }

    [Serializable]
    [Obsolete("SyncField<Int,Short,Long,UInt,UShort,ULong> already packed. So you don't have to use this class")]
    public class SyncFieldPackedShort : SyncFieldShort
    {
    }

    [Serializable]
    [Obsolete("SyncField<Int,Short,Long,UInt,UShort,ULong> already packed. So you don't have to use this class")]
    public class SyncFieldPackedInt : SyncFieldInt
    {
    }

    [Serializable]
    [Obsolete("SyncField<Int,Short,Long,UInt,UShort,ULong> already packed. So you don't have to use this class")]
    public class SyncFieldPackedLong : SyncFieldLong
    {
    }

    [Serializable]
    public class SyncFieldDirectionVector2 : SyncFieldNetSerializableStruct<DirectionVector2>
    {
        protected override bool IsValueChanged(DirectionVector2 oldValue, DirectionVector2 newValue)
        {
            return Value.x != newValue.x || Value.y != newValue.y;
        }
    }

    [Serializable]
    public class SyncFieldDirectionVector3 : SyncFieldNetSerializableStruct<DirectionVector3>
    {
        protected override bool IsValueChanged(DirectionVector3 oldValue, DirectionVector3 newValue)
        {
            return Value.x != newValue.x || Value.y != newValue.y || Value.z != newValue.z;
        }
    }

    [Serializable]
    public class SyncFieldHalfPrecision : SyncFieldNetSerializableStruct<HalfPrecision>
    {
        protected override bool IsValueChanged(HalfPrecision oldValue, HalfPrecision newValue)
        {
            return Value.halfValue != newValue.halfValue;
        }
    }

    [Serializable]
    public class SyncFieldHalfVector2 : SyncFieldNetSerializableStruct<HalfVector2>
    {
        protected override bool IsValueChanged(HalfVector2 oldValue, HalfVector2 newValue)
        {
            return Value.x != newValue.x || Value.y != newValue.y;
        }
    }

    [Serializable]
    public class SyncFieldHalfVector3 : SyncFieldNetSerializableStruct<HalfVector3>
    {
        protected override bool IsValueChanged(HalfVector3 oldValue, HalfVector3 newValue)
        {
            return Value.x != newValue.x || Value.y != newValue.y || Value.z != newValue.z;
        }
    }
    #endregion
}
