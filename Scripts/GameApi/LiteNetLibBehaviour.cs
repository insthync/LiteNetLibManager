using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using LiteNetLib;
using LiteNetLib.Utils;
using UnityEngine.Profiling;

namespace LiteNetLibManager
{
    public partial class LiteNetLibBehaviour : MonoBehaviour, INetSerializable
    {
        private struct CachingFieldName
        {
            public List<FieldInfo> syncFields;
            public List<FieldInfo> syncLists;
        }

        [LiteNetLibReadOnly, SerializeField]
        private byte behaviourIndex;
        public byte BehaviourIndex
        {
            get { return behaviourIndex; }
        }
        
        [Header("Behaviour sync options")]
        public DeliveryMethod sendOptions;
        [Tooltip("Interval to send network data")]
        [Range(0.01f, 2f)]
        public float sendInterval = 0.1f;
        public float SendRate
        {
            get { return 1f / sendInterval; }
        }

        private float lastSentTime;
        
        private static readonly Dictionary<string, CachingFieldName> CacheFieldNames = new Dictionary<string, CachingFieldName>();
        private static readonly Dictionary<string, FieldInfo> CacheSyncFieldInfos = new Dictionary<string, FieldInfo>();
        private static readonly Dictionary<string, FieldInfo> CacheSyncListInfos = new Dictionary<string, FieldInfo>();

        private readonly Dictionary<string, int> netFunctionIds = new Dictionary<string, int>();

        // Optimize garbage collector
        private List<FieldInfo> tempSyncFields;
        private List<FieldInfo> tempSyncLists;
        private CachingFieldName tempCachingFieldName;

        private Type classType;
        /// <summary>
        /// This will be used when setup sync fields and sync lists
        /// </summary>
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
        /// <summary>
        /// This will be used when setup sync fields and sync lists as key for cached fields
        /// </summary>
        public string TypeName
        {
            get
            {
                if (string.IsNullOrEmpty(typeName))
                    typeName = ClassType.Name;
                return typeName;
            }
        }

        private bool isFoundIdentity;
        private LiteNetLibIdentity identity;
        public LiteNetLibIdentity Identity
        {
            get
            {
                if (!isFoundIdentity)
                {
                    identity = GetComponent<LiteNetLibIdentity>();
                    if (identity == null)
                        identity = GetComponentInParent<LiteNetLibIdentity>();
                    isFoundIdentity = identity != null;
                }
                return identity;
            }
        }

        public long ConnectionId
        {
            get { return Identity.ConnectionId; }
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

        public bool IsOwnerClient
        {
            get { return Identity.IsOwnerClient; }
        }

        // Optimize garbage collector
        private int loopCounter;

        internal void NetworkUpdate(float time)
        {
            // Sync behaviour
            // TODO: For now, it's able to sync behaviour from server to clients only
            if (!IsServer)
                return;

            if (time - lastSentTime < sendInterval)
                return;

            lastSentTime = time;

            Profiler.BeginSample("LiteNetLibBehaviour - Update Sync Behaviour");
            if (ShouldSyncBehaviour())
            {
                foreach (long connectionId in Manager.GetConnectionIds())
                {
                    if (Identity.IsSubscribedOrOwning(connectionId))
                        Manager.ServerSendPacket(connectionId, sendOptions, LiteNetLibGameManager.GameMsgTypes.ServerSyncBehaviour, this);
                }
            }
            Profiler.EndSample();
        }

        public void Setup(byte behaviourIndex)
        {
            this.behaviourIndex = behaviourIndex;
            OnSetup();
            tempSyncFields = null;
            tempSyncLists = null;
            // Caching field names
            if (!CacheFieldNames.TryGetValue(TypeName, out tempCachingFieldName))
            {
                tempSyncFields = new List<FieldInfo>();
                tempSyncLists = new List<FieldInfo>();
                List<FieldInfo> fields = new List<FieldInfo>(GetFields(ClassType));
                fields.Sort((a, b) => a.Name.CompareTo(b.Name));
                foreach (FieldInfo field in fields)
                {
                    if (field.FieldType.IsSubclassOf(typeof(LiteNetLibSyncField)))
                        tempSyncFields.Add(field);
                    if (field.FieldType.IsSubclassOf(typeof(LiteNetLibSyncList)))
                        tempSyncLists.Add(field);
                }
                CacheFieldNames.Add(TypeName, new CachingFieldName()
                {
                    syncFields = tempSyncFields,
                    syncLists = tempSyncLists,
                });
            }
            else
            {
                tempSyncFields = tempCachingFieldName.syncFields;
                tempSyncLists = tempCachingFieldName.syncLists;
            }
            SetupSyncElements(tempSyncFields, Identity.syncFields);
            SetupSyncElements(tempSyncLists, Identity.syncLists);
        }

        #region RegisterSyncElements
        private List<FieldInfo> GetFields(Type type, List<FieldInfo> fields = null)
        {
            if (type == typeof(LiteNetLibBehaviour))
            {
                if (fields == null)
                    fields = new List<FieldInfo>();
                return fields;
            }

            if (fields == null)
            {
                fields = new List<FieldInfo>();
                // Get fields from inherit classes
                fields.AddRange(type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance));
            }
            else
            {
                // Get only non public fields from base classes
                // Because all public fields already found while get fields from inherit classes
                fields.AddRange(type.GetFields(BindingFlags.NonPublic | BindingFlags.Instance));
            }

            return GetFields(type.BaseType, fields);
        }

