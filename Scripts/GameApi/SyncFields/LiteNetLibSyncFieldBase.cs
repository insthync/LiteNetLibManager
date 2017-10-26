using UnityEngine;
using LiteNetLib;
using LiteNetLib.Utils;

namespace LiteNetLibHighLevel
{
    public struct SyncFieldInfo
    {
        public uint objectId;
        public int behaviourIndex;
        public ushort fieldId;
        public SyncFieldInfo(uint objectId, int behaviourIndex, ushort fieldId)
        {
            this.objectId = objectId;
            this.behaviourIndex = behaviourIndex;
            this.fieldId = fieldId;
        }
    }
    
    public abstract class LiteNetLibSyncFieldBase
    {
        public SendOptions sendOptions;
        [ReadOnly, SerializeField]
        protected LiteNetLibBehaviour behaviour;
        public LiteNetLibBehaviour Behaviour
        {
            get { return behaviour; }
        }

        [ShowOnly, SerializeField]
        protected ushort fieldId;
        public ushort FieldId
        {
            get { return fieldId; }
        }
        
        public virtual void OnValidateNetworkFunctions(LiteNetLibBehaviour behaviour, ushort fieldId)
        {
            this.behaviour = behaviour;
            this.fieldId = fieldId;
        }

        public LiteNetLibGameManager Manager
        {
            get { return behaviour.Manager; }
        }

        public SyncFieldInfo GetSyncFieldInfo()
        {
            return new SyncFieldInfo(Behaviour.ObjectId, Behaviour.BehaviourIndex, FieldId);
        }

        public virtual void Deserialize(NetDataReader reader) { }
        public virtual void Serialize(NetDataWriter writer) { }
    }

    public abstract class LiteNetLibSyncFieldBase<T> : LiteNetLibSyncFieldBase
    {
        [ReadOnly, SerializeField]
        protected T value;
        public T Value
        {
            get { return value; }
            set
            {
                if (Behaviour == null)
                {
                    Debug.LogError("Sync field error while set value, behaviour is empty");
                    return;
                }
                if (!Behaviour.IsServer)
                {
                    Debug.LogError("Sync field error while set value, not the server");
                    return;
                }
                if (IsValueChanged(value))
                {
                    this.value = value;
                    Manager.SendServerUpdateSyncField(this);
                }
            }
        }

        public abstract bool IsValueChanged(T newValue);

        public static implicit operator T(LiteNetLibSyncFieldBase<T> field)
        {
            return field.Value;
        }
    }
}
