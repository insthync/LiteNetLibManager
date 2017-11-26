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
            Remove,
            RemoveAt,
            Set,
            Dirty,
        }
        public bool forOwnerOnly;

        public abstract void Deserialize(NetDataReader reader);
        public abstract void Serialize(NetDataWriter writer);
    }

    public abstract class LiteNetLibSyncList<TField, TFieldType> : LiteNetLibSyncList, IList<TField>
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
                if (IsValueChanged(value))
                {
                    list[index] = value;
                    SendOperation(Operation.Set, index, value);
                }
            }
        }

        public int Count
        {
            get { return list.Count; }
        }

        public bool IsReadOnly
        {
            get { return false; }
        }

        public void Add(TField item)
        {
            if (!ValidateBeforeAccess())
                return;
            list.Add(item);
            SendOperation(Operation.Add, list.Count - 1, item);
        }

        public void Clear()
        {
            if (!ValidateBeforeAccess())
                return;
            list.Clear();
            SendOperation(Operation.Clear, 0, default(TField));
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

        public void CopyTo(TField[] array, int arrayIndex)
        {
            list.CopyTo(array, arrayIndex);
        }

        public IEnumerator<TField> GetEnumerator()
        {
            return list.GetEnumerator();
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

        public void Insert(int index, TField item)
        {
            if (!ValidateBeforeAccess())
                return;
            list.Insert(index, item);
            SendOperation(Operation.Insert, index, item);
        }

        public bool Remove(TField item)
        {
            if (!ValidateBeforeAccess())
                return false;
            var result = list.Remove(item);
            if (result)
                SendOperation(Operation.Remove, 0, item);
            return result;
        }

        public void RemoveAt(int index)
        {
            if (!ValidateBeforeAccess())
                return;
            list.RemoveAt(index);
            SendOperation(Operation.RemoveAt, index, default(TField));
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return list.GetEnumerator();
        }

        public void Dirty(int index)
        {
            SendOperation(Operation.Dirty, index, list[index]);
        }

        private bool ValidateBeforeAccess()
        {
            if (Behaviour == null)
            {
                Debug.LogError("Sync list error while set value, behaviour is empty");
                return false;
            }
            if (!Behaviour.IsServer)
            {
                Debug.LogError("Sync list error while set value, not the server");
                return false;
            }
            return true;
        }

        public abstract bool IsValueChanged(TFieldType newValue);
        public void SendOperation(Operation operation, int index, TField item)
        {

        }

        public void SendOperation(NetPeer peer, Operation operation, int index, TField item)
        {

        }
    }
}
