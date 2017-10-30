using UnityEngine;
using LiteNetLib;
using LiteNetLib.Utils;
using System;

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
        public bool forOwnerOnly;
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

        public abstract void Deserialize(NetDataReader reader);
        public abstract void Serialize(NetDataWriter writer);
    }

    public abstract class LiteNetLibSyncField<TField, TFieldType> : LiteNetLibSyncField 
        where TField : LiteNetLibNetField<TFieldType>, new()
    {
        protected TField field;
        public TField Field
        {
            get
            {
                if (field == null)
                    field = new TField();
                return field;
            }
        }
        public TFieldType Value
        {
            get { return Field.Value; }
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
                    Field.Value = value;
                    Manager.SendServerUpdateSyncField(this);
                }
            }
        }

        public abstract bool IsValueChanged(TFieldType newValue);

        public static implicit operator TFieldType(LiteNetLibSyncField<TField, TFieldType> field)
        {
            return field.Value;
        }

        public override sealed void Deserialize(NetDataReader reader)
        {
            Field.Deserialize(reader);
        }

        public override sealed void Serialize(NetDataWriter writer)
        {
            Field.Serialize(writer);
        }
    }
}
