using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using LiteNetLib;
using LiteNetLib.Utils;

namespace LiteNetLibHighLevel
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
        public abstract void SendOperation(NetPeer peer, Operation operation, int index);
        public abstract void DeserializeOperation(NetDataReader reader);
        public abstract void SerializeOperation(NetDataWriter writer, Operation operation, int index);
    }

    public class LiteNetLibSyncList<TField, TFieldType> : LiteNetLibSyncList, IList<TFieldType>
        where TField : LiteNetLibNetField<TFieldType>, new()
    {
        protected readonly List<TField> list = new List<TField>();
        protected readonly List<TFieldType> valueList = new List<TFieldType>();
        public TFieldType this[int index]
        {
            get { return list[index]; }
            set
            {
                if (!ValidateBeforeAccess())
                    return;

                if (list[index].IsValueChanged(value))
                {
                    list[index].Value = value;
                    valueList[index] = value;
                    SendOperation(Operation.Set, index);
                }
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

        public TFieldType Get(int index)
        {
            return this[index];
        }

        public void Set(int index, TFieldType value)
        {
            this[index] = value;
        }

        public void Add(TFieldType value)
        {
            var item = new TField();
            item.Value = value;
            Add(item);
        }

        public void Add(TField item)
        {
            if (!ValidateBeforeAccess())
                return;
            list.Add(item);
            valueList.Add(item);
            SendOperation(Operation.Add, list.Count - 1);
        }

        public void Insert(int index, TFieldType value)
        {
            var item = new TField();
            item.Value = value;
            Insert(index, item);
        }

        public void Insert(int index, TField item)
        {
            if (!ValidateBeforeAccess())
                return;
            list.Insert(index, item);
            valueList.Insert(index, item);
            SendOperation(Operation.Insert, index);
        }

        public bool Contains(TFieldType value)
        {
            return valueList.Contains(value);
        }

        public bool Contains(TField item)
        {
            return Contains(item.Value);
        }

        public int IndexOf(TFieldType value)
        {
            return valueList.IndexOf(value);
        }

        public int IndexOf(TField item)
        {
            return IndexOf(item.Value);
        }

        public bool Remove(TFieldType value)
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

        public bool Remove(TField item)
        {
            if (!ValidateBeforeAccess())
                return false;
            var index = IndexOf(item);
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
            valueList.RemoveAt(index);
            SendOperation(Operation.RemoveAt, index);
        }

        public void Clear()
        {
            if (!ValidateBeforeAccess())
                return;
            list.Clear();
            valueList.Clear();
            SendOperation(Operation.Clear, 0);
        }

        public void CopyTo(TFieldType[] array, int arrayIndex)
        {
            valueList.CopyTo(array, arrayIndex);
        }

        public IEnumerator<TFieldType> GetEnumerator()
        {
            return valueList.GetEnumerator();
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

            var peers = manager.Peers;
            if (forOwnerOnly)
            {
                var connectId = Behaviour.ConnectId;
                NetPeer foundPeer;
                if (peers.TryGetValue(connectId, out foundPeer))
                    SendOperation(foundPeer, operation, index);
            }
            else
            {
                var peerValues = peers.Values;
                foreach (var peer in peerValues)
                {
                    if (Behaviour.Identity.IsSubscribedOrOwning(peer.ConnectId))
                        SendOperation(peer, operation, index);
                }
            }
        }

        public override sealed void SendOperation(NetPeer peer, Operation operation, int index)
        {
            if (!ValidateBeforeAccess())
                return;

            var manager = Manager;
            if (!manager.IsServer)
                return;

            manager.SendPacket(SendOptions.ReliableOrdered, peer, LiteNetLibGameManager.GameMsgTypes.ServerUpdateSyncList, (writer) => SerializeForSendOperation(writer, operation, index));
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
            var item = new TField();
            switch (operation)
            {
                case Operation.Add:
                    item.Deserialize(reader);
                    list.Add(item);
                    break;
                case Operation.Insert:
                    index = reader.GetInt();
                    item.Deserialize(reader);
                    list.Insert(index, item);
                    break;
                case Operation.Set:
                case Operation.Dirty:
                    index = reader.GetInt();
                    item.Deserialize(reader);
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
                onOperation(operation, index);
        }

        public override sealed void SerializeOperation(NetDataWriter writer, Operation operation, int index)
        {
            writer.Put((byte)operation);
            switch (operation)
            {
                case Operation.Add:
                    list[index].Serialize(writer);
                    break;
                case Operation.Insert:
                case Operation.Set:
                case Operation.Dirty:
                    writer.Put(index);
                    list[index].Serialize(writer);
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
    [Serializable]
    public class SyncListBool : LiteNetLibSyncList<NetFieldBool, bool>
    {
    }

    [Serializable]
    public class SyncListByte : LiteNetLibSyncList<NetFieldByte, byte>
    {
    }

    [Serializable]
    public class SyncListChar : LiteNetLibSyncList<NetFieldChar, char>
    {
    }

    [Serializable]
    public class SyncListColor : LiteNetLibSyncList<NetFieldColor, Color>
    {
    }

    [Serializable]
    public class SyncListDouble : LiteNetLibSyncList<NetFieldDouble, double>
    {
    }

    [Serializable]
    public class SyncListFloat : LiteNetLibSyncList<NetFieldFloat, float>
    {
    }

    [Serializable]
    public class SyncListInt : LiteNetLibSyncList<NetFieldInt, int>
    {
    }

    [Serializable]
    public class SyncListLong : LiteNetLibSyncList<NetFieldLong, long>
    {
    }

    [Serializable]
    public class SyncListQuaternion : LiteNetLibSyncList<NetFieldQuaternion, Quaternion>
    {
    }

    [Serializable]
    public class SyncListSByte : LiteNetLibSyncList<NetFieldSByte, sbyte>
    {
    }

    [Serializable]
    public class SyncListShort : LiteNetLibSyncList<NetFieldShort, short>
    {
    }

    [Serializable]
    public class SyncListString : LiteNetLibSyncList<NetFieldString, string>
    {
    }

    [Serializable]
    public class SyncListUInt : LiteNetLibSyncList<NetFieldUInt, uint>
    {
    }

    [Serializable]
    public class SyncListULong : LiteNetLibSyncList<NetFieldULong, ulong>
    {
    }

    [Serializable]
    public class SyncListUShort : LiteNetLibSyncList<NetFieldUShort, ushort>
    {
    }

    [Serializable]
    public class SyncListVector2 : LiteNetLibSyncList<NetFieldVector2, Vector2>
    {
    }

    [Serializable]
    public class SyncListVector2Int : LiteNetLibSyncList<NetFieldVector2Int, Vector2Int>
    {
    }

    [Serializable]
    public class SyncListVector3 : LiteNetLibSyncList<NetFieldVector3, Vector3>
    {
    }

    [Serializable]
    public class SyncListVector3Int : LiteNetLibSyncList<NetFieldVector3Int, Vector3Int>
    {
    }

    [Serializable]
    public class SyncListVector4 : LiteNetLibSyncList<NetFieldVector4, Vector4>
    {
    }
    #endregion
}
