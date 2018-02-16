using System;
using System.Collections.Generic;
using System.Reflection;
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
        
        private readonly List<LiteNetLibSyncField> syncFields = new List<LiteNetLibSyncField>();
        private readonly List<LiteNetLibFunction> netFunctions = new List<LiteNetLibFunction>();
        private readonly Dictionary<string, ushort> netFunctionIds = new Dictionary<string, ushort>();
        private readonly List<LiteNetLibSyncList> syncLists = new List<LiteNetLibSyncList>();

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

        internal void NetworkUpdate()
        {
            if (!IsServer)
                return;

            foreach (var syncField in syncFields)
            {
                syncField.NetworkUpdate();
            }
        }

        public void ValidateBehaviour(int behaviourIndex)
        {
            this.behaviourIndex = behaviourIndex;
            syncFields.Clear();
            syncLists.Clear();
            var fields = new List<FieldInfo>(GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance));
            fields.Sort((a, b) => a.Name.CompareTo(b.Name));
            foreach (var field in fields)
            {
                if (field.FieldType.IsSubclassOf(typeof(LiteNetLibSyncField)))
                {
                    var syncField = (LiteNetLibSyncField)field.GetValue(this);
                    var elementId = Convert.ToUInt16(syncFields.Count);
                    syncField.Setup(this, elementId);
                    syncFields.Add(syncField);
                }
                if (field.FieldType.IsSubclassOf(typeof(LiteNetLibSyncList)))
                {
                    var syncList = (LiteNetLibSyncList)field.GetValue(this);
                    var elementId = Convert.ToUInt16(syncLists.Count);
                    syncList.Setup(this, elementId);
                    syncLists.Add(syncList);
                }
            }
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
            var elementId = Convert.ToUInt16(netFunctions.Count);
            netFunction.Setup(this, elementId);
            netFunctions.Add(netFunction);
            netFunctionIds[id] = elementId;
        }

        public void CallNetFunction(string id, FunctionReceivers receivers, params object[] parameters)
        {
            ushort elementId;
            if (netFunctionIds.TryGetValue(id, out elementId))
            {
                var syncFunction = netFunctions[elementId];
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
            ushort elementId;
            if (netFunctionIds.TryGetValue(id, out elementId))
            {
                var syncFunction = netFunctions[elementId];
                syncFunction.Call(connectId, parameters);
            }
            else
            {
                if (Manager.LogError)
                    Debug.LogError("[" + name + "] [" + TypeName + "] cannot call function, function [" + id + "] not found.");
            }
        }

        public LiteNetLibSyncField ProcessSyncField(LiteNetLibElementInfo info, NetDataReader reader)
        {
            if (info.objectId != ObjectId)
                return null;
            var elementId = info.elementId;
            if (elementId >= 0 && elementId < syncFields.Count)
            {
                var syncField = syncFields[elementId];
                syncField.DeserializeValue(reader);
                return syncField;
            }
            else
            {
                if (Manager.LogError)
                    Debug.LogError("[" + name + "] [" + TypeName + "] cannot process sync field, fieldId [" + elementId + "] not found.");
            }
            return null;
        }

        public LiteNetLibFunction ProcessNetFunction(LiteNetLibElementInfo info, NetDataReader reader, bool hookCallback)
        {
            if (info.objectId != ObjectId)
                return null;
            var elementId = info.elementId;
            if (elementId >= 0 && elementId < netFunctions.Count)
            {
                var netFunction = netFunctions[elementId];
                netFunction.DeserializeParameters(reader);
                if (hookCallback)
                    netFunction.HookCallback();
                return netFunction;
            }
            else
            {
                if (Manager.LogError)
                    Debug.LogError("[" + name + "] [" + TypeName + "] cannot process net function, functionId [" + info.elementId + "] not found.");
            }
            return null;
        }

        public LiteNetLibSyncList ProcessSyncList(LiteNetLibElementInfo info, NetDataReader reader)
        {
            if (info.objectId != ObjectId)
                return null;
            var elementId = info.elementId;
            if (elementId >= 0 && elementId < syncLists.Count)
            {
                var syncList = syncLists[elementId];
                syncList.DeserializeOperation(reader);
                return syncList;
            }
            else
            {
                if (Manager.LogError)
                    Debug.LogError("[" + name + "] [" + TypeName + "] cannot process sync field, fieldId [" + elementId + "] not found.");
            }
            return null;
        }

        public void SendInitSyncFields()
        {
            var fields = syncFields;
            foreach (var field in fields)
            {
                field.SendUpdate();
            }
        }

        public void SendInitSyncFields(NetPeer peer)
        {
            var fields = syncFields;
            foreach (var field in fields)
            {
                field.SendUpdate(peer);
            }
        }

        public void SendInitSyncLists()
        {
            var lists = syncLists;
            foreach (var list in lists)
            {
                for (var i = 0; i < list.Count; ++i)
                    list.SendOperation(LiteNetLibSyncList.Operation.Insert, i);
            }
        }

        public void SendInitSyncLists(NetPeer peer)
        {
            var lists = syncLists;
            foreach (var list in lists)
            {
                for (var i = 0; i < list.Count; ++i)
                    list.SendOperation(peer, LiteNetLibSyncList.Operation.Insert, i);
            }
        }
    }
}
