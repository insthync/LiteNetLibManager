using Cysharp.Text;
using LiteNetLib;
using LiteNetLib.Utils;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using UnityEngine;

namespace LiteNetLibManager
{
    public partial class LiteNetLibBehaviour : MonoBehaviour
    {
        public const string TAG_NULL = "<NULL_B>";

        private class CacheFunctions
        {
            public readonly List<MethodInfo> Functions = new List<MethodInfo>();
            public readonly List<MethodInfo> FunctionsCanCallByEveryone = new List<MethodInfo>();
        }

        [ReadOnly, SerializeField]
        private byte _behaviourIndex;
        public byte BehaviourIndex
        {
            get { return _behaviourIndex; }
        }

        private static readonly Dictionary<string, List<FieldInfo>> s_CacheSyncElements = new Dictionary<string, List<FieldInfo>>();
        private static readonly Dictionary<string, CacheFunctions> s_CacheElasticRpcs = new Dictionary<string, CacheFunctions>();
        private static readonly Dictionary<string, CacheFunctions> s_CacheTargetRpcs = new Dictionary<string, CacheFunctions>();
        private static readonly Dictionary<string, CacheFunctions> s_CacheAllRpcs = new Dictionary<string, CacheFunctions>();
        private static readonly Dictionary<string, CacheFunctions> s_CacheServerRpcs = new Dictionary<string, CacheFunctions>();
        private static readonly Dictionary<string, Type[]> s_CacheDyncnamicFunctionTypes = new Dictionary<string, Type[]>();

        private readonly Dictionary<string, int> _targetRpcIds = new Dictionary<string, int>();
        private readonly Dictionary<string, int> _allRpcIds = new Dictionary<string, int>();
        private readonly Dictionary<string, int> _serverRpcIds = new Dictionary<string, int>();

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

        public bool IsSpawned
        {
            get { return Identity.IsSpawned; }
        }

        public bool IsDestroyed
        {
            get { return Identity.IsDestroyed; }
        }

        public long ConnectionId
        {
            get { return Identity.ConnectionId; }
        }

        public uint ObjectId
        {
            get { return Identity.ObjectId; }
        }

