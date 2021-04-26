using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using UnityEngine;
using LiteNetLib;
using LiteNetLib.Utils;
using UnityEngine.Profiling;
using System.Text;

namespace LiteNetLibManager
{
    public partial class LiteNetLibBehaviour : MonoBehaviour, INetSerializable
    {
        private struct CacheFields
        {
            public List<FieldInfo> syncFields;
            public List<FieldInfo> syncLists;
            public List<FieldInfo> syncFieldsWithAttribute;
        }

        private struct CacheFunctions
        {
            public List<MethodInfo> functions;
            public List<MethodInfo> functionsCanCallByEveryone;
        }

        [ReadOnly, SerializeField]
        private byte behaviourIndex;
        public byte BehaviourIndex
        {
            get { return behaviourIndex; }
        }

        [Header("Behaviour Sync Options")]
        public byte dataChannel;
        public DeliveryMethod sendOptions;
        [Tooltip("Interval to send network data")]
        [Range(0.01f, 2f)]
        public float sendInterval = 0.1f;
        /// <summary>
        /// How many times per second it will sync behaviour
        /// </summary>
        public float SendRate
        {
            get { return 1f / sendInterval; }
        }

        private float sendCountDown;

        private static readonly Dictionary<string, CacheFields> CacheSyncElements = new Dictionary<string, CacheFields>();
        private static readonly Dictionary<string, CacheFunctions> CacheElasticRpcs = new Dictionary<string, CacheFunctions>();
        private static readonly Dictionary<string, CacheFunctions> CacheTargetRpcs = new Dictionary<string, CacheFunctions>();
        private static readonly Dictionary<string, CacheFunctions> CacheAllRpcs = new Dictionary<string, CacheFunctions>();
        private static readonly Dictionary<string, CacheFunctions> CacheServerRpcs = new Dictionary<string, CacheFunctions>();
        private static readonly Dictionary<string, MethodInfo> CacheHookFunctions = new Dictionary<string, MethodInfo>();
        private static readonly Dictionary<string, Type[]> CacheDyncnamicFunctionTypes = new Dictionary<string, Type[]>();

        private readonly Dictionary<string, int> targetRpcIds = new Dictionary<string, int>();
        private readonly Dictionary<string, int> allRpcIds = new Dictionary<string, int>();
        private readonly Dictionary<string, int> serverRpcIds = new Dictionary<string, int>();

        // Optimize garbage collector
        private Type tempLookupType;
        private HashSet<string> tempLookupNames = new HashSet<string>();
        private FieldInfo[] tempLookupFields;
        private MethodInfo[] tempLookupMethods;

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

