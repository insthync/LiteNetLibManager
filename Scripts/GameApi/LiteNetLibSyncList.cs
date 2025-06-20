using LiteNetLib.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LiteNetLibManager
{
    public abstract class LiteNetLibSyncList : LiteNetLibSyncElement
    {
        public override byte ElementType => SyncElementTypes.SyncList;

        protected readonly static NetDataWriter s_Writer = new NetDataWriter();

        [Tooltip("If this is `TRUE`, this will update to owner client only, default is `FALSE`")]
        public bool forOwnerOnly = false;

        public abstract Type FieldType { get; }
        public abstract int Count { get; }

        protected bool CanSync()
        {
            return IsServer;
        }

        internal override bool WillSyncFromServerReliably(LiteNetLibPlayer player)
        {
            if (!base.WillSyncFromServerReliably(player))
                return false;
            return !forOwnerOnly || ConnectionId == player.ConnectionId;
        }
    }

    public class LiteNetLibSyncList<TType> : LiteNetLibSyncList, IList<TType>
    {
        protected struct OperationEntry
        {
            public LiteNetLibSyncListOp Operation;
            public int Index;
            public TType Item;
        }

        public delegate void OnOperationDelegate(LiteNetLibSyncListOp op, int itemIndex, TType oldItem, TType newItem);
        public OnOperationDelegate onOperation;

        protected readonly List<TType> _list = new List<TType>();
        protected readonly List<OperationEntry> _operationEntries = new List<OperationEntry>();

        public TType this[int index]
        {
            get { return _list[index]; }
            set
            {
                if (IsSpawned && !IsServer)
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
            if (IsSpawned && !CanSync())
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
            if (IsSpawned && !CanSync())
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
            if (IsSpawned && !CanSync())
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

        public bool Remove(TType item)
        {
            if (IsSpawned && !CanSync())
            {
                Logging.LogError(LogTag, "Cannot access sync list from client.");
                return false;
            }
            int index = IndexOf(item);
            if (index >= 0)
            {
                RemoveAt(index);
                return true;
            }
            return false;
        }

        public void RemoveAt(int index)
        {
            if (IsSpawned && !CanSync())
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
            if (IsSpawned && !CanSync())
            {
                Logging.LogError(LogTag, "Cannot access sync list from client.");
                return;
            }
            if (_list.Count == 0)
                return;
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
            if (IsSpawned && !CanSync())
            {
                Logging.LogError(LogTag, "Cannot access sync list from client.");
                return;
            }
            PrepareOperation(LiteNetLibSyncListOp.Dirty, index, this[index], this[index]);
        }

        public override void Synced()
        {
            _operationEntries.Clear();
            UnregisterUpdating();
        }

        internal override sealed void Reset()
        {
            _list.Clear();
            _operationEntries.Clear();
        }

        protected void PrepareOperation(LiteNetLibSyncListOp operation, int index, TType oldItem, TType newItem)
        {
            if (!IsSpawned)
                return;
            OnOperation(operation, index, oldItem, newItem);
            PrepareOperation(_operationEntries, operation, index, newItem);
        }

        protected void PrepareOperation(List<OperationEntry> operationEntries, LiteNetLibSyncListOp operation, int index, TType item)
        {
            switch (operation)
            {
                case LiteNetLibSyncListOp.Clear:
                    operationEntries.Clear();
                    operationEntries.Add(new OperationEntry()
                    {
                        Operation = operation,
                        Index = index,
                    });
                    break;
                case LiteNetLibSyncListOp.RemoveAt:
                case LiteNetLibSyncListOp.RemoveFirst:
                case LiteNetLibSyncListOp.RemoveLast:
                    RemoveSetOrDirtyOperations(operationEntries, index);
                    operationEntries.Add(new OperationEntry()
                    {
                        Operation = operation,
                        Index = index,
                    });
                    break;
                case LiteNetLibSyncListOp.Set:
                    RemoveSetOrDirtyOperations(operationEntries, index);
                    operationEntries.Add(new OperationEntry()
                    {
                        Operation = operation,
                        Index = index,
                        Item = _list[index],
                    });
                    break;
                case LiteNetLibSyncListOp.Dirty:
                    RemoveDirtyOperations(operationEntries, index);
                    operationEntries.Add(new OperationEntry()
                    {
                        Operation = operation,
                        Index = index,
                        Item = _list[index],
                    });
                    break;
                default:
                    operationEntries.Add(new OperationEntry()
                    {
                        Operation = operation,
                        Index = index,
                        Item = _list[index],
                    });
                    break;
            }
            RegisterUpdating();
        }

        private bool ContainsOperation(List<OperationEntry> operationEntries, int index, LiteNetLibSyncListOp operation)
        {
            for (int i = 0; i < operationEntries.Count; ++i)
            {
                if (operationEntries[i].Operation == operation && operationEntries[i].Index == index)
                    return true;
            }
            return false;
        }

        private void RemoveSetOrDirtyOperations(List<OperationEntry> operationEntries, int index)
        {
            for (int i = operationEntries.Count - 1; i >= 0; --i)
            {
                if ((operationEntries[i].Operation == LiteNetLibSyncListOp.Set || operationEntries[i].Operation == LiteNetLibSyncListOp.Dirty) && operationEntries[i].Index == index)
                    operationEntries.RemoveAt(i);
            }
        }

        private void RemoveDirtyOperations(List<OperationEntry> operationEntries, int index)
        {
            for (int i = operationEntries.Count - 1; i >= 0; --i)
            {
                if (operationEntries[i].Operation == LiteNetLibSyncListOp.Dirty && operationEntries[i].Index == index)
                    operationEntries.RemoveAt(i);
            }
        }

        internal override void WriteSyncData(bool initial, NetDataWriter writer)
        {
            if (initial)
            {
                writer.PutPackedInt(Count);
                if (Count > 0)
                {
                    for (int i = 0; i < Count; ++i)
                    {
                        SerializeValue(writer, this[i]);
                    }
                }
            }
            else
            {
                writer.PutPackedInt(_operationEntries.Count);
                if (_operationEntries.Count <= 0)
                    return;
                for (int i = 0; i < _operationEntries.Count; ++i)
                {
                    SerializeOperation(writer, _operationEntries[i]);
                }
            }
        }

        internal override void ReadSyncData(uint tick, bool initial, NetDataReader reader)
        {
            if (initial)
            {
                _list.Clear();
                int count = reader.GetPackedInt();
                if (count > 0)
                {
                    for (int i = 0; i < count; ++i)
                    {
                        _list.Add(DeserializeValue(reader));
                    }
                }
            }
            else
            {
                int operationCount = reader.GetPackedInt();
                for (int i = 0; i < operationCount; ++i)
                {
                    DeserializeOperation(reader);
                }
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
                    index = reader.GetInt();
                    oldItem = _list[index];
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
            writer.Put((byte)entry.Operation);
            switch (entry.Operation)
            {
                case LiteNetLibSyncListOp.Add:
                case LiteNetLibSyncListOp.AddInitial:
                    SerializeValueForAddOrInsert(entry.Index, writer, entry.Item);
                    break;
                case LiteNetLibSyncListOp.Insert:
                    writer.Put(entry.Index);
                    SerializeValueForAddOrInsert(entry.Index, writer, entry.Item);
                    break;
                case LiteNetLibSyncListOp.Set:
                case LiteNetLibSyncListOp.Dirty:
                    writer.Put(entry.Index);
                    SerializeValueForSetOrDirty(entry.Index, writer, entry.Item);
                    break;
                case LiteNetLibSyncListOp.RemoveAt:
                    writer.Put(entry.Index);
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