        public byte SyncChannelId
        {
            get { return Identity.SyncChannelId; }
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
            get { return Identity.IsOwnerHost; }
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

        public virtual string LogTag
        {
            get
            {
                using (var stringBuilder = ZString.CreateStringBuilder(false))
                {
                    if (Manager != null)
                    {
                        stringBuilder.Append(Manager.LogTag);
                    }
                    else
                    {
                        stringBuilder.Append(LiteNetLibManager.TAG_NULL);
                    }
                    stringBuilder.Append('.');
                    if (this != null)
                    {
                        stringBuilder.Append(name);
                        stringBuilder.Append('<');
                        stringBuilder.Append('B');
                        stringBuilder.Append('_');
                        stringBuilder.Append(GetType().Name);
                        stringBuilder.Append('>');
                    }
                    else
                    {
                        stringBuilder.Append(TAG_NULL);
                    }
                    return stringBuilder.ToString();
                }
            }
        }

        public void Setup(byte behaviourIndex)
        {
            _behaviourIndex = behaviourIndex;
            OnSetup();
            CacheElements(s_CacheSyncElements);
            CacheRpcs<ElasticRpcAttribute>(_serverRpcIds, s_CacheElasticRpcs);
            CacheRpcs<ElasticRpcAttribute>(_allRpcIds, s_CacheElasticRpcs);
            CacheRpcs<ElasticRpcAttribute>(_targetRpcIds, s_CacheElasticRpcs);
            CacheRpcs<ServerRpcAttribute>(_serverRpcIds, s_CacheServerRpcs);
            CacheRpcs<AllRpcAttribute>(_allRpcIds, s_CacheAllRpcs);
            CacheRpcs<TargetRpcAttribute>(_targetRpcIds, s_CacheTargetRpcs);
        }

        private void CacheElements(Dictionary<string, List<FieldInfo>> cacheDict)
        {
            Type baseType = GetType();
            string typeName = baseType.FullName;

            // Find sync elements
            if (!cacheDict.TryGetValue(typeName, out List<FieldInfo> syncElementFieldInfos))
            {
                syncElementFieldInfos = new List<FieldInfo>();
                HashSet<string> tempLookupNames = new HashSet<string>();
                FieldInfo[] tempLookupFields;
                Type tempLookupType = baseType;
                // Find for sync elements from this class
                while (tempLookupType != null && tempLookupType != typeof(LiteNetLibBehaviour))
                {
                    tempLookupFields = tempLookupType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    foreach (FieldInfo lookupField in tempLookupFields)
                    {
                        // Avoid duplicate fields
                        if (tempLookupNames.Contains(lookupField.Name))
                            continue;

                        if (lookupField.FieldType.IsSubclassOf(typeof(LiteNetLibSyncElement)))
                            syncElementFieldInfos.Add(lookupField);

                        tempLookupNames.Add(lookupField.Name);
                    }
                    tempLookupType = tempLookupType.BaseType;
                }
                tempLookupFields = null;
                tempLookupType = null;
                cacheDict.Add(typeName, syncElementFieldInfos);
            }

            if (syncElementFieldInfos.Count == 0)
                return;

            // Setup sync elements
            foreach (FieldInfo syncElementFieldInfo in syncElementFieldInfos)
            {
                LiteNetLibSyncElement syncElement = (LiteNetLibSyncElement)syncElementFieldInfo.GetValue(this);
                if (syncElement == null)
                    continue;
                int syncElementId = LiteNetLibIdentity.GetHashedId(MakeSyncElementId(syncElementFieldInfo));
                syncElement.Setup(this, syncElementId);
                Identity.SyncElements[syncElementId] = syncElement;
            }
        }

        private void CacheRpcs<RpcType>(Dictionary<string, int> ids, Dictionary<string, CacheFunctions> cacheDict)
            where RpcType : RpcAttribute
        {
            Type baseType = GetType();
            string typeName = baseType.FullName;

            if (!cacheDict.TryGetValue(typeName, out CacheFunctions cacheFunctions))
            {
                cacheFunctions = new CacheFunctions();
                HashSet<string> tempLookupNames = new HashSet<string>();
                MethodInfo[] tempLookupMethods;
                Type tempLookupType = baseType;
                RpcType tempAttribute;

                while (tempLookupType != null && tempLookupType != typeof(LiteNetLibBehaviour))
                {
                    tempLookupMethods = tempLookupType.GetMethods(
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);

                    foreach (MethodInfo lookupMethod in tempLookupMethods)
                    {
                        tempAttribute = lookupMethod.GetCustomAttribute<RpcType>();
                        if (tempAttribute == null)
                            continue;

                        if (tempLookupNames.Contains(lookupMethod.Name))
                            continue;

                        if (lookupMethod.ReturnType != typeof(void))
                        {
                            if (Manager.LogError)
                                Logging.LogError(LogTag, $"Cannot register RPC [{lookupMethod.Name}] return type must be void.");
                            continue;
                        }

                        if (!tempAttribute.canCallByEveryone)
                            cacheFunctions.Functions.Add(lookupMethod);
                        else
                            cacheFunctions.FunctionsCanCallByEveryone.Add(lookupMethod);

                        tempLookupNames.Add(lookupMethod.Name);
                    }

                    tempLookupType = tempLookupType.BaseType;
                }

                cacheDict.Add(typeName, cacheFunctions);
            }

            SetupRpcs(ids, cacheFunctions.Functions, false);
            SetupRpcs(ids, cacheFunctions.FunctionsCanCallByEveryone, true);
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
                    Logging.LogError(LogTag, $"[{GetType().FullName}] Cannot register rpc with existed id [{id}].");
                return;
            }
            int elementId = LiteNetLibIdentity.GetHashedId(id);
            netFunction.Setup(this, elementId);
            netFunction.CanCallByEveryone = canCallByEveryone;
            Identity.NetFunctions[elementId] = netFunction;
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
            RPC(func.Method.Name, Identity.DefaultRpcChannelId, DeliveryMethod.ReliableOrdered, receivers);
        }

