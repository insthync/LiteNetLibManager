using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using LiteNetLib;
using LiteNetLib.Utils;

namespace LiteNetLibManager
{
    public abstract partial class LiteNetLibSyncList : LiteNetLibElement
    {
        protected readonly static NetDataWriter s_Writer = new NetDataWriter();
        public abstract Type FieldType { get; }
        public abstract int Count { get; }
        internal abstract void Reset();
        internal abstract void SendInitialList(long connectionId);
        internal abstract bool SendOperations();
        internal abstract void ProcessOperations(NetDataReader reader);

        protected override bool CanSync()
        {
            return IsServer;
        }

        public void RegisterUpdating()
        {
            if (!IsSetup)
                return;
            Manager?.RegisterSyncListUpdating(this);
        }

        public void UnregisterUpdating()
        {
            Manager?.UnregisterSyncListUpdating(this);
        }
    }

    public class LiteNetLibSyncList<TType> : LiteNetLibSyncList, IList<TType>
    {
        protected struct OperationEntry
        {
            public LiteNetLibSyncListOp operation;
            public int index;
            public TType item;
        }

        public delegate void OnOperationDelegate(LiteNetLibSyncListOp op, int itemIndex, TType oldItem, TType newItem);

        [Tooltip("Sending data channel")]
        public byte dataChannel = 0;
        [Tooltip("If this is `TRUE`, this will update to owner client only, default is `FALSE`")]
        public bool forOwnerOnly = false;
        public OnOperationDelegate onOperation;

        protected readonly List<TType> _list = new List<TType>();
        protected readonly List<OperationEntry> _operationEntries = new List<OperationEntry>();

        internal override sealed void Setup(LiteNetLibBehaviour behaviour, int elementId)
        {
            base.Setup(behaviour, elementId);
            if (Count > 0 && onOperation != null)
            {
                for (int i = 0; i < Count; ++i)
                {
                    onOperation.Invoke(LiteNetLibSyncListOp.AddInitial, i, default, this[i]);
                }
            }
            RegisterUpdating();
        }

        public TType this[int index]
        {
            get { return _list[index]; }
            set
            {
                if (IsSetup && !IsServer)
                {
                    Logging.LogError(LogTag, "Cannot access sync list from client.");
                    return;
                }
                TType oldItem = _list[index];
                _list[index] = value;
                PrepareOperation(LiteNetLibSyncListOp.Set, index, oldItem, value);
            }
        }

        public override sealed Type FieldType
        {
            get { return typeof(TType); }
        }

        public override sealed int Count
        {
            get { return _list.Count; }
        }

        public bool IsReadOnly
        {
            get { return false; }
        }

        public TType Get(int index)
        {
            return this[index];
        }

        public void Set(int index, TType value)
        {
            this[index] = value;
        }

        public void Add(TType item)
        {
            if (IsSetup && !CanSync())
            {
                Logging.LogError(LogTag, "Cannot access sync list from client.");
                return;
            }
            int index = _list.Count;
            _list.Add(item);
            PrepareOperation(LiteNetLibSyncListOp.Add, index, default, item);
        }

        public void AddRange(IEnumerable<TType> collection)
        {
            if (IsSetup && !CanSync())
            {
                Logging.LogError(LogTag, "Cannot access sync list from client.");
                return;
            }
            foreach (TType item in collection)
            {
                Add(item);
            }
        }

        public void Insert(int index, TType item)
        {
            if (IsSetup && !CanSync())
            {
                Logging.LogError(LogTag, "Cannot access sync list from client.");
                return;
            }
            _list.Insert(index, item);
            PrepareOperation(LiteNetLibSyncListOp.Insert, index, default, item);
        }

        public bool Contains(TType item)
        {
            return _list.Contains(item);
        }

        public int IndexOf(TType item)
        {
            return _list.IndexOf(item);
        }

