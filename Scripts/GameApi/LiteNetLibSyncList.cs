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
        public OnChanged callback;

        public abstract int Count { get; }
        public abstract void SendOperation(Operation operation, int index);
        public abstract void SendOperation(NetPeer peer, Operation operation, int index);
        public abstract void DeserializeOperation(NetDataReader reader);
        public abstract void SerializeOperation(NetDataWriter writer, Operation operation, int index);
    }

    public class LiteNetLibSyncList<TField, TFieldType> : LiteNetLibSyncList, IList<TField>
        where TField : LiteNetLibNetField<TFieldType>, new()
    {
        protected readonly List<TField> list = new List<TField>();
        public TField this[int index]
        {
            get { return list[index]; }
            set
            {
                if (!ValidateBeforeAccess())
                    return;

                if (list[index].IsValueChanged(value.Value))
                {
                    list[index] = value;
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
            SendOperation(Operation.Insert, index);
        }

        public bool Contains(TFieldType value)
        {
            for (var i = 0; i < list.Count; ++i)
            {
                var listItem = list[i];
                if (listItem.Value.Equals(value))
                    return true;
            }
            return false;
        }

        public bool Contains(TField item)
        {
            for (var i = 0; i < list.Count; ++i)
            {
                var listItem = list[i];
                if (listItem.Value.Equals(item.Value))
                    return true;
            }
            return false;
        }

        public int IndexOf(TFieldType value)
        {
            for (var i = 0; i < list.Count; ++i)
            {
                var listItem = list[i];
                if (listItem.Value.Equals(value))
                    return i;
            }
            return -1;
        }

        public int IndexOf(TField item)
        {
            for (var i = 0; i < list.Count; ++i)
            {
                var listItem = list[i];
                if (listItem.Value.Equals(item.Value))
                    return i;
            }
            return -1;
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
            SendOperation(Operation.RemoveAt, index);
        }

        public void Clear()
        {
            if (!ValidateBeforeAccess())
                return;
            list.Clear();
            SendOperation(Operation.Clear, 0);
        }

        public void CopyTo(TField[] array, int arrayIndex)
        {
            list.CopyTo(array, arrayIndex);
        }

        public IEnumerator<TField> GetEnumerator()
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

            var peers = manager.Peers;
            if (forOwnerOnly)
            {
                var connectId = Behaviour.ConnectId;
                if (peers.ContainsKey(connectId))
                    SendOperation(peers[connectId], operation, index);
            }
            else
            {
                foreach (var peer in peers.Values)
                {
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

            manager.SendPacket(SendOptions.ReliableOrdered, peer, LiteNetLibGameManager.GameMsgTypes.ServerUpdateSyncList, (writer) => SerializeOperation(writer, operation, index));
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
            if (callback != null)
                callback(operation, index);
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