        public void RPC<T1>(NetFunctionDelegate<T1> func, FunctionReceivers receivers, T1 param1)
        {
            RPC(func.Method.Name, Identity.DefaultRpcChannelId, DeliveryMethod.ReliableOrdered, receivers, param1);
        }

        public void RPC<T1, T2>(NetFunctionDelegate<T1, T2> func, FunctionReceivers receivers, T1 param1, T2 param2)
        {
            RPC(func.Method.Name, Identity.DefaultRpcChannelId, DeliveryMethod.ReliableOrdered, receivers, param1, param2);
        }

        public void RPC<T1, T2, T3>(NetFunctionDelegate<T1, T2, T3> func, FunctionReceivers receivers, T1 param1, T2 param2, T3 param3)
        {
            RPC(func.Method.Name, Identity.DefaultRpcChannelId, DeliveryMethod.ReliableOrdered, receivers, param1, param2, param3);
        }

        public void RPC<T1, T2, T3, T4>(NetFunctionDelegate<T1, T2, T3, T4> func, FunctionReceivers receivers, T1 param1, T2 param2, T3 param3, T4 param4)
        {
            RPC(func.Method.Name, Identity.DefaultRpcChannelId, DeliveryMethod.ReliableOrdered, receivers, param1, param2, param3, param4);
        }

        public void RPC<T1, T2, T3, T4, T5>(NetFunctionDelegate<T1, T2, T3, T4, T5> func, FunctionReceivers receivers, T1 param1, T2 param2, T3 param3, T4 param4, T5 param5)
        {
            RPC(func.Method.Name, Identity.DefaultRpcChannelId, DeliveryMethod.ReliableOrdered, receivers, param1, param2, param3, param4, param5);
        }

        public void RPC<T1, T2, T3, T4, T5, T6>(NetFunctionDelegate<T1, T2, T3, T4, T5, T6> func, FunctionReceivers receivers, T1 param1, T2 param2, T3 param3, T4 param4, T5 param5, T6 param6)
        {
            RPC(func.Method.Name, Identity.DefaultRpcChannelId, DeliveryMethod.ReliableOrdered, receivers, param1, param2, param3, param4, param5, param6);
        }

        public void RPC<T1, T2, T3, T4, T5, T6, T7>(NetFunctionDelegate<T1, T2, T3, T4, T5, T6, T7> func, FunctionReceivers receivers, T1 param1, T2 param2, T3 param3, T4 param4, T5 param5, T6 param6, T7 param7)
        {
            RPC(func.Method.Name, Identity.DefaultRpcChannelId, DeliveryMethod.ReliableOrdered, receivers, param1, param2, param3, param4, param5, param6, param7);
        }

        public void RPC<T1, T2, T3, T4, T5, T6, T7, T8>(NetFunctionDelegate<T1, T2, T3, T4, T5, T6, T7, T8> func, FunctionReceivers receivers, T1 param1, T2 param2, T3 param3, T4 param4, T5 param5, T6 param6, T7 param7, T8 param8)
        {
            RPC(func.Method.Name, Identity.DefaultRpcChannelId, DeliveryMethod.ReliableOrdered, receivers, param1, param2, param3, param4, param5, param6, param7, param8);
        }

        public void RPC<T1, T2, T3, T4, T5, T6, T7, T8, T9>(NetFunctionDelegate<T1, T2, T3, T4, T5, T6, T7, T8, T9> func, FunctionReceivers receivers, T1 param1, T2 param2, T3 param3, T4 param4, T5 param5, T6 param6, T7 param7, T8 param8, T9 param9)
        {
            RPC(func.Method.Name, Identity.DefaultRpcChannelId, DeliveryMethod.ReliableOrdered, receivers, param1, param2, param3, param4, param5, param6, param7, param8, param9);
        }

        public void RPC<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(NetFunctionDelegate<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> func, FunctionReceivers receivers, T1 param1, T2 param2, T3 param3, T4 param4, T5 param5, T6 param6, T7 param7, T8 param8, T9 param9, T10 param10)
        {
            RPC(func.Method.Name, Identity.DefaultRpcChannelId, DeliveryMethod.ReliableOrdered, receivers, param1, param2, param3, param4, param5, param6, param7, param8, param9, param10);
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
            RPC(func.Method.Name, Identity.DefaultRpcChannelId, DeliveryMethod.ReliableOrdered);
        }

