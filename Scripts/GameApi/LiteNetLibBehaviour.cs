using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using LiteNetLib;
using LiteNetLib.Utils;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace LiteNetLibHighLevel
{
    [RequireComponent(typeof(LiteNetLibIdentity))]
    public class LiteNetLibBehaviour : MonoBehaviour, ILiteNetLibMessage
    {
        [ReadOnly, SerializeField]
        private ushort behaviourIndex;
        public ushort BehaviourIndex
        {
            get { return behaviourIndex; }
        }

        [ReadOnly, SerializeField]
        private List<string> syncFieldNames = new List<string>();
        [ReadOnly, SerializeField]
        private List<string> syncListNames = new List<string>();
        [Header("Behaviour sync options")]
        public SendOptions sendOptions;
        [Tooltip("Interval to send network data")]
        [Range(0f, 2f)]
        public float sendInterval = 0.1f;

        private float lastSentTime;

        private static Dictionary<string, FieldInfo> CacheSyncFieldInfos = new Dictionary<string, FieldInfo>();
        private static Dictionary<string, FieldInfo> CacheSyncListInfos = new Dictionary<string, FieldInfo>();

        private readonly List<LiteNetLibSyncField> syncFields = new List<LiteNetLibSyncField>();
        private readonly List<LiteNetLibFunction> netFunctions = new List<LiteNetLibFunction>();
        private readonly Dictionary<string, ushort> netFunctionIds = new Dictionary<string, ushort>();
        private readonly List<LiteNetLibSyncList> syncLists = new List<LiteNetLibSyncList>();

        private Type classType;
        public Type ClassType
        {
            get
            {
                if (classType == null)
                    classType = GetType();
                return classType;
            }
        }

        private string typeName;
        public string TypeName
        {
            get
            {
                if (string.IsNullOrEmpty(typeName))
                    typeName = ClassType.Name;
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

            // Sync behaviour
            if (Time.realtimeSinceStartup - lastSentTime < sendInterval)
                return;

            lastSentTime = Time.realtimeSinceStartup;

            if (ShouldSyncBehaviour())
            {
                var peerValues = Manager.Peers.Values;
                foreach (var peer in peerValues)
                {
                    if (Identity.IsSubscribedOrOwning(peer.ConnectId))
                        Manager.SendPacket(sendOptions, peer, LiteNetLibGameManager.GameMsgTypes.ServerSyncBehaviour, this);
                }
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            syncFieldNames.Clear();
            syncListNames.Clear();
            var fields = new List<FieldInfo>(ClassType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance));
            fields.Sort((a, b) => a.Name.CompareTo(b.Name));
            foreach (var field in fields)
            {
                if (field.FieldType.IsSubclassOf(typeof(LiteNetLibSyncField)))
                    syncFieldNames.Add(field.Name);
                if (field.FieldType.IsSubclassOf(typeof(LiteNetLibSyncList)))
                    syncListNames.Add(field.Name);
            }
            EditorUtility.SetDirty(this);
            OnBehaviourValidate();
        }
#endif

        public void Setup(ushort behaviourIndex)
        {
            this.behaviourIndex = behaviourIndex;
            SetupSyncElements(syncFieldNames, CacheSyncFieldInfos, syncFields);
            SetupSyncElements(syncListNames, CacheSyncListInfos, syncLists);
            OnSetup();
        }

        private void SetupSyncElements<T>(List<string> fieldNames, Dictionary<string, FieldInfo> cache, List<T> elementList) where T : LiteNetLibElement
        {
            elementList.Clear();
            foreach (var fieldName in fieldNames)
            {
                var key = TypeName + "_" + fieldName;
                FieldInfo field;
                if (!cache.TryGetValue(key, out field))
                {
                    field = ClassType.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    cache[key] = field;
                }
                if (field == null)
                {
                    Debug.LogWarning("Element named " + fieldName + " was not found");
                    continue;
                }
                var syncList = (T)field.GetValue(this);
                var elementId = Convert.ToUInt16(elementList.Count);
                syncList.Setup(this, elementId);
                elementList.Add(syncList);
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
                syncField.Deserialize(reader);
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

        public void Serialize(NetDataWriter writer)
        {
            if (!IsServer)
                return;

            writer.Put(Identity.ObjectId);
            writer.Put(BehaviourIndex);
            OnSerialize(writer);
        }

        public void Deserialize(NetDataReader reader)
        {
            OnDeserialize(reader);
        }

        /// <summary>
        /// This function will be called when function OnValidate() have been called in edior
        /// </summary>
        public virtual void OnBehaviourValidate() { }

        /// <summary>
        /// This function will be called when this behaviour have been setup by identity
        /// You may do some initialize things within this function
        /// </summary>
        public virtual void OnSetup() { }

        /// <summary>
        /// Override this function to decides that old object should add new object as subscriber or not
        /// </summary>
        /// <param name="subscriber"></param>
        public virtual bool ShouldAddSubscriber(LiteNetLibPlayer subscriber)
        {
            return true;
        }

        /// <summary>
        /// This will be called by Identity when rebuild subscribers
        /// will return TRUE if subscribers have to rebuild
        /// you can override this function to create your own interest management
        /// </summary>
        /// <param name="subscribers"></param>
        /// <param name="initialize"></param>
        /// <returns></returns>
        public virtual bool OnRebuildSubscribers(HashSet<LiteNetLibPlayer> subscribers, bool initialize)
        {
            return false;
        }

        /// <summary>
        /// Override this function to make condition to write custom data to client
        /// </summary>
        /// <returns></returns>
        public virtual bool ShouldSyncBehaviour()
        {
            return true;
        }

        /// <summary>
        /// Override this function to write custom data to send from server to client
        /// </summary>
        public virtual void OnSerialize(NetDataWriter writer) { }

        /// <summary>
        /// Override this function to read data from server at client
        /// </summary>
        public virtual void OnDeserialize(NetDataReader reader) { }
    }
}
