using UnityEngine;
using LiteNetLib;
using LiteNetLib.Utils;

namespace LiteNetLibHighLevel
{
    public struct SyncFieldInfo
    {
        public uint objectId;
        public int behaviourIndex;
        public int fieldIndex;
        public SyncFieldInfo(uint objectId, int behaviourIndex, int variableIndex)
        {
            this.objectId = objectId;
            this.behaviourIndex = behaviourIndex;
            this.fieldIndex = variableIndex;
        }
    }
    
    public abstract class LiteNetLibSyncFieldBase
    {
        public SendOptions sendOptions;
        [ReadOnly, SerializeField]
        private LiteNetLibBehaviour behaviour;
        public LiteNetLibBehaviour Behaviour
        {
            get { return behaviour; }
        }

        [ShowOnly, SerializeField]
        private int variableIndex;
        public int VariableIndex
        {
            get { return variableIndex; }
        }

#if UNITY_EDITOR
        public virtual void OnValidateIdentity(LiteNetLibBehaviour behaviour, int variableIndex)
        {
            this.behaviour = behaviour;
            this.variableIndex = variableIndex;
        }
#endif

        public LiteNetLibGameManager Manager
        {
            get { return behaviour.Manager; }
        }

        public SyncFieldInfo GetSyncFieldInfo()
        {
            return new SyncFieldInfo(Behaviour.ObjectId, Behaviour.BehaviourIndex, VariableIndex);
        }

        public virtual void Deserialize(NetDataReader reader) { }
        public virtual void Serialize(NetDataWriter writer) { }
    }

    public abstract class LiteNetLibSyncFieldBase<T> : LiteNetLibSyncFieldBase
    {
        [ReadOnly, SerializeField]
        private T value;
        public T Value
        {
            get { return value; }
            set
            {
                if (Behaviour == null || !Behaviour.IsServer)
                {
                    Debug.LogError("Sync field error, manager is empty or not the server");
                    return;
                }
                if (this.value == null || !this.value.Equals(value))
                    Manager.SendServerUpdateSyncField(this);
                this.value = value;
            }
        }

        public static implicit operator T(LiteNetLibSyncFieldBase<T> field)
        {
            return field.Value;
        }
    }
}