        public void RPC<T1>(NetFunctionDelegate<T1> func, T1 param1)
        {
            RPC(func.Method.Name, Identity.DefaultRpcChannelId, DeliveryMethod.ReliableOrdered, param1);
        }

        public void RPC<T1, T2>(NetFunctionDelegate<T1, T2> func, T1 param1, T2 param2)
        {
            RPC(func.Method.Name, Identity.DefaultRpcChannelId, DeliveryMethod.ReliableOrdered, param1, param2);
        }

        public void RPC<T1, T2, T3>(NetFunctionDelegate<T1, T2, T3> func, T1 param1, T2 param2, T3 param3)
        {
            RPC(func.Method.Name, Identity.DefaultRpcChannelId, DeliveryMethod.ReliableOrdered, param1, param2, param3);
        }

        public void RPC<T1, T2, T3, T4>(NetFunctionDelegate<T1, T2, T3, T4> func, T1 param1, T2 param2, T3 param3, T4 param4)
        {
            RPC(func.Method.Name, Identity.DefaultRpcChannelId, DeliveryMethod.ReliableOrdered, param1, param2, param3, param4);
        }

        public void RPC<T1, T2, T3, T4, T5>(NetFunctionDelegate<T1, T2, T3, T4, T5> func, T1 param1, T2 param2, T3 param3, T4 param4, T5 param5)
        {
            RPC(func.Method.Name, Identity.DefaultRpcChannelId, DeliveryMethod.ReliableOrdered, param1, param2, param3, param4, param5);
        }

        public void RPC<T1, T2, T3, T4, T5, T6>(NetFunctionDelegate<T1, T2, T3, T4, T5, T6> func, T1 param1, T2 param2, T3 param3, T4 param4, T5 param5, T6 param6)
        {
            RPC(func.Method.Name, Identity.DefaultRpcChannelId, DeliveryMethod.ReliableOrdered, param1, param2, param3, param4, param5, param6);
        }

        public void RPC<T1, T2, T3, T4, T5, T6, T7>(NetFunctionDelegate<T1, T2, T3, T4, T5, T6, T7> func, T1 param1, T2 param2, T3 param3, T4 param4, T5 param5, T6 param6, T7 param7)
        {
            RPC(func.Method.Name, Identity.DefaultRpcChannelId, DeliveryMethod.ReliableOrdered, param1, param2, param3, param4, param5, param6, param7);
        }

        public void RPC<T1, T2, T3, T4, T5, T6, T7, T8>(NetFunctionDelegate<T1, T2, T3, T4, T5, T6, T7, T8> func, T1 param1, T2 param2, T3 param3, T4 param4, T5 param5, T6 param6, T7 param7, T8 param8)
        {
            RPC(func.Method.Name, Identity.DefaultRpcChannelId, DeliveryMethod.ReliableOrdered, param1, param2, param3, param4, param5, param6, param7, param8);
        }

        public void RPC<T1, T2, T3, T4, T5, T6, T7, T8, T9>(NetFunctionDelegate<T1, T2, T3, T4, T5, T6, T7, T8, T9> func, T1 param1, T2 param2, T3 param3, T4 param4, T5 param5, T6 param6, T7 param7, T8 param8, T9 param9)
        {
            RPC(func.Method.Name, Identity.DefaultRpcChannelId, DeliveryMethod.ReliableOrdered, param1, param2, param3, param4, param5, param6, param7, param8, param9);
        }

