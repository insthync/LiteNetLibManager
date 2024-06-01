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
        public const string TAG_NULL = "<NULL_B>";

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

        private float _nextSyncTime;

        private static readonly Dictionary<string, CacheFields> s_CacheSyncElements = new Dictionary<string, CacheFields>();
        private static readonly Dictionary<string, CacheFunctions> s_CacheElasticRpcs = new Dictionary<string, CacheFunctions>();
        private static readonly Dictionary<string, CacheFunctions> s_CacheTargetRpcs = new Dictionary<string, CacheFunctions>();
        private static readonly Dictionary<string, CacheFunctions> s_CacheAllRpcs = new Dictionary<string, CacheFunctions>();
        private static readonly Dictionary<string, CacheFunctions> s_CacheServerRpcs = new Dictionary<string, CacheFunctions>();
        private static readonly Dictionary<string, MethodInfo> s_CacheOnChangeFunctions = new Dictionary<string, MethodInfo>();
        private static readonly Dictionary<string, MethodInfo> s_CacheOnUpdateFunctions = new Dictionary<string, MethodInfo>();
        private static readonly Dictionary<string, Type[]> s_CacheDyncnamicFunctionTypes = new Dictionary<string, Type[]>();

        private readonly Dictionary<string, int> _targetRpcIds = new Dictionary<string, int>();
        private readonly Dictionary<string, int> _allRpcIds = new Dictionary<string, int>();
        private readonly Dictionary<string, int> _serverRpcIds = new Dictionary<string, int>();

        // Optimize garbage collector
        private Type _tempLookupType;
        private HashSet<string> _tempLookupNames = new HashSet<string>();
        private FieldInfo[] _tempLookupFields;
        private MethodInfo[] _tempLookupMethods;

        private Type _classType;
        /// <summary>
        /// This will be used when setup sync fields and sync lists
        /// </summary>
        public Type ClassType
        {
            get
            {
                if (_classType == null)
                    _classType = GetType();
                return _classType;
            }
        }

        /// <summary>
        /// This will be used when setup sync fields and sync lists as key for cached fields
        /// </summary>
        public string TypeName
        {
            get { return ClassType.FullName; }
        }

        private bool _isFoundIdentity;
        private LiteNetLibIdentity _identity;
        public LiteNetLibIdentity Identity
        {
            get
            {
                if (!_isFoundIdentity)
                {
                    _identity = GetComponent<LiteNetLibIdentity>();
                    if (_identity == null)
                        _identity = GetComponentInParent<LiteNetLibIdentity>();
                    _isFoundIdentity = _identity != null;
                }
                return _identity;
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

        public LiteNetLibPlayer Player
        {
            get { return Identity.Player; }
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

        public bool IsOwnerHost
        {
            get { return IsServer && IsOwnerClient; }
        }

        public bool IsOwnedByServer
        {
            get { return Identity.IsOwnedByServer; }
        }

        public bool IsOwnerClientOrOwnedByServer
        {
            get { return Identity.IsOwnerClientOrOwnedByServer; }
        }

        public bool IsSceneObject
        {
            get { return Identity.IsSceneObject; }
        }

        private string _logTag;
        public virtual string LogTag
        {
            get
            {
                if (string.IsNullOrEmpty(_logTag))
                {
                    string managerTag = Manager != null ? Manager.LogTag : LiteNetLibManager.TAG_NULL;
                    string behaviourTag = this != null ? $"{name}<B_{GetType().Name}>" : TAG_NULL;
                    _logTag = $"{managerTag}.{behaviourTag}";
                }
                return _logTag;
            }
        }

        /// <summary>
        /// Return `TRUE` to determine that the update is done and unregister updating
        /// </summary>
        /// <param name="currentTime"></param>
        /// <returns></returns>
        internal bool NetworkUpdate(float currentTime)
        {
            // Sync behaviour
            if (!IsServer || !CanSyncBehaviour())
                return true;

            // Is it time to sync?
            if (currentTime < _nextSyncTime)
                return false;

            // Set next sync time
            _nextSyncTime = currentTime + sendInterval;

            // Should not sync yet, will sync next time
            if (ShouldSyncBehaviour())
                return false;

            Profiler.BeginSample("LiteNetLibBehaviour - Update Sync Behaviour");
            {
                LiteNetLibGameManager manager = Manager;
                LiteNetLibServer server = manager.Server;
                TransportHandler.WritePacket(server.s_Writer, GameMsgTypes.ServerSyncBehaviour);
                Serialize(server.s_Writer);
                foreach (long connectionId in manager.GetConnectionIds())
                {
                    if (Identity.HasSubscriberOrIsOwning(connectionId))
                        server.SendMessage(connectionId, dataChannel, sendOptions, server.s_Writer);
                }
            }
            Profiler.EndSample();
            return true;
        }

        public void Setup(byte behaviourIndex)
        {
            this.behaviourIndex = behaviourIndex;
            OnSetup();
            CacheElements();
            CacheRpcs<ElasticRpcAttribute>(_serverRpcIds, s_CacheElasticRpcs);
            CacheRpcs<ElasticRpcAttribute>(_allRpcIds, s_CacheElasticRpcs);
            CacheRpcs<ElasticRpcAttribute>(_targetRpcIds, s_CacheElasticRpcs);
            CacheRpcs<ServerRpcAttribute>(_serverRpcIds, s_CacheServerRpcs);
            CacheRpcs<AllRpcAttribute>(_allRpcIds, s_CacheAllRpcs);
            CacheRpcs<TargetRpcAttribute>(_targetRpcIds, s_CacheTargetRpcs);
        }

        private void CacheElements()
        {
            CacheFields tempCacheFields;
            if (!s_CacheSyncElements.TryGetValue(TypeName, out tempCacheFields))
            {
                tempCacheFields = new CacheFields()
                {
                    syncFields = new List<FieldInfo>(),
                    syncLists = new List<FieldInfo>(),
                    syncFieldsWithAttribute = new List<FieldInfo>()
                };
                _tempLookupNames.Clear();
                _tempLookupType = ClassType;
                SyncFieldAttribute tempAttribute = null;
                // Find for sync field and sync list from the class
                while (_tempLookupType != null && _tempLookupType != typeof(LiteNetLibBehaviour))
                {
                    _tempLookupFields = _tempLookupType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    foreach (FieldInfo lookupField in _tempLookupFields)
                    {
                        // Avoid duplicate fields
                        if (_tempLookupNames.Contains(lookupField.Name))
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

                        _tempLookupNames.Add(lookupField.Name);
                    }
                    _tempLookupType = _tempLookupType.BaseType;
                }
                // Sort name to make sure the fields will be sync correctly by its index
                tempCacheFields.syncFields.Sort((a, b) => a.Name.ToLower().CompareTo(b.Name.ToLower()));
                tempCacheFields.syncLists.Sort((a, b) => a.Name.ToLower().CompareTo(b.Name.ToLower()));
                s_CacheSyncElements.Add(TypeName, tempCacheFields);
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

            StringBuilder stringBuilder = new StringBuilder();
            SyncFieldAttribute tempAttribute;
            LiteNetLibSyncField tempSyncField;
            MethodInfo tempOnChangeMethod;
            MethodInfo tempOnUpdateMethod;
            foreach (FieldInfo fieldInfo in fieldInfos)
            {
                try
                {
                    tempAttribute = fieldInfo.GetCustomAttribute<SyncFieldAttribute>();
                    // Find on change method
                    tempOnChangeMethod = null;
                    if (!string.IsNullOrEmpty(tempAttribute.onChangeMethodName))
                    {
                        tempOnChangeMethod = FindAndCacheMethods(stringBuilder, tempAttribute.onChangeMethodName, fieldInfo, s_CacheOnChangeFunctions, (tempMethodParams) =>
                        {
                            return tempMethodParams != null && tempMethodParams.Length == 1 && tempMethodParams[0].ParameterType == fieldInfo.FieldType;
                        });
                        if (tempOnChangeMethod == null)
                        {
                            if (Manager.LogError)
                                Logging.LogError(LogTag, $"Cannot find `on change` method named [{tempAttribute.onChangeMethodName}] from [{TypeName}], FYI the function must has 1 parameter with the same type with the field.");
                        }
                    }
                    // Find on update method
                    tempOnUpdateMethod = null;
                    if (!string.IsNullOrEmpty(tempAttribute.onUpdateMethodName))
                    {
                        tempOnUpdateMethod = FindAndCacheMethods(stringBuilder, tempAttribute.onUpdateMethodName, fieldInfo, s_CacheOnUpdateFunctions, (tempMethodParams) =>
                        {
                            return tempMethodParams == null || tempMethodParams.Length == 0;
                        });
                        if (tempOnUpdateMethod == null)
                        {
                            if (Manager.LogError)
                                Logging.LogError(LogTag, $"Cannot find `on update` method named [{tempAttribute.onUpdateMethodName}] from [{TypeName}], FYI the function must has 0 parameter.");
                        }
                    }
                    // Create new sync field container
                    tempSyncField = new LiteNetLibSyncFieldContainer(fieldInfo, this, tempOnChangeMethod, tempOnUpdateMethod);
                    tempSyncField.dataChannel = tempAttribute.dataChannel;
                    tempSyncField.deliveryMethod = tempAttribute.deliveryMethod;
                    tempSyncField.clientDataChannel = tempAttribute.clientDataChannel;
                    tempSyncField.clientDeliveryMethod = tempAttribute.clientDeliveryMethod;
                    tempSyncField.sendInterval = tempAttribute.sendInterval;
                    tempSyncField.syncBehaviour = tempAttribute.syncBehaviour;
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

        private MethodInfo FindAndCacheMethods(StringBuilder stringBuilder, string methodName, FieldInfo fieldInfo, Dictionary<string, MethodInfo> dictionary, Func<ParameterInfo[], bool> parameterValidator)
        {
            MethodInfo tempMethod;
            string key = stringBuilder.Clear().Append(TypeName).Append('.').Append(methodName).ToString();
            if (!dictionary.TryGetValue(key, out tempMethod))
            {
                // Not found hook function in cache dictionary, try find the function
                _tempLookupType = ClassType;
                while (_tempLookupType != null && _tempLookupType != typeof(LiteNetLibBehaviour))
                {
                    _tempLookupMethods = _tempLookupType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                    foreach (MethodInfo lookupMethod in _tempLookupMethods)
                    {
                        // Return type must be `void`
                        if (lookupMethod.ReturnType != typeof(void))
                            continue;

                        // Not the function it's looking for
                        if (!lookupMethod.Name.Equals(methodName))
                            continue;

                        // Parameter not match
                        if (!parameterValidator.Invoke(lookupMethod.GetParameters()))
                            continue;

                        // Found the function
                        tempMethod = lookupMethod;
                        break;
                    }

                    // Found the function so exit the loop, don't find the function in base class
                    if (tempMethod != null)
                        break;

                    _tempLookupType = _tempLookupType.BaseType;
                }
                // Add to cache dictionary althrough it's empty to avoid it try to lookup next time
                dictionary.Add(key, tempMethod);
            }
            return tempMethod;
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
                _tempLookupNames.Clear();
                _tempLookupType = ClassType;
                RpcType tempAttribute;
                // Find for function with [Rpc] attribute to register as RPC
                while (_tempLookupType != null && _tempLookupType != typeof(LiteNetLibBehaviour))
                {
                    _tempLookupMethods = _tempLookupType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                    foreach (MethodInfo lookupMethod in _tempLookupMethods)
                    {
                        // Avoid duplicate functions
                        if (_tempLookupNames.Contains(lookupMethod.Name))
                            continue;

                        // Must have [Rpc] attribute
                        tempAttribute = lookupMethod.GetCustomAttribute<RpcType>();
                        if (tempAttribute == null)
                            continue;

                        // Return type must be `void`
                        if (lookupMethod.ReturnType != typeof(void))
                        {
                            if (Manager.LogError)
                                Logging.LogError(LogTag, $"Cannot register rpc [{lookupMethod.Name}] return type must be void.");
                            continue;
                        }

                        if (!tempAttribute.canCallByEveryone)
                            tempCacheFunctions.functions.Add(lookupMethod);
                        else
                            tempCacheFunctions.functionsCanCallByEveryone.Add(lookupMethod);
                        _tempLookupNames.Add(lookupMethod.Name);
                    }
                    _tempLookupType = _tempLookupType.BaseType;
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
                if (!s_CacheDyncnamicFunctionTypes.TryGetValue(tempFunctionId, out tempParamTypes))
                {
                    tempParamTypes = methodInfo.GetParameters().Select(p => p.ParameterType).ToArray();
                    s_CacheDyncnamicFunctionTypes[tempFunctionId] = tempParamTypes;
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
            RegisterRPC(_serverRpcIds, func, canCallByEveryone);
            RegisterRPC(_allRpcIds, func, canCallByEveryone);
            RegisterRPC(_targetRpcIds, func, canCallByEveryone);
        }

        public void RegisterElasticRPC<T1>(NetFunctionDelegate<T1> func, bool canCallByEveryone = false)
        {
            RegisterRPC(_serverRpcIds, func, canCallByEveryone);
            RegisterRPC(_allRpcIds, func, canCallByEveryone);
            RegisterRPC(_targetRpcIds, func, canCallByEveryone);
        }

        public void RegisterElasticRPC<T1, T2>(NetFunctionDelegate<T1, T2> func, bool canCallByEveryone = false)
        {
            RegisterRPC(_serverRpcIds, func, canCallByEveryone);
            RegisterRPC(_allRpcIds, func, canCallByEveryone);
            RegisterRPC(_targetRpcIds, func, canCallByEveryone);
        }

        public void RegisterElasticRPC<T1, T2, T3>(NetFunctionDelegate<T1, T2, T3> func, bool canCallByEveryone = false)
        {
            RegisterRPC(_serverRpcIds, func, canCallByEveryone);
            RegisterRPC(_allRpcIds, func, canCallByEveryone);
            RegisterRPC(_targetRpcIds, func, canCallByEveryone);
        }

        public void RegisterElasticRPC<T1, T2, T3, T4>(NetFunctionDelegate<T1, T2, T3, T4> func, bool canCallByEveryone = false)
        {
            RegisterRPC(_serverRpcIds, func, canCallByEveryone);
            RegisterRPC(_allRpcIds, func, canCallByEveryone);
            RegisterRPC(_targetRpcIds, func, canCallByEveryone);
        }

        public void RegisterElasticRPC<T1, T2, T3, T4, T5>(NetFunctionDelegate<T1, T2, T3, T4, T5> func, bool canCallByEveryone = false)
        {
            RegisterRPC(_serverRpcIds, func, canCallByEveryone);
            RegisterRPC(_allRpcIds, func, canCallByEveryone);
            RegisterRPC(_targetRpcIds, func, canCallByEveryone);
        }

        public void RegisterElasticRPC<T1, T2, T3, T4, T5, T6>(NetFunctionDelegate<T1, T2, T3, T4, T5, T6> func, bool canCallByEveryone = false)
        {
            RegisterRPC(_serverRpcIds, func, canCallByEveryone);
            RegisterRPC(_allRpcIds, func, canCallByEveryone);
            RegisterRPC(_targetRpcIds, func, canCallByEveryone);
        }

        public void RegisterElasticRPC<T1, T2, T3, T4, T5, T6, T7>(NetFunctionDelegate<T1, T2, T3, T4, T5, T6, T7> func, bool canCallByEveryone = false)
        {
            RegisterRPC(_serverRpcIds, func, canCallByEveryone);
            RegisterRPC(_allRpcIds, func, canCallByEveryone);
            RegisterRPC(_targetRpcIds, func, canCallByEveryone);
        }

        public void RegisterElasticRPC<T1, T2, T3, T4, T5, T6, T7, T8>(NetFunctionDelegate<T1, T2, T3, T4, T5, T6, T7, T8> func, bool canCallByEveryone = false)
        {
            RegisterRPC(_serverRpcIds, func, canCallByEveryone);
            RegisterRPC(_allRpcIds, func, canCallByEveryone);
            RegisterRPC(_targetRpcIds, func, canCallByEveryone);
        }

        public void RegisterElasticRPC<T1, T2, T3, T4, T5, T6, T7, T8, T9>(NetFunctionDelegate<T1, T2, T3, T4, T5, T6, T7, T8, T9> func, bool canCallByEveryone = false)
        {
            RegisterRPC(_serverRpcIds, func, canCallByEveryone);
            RegisterRPC(_allRpcIds, func, canCallByEveryone);
            RegisterRPC(_targetRpcIds, func, canCallByEveryone);
        }

        public void RegisterElasticRPC<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(NetFunctionDelegate<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> func, bool canCallByEveryone = false)
        {
            RegisterRPC(_serverRpcIds, func, canCallByEveryone);
            RegisterRPC(_allRpcIds, func, canCallByEveryone);
            RegisterRPC(_targetRpcIds, func, canCallByEveryone);
        }

        public void RegisterServerRPC(NetFunctionDelegate func, bool canCallByEveryone = false)
        {
            RegisterRPC(_serverRpcIds, func, canCallByEveryone);
        }

        public void RegisterServerRPC<T1>(NetFunctionDelegate<T1> func, bool canCallByEveryone = false)
        {
            RegisterRPC(_serverRpcIds, func, canCallByEveryone);
        }

        public void RegisterServerRPC<T1, T2>(NetFunctionDelegate<T1, T2> func, bool canCallByEveryone = false)
        {
            RegisterRPC(_serverRpcIds, func, canCallByEveryone);
        }

        public void RegisterServerRPC<T1, T2, T3>(NetFunctionDelegate<T1, T2, T3> func, bool canCallByEveryone = false)
        {
            RegisterRPC(_serverRpcIds, func, canCallByEveryone);
        }

        public void RegisterServerRPC<T1, T2, T3, T4>(NetFunctionDelegate<T1, T2, T3, T4> func, bool canCallByEveryone = false)
        {
            RegisterRPC(_serverRpcIds, func, canCallByEveryone);
        }

        public void RegisterServerRPC<T1, T2, T3, T4, T5>(NetFunctionDelegate<T1, T2, T3, T4, T5> func, bool canCallByEveryone = false)
        {
            RegisterRPC(_serverRpcIds, func, canCallByEveryone);
        }

        public void RegisterServerRPC<T1, T2, T3, T4, T5, T6>(NetFunctionDelegate<T1, T2, T3, T4, T5, T6> func, bool canCallByEveryone = false)
        {
            RegisterRPC(_serverRpcIds, func, canCallByEveryone);
        }

        public void RegisterServerRPC<T1, T2, T3, T4, T5, T6, T7>(NetFunctionDelegate<T1, T2, T3, T4, T5, T6, T7> func, bool canCallByEveryone = false)
        {
            RegisterRPC(_serverRpcIds, func, canCallByEveryone);
        }

        public void RegisterServerRPC<T1, T2, T3, T4, T5, T6, T7, T8>(NetFunctionDelegate<T1, T2, T3, T4, T5, T6, T7, T8> func, bool canCallByEveryone = false)
        {
            RegisterRPC(_serverRpcIds, func, canCallByEveryone);
        }

        public void RegisterServerRPC<T1, T2, T3, T4, T5, T6, T7, T8, T9>(NetFunctionDelegate<T1, T2, T3, T4, T5, T6, T7, T8, T9> func, bool canCallByEveryone = false)
        {
            RegisterRPC(_serverRpcIds, func, canCallByEveryone);
        }

        public void RegisterServerRPC<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(NetFunctionDelegate<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> func, bool canCallByEveryone = false)
        {
            RegisterRPC(_serverRpcIds, func, canCallByEveryone);
        }

        public void RegisterAllRPC(NetFunctionDelegate func, bool canCallByEveryone = false)
        {
            RegisterRPC(_allRpcIds, func, canCallByEveryone);
        }

        public void RegisterAllRPC<T1>(NetFunctionDelegate<T1> func, bool canCallByEveryone = false)
        {
            RegisterRPC(_allRpcIds, func, canCallByEveryone);
        }

        public void RegisterAllRPC<T1, T2>(NetFunctionDelegate<T1, T2> func, bool canCallByEveryone = false)
        {
            RegisterRPC(_allRpcIds, func, canCallByEveryone);
        }

        public void RegisterAllRPC<T1, T2, T3>(NetFunctionDelegate<T1, T2, T3> func, bool canCallByEveryone = false)
        {
            RegisterRPC(_allRpcIds, func, canCallByEveryone);
        }

        public void RegisterAllRPC<T1, T2, T3, T4>(NetFunctionDelegate<T1, T2, T3, T4> func, bool canCallByEveryone = false)
        {
            RegisterRPC(_allRpcIds, func, canCallByEveryone);
        }

        public void RegisterAllRPC<T1, T2, T3, T4, T5>(NetFunctionDelegate<T1, T2, T3, T4, T5> func, bool canCallByEveryone = false)
        {
            RegisterRPC(_allRpcIds, func, canCallByEveryone);
        }

        public void RegisterAllRPC<T1, T2, T3, T4, T5, T6>(NetFunctionDelegate<T1, T2, T3, T4, T5, T6> func, bool canCallByEveryone = false)
        {
            RegisterRPC(_allRpcIds, func, canCallByEveryone);
        }

        public void RegisterAllRPC<T1, T2, T3, T4, T5, T6, T7>(NetFunctionDelegate<T1, T2, T3, T4, T5, T6, T7> func, bool canCallByEveryone = false)
        {
            RegisterRPC(_allRpcIds, func, canCallByEveryone);
        }

        public void RegisterAllRPC<T1, T2, T3, T4, T5, T6, T7, T8>(NetFunctionDelegate<T1, T2, T3, T4, T5, T6, T7, T8> func, bool canCallByEveryone = false)
        {
            RegisterRPC(_allRpcIds, func, canCallByEveryone);
        }

        public void RegisterAllRPC<T1, T2, T3, T4, T5, T6, T7, T8, T9>(NetFunctionDelegate<T1, T2, T3, T4, T5, T6, T7, T8, T9> func, bool canCallByEveryone = false)
        {
            RegisterRPC(_allRpcIds, func, canCallByEveryone);
        }

        public void RegisterAllRPC<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(NetFunctionDelegate<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> func, bool canCallByEveryone = false)
        {
            RegisterRPC(_allRpcIds, func, canCallByEveryone);
        }

        public void RegisterTargetRpc(NetFunctionDelegate func, bool canCallByEveryone = false)
        {
            RegisterRPC(_targetRpcIds, func, canCallByEveryone);
        }

        public void RegisterTargetRpc<T1>(NetFunctionDelegate<T1> func, bool canCallByEveryone = false)
        {
            RegisterRPC(_targetRpcIds, func, canCallByEveryone);
        }

        public void RegisterTargetRpc<T1, T2>(NetFunctionDelegate<T1, T2> func, bool canCallByEveryone = false)
        {
            RegisterRPC(_targetRpcIds, func, canCallByEveryone);
        }

        public void RegisterTargetRpc<T1, T2, T3>(NetFunctionDelegate<T1, T2, T3> func, bool canCallByEveryone = false)
        {
            RegisterRPC(_targetRpcIds, func, canCallByEveryone);
        }

        public void RegisterTargetRpc<T1, T2, T3, T4>(NetFunctionDelegate<T1, T2, T3, T4> func, bool canCallByEveryone = false)
        {
            RegisterRPC(_targetRpcIds, func, canCallByEveryone);
        }

        public void RegisterTargetRpc<T1, T2, T3, T4, T5>(NetFunctionDelegate<T1, T2, T3, T4, T5> func, bool canCallByEveryone = false)
        {
            RegisterRPC(_targetRpcIds, func, canCallByEveryone);
        }

        public void RegisterTargetRpc<T1, T2, T3, T4, T5, T6>(NetFunctionDelegate<T1, T2, T3, T4, T5, T6> func, bool canCallByEveryone = false)
        {
            RegisterRPC(_targetRpcIds, func, canCallByEveryone);
        }

        public void RegisterTargetRpc<T1, T2, T3, T4, T5, T6, T7>(NetFunctionDelegate<T1, T2, T3, T4, T5, T6, T7> func, bool canCallByEveryone = false)
        {
            RegisterRPC(_targetRpcIds, func, canCallByEveryone);
        }

        public void RegisterTargetRpc<T1, T2, T3, T4, T5, T6, T7, T8>(NetFunctionDelegate<T1, T2, T3, T4, T5, T6, T7, T8> func, bool canCallByEveryone = false)
        {
            RegisterRPC(_targetRpcIds, func, canCallByEveryone);
        }

        public void RegisterTargetRpc<T1, T2, T3, T4, T5, T6, T7, T8, T9>(NetFunctionDelegate<T1, T2, T3, T4, T5, T6, T7, T8, T9> func, bool canCallByEveryone = false)
        {
            RegisterRPC(_targetRpcIds, func, canCallByEveryone);
        }

        public void RegisterTargetRpc<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(NetFunctionDelegate<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> func, bool canCallByEveryone = false)
        {
            RegisterRPC(_targetRpcIds, func, canCallByEveryone);
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
                    Logging.LogError(LogTag, $"[{TypeName}] cannot register rpc with existed id [{id}].");
                return;
            }
            if (Identity.NetFunctions.Count >= int.MaxValue)
            {
                if (Manager.LogError)
                    Logging.LogError(LogTag, $"[{TypeName}] cannot register rpc it's exceeds limit.");
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
                    if (_allRpcIds.TryGetValue(MakeNetFunctionId(methodName), out elementId))
                    {
                        Identity.NetFunctions[elementId].Call(dataChannel, deliveryMethod, receivers, parameters);
                    }
                    else
                    {
                        if (Manager.LogError)
                            Logging.LogError(LogTag, $"[{TypeName}] cannot call rpc, any rpc [{methodName}] not found.");
                    }
                    break;
                case FunctionReceivers.Server:
                    if (_serverRpcIds.TryGetValue(MakeNetFunctionId(methodName), out elementId))
                    {
                        Identity.NetFunctions[elementId].Call(dataChannel, deliveryMethod, receivers, parameters);
                    }
                    else
                    {
                        if (Manager.LogError)
                            Logging.LogError(LogTag, $"[{TypeName}] cannot call rpc, any rpc [{methodName}] not found.");
                    }
                    break;
                default:
                    if (Manager.LogError)
                        Logging.LogError(LogTag, $"[{TypeName}] cannot call rpc, rpc [{methodName}] receives must be `All` or `Server`.");
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
            if (_allRpcIds.TryGetValue(MakeNetFunctionId(methodName), out elementId))
            {
                Identity.NetFunctions[elementId].Call(dataChannel, deliveryMethod, FunctionReceivers.All, parameters);
            }
            else if (_serverRpcIds.TryGetValue(MakeNetFunctionId(methodName), out elementId))
            {
                Identity.NetFunctions[elementId].Call(dataChannel, deliveryMethod, FunctionReceivers.Server, parameters);
            }
            else
            {
                if (Manager.LogError)
                    Logging.LogError(LogTag, $"[{TypeName}] cannot call rpc, client or server rpc [{methodName}] not found.");
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
            if (_targetRpcIds.TryGetValue(MakeNetFunctionId(methodName), out elementId))
            {
                Identity.NetFunctions[elementId].Call(0, DeliveryMethod.ReliableOrdered, connectionId, parameters);
            }
            else
            {
                if (Manager.LogError)
                    Logging.LogError(LogTag, $"[{TypeName}] cannot call rpc, target rpc [{methodName}] not found.");
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

        public void ClientSendMessage(byte dataChannel, DeliveryMethod deliveryMethod, NetDataWriter writer)
        {
            if (!IsClient)
                return;
            Manager.ClientSendMessage(dataChannel, deliveryMethod, writer);
        }

        public void ClientSendPacket(byte dataChannel, DeliveryMethod deliveryMethod, ushort msgType, SerializerDelegate serializerDelegate)
        {
            if (!IsClient)
                return;
            Manager.ClientSendPacket(dataChannel, deliveryMethod, msgType, serializerDelegate);
        }

        public void ServerSendMessage(long connectionId, byte dataChannel, DeliveryMethod deliveryMethod, NetDataWriter writer)
        {
            if (!IsServer)
                return;
            Manager.ServerSendMessage(connectionId, dataChannel, deliveryMethod, writer);
        }

        public void ServerSendPacket(long connectionId, byte dataChannel, DeliveryMethod deliveryMethod, ushort msgType, SerializerDelegate serializerDelegate)
        {
            if (!IsServer)
                return;
            Manager.ServerSendPacket(connectionId, dataChannel, deliveryMethod, msgType, serializerDelegate);
        }

        public void ServerSendMessageToAllConnections(byte dataChannel, DeliveryMethod deliveryMethod, NetDataWriter writer)
        {
            if (!IsServer)
                return;
            Manager.ServerSendMessageToAllConnections(dataChannel, deliveryMethod, writer);
        }

        public void ServerSendPacketToAllConnections(byte dataChannel, DeliveryMethod deliveryMethod, ushort msgType, SerializerDelegate serializerDelegate)
        {
            if (!IsServer)
                return;
            Manager.ServerSendPacketToAllConnections(dataChannel, deliveryMethod, msgType, serializerDelegate);
        }

        public void ServerSendMessageToSubscribers(byte dataChannel, DeliveryMethod deliveryMethod, NetDataWriter writer)
        {
            if (!IsServer)
                return;
            foreach (long connectionId in Manager.GetConnectionIds())
            {
                if (Identity.HasSubscriber(connectionId))
                    Manager.ServerSendMessage(connectionId, dataChannel, deliveryMethod, writer);
            }
        }

        public void ServerSendPacketToSubscribers(byte dataChannel, DeliveryMethod deliveryMethod, ushort msgType, SerializerDelegate serializerDelegate)
        {
            if (!IsServer)
                return;
            TransportHandler.WritePacket(Manager.Server.s_Writer, msgType, serializerDelegate);
            foreach (long connectionId in Manager.GetConnectionIds())
            {
                if (Identity.HasSubscriber(connectionId))
                    Manager.ServerSendMessage(connectionId, dataChannel, deliveryMethod, Manager.Server.s_Writer);
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
