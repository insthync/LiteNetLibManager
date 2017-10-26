using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using LiteNetLib.Utils;

namespace LiteNetLibHighLevel
{
    [RequireComponent(typeof(LiteNetLibIdentity))]
    public class LiteNetLibBehaviour : MonoBehaviour
    {
        [ShowOnly, SerializeField]
        private int behaviourIndex;
        public int BehaviourIndex
        {
            get { return behaviourIndex; }
        }

        [SerializeField, HideInInspector]
        private List<LiteNetLibSyncFieldBase> syncFields = new List<LiteNetLibSyncFieldBase>();

        private LiteNetLibIdentity identity;
        public LiteNetLibIdentity Identity
        {
            get
            {
                if (identity == null)
                    identity = GetComponent<LiteNetLibIdentity>();
                return identity;
            }
        }

        public long ConnectId
        {
            get { return Identity.ConnectId; }
        }

        public uint ObjectId
        {
            get { return Identity.ObjectId; }
        }

        public LiteNetLibGameManager Manager
        {
            get { return Identity.Manager; }
        }

        public bool IsServer
        {
            get { return Identity.IsServer; }
        }

        public bool IsClient
        {
            get { return Identity.IsClient; }
        }

        public bool IsLocalClient
        {
            get { return Identity.IsLocalClient; }
        }

#if UNITY_EDITOR
        public virtual void OnValidateIdentity(int behaviourIndex)
        {
            this.behaviourIndex = behaviourIndex;
            syncFields.Clear();
            var baseType = typeof(LiteNetLibSyncFieldBase);
            var type = GetType();
            var fields = type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            foreach (var field in fields)
            {
                var fieldType = field.FieldType;
                if (fieldType.BaseType.BaseType == baseType)
                {
                    var fieldValue = field.GetValue(this) as LiteNetLibSyncFieldBase;
                    fieldValue.OnValidateIdentity(this, syncFields.Count);
                    syncFields.Add(fieldValue);
                }
            }
        }
#endif

        public void ProcessSyncField(SyncFieldInfo info, NetDataReader reader)
        {
            if (info.objectId != ObjectId)
                return;
            if (info.fieldIndex < 0 || info.fieldIndex >= syncFields.Count)
                return;
            syncFields[info.fieldIndex].Deserialize(reader);
        }
    }
}