        /// <summary>
        /// This will be used when setup sync fields and sync lists as key for cached fields
        /// </summary>
        public string TypeName
        {
            get { return ClassType.FullName; }
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

        public bool IsOwnerClientOrOwnedByServer
        {
            get { return Identity.IsOwnerClientOrOwnedByServer; }
        }

        public bool IsSceneObject
        {
            get { return Identity.IsSceneObject; }
        }

        private string logTag;
        public virtual string LogTag
        {
            get
            {
                if (string.IsNullOrEmpty(logTag))
                    logTag = $"{Manager.LogTag}->{name}({GetType().Name})";
                return logTag;
            }
        }

        internal void NetworkUpdate(float deltaTime)
        {
            // Sync behaviour
            if (!IsServer || !CanSyncBehaviour())
                return;

            // It's time to send update?
            sendCountDown -= deltaTime;
            if (sendCountDown > 0)
                return;

            // Set count down
            sendCountDown = sendInterval;

            Profiler.BeginSample("LiteNetLibBehaviour - Update Sync Behaviour");
            if (ShouldSyncBehaviour())
            {
                foreach (long connectionId in Manager.GetConnectionIds())
                {
                    if (Identity.HasSubscriberOrIsOwning(connectionId))
                        Manager.ServerSendPacket(connectionId, dataChannel, sendOptions, GameMsgTypes.ServerSyncBehaviour, this);
                }
            }
            Profiler.EndSample();
        }

        public void Setup(byte behaviourIndex)
        {
            this.behaviourIndex = behaviourIndex;
            OnSetup();
            CacheElements();
            CacheRpcs<ElasticRpcAttribute>(serverRpcIds, CacheElasticRpcs);
            CacheRpcs<ElasticRpcAttribute>(allRpcIds, CacheElasticRpcs);
            CacheRpcs<ElasticRpcAttribute>(targetRpcIds, CacheElasticRpcs);
            CacheRpcs<ServerRpcAttribute>(serverRpcIds, CacheServerRpcs);
            CacheRpcs<AllRpcAttribute>(allRpcIds, CacheAllRpcs);
            CacheRpcs<TargetRpcAttribute>(targetRpcIds, CacheTargetRpcs);
        }

        private void CacheElements()
        {
            CacheFields tempCacheFields;
            if (!CacheSyncElements.TryGetValue(TypeName, out tempCacheFields))
            {
                tempCacheFields = new CacheFields()
                {
                    syncFields = new List<FieldInfo>(),
                    syncLists = new List<FieldInfo>(),
                    syncFieldsWithAttribute = new List<FieldInfo>()
                };
                tempLookupNames.Clear();
                tempLookupType = ClassType;
                SyncFieldAttribute tempAttribute = null;
                // Find for sync field and sync list from the class
                while (tempLookupType != null && tempLookupType != typeof(LiteNetLibBehaviour))
                {
                    tempLookupFields = tempLookupType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    foreach (FieldInfo lookupField in tempLookupFields)
                    {
                        // Avoid duplicate fields
                        if (tempLookupNames.Contains(lookupField.Name))
                            continue;

                        if (lookupField.FieldType.IsSubclassOf(typeof(LiteNetLibSyncField)))
                        {
                            tempCacheFields.syncFields.Add(lookupField);
                        }
                        else if (lookupField.FieldType.IsSubclassOf(typeof(LiteNetLibSyncList)))
                        {
                            tempCacheFields.syncLists.Add(lookupField);
                        }
                        else
                        {
                            // Must have [SyncField] attribute
                            tempAttribute = lookupField.GetCustomAttribute<SyncFieldAttribute>();
                            if (tempAttribute != null)
                                tempCacheFields.syncFieldsWithAttribute.Add(lookupField);
                        }

                        tempLookupNames.Add(lookupField.Name);
                    }
                    tempLookupType = tempLookupType.BaseType;
                }
                // Sort name to make sure the fields will be sync correctly by its index
                tempCacheFields.syncFields.Sort((a, b) => a.Name.CompareTo(b.Name));
                tempCacheFields.syncLists.Sort((a, b) => a.Name.CompareTo(b.Name));
                CacheSyncElements.Add(TypeName, tempCacheFields);
            }
            SetupSyncElements(tempCacheFields.syncFields, Identity.SyncFields);
            SetupSyncElements(tempCacheFields.syncLists, Identity.SyncLists);
            SetupSyncFieldsWithAttribute(tempCacheFields.syncFieldsWithAttribute);
        }

        private void SetupSyncElements<T>(List<FieldInfo> fields, List<T> elementList) where T : LiteNetLibElement
        {
            if (fields == null || fields.Count == 0)
                return;

            foreach (FieldInfo field in fields)
            {
                try
                {
                    RegisterSyncElement((T)field.GetValue(this), elementList);
                }
                catch (Exception ex)
                {
                    if (Manager.LogFatal)
                        Logging.LogException(LogTag, ex);
                }
            }
        }

        private void SetupSyncFieldsWithAttribute(List<FieldInfo> fieldInfos)
        {
            if (fieldInfos == null || fieldInfos.Count == 0)
                return;

            SyncFieldAttribute tempAttribute;
            LiteNetLibSyncField tempSyncField;
            MethodInfo tempOnChangeMethod;
            ParameterInfo[] tempOnChangeMethodParams;
            string tempHookFunctionKey;
            foreach (FieldInfo fieldInfo in fieldInfos)
            {
                try
                {
                    tempAttribute = fieldInfo.GetCustomAttribute<SyncFieldAttribute>();
                    tempOnChangeMethod = null;
                    tempHookFunctionKey = new StringBuilder(TypeName).Append('.').Append(tempAttribute.hook).ToString();
                    if (!string.IsNullOrEmpty(tempAttribute.hook) &&
                        !CacheHookFunctions.TryGetValue(tempHookFunctionKey, out tempOnChangeMethod))
                    {
                        // Not found hook function in cache dictionary, try find the function
                        tempLookupType = ClassType;
                        while (tempLookupType != null && tempLookupType != typeof(LiteNetLibBehaviour))
                        {
                            tempLookupMethods = tempLookupType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                            foreach (MethodInfo lookupMethod in tempLookupMethods)
                            {
                                // Return type must be `void`
                                if (lookupMethod.ReturnType != typeof(void))
                                    continue;

                                // Not the function it's looking for
                                if (!lookupMethod.Name.Equals(tempAttribute.hook))
                                    continue;

                                // Parameter not match
                                tempOnChangeMethodParams = lookupMethod.GetParameters();
                                if (tempOnChangeMethodParams == null ||
                                    tempOnChangeMethodParams.Length != 1 ||
                                    tempOnChangeMethodParams[0].ParameterType != fieldInfo.FieldType)
                                    continue;

                                // Found the function
                                tempOnChangeMethod = lookupMethod;
                                break;
                            }

                            // Found the function so exit the loop, don't find the function in base class
                            if (tempOnChangeMethod != null)
                                break;

                            tempLookupType = tempLookupType.BaseType;
                        }
                        // Tell developers that it can't find the function and clear the function's instance
                        if (tempOnChangeMethod == null)
                        {
                            if (Manager.LogError)
                                Logging.LogError(LogTag, "Cannot find invoking function named [" + tempAttribute.hook + "] from [" + TypeName + "], FYI the function must has 1 parameter with the same type with the field.");
                        }
                        // Add to cache dictionary althrough it's empty to avoid it try to lookup next time
                        CacheHookFunctions.Add(tempHookFunctionKey, tempOnChangeMethod);
                    }
                    tempSyncField = new LiteNetLibSyncFieldContainer(fieldInfo, this, tempOnChangeMethod);
                    tempSyncField.deliveryMethod = tempAttribute.deliveryMethod;
                    tempSyncField.sendInterval = tempAttribute.sendInterval;
                    tempSyncField.alwaysSync = tempAttribute.alwaysSync;
                    tempSyncField.doNotSyncInitialDataImmediately = tempAttribute.doNotSyncInitialDataImmediately;
                    tempSyncField.syncMode = tempAttribute.syncMode;
                    RegisterSyncElement(tempSyncField, Identity.SyncFields);
                }
                catch (Exception ex)
                {
                    if (Manager.LogFatal)
                        Logging.LogException(LogTag, ex);
                }
            }
        }

        private void CacheRpcs<RpcType>(Dictionary<string, int> ids, Dictionary<string, CacheFunctions> cacheDict)
            where RpcType : RpcAttribute
        {
            CacheFunctions tempCacheFunctions;
            if (!cacheDict.TryGetValue(TypeName, out tempCacheFunctions))
            {
                tempCacheFunctions = new CacheFunctions()
                {
                    functions = new List<MethodInfo>(),
                    functionsCanCallByEveryone = new List<MethodInfo>()
                };
                tempLookupNames.Clear();
                tempLookupType = ClassType;
                RpcType tempAttribute;
                // Find for function with [Rpc] attribute to register as RPC
                while (tempLookupType != null && tempLookupType != typeof(LiteNetLibBehaviour))
                {
                    tempLookupMethods = tempLookupType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                    foreach (MethodInfo lookupMethod in tempLookupMethods)
                    {
                        // Avoid duplicate functions
                        if (tempLookupNames.Contains(lookupMethod.Name))
                            continue;

                        // Must have [Rpc] attribute
                        tempAttribute = lookupMethod.GetCustomAttribute<RpcType>();
                        if (tempAttribute == null)
                            continue;

                        // Return type must be `void`
                        if (lookupMethod.ReturnType != typeof(void))
                        {
                            if (Manager.LogError)
                                Logging.LogError(LogTag, "Cannot register rpc [" + lookupMethod.Name + "] return type must be void");
                            continue;
                        }

                        if (!tempAttribute.canCallByEveryone)
                            tempCacheFunctions.functions.Add(lookupMethod);
                        else
                            tempCacheFunctions.functionsCanCallByEveryone.Add(lookupMethod);
                        tempLookupNames.Add(lookupMethod.Name);
                    }
                    tempLookupType = tempLookupType.BaseType;
                }
                cacheDict.Add(TypeName, tempCacheFunctions);
            }
            SetupRpcs(ids, tempCacheFunctions.functions, false);
            SetupRpcs(ids, tempCacheFunctions.functionsCanCallByEveryone, true);
        }

        private void SetupRpcs(Dictionary<string, int> ids, List<MethodInfo> methodInfos, bool canCallByEveryone)
        {
            if (methodInfos == null || methodInfos.Count == 0)
                return;

            string tempFunctionId;
            Type[] tempParamTypes;
            foreach (MethodInfo methodInfo in methodInfos)
            {
                tempFunctionId = MakeNetFunctionId(methodInfo);
                if (!CacheDyncnamicFunctionTypes.TryGetValue(tempFunctionId, out tempParamTypes))
                {
                    tempParamTypes = methodInfo.GetParameters().Select(p => p.ParameterType).ToArray();
                    CacheDyncnamicFunctionTypes[tempFunctionId] = tempParamTypes;
                }
                RegisterRPC(ids, tempFunctionId, new LiteNetLibFunctionDynamic(tempParamTypes, this, methodInfo), canCallByEveryone);
            }
        }

        #region Sync Elements Registration
        private void RegisterSyncElement<T>(T element, List<T> elementList) where T : LiteNetLibElement
        {
            int elementId = elementList.Count;
            element.Setup(this, elementId);
            elementList.Add(element);
        }

        public void RegisterSyncField<T>(T syncField) where T : LiteNetLibSyncField
        {
            RegisterSyncElement(syncField, Identity.SyncFields);
        }

        public void RegisterSyncList<T>(T syncList) where T : LiteNetLibSyncList
        {
            RegisterSyncElement(syncList, Identity.SyncLists);
        }
        #endregion

        #region RPCs Registration
        /// <summary>
        /// This is another synonym of `RegisterElasticRPC`
        /// </summary>
        public void RegisterNetFunction(NetFunctionDelegate func, bool canCallByEveryone = false)
        {
            RegisterElasticRPC(func, canCallByEveryone);
        }

        /// <summary>
        /// This is another synonym of `RegisterElasticRPC`
        /// </summary>
        public void RegisterNetFunction<T1>(NetFunctionDelegate<T1> func, bool canCallByEveryone = false)
        {
            RegisterElasticRPC(func, canCallByEveryone);
        }

        /// <summary>
        /// This is another synonym of `RegisterElasticRPC`
        /// </summary>
        public void RegisterNetFunction<T1, T2>(NetFunctionDelegate<T1, T2> func, bool canCallByEveryone = false)
        {
            RegisterElasticRPC(func, canCallByEveryone);
        }

        /// <summary>
        /// This is another synonym of `RegisterElasticRPC`
        /// </summary>
        public void RegisterNetFunction<T1, T2, T3>(NetFunctionDelegate<T1, T2, T3> func, bool canCallByEveryone = false)
        {
            RegisterElasticRPC(func, canCallByEveryone);
        }

        /// <summary>
        /// This is another synonym of `RegisterElasticRPC`
        /// </summary>
        public void RegisterNetFunction<T1, T2, T3, T4>(NetFunctionDelegate<T1, T2, T3, T4> func, bool canCallByEveryone = false)
        {
            RegisterElasticRPC(func, canCallByEveryone);
        }

        /// <summary>
        /// This is another synonym of `RegisterElasticRPC`
        /// </summary>
        public void RegisterNetFunction<T1, T2, T3, T4, T5>(NetFunctionDelegate<T1, T2, T3, T4, T5> func, bool canCallByEveryone = false)
        {
            RegisterElasticRPC(func, canCallByEveryone);
        }

        /// <summary>
        /// This is another synonym of `RegisterElasticRPC`
        /// </summary>
        public void RegisterNetFunction<T1, T2, T3, T4, T5, T6>(NetFunctionDelegate<T1, T2, T3, T4, T5, T6> func, bool canCallByEveryone = false)
        {
            RegisterElasticRPC(func, canCallByEveryone);
        }

        /// <summary>
        /// This is another synonym of `RegisterElasticRPC`
        /// </summary>
        public void RegisterNetFunction<T1, T2, T3, T4, T5, T6, T7>(NetFunctionDelegate<T1, T2, T3, T4, T5, T6, T7> func, bool canCallByEveryone = false)
        {
            RegisterElasticRPC(func, canCallByEveryone);
        }

        /// <summary>
        /// This is another synonym of `RegisterElasticRPC`
        /// </summary>
        public void RegisterNetFunction<T1, T2, T3, T4, T5, T6, T7, T8>(NetFunctionDelegate<T1, T2, T3, T4, T5, T6, T7, T8> func, bool canCallByEveryone = false)
        {
            RegisterElasticRPC(func, canCallByEveryone);
        }

        /// <summary>
        /// This is another synonym of `RegisterElasticRPC`
        /// </summary>
        public void RegisterNetFunction<T1, T2, T3, T4, T5, T6, T7, T8, T9>(NetFunctionDelegate<T1, T2, T3, T4, T5, T6, T7, T8, T9> func, bool canCallByEveryone = false)
        {
            RegisterElasticRPC(func, canCallByEveryone);
        }

        /// <summary>
        /// This is another synonym of `RegisterElasticRPC`
        /// </summary>
        public void RegisterNetFunction<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(NetFunctionDelegate<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> func, bool canCallByEveryone = false)
        {
            RegisterElasticRPC(func, canCallByEveryone);
        }

        public void RegisterElasticRPC(NetFunctionDelegate func, bool canCallByEveryone = false)
        {
            RegisterRPC(serverRpcIds, func, canCallByEveryone);
            RegisterRPC(allRpcIds, func, canCallByEveryone);
            RegisterRPC(targetRpcIds, func, canCallByEveryone);
        }

        public void RegisterElasticRPC<T1>(NetFunctionDelegate<T1> func, bool canCallByEveryone = false)
        {
            RegisterRPC(serverRpcIds, func, canCallByEveryone);
            RegisterRPC(allRpcIds, func, canCallByEveryone);
            RegisterRPC(targetRpcIds, func, canCallByEveryone);
        }

        public void RegisterElasticRPC<T1, T2>(NetFunctionDelegate<T1, T2> func, bool canCallByEveryone = false)
        {
            RegisterRPC(serverRpcIds, func, canCallByEveryone);
            RegisterRPC(allRpcIds, func, canCallByEveryone);
            RegisterRPC(targetRpcIds, func, canCallByEveryone);
        }

        public void RegisterElasticRPC<T1, T2, T3>(NetFunctionDelegate<T1, T2, T3> func, bool canCallByEveryone = false)
        {
            RegisterRPC(serverRpcIds, func, canCallByEveryone);
            RegisterRPC(allRpcIds, func, canCallByEveryone);
            RegisterRPC(targetRpcIds, func, canCallByEveryone);
        }

        public void RegisterElasticRPC<T1, T2, T3, T4>(NetFunctionDelegate<T1, T2, T3, T4> func, bool canCallByEveryone = false)
        {
            RegisterRPC(serverRpcIds, func, canCallByEveryone);
            RegisterRPC(allRpcIds, func, canCallByEveryone);
            RegisterRPC(targetRpcIds, func, canCallByEveryone);
        }

        public void RegisterElasticRPC<T1, T2, T3, T4, T5>(NetFunctionDelegate<T1, T2, T3, T4, T5> func, bool canCallByEveryone = false)
        {
            RegisterRPC(serverRpcIds, func, canCallByEveryone);
            RegisterRPC(allRpcIds, func, canCallByEveryone);
            RegisterRPC(targetRpcIds, func, canCallByEveryone);
        }

        public void RegisterElasticRPC<T1, T2, T3, T4, T5, T6>(NetFunctionDelegate<T1, T2, T3, T4, T5, T6> func, bool canCallByEveryone = false)
        {
            RegisterRPC(serverRpcIds, func, canCallByEveryone);
            RegisterRPC(allRpcIds, func, canCallByEveryone);
            RegisterRPC(targetRpcIds, func, canCallByEveryone);
        }

        public void RegisterElasticRPC<T1, T2, T3, T4, T5, T6, T7>(NetFunctionDelegate<T1, T2, T3, T4, T5, T6, T7> func, bool canCallByEveryone = false)
        {
            RegisterRPC(serverRpcIds, func, canCallByEveryone);
            RegisterRPC(allRpcIds, func, canCallByEveryone);
            RegisterRPC(targetRpcIds, func, canCallByEveryone);
        }

        public void RegisterElasticRPC<T1, T2, T3, T4, T5, T6, T7, T8>(NetFunctionDelegate<T1, T2, T3, T4, T5, T6, T7, T8> func, bool canCallByEveryone = false)
        {
            RegisterRPC(serverRpcIds, func, canCallByEveryone);
            RegisterRPC(allRpcIds, func, canCallByEveryone);
            RegisterRPC(targetRpcIds, func, canCallByEveryone);
        }

        public void RegisterElasticRPC<T1, T2, T3, T4, T5, T6, T7, T8, T9>(NetFunctionDelegate<T1, T2, T3, T4, T5, T6, T7, T8, T9> func, bool canCallByEveryone = false)
        {
            RegisterRPC(serverRpcIds, func, canCallByEveryone);
            RegisterRPC(allRpcIds, func, canCallByEveryone);
            RegisterRPC(targetRpcIds, func, canCallByEveryone);
        }

        public void RegisterElasticRPC<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(NetFunctionDelegate<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> func, bool canCallByEveryone = false)
        {
            RegisterRPC(serverRpcIds, func, canCallByEveryone);
            RegisterRPC(allRpcIds, func, canCallByEveryone);
            RegisterRPC(targetRpcIds, func, canCallByEveryone);
        }

        public void RegisterServerRPC(NetFunctionDelegate func, bool canCallByEveryone = false)
        {
            RegisterRPC(serverRpcIds, func, canCallByEveryone);
        }

        public void RegisterServerRPC<T1>(NetFunctionDelegate<T1> func, bool canCallByEveryone = false)
        {
            RegisterRPC(serverRpcIds, func, canCallByEveryone);
        }

        public void RegisterServerRPC<T1, T2>(NetFunctionDelegate<T1, T2> func, bool canCallByEveryone = false)
        {
            RegisterRPC(serverRpcIds, func, canCallByEveryone);
        }

        public void RegisterServerRPC<T1, T2, T3>(NetFunctionDelegate<T1, T2, T3> func, bool canCallByEveryone = false)
        {
            RegisterRPC(serverRpcIds, func, canCallByEveryone);
        }

        public void RegisterServerRPC<T1, T2, T3, T4>(NetFunctionDelegate<T1, T2, T3, T4> func, bool canCallByEveryone = false)
        {
            RegisterRPC(serverRpcIds, func, canCallByEveryone);
        }

        public void RegisterServerRPC<T1, T2, T3, T4, T5>(NetFunctionDelegate<T1, T2, T3, T4, T5> func, bool canCallByEveryone = false)
        {
            RegisterRPC(serverRpcIds, func, canCallByEveryone);
        }

        public void RegisterServerRPC<T1, T2, T3, T4, T5, T6>(NetFunctionDelegate<T1, T2, T3, T4, T5, T6> func, bool canCallByEveryone = false)
        {
            RegisterRPC(serverRpcIds, func, canCallByEveryone);
        }

        public void RegisterServerRPC<T1, T2, T3, T4, T5, T6, T7>(NetFunctionDelegate<T1, T2, T3, T4, T5, T6, T7> func, bool canCallByEveryone = false)
        {
            RegisterRPC(serverRpcIds, func, canCallByEveryone);
        }

        public void RegisterServerRPC<T1, T2, T3, T4, T5, T6, T7, T8>(NetFunctionDelegate<T1, T2, T3, T4, T5, T6, T7, T8> func, bool canCallByEveryone = false)
        {
            RegisterRPC(serverRpcIds, func, canCallByEveryone);
        }

        public void RegisterServerRPC<T1, T2, T3, T4, T5, T6, T7, T8, T9>(NetFunctionDelegate<T1, T2, T3, T4, T5, T6, T7, T8, T9> func, bool canCallByEveryone = false)
        {
            RegisterRPC(serverRpcIds, func, canCallByEveryone);
        }

        public void RegisterServerRPC<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(NetFunctionDelegate<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> func, bool canCallByEveryone = false)
        {
            RegisterRPC(serverRpcIds, func, canCallByEveryone);
        }

        public void RegisterAllRPC(NetFunctionDelegate func, bool canCallByEveryone = false)
        {
            RegisterRPC(allRpcIds, func, canCallByEveryone);
        }

        public void RegisterAllRPC<T1>(NetFunctionDelegate<T1> func, bool canCallByEveryone = false)
        {
            RegisterRPC(allRpcIds, func, canCallByEveryone);
        }

        public void RegisterAllRPC<T1, T2>(NetFunctionDelegate<T1, T2> func, bool canCallByEveryone = false)
        {
            RegisterRPC(allRpcIds, func, canCallByEveryone);
        }

        public void RegisterAllRPC<T1, T2, T3>(NetFunctionDelegate<T1, T2, T3> func, bool canCallByEveryone = false)
        {
            RegisterRPC(allRpcIds, func, canCallByEveryone);
        }

        public void RegisterAllRPC<T1, T2, T3, T4>(NetFunctionDelegate<T1, T2, T3, T4> func, bool canCallByEveryone = false)
        {
            RegisterRPC(allRpcIds, func, canCallByEveryone);
        }

        public void RegisterAllRPC<T1, T2, T3, T4, T5>(NetFunctionDelegate<T1, T2, T3, T4, T5> func, bool canCallByEveryone = false)
        {
            RegisterRPC(allRpcIds, func, canCallByEveryone);
        }

        public void RegisterAllRPC<T1, T2, T3, T4, T5, T6>(NetFunctionDelegate<T1, T2, T3, T4, T5, T6> func, bool canCallByEveryone = false)
        {
            RegisterRPC(allRpcIds, func, canCallByEveryone);
        }

        public void RegisterAllRPC<T1, T2, T3, T4, T5, T6, T7>(NetFunctionDelegate<T1, T2, T3, T4, T5, T6, T7> func, bool canCallByEveryone = false)
        {
            RegisterRPC(allRpcIds, func, canCallByEveryone);
        }

        public void RegisterAllRPC<T1, T2, T3, T4, T5, T6, T7, T8>(NetFunctionDelegate<T1, T2, T3, T4, T5, T6, T7, T8> func, bool canCallByEveryone = false)
        {
            RegisterRPC(allRpcIds, func, canCallByEveryone);
        }

        public void RegisterAllRPC<T1, T2, T3, T4, T5, T6, T7, T8, T9>(NetFunctionDelegate<T1, T2, T3, T4, T5, T6, T7, T8, T9> func, bool canCallByEveryone = false)
        {
            RegisterRPC(allRpcIds, func, canCallByEveryone);
        }

        public void RegisterAllRPC<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(NetFunctionDelegate<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> func, bool canCallByEveryone = false)
        {
            RegisterRPC(allRpcIds, func, canCallByEveryone);
        }

        public void RegisterTargetRpc(NetFunctionDelegate func, bool canCallByEveryone = false)
        {
            RegisterRPC(targetRpcIds, func, canCallByEveryone);
        }

        public void RegisterTargetRpc<T1>(NetFunctionDelegate<T1> func, bool canCallByEveryone = false)
        {
            RegisterRPC(targetRpcIds, func, canCallByEveryone);
        }

        public void RegisterTargetRpc<T1, T2>(NetFunctionDelegate<T1, T2> func, bool canCallByEveryone = false)
        {
            RegisterRPC(targetRpcIds, func, canCallByEveryone);
        }

        public void RegisterTargetRpc<T1, T2, T3>(NetFunctionDelegate<T1, T2, T3> func, bool canCallByEveryone = false)
        {
            RegisterRPC(targetRpcIds, func, canCallByEveryone);
        }

        public void RegisterTargetRpc<T1, T2, T3, T4>(NetFunctionDelegate<T1, T2, T3, T4> func, bool canCallByEveryone = false)
        {
            RegisterRPC(targetRpcIds, func, canCallByEveryone);
        }

        public void RegisterTargetRpc<T1, T2, T3, T4, T5>(NetFunctionDelegate<T1, T2, T3, T4, T5> func, bool canCallByEveryone = false)
        {
            RegisterRPC(targetRpcIds, func, canCallByEveryone);
        }

        public void RegisterTargetRpc<T1, T2, T3, T4, T5, T6>(NetFunctionDelegate<T1, T2, T3, T4, T5, T6> func, bool canCallByEveryone = false)
        {
            RegisterRPC(targetRpcIds, func, canCallByEveryone);
        }

        public void RegisterTargetRpc<T1, T2, T3, T4, T5, T6, T7>(NetFunctionDelegate<T1, T2, T3, T4, T5, T6, T7> func, bool canCallByEveryone = false)
        {
            RegisterRPC(targetRpcIds, func, canCallByEveryone);
        }

        public void RegisterTargetRpc<T1, T2, T3, T4, T5, T6, T7, T8>(NetFunctionDelegate<T1, T2, T3, T4, T5, T6, T7, T8> func, bool canCallByEveryone = false)
        {
            RegisterRPC(targetRpcIds, func, canCallByEveryone);
        }

        public void RegisterTargetRpc<T1, T2, T3, T4, T5, T6, T7, T8, T9>(NetFunctionDelegate<T1, T2, T3, T4, T5, T6, T7, T8, T9> func, bool canCallByEveryone = false)
        {
            RegisterRPC(targetRpcIds, func, canCallByEveryone);
        }

        public void RegisterTargetRpc<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(NetFunctionDelegate<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> func, bool canCallByEveryone = false)
        {
            RegisterRPC(targetRpcIds, func, canCallByEveryone);
        }
        #endregion

        private void RegisterRPC(Dictionary<string, int> dict, NetFunctionDelegate func, bool canCallByEveryone = false)
        {
            RegisterRPC(dict, MakeNetFunctionId(func.Method), new LiteNetLibFunction(func), canCallByEveryone);
        }

        private void RegisterRPC<T1>(Dictionary<string, int> dict, NetFunctionDelegate<T1> func, bool canCallByEveryone = false)
        {
            RegisterRPC(dict, MakeNetFunctionId(func.Method), new LiteNetLibFunction<T1>(func), canCallByEveryone);
        }

        private void RegisterRPC<T1, T2>(Dictionary<string, int> dict, NetFunctionDelegate<T1, T2> func, bool canCallByEveryone = false)
        {
            RegisterRPC(dict, MakeNetFunctionId(func.Method), new LiteNetLibFunction<T1, T2>(func), canCallByEveryone);
        }

        private void RegisterRPC<T1, T2, T3>(Dictionary<string, int> dict, NetFunctionDelegate<T1, T2, T3> func, bool canCallByEveryone = false)
        {
            RegisterRPC(dict, MakeNetFunctionId(func.Method), new LiteNetLibFunction<T1, T2, T3>(func), canCallByEveryone);
        }

        private void RegisterRPC<T1, T2, T3, T4>(Dictionary<string, int> dict, NetFunctionDelegate<T1, T2, T3, T4> func, bool canCallByEveryone = false)
        {
            RegisterRPC(dict, MakeNetFunctionId(func.Method), new LiteNetLibFunction<T1, T2, T3, T4>(func), canCallByEveryone);
        }

        private void RegisterRPC<T1, T2, T3, T4, T5>(Dictionary<string, int> dict, NetFunctionDelegate<T1, T2, T3, T4, T5> func, bool canCallByEveryone = false)
        {
            RegisterRPC(dict, MakeNetFunctionId(func.Method), new LiteNetLibFunction<T1, T2, T3, T4, T5>(func), canCallByEveryone);
        }

        private void RegisterRPC<T1, T2, T3, T4, T5, T6>(Dictionary<string, int> dict, NetFunctionDelegate<T1, T2, T3, T4, T5, T6> func, bool canCallByEveryone = false)
        {
            RegisterRPC(dict, MakeNetFunctionId(func.Method), new LiteNetLibFunction<T1, T2, T3, T4, T5, T6>(func), canCallByEveryone);
        }

        private void RegisterRPC<T1, T2, T3, T4, T5, T6, T7>(Dictionary<string, int> dict, NetFunctionDelegate<T1, T2, T3, T4, T5, T6, T7> func, bool canCallByEveryone = false)
        {
            RegisterRPC(dict, MakeNetFunctionId(func.Method), new LiteNetLibFunction<T1, T2, T3, T4, T5, T6, T7>(func), canCallByEveryone);
        }

        private void RegisterRPC<T1, T2, T3, T4, T5, T6, T7, T8>(Dictionary<string, int> dict, NetFunctionDelegate<T1, T2, T3, T4, T5, T6, T7, T8> func, bool canCallByEveryone = false)
        {
            RegisterRPC(dict, MakeNetFunctionId(func.Method), new LiteNetLibFunction<T1, T2, T3, T4, T5, T6, T7, T8>(func), canCallByEveryone);
        }

        private void RegisterRPC<T1, T2, T3, T4, T5, T6, T7, T8, T9>(Dictionary<string, int> dict, NetFunctionDelegate<T1, T2, T3, T4, T5, T6, T7, T8, T9> func, bool canCallByEveryone = false)
        {
            RegisterRPC(dict, MakeNetFunctionId(func.Method), new LiteNetLibFunction<T1, T2, T3, T4, T5, T6, T7, T8, T9>(func), canCallByEveryone);
        }

        private void RegisterRPC<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(Dictionary<string, int> dict, NetFunctionDelegate<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> func, bool canCallByEveryone = false)
        {
            RegisterRPC(dict, MakeNetFunctionId(func.Method), new LiteNetLibFunction<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(func), canCallByEveryone);
        }

        private void RegisterRPC(Dictionary<string, int> dict, string id, LiteNetLibFunction netFunction, bool canCallByEveryone)
        {
            if (dict.ContainsKey(id))
            {
                if (Manager.LogError)
                    Logging.LogError(LogTag, "[" + TypeName + "] cannot register rpc with existed id [" + id + "].");
                return;
            }
            if (Identity.NetFunctions.Count >= int.MaxValue)
            {
                if (Manager.LogError)
                    Logging.LogError(LogTag, "[" + TypeName + "] cannot register rpc it's exceeds limit.");
                return;
            }
            int elementId = Identity.NetFunctions.Count;
            netFunction.Setup(this, elementId);
            netFunction.CanCallByEveryone = canCallByEveryone;
            Identity.NetFunctions.Add(netFunction);
            dict[id] = elementId;
        }

        #region Elastic RPC with receivers and parameters        
        /// <summary>
        /// This is another synonym of `RPC`
        /// </summary>
        public void CallNetFunction(NetFunctionDelegate func, FunctionReceivers receivers)
        {
            RPC(func, receivers);
        }

        /// <summary>
        /// This is another synonym of `RPC`
        /// </summary>
        public void CallNetFunction<T1>(NetFunctionDelegate<T1> func, FunctionReceivers receivers, T1 param1)
        {
            RPC(func, receivers, param1);
        }

        /// <summary>
        /// This is another synonym of `RPC`
        /// </summary>
        public void CallNetFunction<T1, T2>(NetFunctionDelegate<T1, T2> func, FunctionReceivers receivers, T1 param1, T2 param2)
        {
            RPC(func, receivers, param1, param2);
        }

        /// <summary>
        /// This is another synonym of `RPC`
        /// </summary>
        public void CallNetFunction<T1, T2, T3>(NetFunctionDelegate<T1, T2, T3> func, FunctionReceivers receivers, T1 param1, T2 param2, T3 param3)
        {
            RPC(func, receivers, param1, param2, param3);
        }

        /// <summary>
        /// This is another synonym of `RPC`
        /// </summary>
        public void CallNetFunction<T1, T2, T3, T4>(NetFunctionDelegate<T1, T2, T3, T4> func, FunctionReceivers receivers, T1 param1, T2 param2, T3 param3, T4 param4)
        {
            RPC(func, receivers, param1, param2, param3, param4);
        }

        /// <summary>
        /// This is another synonym of `RPC`
        /// </summary>
        public void CallNetFunction<T1, T2, T3, T4, T5>(NetFunctionDelegate<T1, T2, T3, T4, T5> func, FunctionReceivers receivers, T1 param1, T2 param2, T3 param3, T4 param4, T5 param5)
        {
            RPC(func, receivers, param1, param2, param3, param4, param5);
        }

        /// <summary>
        /// This is another synonym of `RPC`
        /// </summary>
        public void CallNetFunction<T1, T2, T3, T4, T5, T6>(NetFunctionDelegate<T1, T2, T3, T4, T5, T6> func, FunctionReceivers receivers, T1 param1, T2 param2, T3 param3, T4 param4, T5 param5, T6 param6)
        {
            RPC(func, receivers, param1, param2, param3, param4, param5, param6);
        }

        /// <summary>
        /// This is another synonym of `RPC`
        /// </summary>
        public void CallNetFunction<T1, T2, T3, T4, T5, T6, T7>(NetFunctionDelegate<T1, T2, T3, T4, T5, T6, T7> func, FunctionReceivers receivers, T1 param1, T2 param2, T3 param3, T4 param4, T5 param5, T6 param6, T7 param7)
        {
            RPC(func, receivers, param1, param2, param3, param4, param5, param6, param7);
        }

        /// <summary>
        /// This is another synonym of `RPC`
        /// </summary>
        public void CallNetFunction<T1, T2, T3, T4, T5, T6, T7, T8>(NetFunctionDelegate<T1, T2, T3, T4, T5, T6, T7, T8> func, FunctionReceivers receivers, T1 param1, T2 param2, T3 param3, T4 param4, T5 param5, T6 param6, T7 param7, T8 param8)
        {
            RPC(func, receivers, param1, param2, param3, param4, param5, param6, param7, param8);
        }

        /// <summary>
        /// This is another synonym of `RPC`
        /// </summary>
        public void CallNetFunction<T1, T2, T3, T4, T5, T6, T7, T8, T9>(NetFunctionDelegate<T1, T2, T3, T4, T5, T6, T7, T8, T9> func, FunctionReceivers receivers, T1 param1, T2 param2, T3 param3, T4 param4, T5 param5, T6 param6, T7 param7, T8 param8, T9 param9)
        {
            RPC(func, receivers, param1, param2, param3, param4, param5, param6, param7, param8, param9);
        }

        /// <summary>
        /// This is another synonym of `RPC`
        /// </summary>
        public void CallNetFunction<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(NetFunctionDelegate<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> func, FunctionReceivers receivers, T1 param1, T2 param2, T3 param3, T4 param4, T5 param5, T6 param6, T7 param7, T8 param8, T9 param9, T10 param10)
        {
            RPC(func, receivers, param1, param2, param3, param4, param5, param6, param7, param8, param9, param10);
        }

        public void RPC(NetFunctionDelegate func, FunctionReceivers receivers)
        {
            RPC(func.Method.Name, 0, DeliveryMethod.ReliableOrdered, receivers);
        }

        public void RPC<T1>(NetFunctionDelegate<T1> func, FunctionReceivers receivers, T1 param1)
        {
            RPC(func.Method.Name, 0, DeliveryMethod.ReliableOrdered, receivers, param1);
        }

        public void RPC<T1, T2>(NetFunctionDelegate<T1, T2> func, FunctionReceivers receivers, T1 param1, T2 param2)
        {
            RPC(func.Method.Name, 0, DeliveryMethod.ReliableOrdered, receivers, param1, param2);
        }

        public void RPC<T1, T2, T3>(NetFunctionDelegate<T1, T2, T3> func, FunctionReceivers receivers, T1 param1, T2 param2, T3 param3)
        {
            RPC(func.Method.Name, 0, DeliveryMethod.ReliableOrdered, receivers, param1, param2, param3);
        }

        public void RPC<T1, T2, T3, T4>(NetFunctionDelegate<T1, T2, T3, T4> func, FunctionReceivers receivers, T1 param1, T2 param2, T3 param3, T4 param4)
        {
            RPC(func.Method.Name, 0, DeliveryMethod.ReliableOrdered, receivers, param1, param2, param3, param4);
        }

        public void RPC<T1, T2, T3, T4, T5>(NetFunctionDelegate<T1, T2, T3, T4, T5> func, FunctionReceivers receivers, T1 param1, T2 param2, T3 param3, T4 param4, T5 param5)
        {
            RPC(func.Method.Name, 0, DeliveryMethod.ReliableOrdered, receivers, param1, param2, param3, param4, param5);
        }

        public void RPC<T1, T2, T3, T4, T5, T6>(NetFunctionDelegate<T1, T2, T3, T4, T5, T6> func, FunctionReceivers receivers, T1 param1, T2 param2, T3 param3, T4 param4, T5 param5, T6 param6)
        {
            RPC(func.Method.Name, 0, DeliveryMethod.ReliableOrdered, receivers, param1, param2, param3, param4, param5, param6);
        }

        public void RPC<T1, T2, T3, T4, T5, T6, T7>(NetFunctionDelegate<T1, T2, T3, T4, T5, T6, T7> func, FunctionReceivers receivers, T1 param1, T2 param2, T3 param3, T4 param4, T5 param5, T6 param6, T7 param7)
        {
            RPC(func.Method.Name, 0, DeliveryMethod.ReliableOrdered, receivers, param1, param2, param3, param4, param5, param6, param7);
        }

        public void RPC<T1, T2, T3, T4, T5, T6, T7, T8>(NetFunctionDelegate<T1, T2, T3, T4, T5, T6, T7, T8> func, FunctionReceivers receivers, T1 param1, T2 param2, T3 param3, T4 param4, T5 param5, T6 param6, T7 param7, T8 param8)
        {
            RPC(func.Method.Name, 0, DeliveryMethod.ReliableOrdered, receivers, param1, param2, param3, param4, param5, param6, param7, param8);
        }

        public void RPC<T1, T2, T3, T4, T5, T6, T7, T8, T9>(NetFunctionDelegate<T1, T2, T3, T4, T5, T6, T7, T8, T9> func, FunctionReceivers receivers, T1 param1, T2 param2, T3 param3, T4 param4, T5 param5, T6 param6, T7 param7, T8 param8, T9 param9)
        {
            RPC(func.Method.Name, 0, DeliveryMethod.ReliableOrdered, receivers, param1, param2, param3, param4, param5, param6, param7, param8, param9);
        }

        public void RPC<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(NetFunctionDelegate<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> func, FunctionReceivers receivers, T1 param1, T2 param2, T3 param3, T4 param4, T5 param5, T6 param6, T7 param7, T8 param8, T9 param9, T10 param10)
        {
            RPC(func.Method.Name, 0, DeliveryMethod.ReliableOrdered, receivers, param1, param2, param3, param4, param5, param6, param7, param8, param9, param10);
        }
        #endregion

        #region Elastic RPC with delivery method, receivers and parameters
        /// <summary>
        /// This is another synonym of `RPC`
        /// </summary>
        public void CallNetFunction(NetFunctionDelegate func, byte dataChannel, DeliveryMethod deliveryMethod, FunctionReceivers receivers)
        {
            RPC(func, dataChannel, deliveryMethod, receivers);
        }

        /// <summary>
        /// This is another synonym of `RPC`
        /// </summary>
        public void CallNetFunction<T1>(NetFunctionDelegate<T1> func, byte dataChannel, DeliveryMethod deliveryMethod, FunctionReceivers receivers, T1 param1)
        {
            RPC(func, dataChannel, deliveryMethod, receivers, param1);
        }

        /// <summary>
        /// This is another synonym of `RPC`
        /// </summary>
        public void CallNetFunction<T1, T2>(NetFunctionDelegate<T1, T2> func, byte dataChannel, DeliveryMethod deliveryMethod, FunctionReceivers receivers, T1 param1, T2 param2)
        {
            RPC(func, dataChannel, deliveryMethod, receivers, param1, param2);
        }

        /// <summary>
        /// This is another synonym of `RPC`
        /// </summary>
        public void CallNetFunction<T1, T2, T3>(NetFunctionDelegate<T1, T2, T3> func, byte dataChannel, DeliveryMethod deliveryMethod, FunctionReceivers receivers, T1 param1, T2 param2, T3 param3)
        {
            RPC(func, dataChannel, deliveryMethod, receivers, param1, param2, param3);
        }

        /// <summary>
        /// This is another synonym of `RPC`
        /// </summary>
        public void CallNetFunction<T1, T2, T3, T4>(NetFunctionDelegate<T1, T2, T3, T4> func, byte dataChannel, DeliveryMethod deliveryMethod, FunctionReceivers receivers, T1 param1, T2 param2, T3 param3, T4 param4)
        {
            RPC(func, dataChannel, deliveryMethod, receivers, param1, param2, param3, param4);
        }

        /// <summary>
        /// This is another synonym of `RPC`
        /// </summary>
        public void CallNetFunction<T1, T2, T3, T4, T5>(NetFunctionDelegate<T1, T2, T3, T4, T5> func, byte dataChannel, DeliveryMethod deliveryMethod, FunctionReceivers receivers, T1 param1, T2 param2, T3 param3, T4 param4, T5 param5)
        {
            RPC(func, dataChannel, deliveryMethod, receivers, param1, param2, param3, param4, param5);
        }

        /// <summary>
        /// This is another synonym of `RPC`
        /// </summary>
        public void CallNetFunction<T1, T2, T3, T4, T5, T6>(NetFunctionDelegate<T1, T2, T3, T4, T5, T6> func, byte dataChannel, DeliveryMethod deliveryMethod, FunctionReceivers receivers, T1 param1, T2 param2, T3 param3, T4 param4, T5 param5, T6 param6)
        {
            RPC(func, dataChannel, deliveryMethod, receivers, param1, param2, param3, param4, param5, param6);
        }

        /// <summary>
        /// This is another synonym of `RPC`
        /// </summary>
        public void CallNetFunction<T1, T2, T3, T4, T5, T6, T7>(NetFunctionDelegate<T1, T2, T3, T4, T5, T6, T7> func, byte dataChannel, DeliveryMethod deliveryMethod, FunctionReceivers receivers, T1 param1, T2 param2, T3 param3, T4 param4, T5 param5, T6 param6, T7 param7)
        {
            RPC(func, dataChannel, deliveryMethod, receivers, param1, param2, param3, param4, param5, param6, param7);
        }

        /// <summary>
        /// This is another synonym of `RPC`
        /// </summary>
        public void CallNetFunction<T1, T2, T3, T4, T5, T6, T7, T8>(NetFunctionDelegate<T1, T2, T3, T4, T5, T6, T7, T8> func, byte dataChannel, DeliveryMethod deliveryMethod, FunctionReceivers receivers, T1 param1, T2 param2, T3 param3, T4 param4, T5 param5, T6 param6, T7 param7, T8 param8)
        {
            RPC(func, dataChannel, deliveryMethod, receivers, param1, param2, param3, param4, param5, param6, param7, param8);
        }

        /// <summary>
        /// This is another synonym of `RPC`
        /// </summary>
        public void CallNetFunction<T1, T2, T3, T4, T5, T6, T7, T8, T9>(NetFunctionDelegate<T1, T2, T3, T4, T5, T6, T7, T8, T9> func, byte dataChannel, DeliveryMethod deliveryMethod, FunctionReceivers receivers, T1 param1, T2 param2, T3 param3, T4 param4, T5 param5, T6 param6, T7 param7, T8 param8, T9 param9)
        {
            RPC(func, dataChannel, deliveryMethod, receivers, param1, param2, param3, param4, param5, param6, param7, param8, param9);
        }

        /// <summary>
        /// This is another synonym of `RPC`
        /// </summary>
        public void CallNetFunction<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(NetFunctionDelegate<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> func, byte dataChannel, DeliveryMethod deliveryMethod, FunctionReceivers receivers, T1 param1, T2 param2, T3 param3, T4 param4, T5 param5, T6 param6, T7 param7, T8 param8, T9 param9, T10 param10)
        {
            RPC(func, dataChannel, deliveryMethod, receivers, param1, param2, param3, param4, param5, param6, param7, param8, param9, param10);
        }

        public void RPC(NetFunctionDelegate func, byte dataChannel, DeliveryMethod deliveryMethod, FunctionReceivers receivers)
        {
            RPC(func.Method.Name, dataChannel, deliveryMethod, receivers);
        }

        public void RPC<T1>(NetFunctionDelegate<T1> func, byte dataChannel, DeliveryMethod deliveryMethod, FunctionReceivers receivers, T1 param1)
        {
            RPC(func.Method.Name, dataChannel, deliveryMethod, receivers, param1);
        }

        public void RPC<T1, T2>(NetFunctionDelegate<T1, T2> func, byte dataChannel, DeliveryMethod deliveryMethod, FunctionReceivers receivers, T1 param1, T2 param2)
        {
            RPC(func.Method.Name, dataChannel, deliveryMethod, receivers, param1, param2);
        }

        public void RPC<T1, T2, T3>(NetFunctionDelegate<T1, T2, T3> func, byte dataChannel, DeliveryMethod deliveryMethod, FunctionReceivers receivers, T1 param1, T2 param2, T3 param3)
        {
            RPC(func.Method.Name, dataChannel, deliveryMethod, receivers, param1, param2, param3);
        }

        public void RPC<T1, T2, T3, T4>(NetFunctionDelegate<T1, T2, T3, T4> func, byte dataChannel, DeliveryMethod deliveryMethod, FunctionReceivers receivers, T1 param1, T2 param2, T3 param3, T4 param4)
        {
            RPC(func.Method.Name, dataChannel, deliveryMethod, receivers, param1, param2, param3, param4);
        }

        public void RPC<T1, T2, T3, T4, T5>(NetFunctionDelegate<T1, T2, T3, T4, T5> func, byte dataChannel, DeliveryMethod deliveryMethod, FunctionReceivers receivers, T1 param1, T2 param2, T3 param3, T4 param4, T5 param5)
        {
            RPC(func.Method.Name, dataChannel, deliveryMethod, receivers, param1, param2, param3, param4, param5);
        }

        public void RPC<T1, T2, T3, T4, T5, T6>(NetFunctionDelegate<T1, T2, T3, T4, T5, T6> func, byte dataChannel, DeliveryMethod deliveryMethod, FunctionReceivers receivers, T1 param1, T2 param2, T3 param3, T4 param4, T5 param5, T6 param6)
        {
            RPC(func.Method.Name, dataChannel, deliveryMethod, receivers, param1, param2, param3, param4, param5, param6);
        }

        public void RPC<T1, T2, T3, T4, T5, T6, T7>(NetFunctionDelegate<T1, T2, T3, T4, T5, T6, T7> func, byte dataChannel, DeliveryMethod deliveryMethod, FunctionReceivers receivers, T1 param1, T2 param2, T3 param3, T4 param4, T5 param5, T6 param6, T7 param7)
        {
            RPC(func.Method.Name, dataChannel, deliveryMethod, receivers, param1, param2, param3, param4, param5, param6, param7);
        }

        public void RPC<T1, T2, T3, T4, T5, T6, T7, T8>(NetFunctionDelegate<T1, T2, T3, T4, T5, T6, T7, T8> func, byte dataChannel, DeliveryMethod deliveryMethod, FunctionReceivers receivers, T1 param1, T2 param2, T3 param3, T4 param4, T5 param5, T6 param6, T7 param7, T8 param8)
        {
            RPC(func.Method.Name, dataChannel, deliveryMethod, receivers, param1, param2, param3, param4, param5, param6, param7, param8);
        }

        public void RPC<T1, T2, T3, T4, T5, T6, T7, T8, T9>(NetFunctionDelegate<T1, T2, T3, T4, T5, T6, T7, T8, T9> func, byte dataChannel, DeliveryMethod deliveryMethod, FunctionReceivers receivers, T1 param1, T2 param2, T3 param3, T4 param4, T5 param5, T6 param6, T7 param7, T8 param8, T9 param9)
        {
            RPC(func.Method.Name, dataChannel, deliveryMethod, receivers, param1, param2, param3, param4, param5, param6, param7, param8, param9);
        }

        public void RPC<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(NetFunctionDelegate<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> func, byte dataChannel, DeliveryMethod deliveryMethod, FunctionReceivers receivers, T1 param1, T2 param2, T3 param3, T4 param4, T5 param5, T6 param6, T7 param7, T8 param8, T9 param9, T10 param10)
        {
            RPC(func.Method.Name, dataChannel, deliveryMethod, receivers, param1, param2, param3, param4, param5, param6, param7, param8, param9, param10);
        }
        #endregion

        #region All RPC or Server RPC with parameters
        public void RPC(NetFunctionDelegate func)
        {
            RPC(func.Method.Name, 0, DeliveryMethod.ReliableOrdered);
        }

        public void RPC<T1>(NetFunctionDelegate<T1> func, T1 param1)
        {
            RPC(func.Method.Name, 0, DeliveryMethod.ReliableOrdered, param1);
        }

        public void RPC<T1, T2>(NetFunctionDelegate<T1, T2> func, T1 param1, T2 param2)
        {
            RPC(func.Method.Name, 0, DeliveryMethod.ReliableOrdered, param1, param2);
        }

        public void RPC<T1, T2, T3>(NetFunctionDelegate<T1, T2, T3> func, T1 param1, T2 param2, T3 param3)
        {
            RPC(func.Method.Name, 0, DeliveryMethod.ReliableOrdered, param1, param2, param3);
        }

        public void RPC<T1, T2, T3, T4>(NetFunctionDelegate<T1, T2, T3, T4> func, T1 param1, T2 param2, T3 param3, T4 param4)
        {
            RPC(func.Method.Name, 0, DeliveryMethod.ReliableOrdered, param1, param2, param3, param4);
        }

        public void RPC<T1, T2, T3, T4, T5>(NetFunctionDelegate<T1, T2, T3, T4, T5> func, T1 param1, T2 param2, T3 param3, T4 param4, T5 param5)
        {
            RPC(func.Method.Name, 0, DeliveryMethod.ReliableOrdered, param1, param2, param3, param4, param5);
        }

        public void RPC<T1, T2, T3, T4, T5, T6>(NetFunctionDelegate<T1, T2, T3, T4, T5, T6> func, T1 param1, T2 param2, T3 param3, T4 param4, T5 param5, T6 param6)
        {
            RPC(func.Method.Name, 0, DeliveryMethod.ReliableOrdered, param1, param2, param3, param4, param5, param6);
        }

        public void RPC<T1, T2, T3, T4, T5, T6, T7>(NetFunctionDelegate<T1, T2, T3, T4, T5, T6, T7> func, T1 param1, T2 param2, T3 param3, T4 param4, T5 param5, T6 param6, T7 param7)
        {
            RPC(func.Method.Name, 0, DeliveryMethod.ReliableOrdered, param1, param2, param3, param4, param5, param6, param7);
        }

        public void RPC<T1, T2, T3, T4, T5, T6, T7, T8>(NetFunctionDelegate<T1, T2, T3, T4, T5, T6, T7, T8> func, T1 param1, T2 param2, T3 param3, T4 param4, T5 param5, T6 param6, T7 param7, T8 param8)
        {
            RPC(func.Method.Name, 0, DeliveryMethod.ReliableOrdered, param1, param2, param3, param4, param5, param6, param7, param8);
        }

        public void RPC<T1, T2, T3, T4, T5, T6, T7, T8, T9>(NetFunctionDelegate<T1, T2, T3, T4, T5, T6, T7, T8, T9> func, T1 param1, T2 param2, T3 param3, T4 param4, T5 param5, T6 param6, T7 param7, T8 param8, T9 param9)
        {
            RPC(func.Method.Name, 0, DeliveryMethod.ReliableOrdered, param1, param2, param3, param4, param5, param6, param7, param8, param9);
        }

        public void RPC<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(NetFunctionDelegate<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> func, T1 param1, T2 param2, T3 param3, T4 param4, T5 param5, T6 param6, T7 param7, T8 param8, T9 param9, T10 param10)
        {
            RPC(func.Method.Name, 0, DeliveryMethod.ReliableOrdered, param1, param2, param3, param4, param5, param6, param7, param8, param9, param10);
        }
        #endregion

        #region All RPC or Server RPC with delivery method and parameters
        public void RPC(NetFunctionDelegate func, byte dataChannel, DeliveryMethod deliveryMethod)
        {
            RPC(func.Method.Name, dataChannel, deliveryMethod);
        }

        public void RPC<T1>(NetFunctionDelegate<T1> func, byte dataChannel, DeliveryMethod deliveryMethod, T1 param1)
        {
            RPC(func.Method.Name, dataChannel, deliveryMethod, param1);
        }

        public void RPC<T1, T2>(NetFunctionDelegate<T1, T2> func, byte dataChannel, DeliveryMethod deliveryMethod, T1 param1, T2 param2)
        {
            RPC(func.Method.Name, dataChannel, deliveryMethod, param1, param2);
        }

        public void RPC<T1, T2, T3>(NetFunctionDelegate<T1, T2, T3> func, byte dataChannel, DeliveryMethod deliveryMethod, T1 param1, T2 param2, T3 param3)
        {
            RPC(func.Method.Name, dataChannel, deliveryMethod, param1, param2, param3);
        }

        public void RPC<T1, T2, T3, T4>(NetFunctionDelegate<T1, T2, T3, T4> func, byte dataChannel, DeliveryMethod deliveryMethod, T1 param1, T2 param2, T3 param3, T4 param4)
        {
            RPC(func.Method.Name, dataChannel, deliveryMethod, param1, param2, param3, param4);
        }

        public void RPC<T1, T2, T3, T4, T5>(NetFunctionDelegate<T1, T2, T3, T4, T5> func, byte dataChannel, DeliveryMethod deliveryMethod, T1 param1, T2 param2, T3 param3, T4 param4, T5 param5)
        {
            RPC(func.Method.Name, dataChannel, deliveryMethod, param1, param2, param3, param4, param5);
        }

        public void RPC<T1, T2, T3, T4, T5, T6>(NetFunctionDelegate<T1, T2, T3, T4, T5, T6> func, byte dataChannel, DeliveryMethod deliveryMethod, T1 param1, T2 param2, T3 param3, T4 param4, T5 param5, T6 param6)
        {
            RPC(func.Method.Name, dataChannel, deliveryMethod, param1, param2, param3, param4, param5, param6);
        }

        public void RPC<T1, T2, T3, T4, T5, T6, T7>(NetFunctionDelegate<T1, T2, T3, T4, T5, T6, T7> func, byte dataChannel, DeliveryMethod deliveryMethod, T1 param1, T2 param2, T3 param3, T4 param4, T5 param5, T6 param6, T7 param7)
        {
            RPC(func.Method.Name, dataChannel, deliveryMethod, param1, param2, param3, param4, param5, param6, param7);
        }

        public void RPC<T1, T2, T3, T4, T5, T6, T7, T8>(NetFunctionDelegate<T1, T2, T3, T4, T5, T6, T7, T8> func, byte dataChannel, DeliveryMethod deliveryMethod, T1 param1, T2 param2, T3 param3, T4 param4, T5 param5, T6 param6, T7 param7, T8 param8)
        {
            RPC(func.Method.Name, dataChannel, deliveryMethod, param1, param2, param3, param4, param5, param6, param7, param8);
        }

        public void RPC<T1, T2, T3, T4, T5, T6, T7, T8, T9>(NetFunctionDelegate<T1, T2, T3, T4, T5, T6, T7, T8, T9> func, byte dataChannel, DeliveryMethod deliveryMethod, T1 param1, T2 param2, T3 param3, T4 param4, T5 param5, T6 param6, T7 param7, T8 param8, T9 param9)
        {
            RPC(func.Method.Name, dataChannel, deliveryMethod, param1, param2, param3, param4, param5, param6, param7, param8, param9);
        }

        public void RPC<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(NetFunctionDelegate<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> func, byte dataChannel, DeliveryMethod deliveryMethod, T1 param1, T2 param2, T3 param3, T4 param4, T5 param5, T6 param6, T7 param7, T8 param8, T9 param9, T10 param10)
        {
            RPC(func.Method.Name, dataChannel, deliveryMethod, param1, param2, param3, param4, param5, param6, param7, param8, param9, param10);
        }
        #endregion

        #region Target RPC with connectionId and parameters
        /// <summary>
        /// This is another synonym of `RPC` which contains `connectionId`
        /// </summary>
        public void CallNetFunction(NetFunctionDelegate func, long connectionId)
        {
            RPC(func, connectionId);
        }

        /// <summary>
        /// This is another synonym of `RPC` which contains `connectionId`
        /// </summary>
        public void CallNetFunction<T1>(NetFunctionDelegate<T1> func, long connectionId, T1 param1)
        {
            RPC(func, connectionId, param1);
        }

        /// <summary>
        /// This is another synonym of `RPC` which contains `connectionId`
        /// </summary>
        public void CallNetFunction<T1, T2>(NetFunctionDelegate<T1, T2> func, long connectionId, T1 param1, T2 param2)
        {
            RPC(func, connectionId, param1, param2);
        }

        /// <summary>
        /// This is another synonym of `RPC` which contains `connectionId`
        /// </summary>
        public void CallNetFunction<T1, T2, T3>(NetFunctionDelegate<T1, T2, T3> func, long connectionId, T1 param1, T2 param2, T3 param3)
        {
            RPC(func, connectionId, param1, param2, param3);
        }

        /// <summary>
        /// This is another synonym of `RPC` which contains `connectionId`
        /// </summary>
        public void CallNetFunction<T1, T2, T3, T4>(NetFunctionDelegate<T1, T2, T3, T4> func, long connectionId, T1 param1, T2 param2, T3 param3, T4 param4)
        {
            RPC(func, connectionId, param1, param2, param3, param4);
        }

        /// <summary>
        /// This is another synonym of `RPC` which contains `connectionId`
        /// </summary>
        public void CallNetFunction<T1, T2, T3, T4, T5>(NetFunctionDelegate<T1, T2, T3, T4, T5> func, long connectionId, T1 param1, T2 param2, T3 param3, T4 param4, T5 param5)
        {
            RPC(func, connectionId, param1, param2, param3, param4, param5);
        }

        /// <summary>
        /// This is another synonym of `RPC` which contains `connectionId`
        /// </summary>
        public void CallNetFunction<T1, T2, T3, T4, T5, T6>(NetFunctionDelegate<T1, T2, T3, T4, T5, T6> func, long connectionId, T1 param1, T2 param2, T3 param3, T4 param4, T5 param5, T6 param6)
        {
            RPC(func, connectionId, param1, param2, param3, param4, param5, param6);
        }

        /// <summary>
        /// This is another synonym of `RPC` which contains `connectionId`
        /// </summary>
        public void CallNetFunction<T1, T2, T3, T4, T5, T6, T7>(NetFunctionDelegate<T1, T2, T3, T4, T5, T6, T7> func, long connectionId, T1 param1, T2 param2, T3 param3, T4 param4, T5 param5, T6 param6, T7 param7)
        {
            RPC(func, connectionId, param1, param2, param3, param4, param5, param6, param7);
        }

        /// <summary>
        /// This is another synonym of `RPC` which contains `connectionId`
        /// </summary>
        public void CallNetFunction<T1, T2, T3, T4, T5, T6, T7, T8>(NetFunctionDelegate<T1, T2, T3, T4, T5, T6, T7, T8> func, long connectionId, T1 param1, T2 param2, T3 param3, T4 param4, T5 param5, T6 param6, T7 param7, T8 param8)
        {
            RPC(func, connectionId, param1, param2, param3, param4, param5, param6, param7, param8);
        }

        /// <summary>
        /// This is another synonym of `RPC` which contains `connectionId`
        /// </summary>
        public void CallNetFunction<T1, T2, T3, T4, T5, T6, T7, T8, T9>(NetFunctionDelegate<T1, T2, T3, T4, T5, T6, T7, T8, T9> func, long connectionId, T1 param1, T2 param2, T3 param3, T4 param4, T5 param5, T6 param6, T7 param7, T8 param8, T9 param9)
        {
            RPC(func, connectionId, param1, param2, param3, param4, param5, param6, param7, param8, param9);
        }

        /// <summary>
        /// This is another synonym of `RPC` which contains `connectionId`
        /// </summary>
        public void CallNetFunction<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(NetFunctionDelegate<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> func, long connectionId, T1 param1, T2 param2, T3 param3, T4 param4, T5 param5, T6 param6, T7 param7, T8 param8, T9 param9, T10 param10)
        {
            RPC(func, connectionId, param1, param2, param3, param4, param5, param6, param7, param8, param9, param10);
        }

        public void RPC(NetFunctionDelegate func, long connectionId)
        {
            RPC(func.Method.Name, connectionId);
        }

        public void RPC<T1>(NetFunctionDelegate<T1> func, long connectionId, T1 param1)
        {
            RPC(func.Method.Name, connectionId, param1);
        }

        public void RPC<T1, T2>(NetFunctionDelegate<T1, T2> func, long connectionId, T1 param1, T2 param2)
        {
            RPC(func.Method.Name, connectionId, param1, param2);
        }

        public void RPC<T1, T2, T3>(NetFunctionDelegate<T1, T2, T3> func, long connectionId, T1 param1, T2 param2, T3 param3)
        {
            RPC(func.Method.Name, connectionId, param1, param2, param3);
        }

        public void RPC<T1, T2, T3, T4>(NetFunctionDelegate<T1, T2, T3, T4> func, long connectionId, T1 param1, T2 param2, T3 param3, T4 param4)
        {
            RPC(func.Method.Name, connectionId, param1, param2, param3, param4);
        }

        public void RPC<T1, T2, T3, T4, T5>(NetFunctionDelegate<T1, T2, T3, T4, T5> func, long connectionId, T1 param1, T2 param2, T3 param3, T4 param4, T5 param5)
        {
            RPC(func.Method.Name, connectionId, param1, param2, param3, param4, param5);
        }

        public void RPC<T1, T2, T3, T4, T5, T6>(NetFunctionDelegate<T1, T2, T3, T4, T5, T6> func, long connectionId, T1 param1, T2 param2, T3 param3, T4 param4, T5 param5, T6 param6)
        {
            RPC(func.Method.Name, connectionId, param1, param2, param3, param4, param5, param6);
        }

        public void RPC<T1, T2, T3, T4, T5, T6, T7>(NetFunctionDelegate<T1, T2, T3, T4, T5, T6, T7> func, long connectionId, T1 param1, T2 param2, T3 param3, T4 param4, T5 param5, T6 param6, T7 param7)
        {
            RPC(func.Method.Name, connectionId, param1, param2, param3, param4, param5, param6, param7);
        }

        public void RPC<T1, T2, T3, T4, T5, T6, T7, T8>(NetFunctionDelegate<T1, T2, T3, T4, T5, T6, T7, T8> func, long connectionId, T1 param1, T2 param2, T3 param3, T4 param4, T5 param5, T6 param6, T7 param7, T8 param8)
        {
            RPC(func.Method.Name, connectionId, param1, param2, param3, param4, param5, param6, param7, param8);
        }

        public void RPC<T1, T2, T3, T4, T5, T6, T7, T8, T9>(NetFunctionDelegate<T1, T2, T3, T4, T5, T6, T7, T8, T9> func, long connectionId, T1 param1, T2 param2, T3 param3, T4 param4, T5 param5, T6 param6, T7 param7, T8 param8, T9 param9)
        {
            RPC(func.Method.Name, connectionId, param1, param2, param3, param4, param5, param6, param7, param8, param9);
        }

        public void RPC<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(NetFunctionDelegate<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> func, long connectionId, T1 param1, T2 param2, T3 param3, T4 param4, T5 param5, T6 param6, T7 param7, T8 param8, T9 param9, T10 param10)
        {
            RPC(func.Method.Name, connectionId, param1, param2, param3, param4, param5, param6, param7, param8, param9, param10);
        }
        #endregion

        /// <summary>
        /// This is another synonym of `RPC` which contains `receivers` and `parameters`
        /// </summary>
        public void CallNetFunction(string methodName, FunctionReceivers receivers, params object[] parameters)
        {
            RPC(methodName, receivers, parameters);
        }

        /// <summary>
        /// Call elastic RPC, it can be `All RPC` or `Server RPC` up to how you define `receivers`
        /// </summary>
        /// <param name="methodName"></param>
        /// <param name="receivers"></param>
        /// <param name="parameters"></param>
        public void RPC(string methodName, FunctionReceivers receivers, params object[] parameters)
        {
            RPC(methodName, 0, DeliveryMethod.ReliableOrdered, receivers, parameters);
        }

        /// <summary>
        /// This is another synonym of `RPC` which contains `dataChannel`, `deliveryMethod`, `receivers` and `parameters`
        /// </summary>
        public void CallNetFunction(string methodName, byte dataChannel, DeliveryMethod deliveryMethod, FunctionReceivers receivers, params object[] parameters)
        {
            RPC(methodName, dataChannel, deliveryMethod, receivers, parameters);
        }

        /// <summary>
        /// Call elastic RPC, it can be `All RPC` or `Server RPC` up to how you define `receivers`
        /// </summary>
        /// <param name="methodName"></param>
        /// <param name="dataChannel"></param>
        /// <param name="deliveryMethod"></param>
        /// <param name="receivers"></param>
        /// <param name="parameters"></param>
        public void RPC(string methodName, byte dataChannel, DeliveryMethod deliveryMethod, FunctionReceivers receivers, params object[] parameters)
        {
            int elementId;
            switch (receivers)
            {
                case FunctionReceivers.All:
                    if (allRpcIds.TryGetValue(MakeNetFunctionId(methodName), out elementId))
                    {
                        Identity.NetFunctions[elementId].Call(dataChannel, deliveryMethod, receivers, parameters);
                    }
                    else
                    {
                        if (Manager.LogError)
                            Logging.LogError(LogTag, "[" + TypeName + "] cannot call rpc, any rpc [" + methodName + "] not found.");
                    }
                    break;
                case FunctionReceivers.Server:
                    if (serverRpcIds.TryGetValue(MakeNetFunctionId(methodName), out elementId))
                    {
                        Identity.NetFunctions[elementId].Call(dataChannel, deliveryMethod, receivers, parameters);
                    }
                    else
                    {
                        if (Manager.LogError)
                            Logging.LogError(LogTag, "[" + TypeName + "] cannot call rpc, any rpc [" + methodName + "] not found.");
                    }
                    break;
                default:
                    if (Manager.LogError)
                        Logging.LogError(LogTag, "[" + TypeName + "] cannot call rpc, rpc [" + methodName + "] receives must be `All` or `Server`.");
                    break;
            }
        }

        /// <summary>
        /// Call `All RPC` or `Server RPC`, if it's elastic RPC, it will call `All RPC`
        /// </summary>
        /// <param name="methodName"></param>
        /// <param name="parameters"></param>
        public void RPC(string methodName, params object[] parameters)
        {
            RPC(methodName, 0, DeliveryMethod.ReliableOrdered, parameters);
        }

        /// <summary>
        /// Call `All RPC` or `Server RPC`, if it's elastic RPC, it will call `All RPC`
        /// </summary>
        /// <param name="methodName"></param>
        /// <param name="dataChannel"></param>
        /// <param name="deliveryMethod"></param>
        /// <param name="parameters"></param>
        public void RPC(string methodName, byte dataChannel, DeliveryMethod deliveryMethod, params object[] parameters)
        {
            int elementId;
            if (allRpcIds.TryGetValue(MakeNetFunctionId(methodName), out elementId))
            {
                Identity.NetFunctions[elementId].Call(dataChannel, deliveryMethod, FunctionReceivers.All, parameters);
            }
            else if (serverRpcIds.TryGetValue(MakeNetFunctionId(methodName), out elementId))
            {
                Identity.NetFunctions[elementId].Call(dataChannel, deliveryMethod, FunctionReceivers.Server, parameters);
            }
            else
            {
                if (Manager.LogError)
                    Logging.LogError(LogTag, "[" + TypeName + "] cannot call rpc, client or server rpc [" + methodName + "] not found.");
            }
        }

        /// <summary>
        /// This is another synonym of `RPC` which contains `connectionId` and `parameters`
        /// </summary>
        public void CallNetFunction(string methodName, long connectionId, params object[] parameters)
        {
            RPC(methodName, connectionId, parameters);
        }

        /// <summary>
        /// Call function at target client by connection id
        /// </summary>
        /// <param name="methodName"></param>
        /// <param name="connectionId"></param>
        /// <param name="parameters"></param>
        public void RPC(string methodName, long connectionId, params object[] parameters)
        {
            int elementId;
            if (targetRpcIds.TryGetValue(MakeNetFunctionId(methodName), out elementId))
            {
                Identity.NetFunctions[elementId].Call(0, DeliveryMethod.ReliableOrdered, connectionId, parameters);
            }
            else
            {
                if (Manager.LogError)
                    Logging.LogError(LogTag, "[" + TypeName + "] cannot call rpc, target rpc [" + methodName + "] not found.");
            }
        }

        private string MakeNetFunctionId(MethodInfo methodInfo)
        {
            return MakeNetFunctionId(methodInfo.Name);
        }

        private string MakeNetFunctionId(string methodName)
        {
            return new StringBuilder(TypeName).Append('+').Append(methodName).ToString();
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

        public void SetOwnerClient(long connectionId)
        {
            Identity.SetOwnerClient(connectionId);
        }

        public void NetworkDestroy()
        {
            Identity.NetworkDestroy();
        }

        public void NetworkDestroy(float delay)
        {
            Identity.NetworkDestroy(delay);
        }

        public void ClientSendPacket(byte dataChannel, DeliveryMethod deliveryMethod, ushort msgType, SerializerDelegate serializerDelegate)
        {
            if (!IsClient)
                return;
            Manager.ClientSendPacket(dataChannel, deliveryMethod, msgType, serializerDelegate);
        }

        public void ServerSendPacket(long connectionId, byte dataChannel, DeliveryMethod deliveryMethod, ushort msgType, SerializerDelegate serializerDelegate)
        {
            if (!IsServer)
                return;
            Manager.ServerSendPacket(connectionId, dataChannel, deliveryMethod, msgType, serializerDelegate);
        }

        public void ServerSendPacketToAllConnections(byte dataChannel, DeliveryMethod deliveryMethod, ushort msgType, SerializerDelegate serializerDelegate)
        {
            if (!IsServer)
                return;
            Manager.ServerSendPacketToAllConnections(dataChannel, deliveryMethod, msgType, serializerDelegate);
        }

        public void ServerSendPacketToSubscribers(byte dataChannel, DeliveryMethod deliveryMethod, ushort msgType, SerializerDelegate serializerDelegate)
        {
            if (!IsServer)
                return;
            foreach (long connectionId in Manager.GetConnectionIds())
            {
                if (Identity.HasSubscriber(connectionId))
                    Manager.ServerSendPacket(connectionId, dataChannel, deliveryMethod, msgType, serializerDelegate);
            }
        }

        /// <summary>
        /// This function will be called when this behaviour spawned at server
        /// </summary>
        public virtual void OnStartServer() { }

        /// <summary>
        /// This function will be called when this behaviour spawned at client (all client types `non-owner and owner`)
        /// </summary>
        public virtual void OnStartClient() { }

        /// <summary>
        /// This function will be called when this behaviour spawned at owner-client
        /// </summary>
        public virtual void OnStartOwnerClient() { }

        /// <summary>
        /// This function will be called when this client has been verified as owner client
        /// </summary>
        public virtual void OnSetOwnerClient(bool isOwnerClient) { }

        /// <summary>
        /// This function will be called when this client receive spawn object message with position and rotation
        /// </summary>
        /// <param name="position"></param>
        /// <param name="rotation"></param>
        public virtual void InitTransform(Vector3 position, Quaternion rotation) { }

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
        /// Override this function to define condition to sync behaviour data to client or not
        /// </summary>
        /// <returns></returns>
        public virtual bool CanSyncBehaviour()
        {
            return false;
        }

        /// <summary>
        /// Override this function to define condition to sync behaviour data to client when it should
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