        private void SetupSyncElements<T>(List<FieldInfo> fields, List<T> elementList) where T : LiteNetLibElement
        {
            if (fields == null || fields.Count == 0)
                return;

            foreach (FieldInfo field in fields)
            {
                // Get field info
                try
                {
                    RegisterSyncElement((T)field.GetValue(this), elementList);
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            }
        }

        private void RegisterSyncElement<T>(T element, List<T> elementList) where T : LiteNetLibElement
        {
            int elementId = elementList.Count;
            element.Setup(this, elementId);
            elementList.Add(element);
        }

        public void RegisterSyncField<T>(T syncField) where T : LiteNetLibSyncField
        {
            RegisterSyncElement(syncField, Identity.syncFields);
        }

        public void RegisterSyncList<T>(T syncList) where T : LiteNetLibSyncList
        {
            RegisterSyncElement(syncList, Identity.syncLists);
        }
        #endregion

        #region RegisterNetFunction
        public void RegisterNetFunction(NetFunctionDelegate func)
        {
            RegisterNetFunction(func.Method.Name, new LiteNetLibFunction(func));
        }

        public void RegisterNetFunction<T1>(NetFunctionDelegate<T1> func)
        {
            RegisterNetFunction(func.Method.Name, new LiteNetLibFunction<T1>(func));
        }

        public void RegisterNetFunction<T1, T2>(NetFunctionDelegate<T1, T2> func)
        {
            RegisterNetFunction(func.Method.Name, new LiteNetLibFunction<T1, T2>(func));
        }

        public void RegisterNetFunction<T1, T2, T3>(NetFunctionDelegate<T1, T2, T3> func)
        {
            RegisterNetFunction(func.Method.Name, new LiteNetLibFunction<T1, T2, T3>(func));
        }

        public void RegisterNetFunction<T1, T2, T3, T4>(NetFunctionDelegate<T1, T2, T3, T4> func)
        {
            RegisterNetFunction(func.Method.Name, new LiteNetLibFunction<T1, T2, T3, T4>(func));
        }

        public void RegisterNetFunction<T1, T2, T3, T4, T5>(NetFunctionDelegate<T1, T2, T3, T4, T5> func)
        {
            RegisterNetFunction(func.Method.Name, new LiteNetLibFunction<T1, T2, T3, T4, T5>(func));
        }

        public void RegisterNetFunction<T1, T2, T3, T4, T5, T6>(NetFunctionDelegate<T1, T2, T3, T4, T5, T6> func)
        {
            RegisterNetFunction(func.Method.Name, new LiteNetLibFunction<T1, T2, T3, T4, T5, T6>(func));
        }

        public void RegisterNetFunction<T1, T2, T3, T4, T5, T6, T7>(NetFunctionDelegate<T1, T2, T3, T4, T5, T6, T7> func)
        {
            RegisterNetFunction(func.Method.Name, new LiteNetLibFunction<T1, T2, T3, T4, T5, T6, T7>(func));
        }

        public void RegisterNetFunction<T1, T2, T3, T4, T5, T6, T7, T8>(NetFunctionDelegate<T1, T2, T3, T4, T5, T6, T7, T8> func)
        {
            RegisterNetFunction(func.Method.Name, new LiteNetLibFunction<T1, T2, T3, T4, T5, T6, T7, T8>(func));
        }

        public void RegisterNetFunction<T1, T2, T3, T4, T5, T6, T7, T8, T9>(NetFunctionDelegate<T1, T2, T3, T4, T5, T6, T7, T8, T9> func)
        {
            RegisterNetFunction(func.Method.Name, new LiteNetLibFunction<T1, T2, T3, T4, T5, T6, T7, T8, T9>(func));
        }

        public void RegisterNetFunction<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(NetFunctionDelegate<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> func)
        {
            RegisterNetFunction(func.Method.Name, new LiteNetLibFunction<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(func));
        }
        #endregion

        public void RegisterNetFunction(string id, LiteNetLibFunction netFunction)
        {
            if (netFunctionIds.ContainsKey(id))
            {
                if (Manager.LogError)
                    Debug.LogError("[" + name + "] [" + TypeName + "] cannot register net function with existed id [" + id + "].");
                return;
            }
            if (Identity.netFunctions.Count >= int.MaxValue)
            {
                if (Manager.LogError)
                    Debug.LogError("[" + name + "] [" + TypeName + "] cannot register net function it's exceeds limit.");
                return;
            }
            int elementId = Identity.netFunctions.Count;
            netFunction.Setup(this, elementId);
            Identity.netFunctions.Add(netFunction);
            netFunctionIds[id] = elementId;
        }

        #region CallNetFunction with receivers and parameters
        public void CallNetFunction(NetFunctionDelegate func, FunctionReceivers receivers)
        {
            CallNetFunction(func.Method.Name, DeliveryMethod.ReliableOrdered, receivers);
        }

        public void CallNetFunction<T1>(NetFunctionDelegate<T1> func, FunctionReceivers receivers, T1 param1)
        {
            CallNetFunction(func.Method.Name, DeliveryMethod.ReliableOrdered, receivers, param1);
        }

        public void CallNetFunction<T1, T2>(NetFunctionDelegate<T1, T2> func, FunctionReceivers receivers, T1 param1, T2 param2)
        {
            CallNetFunction(func.Method.Name, DeliveryMethod.ReliableOrdered, receivers, param1, param2);
        }

        public void CallNetFunction<T1, T2, T3>(NetFunctionDelegate<T1, T2, T3> func, FunctionReceivers receivers, T1 param1, T2 param2, T3 param3)
        {
            CallNetFunction(func.Method.Name, DeliveryMethod.ReliableOrdered, receivers, param1, param2, param3);
        }

        public void CallNetFunction<T1, T2, T3, T4>(NetFunctionDelegate<T1, T2, T3, T4> func, FunctionReceivers receivers, T1 param1, T2 param2, T3 param3, T4 param4)
        {
            CallNetFunction(func.Method.Name, DeliveryMethod.ReliableOrdered, receivers, param1, param2, param3, param4);
        }

        public void CallNetFunction<T1, T2, T3, T4, T5>(NetFunctionDelegate<T1, T2, T3, T4, T5> func, FunctionReceivers receivers, T1 param1, T2 param2, T3 param3, T4 param4, T5 param5)
        {
            CallNetFunction(func.Method.Name, DeliveryMethod.ReliableOrdered, receivers, param1, param2, param3, param4, param5);
        }

        public void CallNetFunction<T1, T2, T3, T4, T5, T6>(NetFunctionDelegate<T1, T2, T3, T4, T5, T6> func, FunctionReceivers receivers, T1 param1, T2 param2, T3 param3, T4 param4, T5 param5, T6 param6)
        {
            CallNetFunction(func.Method.Name, DeliveryMethod.ReliableOrdered, receivers, param1, param2, param3, param4, param5, param6);
        }

        public void CallNetFunction<T1, T2, T3, T4, T5, T6, T7>(NetFunctionDelegate<T1, T2, T3, T4, T5, T6, T7> func, FunctionReceivers receivers, T1 param1, T2 param2, T3 param3, T4 param4, T5 param5, T6 param6, T7 param7)
        {
            CallNetFunction(func.Method.Name, DeliveryMethod.ReliableOrdered, receivers, param1, param2, param3, param4, param5, param6, param7);
        }

        public void CallNetFunction<T1, T2, T3, T4, T5, T6, T7, T8>(NetFunctionDelegate<T1, T2, T3, T4, T5, T6, T7, T8> func, FunctionReceivers receivers, T1 param1, T2 param2, T3 param3, T4 param4, T5 param5, T6 param6, T7 param7, T8 param8)
        {
            CallNetFunction(func.Method.Name, DeliveryMethod.ReliableOrdered, receivers, param1, param2, param3, param4, param5, param6, param7, param8);
        }

        public void CallNetFunction<T1, T2, T3, T4, T5, T6, T7, T8, T9>(NetFunctionDelegate<T1, T2, T3, T4, T5, T6, T7, T8, T9> func, FunctionReceivers receivers, T1 param1, T2 param2, T3 param3, T4 param4, T5 param5, T6 param6, T7 param7, T8 param8, T9 param9)
        {
            CallNetFunction(func.Method.Name, DeliveryMethod.ReliableOrdered, receivers, param1, param2, param3, param4, param5, param6, param7, param8, param9);
        }

        public void CallNetFunction<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(NetFunctionDelegate<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> func, FunctionReceivers receivers, T1 param1, T2 param2, T3 param3, T4 param4, T5 param5, T6 param6, T7 param7, T8 param8, T9 param9, T10 param10)
        {
            CallNetFunction(func.Method.Name, DeliveryMethod.ReliableOrdered, receivers, param1, param2, param3, param4, param5, param6, param7, param8, param9, param10);
        }
        #endregion

        #region CallNetFunction with delivery method, receivers and parameters
        public void CallNetFunction(NetFunctionDelegate func, DeliveryMethod deliveryMethod, FunctionReceivers receivers)
        {
            CallNetFunction(func.Method.Name, deliveryMethod, receivers);
        }

        public void CallNetFunction<T1>(NetFunctionDelegate<T1> func, DeliveryMethod deliveryMethod, FunctionReceivers receivers, T1 param1)
        {
            CallNetFunction(func.Method.Name, deliveryMethod, receivers, param1);
        }

        public void CallNetFunction<T1, T2>(NetFunctionDelegate<T1, T2> func, DeliveryMethod deliveryMethod, FunctionReceivers receivers, T1 param1, T2 param2)
        {
            CallNetFunction(func.Method.Name, deliveryMethod, receivers, param1, param2);
        }

        public void CallNetFunction<T1, T2, T3>(NetFunctionDelegate<T1, T2, T3> func, DeliveryMethod deliveryMethod, FunctionReceivers receivers, T1 param1, T2 param2, T3 param3)
        {
            CallNetFunction(func.Method.Name, deliveryMethod, receivers, param1, param2, param3);
        }

        public void CallNetFunction<T1, T2, T3, T4>(NetFunctionDelegate<T1, T2, T3, T4> func, DeliveryMethod deliveryMethod, FunctionReceivers receivers, T1 param1, T2 param2, T3 param3, T4 param4)
        {
            CallNetFunction(func.Method.Name, deliveryMethod, receivers, param1, param2, param3, param4);
        }

        public void CallNetFunction<T1, T2, T3, T4, T5>(NetFunctionDelegate<T1, T2, T3, T4, T5> func, DeliveryMethod deliveryMethod, FunctionReceivers receivers, T1 param1, T2 param2, T3 param3, T4 param4, T5 param5)
        {
            CallNetFunction(func.Method.Name, deliveryMethod, receivers, param1, param2, param3, param4, param5);
        }

        public void CallNetFunction<T1, T2, T3, T4, T5, T6>(NetFunctionDelegate<T1, T2, T3, T4, T5, T6> func, DeliveryMethod deliveryMethod, FunctionReceivers receivers, T1 param1, T2 param2, T3 param3, T4 param4, T5 param5, T6 param6)
        {
            CallNetFunction(func.Method.Name, deliveryMethod, receivers, param1, param2, param3, param4, param5, param6);
        }

        public void CallNetFunction<T1, T2, T3, T4, T5, T6, T7>(NetFunctionDelegate<T1, T2, T3, T4, T5, T6, T7> func, DeliveryMethod deliveryMethod, FunctionReceivers receivers, T1 param1, T2 param2, T3 param3, T4 param4, T5 param5, T6 param6, T7 param7)
        {
            CallNetFunction(func.Method.Name, deliveryMethod, receivers, param1, param2, param3, param4, param5, param6, param7);
        }

        public void CallNetFunction<T1, T2, T3, T4, T5, T6, T7, T8>(NetFunctionDelegate<T1, T2, T3, T4, T5, T6, T7, T8> func, DeliveryMethod deliveryMethod, FunctionReceivers receivers, T1 param1, T2 param2, T3 param3, T4 param4, T5 param5, T6 param6, T7 param7, T8 param8)
        {
            CallNetFunction(func.Method.Name, deliveryMethod, receivers, param1, param2, param3, param4, param5, param6, param7, param8);
        }

        public void CallNetFunction<T1, T2, T3, T4, T5, T6, T7, T8, T9>(NetFunctionDelegate<T1, T2, T3, T4, T5, T6, T7, T8, T9> func, DeliveryMethod deliveryMethod, FunctionReceivers receivers, T1 param1, T2 param2, T3 param3, T4 param4, T5 param5, T6 param6, T7 param7, T8 param8, T9 param9)
        {
            CallNetFunction(func.Method.Name, deliveryMethod, receivers, param1, param2, param3, param4, param5, param6, param7, param8, param9);
        }

        public void CallNetFunction<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(NetFunctionDelegate<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> func, DeliveryMethod deliveryMethod, FunctionReceivers receivers, T1 param1, T2 param2, T3 param3, T4 param4, T5 param5, T6 param6, T7 param7, T8 param8, T9 param9, T10 param10)
        {
            CallNetFunction(func.Method.Name, deliveryMethod, receivers, param1, param2, param3, param4, param5, param6, param7, param8, param9, param10);
        }
        #endregion

        public void CallNetFunction(string id, DeliveryMethod deliveryMethod, FunctionReceivers receivers, params object[] parameters)
        {
            int elementId;
            if (netFunctionIds.TryGetValue(id, out elementId))
            {
                Identity.netFunctions[elementId].Call(deliveryMethod, receivers, parameters);
            }
            else
            {
                if (Manager.LogError)
                    Debug.LogError("[" + name + "] [" + TypeName + "] cannot call function, function [" + id + "] not found.");
            }
        }

        #region CallNetFunction with connectionId and parameters, for call function at target connection Id only
        public void CallNetFunction(NetFunctionDelegate func, long connectionId)
        {
            CallNetFunction(func.Method.Name, connectionId);
        }

        public void CallNetFunction<T1>(NetFunctionDelegate<T1> func, long connectionId, T1 param1)
        {
            CallNetFunction(func.Method.Name, connectionId, param1);
        }

        public void CallNetFunction<T1, T2>(NetFunctionDelegate<T1, T2> func, long connectionId, T1 param1, T2 param2)
        {
            CallNetFunction(func.Method.Name, connectionId, param1, param2);
        }

        public void CallNetFunction<T1, T2, T3>(NetFunctionDelegate<T1, T2, T3> func, long connectionId, T1 param1, T2 param2, T3 param3)
        {
            CallNetFunction(func.Method.Name, connectionId, param1, param2, param3);
        }

        public void CallNetFunction<T1, T2, T3, T4>(NetFunctionDelegate<T1, T2, T3, T4> func, long connectionId, T1 param1, T2 param2, T3 param3, T4 param4)
        {
            CallNetFunction(func.Method.Name, connectionId, param1, param2, param3, param4);
        }

        public void CallNetFunction<T1, T2, T3, T4, T5>(NetFunctionDelegate<T1, T2, T3, T4, T5> func, long connectionId, T1 param1, T2 param2, T3 param3, T4 param4, T5 param5)
        {
            CallNetFunction(func.Method.Name, connectionId, param1, param2, param3, param4, param5);
        }

        public void CallNetFunction<T1, T2, T3, T4, T5, T6>(NetFunctionDelegate<T1, T2, T3, T4, T5, T6> func, long connectionId, T1 param1, T2 param2, T3 param3, T4 param4, T5 param5, T6 param6)
        {
            CallNetFunction(func.Method.Name, connectionId, param1, param2, param3, param4, param5, param6);
        }

        public void CallNetFunction<T1, T2, T3, T4, T5, T6, T7>(NetFunctionDelegate<T1, T2, T3, T4, T5, T6, T7> func, long connectionId, T1 param1, T2 param2, T3 param3, T4 param4, T5 param5, T6 param6, T7 param7)
        {
            CallNetFunction(func.Method.Name, connectionId, param1, param2, param3, param4, param5, param6, param7);
        }

        public void CallNetFunction<T1, T2, T3, T4, T5, T6, T7, T8>(NetFunctionDelegate<T1, T2, T3, T4, T5, T6, T7, T8> func, long connectionId, T1 param1, T2 param2, T3 param3, T4 param4, T5 param5, T6 param6, T7 param7, T8 param8)
        {
            CallNetFunction(func.Method.Name, connectionId, param1, param2, param3, param4, param5, param6, param7, param8);
        }

        public void CallNetFunction<T1, T2, T3, T4, T5, T6, T7, T8, T9>(NetFunctionDelegate<T1, T2, T3, T4, T5, T6, T7, T8, T9> func, long connectionId, T1 param1, T2 param2, T3 param3, T4 param4, T5 param5, T6 param6, T7 param7, T8 param8, T9 param9)
        {
            CallNetFunction(func.Method.Name, connectionId, param1, param2, param3, param4, param5, param6, param7, param8, param9);
        }

        public void CallNetFunction<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(NetFunctionDelegate<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> func, long connectionId, T1 param1, T2 param2, T3 param3, T4 param4, T5 param5, T6 param6, T7 param7, T8 param8, T9 param9, T10 param10)
        {
            CallNetFunction(func.Method.Name, connectionId, param1, param2, param3, param4, param5, param6, param7, param8, param9, param10);
        }
        #endregion

        public void CallNetFunction(string id, long connectionId, params object[] parameters)
        {
            int elementId;
            if (netFunctionIds.TryGetValue(id, out elementId))
            {
                Identity.netFunctions[elementId].Call(connectionId, parameters);
            }
            else
            {
                if (Manager.LogError)
                    Debug.LogError("[" + name + "] [" + TypeName + "] cannot call function, function [" + id + "] not found.");
            }
        }

        public void Serialize(NetDataWriter writer)
        {
            if (!IsServer)
                return;

            writer.PutPackedUInt(Identity.ObjectId);
            writer.Put(BehaviourIndex);
            OnSerialize(writer);
        }

        public void Deserialize(NetDataReader reader)
        {
            OnDeserialize(reader);
        }

        public void NetworkDestroy()
        {
            Identity.NetworkDestroy();
        }

        public void NetworkDestroy(float delay)
        {
            Identity.NetworkDestroy(delay);
        }

        /// <summary>
        /// This function will be called when this client has been verified as owner client
        /// </summary>
        public virtual void OnSetOwnerClient(bool isOwnerClient) { }

        /// <summary>
        /// This function will be called when object destroy from server
        /// </summary>
        /// <param name="reasons"></param>
        public virtual void OnNetworkDestroy(byte reasons) { }

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
            return false;
        }

        /// <summary>
        /// Override this function to write custom data to send from server to client
        /// </summary>
        public virtual void OnSerialize(NetDataWriter writer) { }

        /// <summary>
        /// Override this function to read data from server at client
        /// </summary>
        public virtual void OnDeserialize(NetDataReader reader) { }

        /// <summary>
        /// Override this function to change object visibility when this added to player as subcribing 
        /// </summary>
        public virtual void OnServerSubscribingAdded() { }

        /// <summary>
        /// Override this function to change object visibility when this removed from player as subcribing 
        /// </summary>
        public virtual void OnServerSubscribingRemoved() { }
    }
}