        public bool Remove(TType value)
        {
            if (IsSetup && !CanSync())
            {
                Logging.LogError(LogTag, "Cannot access sync list from client.");
                return false;
            }
            int index = IndexOf(value);
            if (index >= 0)
            {
                RemoveAt(index);
                return true;
            }
            return false;
        }

        public void RemoveAt(int index)
        {
            if (IsSetup && !CanSync())
            {
                Logging.LogError(LogTag, "Cannot access sync list from client.");
                return;
            }
            TType oldItem = _list[index];
            if (index == 0)
            {
                _list.RemoveAt(index);
                PrepareOperation(LiteNetLibSyncListOp.RemoveFirst, index, oldItem, default);
            }
            else if (index == _list.Count - 1)
            {
                _list.RemoveAt(index);
                PrepareOperation(LiteNetLibSyncListOp.RemoveLast, index, oldItem, default);
            }
            else
            {
                _list.RemoveAt(index);
                PrepareOperation(LiteNetLibSyncListOp.RemoveAt, index, oldItem, default);
            }
        }

        public void Clear()
        {
            if (IsSetup && !CanSync())
            {
                Logging.LogError(LogTag, "Cannot access sync list from client.");
                return;
            }
            _list.Clear();
            PrepareOperation(LiteNetLibSyncListOp.Clear, -1, default, default);
        }

        public void CopyTo(TType[] array, int arrayIndex)
        {
            _list.CopyTo(array, arrayIndex);
        }

        public IEnumerator<TType> GetEnumerator()
        {
            return _list.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _list.GetEnumerator();
        }

        public void Dirty(int index)
        {
            if (IsSetup && !CanSync())
            {
                Logging.LogError(LogTag, "Cannot access sync list from client.");
                return;
            }
            PrepareOperation(LiteNetLibSyncListOp.Dirty, index, this[index], this[index]);
        }

        internal override sealed void Reset()
        {
            _list.Clear();
            _operationEntries.Clear();
        }

        protected void PrepareOperation(LiteNetLibSyncListOp operation, int index, TType oldItem, TType newItem)
        {
            OnOperation(operation, index, oldItem, newItem);
            PrepareOperation(_operationEntries, operation, index, oldItem, newItem);
        }

        protected void PrepareOperation(List<OperationEntry> operationEntries, LiteNetLibSyncListOp operation, int index, TType oldItem, TType newItem)
        {
            switch (operation)
            {
                case LiteNetLibSyncListOp.Clear:
                    operationEntries.Add(new OperationEntry()
                    {
                        operation = operation,
                        index = index,
                    });
                    break;
                case LiteNetLibSyncListOp.RemoveAt:
                case LiteNetLibSyncListOp.RemoveFirst:
                case LiteNetLibSyncListOp.RemoveLast:
                    operationEntries.Add(new OperationEntry()
                    {
                        operation = operation,
                        index = index,
                    });
                    break;
                default:
                    operationEntries.Add(new OperationEntry()
                    {
                        operation = operation,
                        index = index,
                        item = _list[index],
                    });
                    break;
            }
            RegisterUpdating();
        }

        internal override sealed void SendInitialList(long connectionId)
        {
            if (!CanSync())
                return;
            List<OperationEntry> addInitialOperationEntries = new List<OperationEntry>();
            for (int i = 0; i < Count; ++i)
            {
                if (!ContainsAddOperation(i))
                    PrepareOperation(addInitialOperationEntries, LiteNetLibSyncListOp.AddInitial, i, default, this[i]);
            }
            LiteNetLibServer server = Manager.Server;
            TransportHandler.WritePacket(s_Writer, GameMsgTypes.OperateSyncList);
            SerializeForSendOperations(s_Writer, addInitialOperationEntries);
            server.SendMessage(connectionId, dataChannel, DeliveryMethod.ReliableOrdered, s_Writer);
        }

