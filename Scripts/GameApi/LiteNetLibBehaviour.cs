using System;
using System.Collections.Generic;
using UnityEngine;
using LiteNetLib;
using LiteNetLib.Utils;
#if UNITY_EDITOR
using System.Reflection;
using UnityEditor;
#endif

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

        [ReadOnly, SerializeField]
        private List<LiteNetLibSyncField> syncFields = new List<LiteNetLibSyncField>();
        
        private readonly Dictionary<ushort, LiteNetLibFunction> netFunctions = new Dictionary<ushort, LiteNetLibFunction>();
        private readonly Dictionary<string, ushort> netFunctionIds = new Dictionary<string, ushort>();

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

        public void RegisterNetFunction(string id, LiteNetLibFunction netFunction)
        {
            if (netFunctionIds.ContainsKey(id))
            {
                if (Manager.LogError)
                    Debug.LogError("[" + name + "] [" + TypeName + "] cannot register net function with existed id [" + id + "].");
                return;
            }
            if (netFunctions.Count == ushort.MaxValue)
            {
                if (Manager.LogError)
                    Debug.LogError("[" + name + "] [" + TypeName + "] cannot register net function it's exceeds limit.");
                return;
            }
            var realId = Convert.ToUInt16(netFunctions.Count);
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
            var fieldId = info.fieldId;
            if (fieldId >= 0 && fieldId < syncFields.Count)
            {
                var syncField = syncFields[fieldId];
                syncField.Deserialize(reader);
                return syncField;
            }
            else
            {
                if (Manager.LogError)
                    Debug.LogError("[" + name + "] [" + TypeName + "] cannot process sync field, fieldId [" + fieldId + "] not found.");
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

        public void SendUpdateAllSyncFields()
        {
            var fields = syncFields;
            foreach (var field in fields)
            {
                field.SendUpdate();
            }
        }

        public void SendUpdateAllSyncFields(NetPeer peer)
        {
            var fields = syncFields;
            foreach (var field in fields)
            {
                field.SendUpdate(peer);
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            syncFields.Clear();
            FieldInfo[] fields = GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (var field in fields)
            {
                if (field.FieldType.IsSubclassOf(typeof(LiteNetLibSyncField)))
                {
                    var syncField = (LiteNetLibSyncField)field.GetValue(this);
                    syncField.OnRegister(this, Convert.ToUInt16(syncFields.Count));
                    syncFields.Add(syncField);
                }
            }
            EditorUtility.SetDirty(this);
        }
#endif
    }
}
