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

        private readonly Dictionary<ushort, LiteNetLibSyncFieldBase> syncFields = new Dictionary<ushort, LiteNetLibSyncFieldBase>();

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

        public void OnValidateNetworkFunctions(int behaviourIndex)
        {
            this.behaviourIndex = behaviourIndex;
        }

        public void RegisterSyncField(ushort id, LiteNetLibSyncFieldBase syncField)
        {
            syncField.OnValidateNetworkFunctions(this, id);
            syncFields[id] = syncField;
        }

        public void ProcessSyncField(SyncFieldInfo info, NetDataReader reader)
        {
            if (info.objectId != ObjectId)
                return;
            if (syncFields.ContainsKey(info.fieldId))
                syncFields[info.fieldId].Deserialize(reader);
        }
    }
}
