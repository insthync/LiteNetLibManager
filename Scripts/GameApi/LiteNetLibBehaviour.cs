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
        private readonly Dictionary<string, ushort> syncFieldIds = new Dictionary<string, ushort>();
        private readonly Dictionary<ushort, LiteNetLibFunction> netFunctions = new Dictionary<ushort, LiteNetLibFunction>();
        private readonly Dictionary<string, ushort> netFunctionIds = new Dictionary<string, ushort>();
        private ushort syncFieldIdCounter = 0;
        private ushort netFunctionIdCounter = 0;

        private string typeName;
        public string TypeName
        {
            get
            {
                if (string.IsNullOrEmpty(typeName))
                    typeName = GetType().Name;
                return typeName;
            }
        }

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

        public void ValidateBehaviour(int behaviourIndex)
        {
            this.behaviourIndex = behaviourIndex;
        }

        public void RegisterSyncField(string id, LiteNetLibSyncField syncField)
        {
            if (netFunctionIds.ContainsKey(id))
            {
                if (Manager.LogError)
                    Debug.LogError("[" + name + "] [" + TypeName + "] cannot register sync field with existed id (" + id + ").");
                return;
            }
            if (netFunctionIdCounter == ushort.MaxValue)
            {
                if (Manager.LogError)
                    Debug.LogError("[" + name + "] [" + TypeName + "] cannot register sync field it's exceeds limit.");
                return;
            }
            var realId = ++syncFieldIdCounter;
            syncField.OnRegister(this, realId);
            syncFields[realId] = syncField;
            syncFieldIds[id] = realId;
        }

        public void RegisterNetFunction(string id, LiteNetLibFunction netFunction)
        {
            if (netFunctionIds.ContainsKey(id))
            {
                if (Manager.LogError)
                    Debug.LogError("[" + name + "] [" + TypeName + "] cannot register net function with existed id [" + id + "].");
                return;
            }
            if (netFunctionIdCounter == ushort.MaxValue)
            {
                if (Manager.LogError)
                    Debug.LogError("[" + name + "] [" + TypeName + "] cannot register net function it's exceeds limit.");
                return;
            }
            var realId = ++netFunctionIdCounter;
            netFunction.OnRegister(this, realId);
            netFunctions[realId] = netFunction;
            netFunctionIds[id] = realId;
        }

        public void CallNetFunction(string id, FunctionReceivers receivers, params object[] parameters)
        {
            ushort realId;
            if (netFunctionIds.TryGetValue(id, out realId))
            {
                var syncFunction = netFunctions[realId];
                syncFunction.Call(receivers, parameters);
            }
            else
            {
                if (Manager.LogError)
                    Debug.LogError("[" + name + "] [" + TypeName + "] cannot call function, function [" + id + "] not found.");
            }
        }

        public void CallNetFunction(string id, long connectId, params object[] parameters)
        {
            ushort realId;
            if (netFunctionIds.TryGetValue(id, out realId))
            {
                var syncFunction = netFunctions[realId];
                syncFunction.Call(connectId, parameters);
            }
            else
            {
                if (Manager.LogError)
                    Debug.LogError("[" + name + "] [" + TypeName + "] cannot call function, function [" + id + "] not found.");
            }
        }

        public LiteNetLibSyncField ProcessSyncField(SyncFieldInfo info, NetDataReader reader)
        {
            if (info.objectId != ObjectId)
                return null;
            if (syncFields.ContainsKey(info.fieldId))
            {
                var syncField = syncFields[info.fieldId];
                syncField.Deserialize(reader);
                return syncField;
            }
            else
            {
                if (Manager.LogError)
                    Debug.LogError("[" + name + "] [" + TypeName + "] cannot process sync field, fieldId [" + info.fieldId + "] not found.");
            }
            return null;
        }

        public LiteNetLibFunction ProcessNetFunction(NetFunctionInfo info, NetDataReader reader, bool hookCallback)
        {
            if (info.objectId != ObjectId)
                return null;
            if (netFunctions.ContainsKey(info.functionId))
            {
                var netFunction = netFunctions[info.functionId];
                netFunction.Deserialize(reader);
                if (hookCallback)
                    netFunction.HookCallback();
                return netFunction;
            }
            else
            {
                if (Manager.LogError)
                    Debug.LogError("[" + name + "] [" + TypeName + "] cannot process net function, functionId [" + info.functionId + "] not found.");
            }
            return null;
        }
    }
}