        private bool ContainsAddOperation(int index)
        {
            for (int i = 0; i < _operationEntries.Count; ++i)
            {
                if (_operationEntries[i].operation == LiteNetLibSyncListOp.Add && _operationEntries[i].index == index)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Return `TRUE` to determine that the update is done and unregister updating
        /// </summary>
        /// <returns></returns>
        internal override sealed bool SendOperations()
        {
            if (!CanSync())
                return false;

            if (_operationEntries.Count <= 0)
                return true;

            LiteNetLibGameManager manager = Manager;
            LiteNetLibServer server = manager.Server;
            TransportHandler.WritePacket(s_Writer, GameMsgTypes.OperateSyncList);
            SerializeForSendOperations(s_Writer, _operationEntries);
            _operationEntries.Clear();
            if (forOwnerOnly)
            {
                if (manager.ContainsConnectionId(ConnectionId))
                    server.SendMessage(ConnectionId, dataChannel, DeliveryMethod.ReliableOrdered, s_Writer);
            }
            else
            {
                foreach (long connectionId in manager.GetConnectionIds())
                {
                    if (Identity.HasSubscriberOrIsOwning(connectionId))
                        server.SendMessage(connectionId, dataChannel, DeliveryMethod.ReliableOrdered, s_Writer);
                }
            }

            return true;
        }

        internal override sealed void ProcessOperations(NetDataReader reader)
        {
            int operationCount = reader.GetPackedInt();
            for (int i = 0; i < operationCount; ++i)
            {
                DeserializeOperation(reader);
            }
        }

        protected void SerializeForSendOperations(NetDataWriter writer, List<OperationEntry> operationEntries)
        {
            LiteNetLibElementInfo.SerializeInfo(GetInfo(), writer);
            writer.PutPackedInt(operationEntries.Count);
            for (int i = 0; i < operationEntries.Count; ++i)
            {
                SerializeOperation(writer, operationEntries[i]);
            }
        }

        protected void DeserializeOperation(NetDataReader reader)
        {
            LiteNetLibSyncListOp operation = (LiteNetLibSyncListOp)reader.GetByte();
            int index = -1;
            TType oldItem = default;
            TType newItem = default;
            switch (operation)
            {
                case LiteNetLibSyncListOp.Add:
                case LiteNetLibSyncListOp.AddInitial:
                    newItem = DeserializeValueForAddOrInsert(index, reader);
                    index = _list.Count;
                    _list.Add(newItem);
                    break;
                case LiteNetLibSyncListOp.Insert:
                    index = reader.GetInt();
                    newItem = DeserializeValueForAddOrInsert(index, reader);
                    _list.Insert(index, newItem);
                    break;
                case LiteNetLibSyncListOp.Set:
                case LiteNetLibSyncListOp.Dirty:
                    oldItem = _list[index];
                    index = reader.GetInt();
                    newItem = DeserializeValueForSetOrDirty(index, reader);
                    _list[index] = newItem;
                    break;
                case LiteNetLibSyncListOp.RemoveAt:
                    index = reader.GetInt();
                    oldItem = _list[index];
                    _list.RemoveAt(index);
                    break;
                case LiteNetLibSyncListOp.RemoveFirst:
                    index = 0;
                    oldItem = _list[index];
                    _list.RemoveAt(index);
                    break;
                case LiteNetLibSyncListOp.RemoveLast:
                    index = _list.Count - 1;
                    oldItem = _list[index];
                    _list.RemoveAt(index);
                    break;
                case LiteNetLibSyncListOp.Clear:
                    _list.Clear();
                    break;
            }
            OnOperation(operation, index, oldItem, newItem);
        }

        protected void SerializeOperation(NetDataWriter writer, OperationEntry entry)
        {
            writer.Put((byte)entry.operation);
            switch (entry.operation)
            {
                case LiteNetLibSyncListOp.Add:
                case LiteNetLibSyncListOp.AddInitial:
                    SerializeValueForAddOrInsert(entry.index, writer, entry.item);
                    break;
                case LiteNetLibSyncListOp.Insert:
                    writer.Put(entry.index);
                    SerializeValueForAddOrInsert(entry.index, writer, entry.item);
                    break;
                case LiteNetLibSyncListOp.Set:
                case LiteNetLibSyncListOp.Dirty:
                    writer.Put(entry.index);
                    SerializeValueForSetOrDirty(entry.index, writer, entry.item);
                    break;
                case LiteNetLibSyncListOp.RemoveAt:
                    writer.Put(entry.index);
                    break;
                case LiteNetLibSyncListOp.RemoveFirst:
                case LiteNetLibSyncListOp.RemoveLast:
                case LiteNetLibSyncListOp.Clear:
                    break;
            }
        }

        protected virtual TType DeserializeValue(NetDataReader reader)
        {
            return reader.GetValue<TType>();
        }

        protected virtual void SerializeValue(NetDataWriter writer, TType value)
        {
            writer.PutValue(value);
        }

        protected virtual TType DeserializeValueForAddOrInsert(int index, NetDataReader reader)
        {
            return DeserializeValue(reader);
        }

        protected virtual void SerializeValueForAddOrInsert(int index, NetDataWriter writer, TType value)
        {
            SerializeValue(writer, value);
        }

        protected virtual TType DeserializeValueForSetOrDirty(int index, NetDataReader reader)
        {
            return DeserializeValue(reader);
        }

        protected virtual void SerializeValueForSetOrDirty(int index, NetDataWriter writer, TType value)
        {
            SerializeValue(writer, value);
        }

        protected void OnOperation(LiteNetLibSyncListOp operation, int index, TType oldItem, TType newItem)
        {
            if (onOperation != null)
                onOperation.Invoke(operation, index, oldItem, newItem);
        }
    }

    #region Implement for general usages and serializable
    // Generics

    [Serializable]
    public class SyncListBool : LiteNetLibSyncList<bool>
    {
    }

    [Serializable]
    public class SyncListByte : LiteNetLibSyncList<byte>
    {
    }

    [Serializable]
    public class SyncListChar : LiteNetLibSyncList<char>
    {
    }

    [Serializable]
    public class SyncListDouble : LiteNetLibSyncList<double>
    {
    }

    [Serializable]
    public class SyncListFloat : LiteNetLibSyncList<float>
    {
    }

    [Serializable]
    public class SyncListInt : LiteNetLibSyncList<int>
    {
    }

    [Serializable]
    public class SyncListLong : LiteNetLibSyncList<long>
    {
    }

    [Serializable]
    public class SyncListSByte : LiteNetLibSyncList<sbyte>
    {
    }

    [Serializable]
    public class SyncListShort : LiteNetLibSyncList<short>
    {
    }

    [Serializable]
    public class SyncListString : LiteNetLibSyncList<string>
    {
    }

    [Serializable]
    public class SyncListUInt : LiteNetLibSyncList<uint>
    {
    }

    [Serializable]
    public class SyncListULong : LiteNetLibSyncList<ulong>
    {
    }

    [Serializable]
    public class SyncListUShort : LiteNetLibSyncList<ushort>
    {
    }

    // Unity

    [Serializable]
    public class SyncListColor : LiteNetLibSyncList<Color>
    {
    }

    [Serializable]
    public class SyncListQuaternion : LiteNetLibSyncList<Quaternion>
    {
    }

    [Serializable]
    public class SyncListVector2 : LiteNetLibSyncList<Vector2>
    {
    }

    [Serializable]
    public class SyncListVector2Int : LiteNetLibSyncList<Vector2Int>
    {
    }

    [Serializable]
    public class SyncListVector3 : LiteNetLibSyncList<Vector3>
    {
    }

    [Serializable]
    public class SyncListVector3Int : LiteNetLibSyncList<Vector3Int>
    {
    }

    [Serializable]
    public class SyncListVector4 : LiteNetLibSyncList<Vector4>
    {
    }

    [Serializable]
    public class SyncListDirectionVector2 : LiteNetLibSyncList<DirectionVector2>
    {
    }

    [Serializable]
    public class SyncListDirectionVector3 : LiteNetLibSyncList<DirectionVector3>
    {
    }
    #endregion
}
