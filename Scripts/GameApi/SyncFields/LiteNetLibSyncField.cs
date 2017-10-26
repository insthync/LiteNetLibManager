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
    
    public abstract class LiteNetLibSyncField
    {
        public SendOptions sendOptions;
        [ReadOnly, SerializeField]
        protected LiteNetLibBehaviour behaviour;
        public LiteNetLibBehaviour Behaviour
        {
            get { return behaviour; }
        }

        [ReadOnly, SerializeField]
        protected ushort fieldId;
        public ushort FieldId
        {
            get { return fieldId; }
        }

        public LiteNetLibGameManager Manager
        {
            get { return behaviour.Manager; }
        }

        public virtual void OnRegister(LiteNetLibBehaviour behaviour, ushort fieldId)
        {
            this.behaviour = behaviour;
            this.fieldId = fieldId;
        }

        public SyncFieldInfo GetSyncFieldInfo()
        {
            return new SyncFieldInfo(Behaviour.ObjectId, Behaviour.BehaviourIndex, FieldId);
        }

        public virtual void Deserialize(NetDataReader reader) { }
        public virtual void Serialize(NetDataWriter writer) { }
    }

    public abstract class LiteNetLibSyncField<T> : LiteNetLibSyncField
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

        public static implicit operator T(LiteNetLibSyncField<T> field)
        {
            return field.Value;
        }
    }
}
