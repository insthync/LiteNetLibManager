using System;
using System.Collections.Generic;
using UnityEngine;
using LiteNetLib;
using LiteNetLib.Utils;

namespace LiteNetLibHighLevel
{
    [RequireComponent(typeof(LiteNetLibIdentity))]
    public class LiteNetLibBehaviour : MonoBehaviour
    {
        [ReadOnly, SerializeField]
        private int behaviourIndex;
        public int BehaviourIndex
        {
            get { return behaviourIndex; }
        }

        private readonly Dictionary<ushort, LiteNetLibSyncField> syncFields = new Dictionary<ushort, LiteNetLibSyncField>();
        private readonly Dictionary<ushort, LiteNetLibFunction> netFunctions = new Dictionary<ushort, LiteNetLibFunction>();

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

        public void RegisterSyncField(ushort id, LiteNetLibSyncField syncField)
        {
            syncField.OnRegister(this, id);
            syncFields[id] = syncField;
        }

        public void RegisterNetFunction(ushort id, LiteNetLibFunction netFunction)
        {
            netFunction.OnRegister(this, id);
            netFunctions[id] = netFunction;
        }

        public void CallNetFunction(ushort id, SendOptions sendOptions, params object[] parameters)
        {
            if (netFunctions.ContainsKey(id))
            {
                var syncFunction = netFunctions[id];
                syncFunction.Call(sendOptions, parameters);
            }
        }

        public void ProcessSyncField(SyncFieldInfo info, NetDataReader reader)
        {
            if (info.objectId != ObjectId)
                return;
            if (syncFields.ContainsKey(info.fieldId))
            {
                var syncField = syncFields[info.fieldId];
                syncField.Deserialize(reader);
            }
        }

        public void ProcessNetFunction(SyncFunctionInfo info, NetDataReader reader)
        {
            if (info.objectId != ObjectId)
                return;
            if (netFunctions.ContainsKey(info.functionId))
            {
                var syncFunction = netFunctions[info.functionId];
                syncFunction.Deserialize(reader);
                syncFunction.HookCallback();
            }
        }
    }
}
