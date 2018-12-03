using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using LiteNetLib;
using LiteNetLib.Utils;

namespace LiteNetLibManager
{
    public abstract class LiteNetLibSyncList : LiteNetLibElement
    {
        public delegate void OnChanged(Operation op, int itemIndex);
        public enum Operation : byte
        {
            Add,
            Clear,
            Insert,
            RemoveAt,
            Set,
            Dirty,
        }
        public bool forOwnerOnly;
        public OnChanged onOperation;

        public abstract int Count { get; }
        public abstract void SendOperation(Operation operation, int index);
        public abstract void SendOperation(long connectionId, Operation operation, int index);
        public abstract void DeserializeOperation(NetDataReader reader);
        public abstract void SerializeOperation(NetDataWriter writer, Operation operation, int index);
    }

    public class LiteNetLibSyncList<TType> : LiteNetLibSyncList, IList<TType>
    {
        protected readonly List<TType> list = new List<TType>();
        public TType this[int index]
        {
            get { return list[index]; }
            set
            {
                if (!ValidateBeforeAccess())
                    return;

                list[index] = value;
                SendOperation(Operation.Set, index);
            }
        }

        public override sealed int Count
        {
            get { return list.Count; }
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
            if (!ValidateBeforeAccess())
                return;
            list.Add(item);
            SendOperation(Operation.Add, list.Count - 1);
        }

        public void Insert(int index, TType item)
        {
            if (!ValidateBeforeAccess())
                return;
            list.Insert(index, item);
            SendOperation(Operation.Insert, index);
        }

        public bool Contains(TType item)
        {
            return list.Contains(item);
        }

        public int IndexOf(TType item)
        {
            return list.IndexOf(item);
        }

        public bool Remove(TType value)
        {
            if (!ValidateBeforeAccess())
                return false;
            var index = IndexOf(value);
            if (index >= 0)
            {
                RemoveAt(index);
                return true;
            }
            return false;
        }

        public void RemoveAt(int index)
        {
            if (!ValidateBeforeAccess())
                return;
            list.RemoveAt(index);
            SendOperation(Operation.RemoveAt, index);
        }

        public void Clear()
        {
            if (!ValidateBeforeAccess())
                return;
            list.Clear();
            SendOperation(Operation.Clear, -1);
        }

        public void CopyTo(TType[] array, int arrayIndex)
        {
            list.CopyTo(array, arrayIndex);
        }

        public IEnumerator<TType> GetEnumerator()
        {
            return list.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return list.GetEnumerator();
        }

        public void Dirty(int index)
        {
            SendOperation(Operation.Dirty, index);
        }

        public override sealed void SendOperation(Operation operation, int index)
        {
            if (!ValidateBeforeAccess())
                return;

            var manager = Manager;
            if (!manager.IsServer)
                return;

            if (onOperation != null)
                onOperation.Invoke(operation, index);
            
            if (forOwnerOnly)
            {
                var connectId = Behaviour.ConnectionId;
                if (manager.ContainsConnectionId(connectId))
                    SendOperation(connectId, operation, index);
            }
            else
            {
                foreach (var connectionId in manager.GetConnectionIds())
                {
                    if (Behaviour.Identity.IsSubscribedOrOwning(connectionId))
                        SendOperation(connectionId, operation, index);
                }
            }
        }

        public override sealed void SendOperation(long connectionId, Operation operation, int index)
        {
            if (!ValidateBeforeAccess())
                return;

            if (!Manager.IsServer)
                return;

            Manager.ServerSendPacket(connectionId, SendOptions.ReliableOrdered, LiteNetLibGameManager.GameMsgTypes.ServerUpdateSyncList, (writer) => SerializeForSendOperation(writer, operation, index));
        }

        protected void SerializeForSendOperation(NetDataWriter writer, Operation operation, int index)
        {
            LiteNetLibElementInfo.SerializeInfo(GetInfo(), writer);
            SerializeOperation(writer, operation, index);
        }

        public override sealed void DeserializeOperation(NetDataReader reader)
        {
            var operation = (Operation)reader.GetByte();
            var index = -1;
            TType item;
            switch (operation)
            {
                case Operation.Add:
                    item = (TType)reader.GetValue(typeof(TType));
                    list.Add(item);
                    index = list.Count - 1;
                    break;
                case Operation.Insert:
                    index = reader.GetInt();
                    item = (TType)reader.GetValue(typeof(TType));
                    list.Insert(index, item);
                    break;
                case Operation.Set:
                case Operation.Dirty:
                    index = reader.GetInt();
                    item = (TType)reader.GetValue(typeof(TType));
                    list[index] = item;
                    break;
                case Operation.RemoveAt:
                    index = reader.GetInt();
                    list.RemoveAt(index);
                    break;
                case Operation.Clear:
                    list.Clear();
                    break;
            }
            if (onOperation != null)
                onOperation.Invoke(operation, index);
        }

        public override sealed void SerializeOperation(NetDataWriter writer, Operation operation, int index)
        {
            writer.Put((byte)operation);
            switch (operation)
            {
                case Operation.Add:
                    writer.PutValue(list[index]);
                    break;
                case Operation.Insert:
                case Operation.Set:
                case Operation.Dirty:
                    writer.Put(index);
                    writer.PutValue(list[index]);
                    break;
                case Operation.RemoveAt:
                    writer.Put(index);
                    break;
                case Operation.Clear:
                    break;
            }
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
    #endregion
}