        public void RPC<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(NetFunctionDelegate<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> func, T1 param1, T2 param2, T3 param3, T4 param4, T5 param5, T6 param6, T7 param7, T8 param8, T9 param9, T10 param10)
        {
            RPC(func.Method.Name, Identity.DefaultRpcChannelId, DeliveryMethod.ReliableOrdered, param1, param2, param3, param4, param5, param6, param7, param8, param9, param10);
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
            RPC(methodName, Identity.DefaultRpcChannelId, DeliveryMethod.ReliableOrdered, receivers, parameters);
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
            string id = MakeNetFunctionId(methodName);
            int elementId;
            switch (receivers)
            {
                case FunctionReceivers.All:
                    if (_allRpcIds.TryGetValue(id, out elementId))
                    {
                        Identity.NetFunctions[elementId].Call(dataChannel, deliveryMethod, receivers, parameters);
                    }
                    else
                    {
                        if (Manager.LogError)
                            Logging.LogError(LogTag, $"[{GetType().FullName}] cannot call rpc, any rpc [{methodName}] not found.");
                    }
                    break;
                case FunctionReceivers.Server:
                    if (_serverRpcIds.TryGetValue(id, out elementId))
                    {
                        Identity.NetFunctions[elementId].Call(dataChannel, deliveryMethod, receivers, parameters);
                    }
                    else
                    {
                        if (Manager.LogError)
                            Logging.LogError(LogTag, $"[{GetType().FullName}] cannot call rpc, any rpc [{methodName}] not found.");
                    }
                    break;
                default:
                    if (Manager.LogError)
                        Logging.LogError(LogTag, $"[{GetType().FullName}] cannot call rpc, rpc [{methodName}] receives must be `All` or `Server`.");
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
            RPC(methodName, Identity.DefaultRpcChannelId, DeliveryMethod.ReliableOrdered, parameters);
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
            string id = MakeNetFunctionId(methodName);
            int elementId;
            if (_allRpcIds.TryGetValue(id, out elementId))
            {
                Identity.NetFunctions[elementId].Call(dataChannel, deliveryMethod, FunctionReceivers.All, parameters);
            }
            else if (_serverRpcIds.TryGetValue(id, out elementId))
            {
                Identity.NetFunctions[elementId].Call(dataChannel, deliveryMethod, FunctionReceivers.Server, parameters);
            }
            else
            {
                if (Manager.LogError)
                    Logging.LogError(LogTag, $"[{GetType().FullName}] cannot call rpc, client or server rpc [{methodName}] not found.");
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
            string id = MakeNetFunctionId(methodName);
            int elementId;
            if (_targetRpcIds.TryGetValue(id, out elementId))
            {
                Identity.NetFunctions[elementId].Call(Identity.DefaultRpcChannelId, DeliveryMethod.ReliableOrdered, connectionId, parameters);
            }
            else
            {
                if (Manager.LogError)
                    Logging.LogError(LogTag, $"[{GetType().FullName}] cannot call rpc, target rpc [{methodName}] not found.");
            }
        }

        private string MakeNetFunctionId(MethodInfo methodInfo)
        {
            return MakeNetFunctionId(methodInfo.Name);
        }

        private string MakeNetFunctionId(string methodName)
        {
            using (var stringBuilder = ZString.CreateStringBuilder(true))
            {
                stringBuilder.Append(GetType().FullName);
                stringBuilder.Append('_');
                stringBuilder.Append(_behaviourIndex);
                stringBuilder.Append('_');
                stringBuilder.Append(methodName);
                return stringBuilder.ToString();
            }
        }

        private string MakeSyncElementId(FieldInfo fieldInfo)
        {
            return MakeSyncElementId(fieldInfo.Name);
        }

        private string MakeSyncElementId(string elementName)
        {
            using (var stringBuilder = ZString.CreateStringBuilder(true))
            {
                stringBuilder.Append(GetType().FullName);
                stringBuilder.Append('_');
                stringBuilder.Append(_behaviourIndex);
                stringBuilder.Append('_');
                stringBuilder.Append(elementName);
                return stringBuilder.ToString();
            }
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
        /// This function will be called when its identity initialized
        /// </summary>
        public virtual void OnIdentityInitialize() { }

        /// <summary>
        /// This function will be called when its identity destroyed
        /// </summary>
        public virtual void OnIdentityDestroy() { }

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
        /// Override this function to change object visibility when this added to player as subcribing
        /// </summary>
        public virtual void OnServerSubscribingAdded() { }

        /// <summary>
        /// Override this function to change object visibility when this removed from player as subcribing
        /// </summary>
        public virtual void OnServerSubscribingRemoved() { }
    }
}
