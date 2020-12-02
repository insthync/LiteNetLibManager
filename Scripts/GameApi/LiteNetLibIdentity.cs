using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif
using LiteNetLib.Utils;
using UnityEngine.Profiling;
using LiteNetLib;

namespace LiteNetLibManager
{
    [DisallowMultipleComponent]
    public sealed class LiteNetLibIdentity : MonoBehaviour
    {
        public static uint HighestObjectId { get; private set; }
        [LiteNetLibReadOnly, SerializeField]
        private string assetId;
        [LiteNetLibReadOnly, SerializeField]
        private uint objectId;
#if UNITY_EDITOR
        [LiteNetLibReadOnly, SerializeField]
        private List<long> subscriberIds = new List<long>();
#endif

        /// <summary>
        /// This will be true when identity setup
        /// </summary>
        public bool IsSetupBehaviours { get; private set; }
        /// <summary>
        /// List of sync fields from all behaviours (include children behaviours)
        /// </summary>
        internal readonly List<LiteNetLibSyncField> SyncFields = new List<LiteNetLibSyncField>();
        /// <summary>
        /// List of net functions from all behaviours (include children behaviours)
        /// </summary>
        internal readonly List<LiteNetLibFunction> NetFunctions = new List<LiteNetLibFunction>();
        /// <summary>
        /// List of sync lists from all behaviours (include children behaviours)
        /// </summary>
        internal readonly List<LiteNetLibSyncList> SyncLists = new List<LiteNetLibSyncList>();
        /// <summary>
        /// List of sync behaviours
        /// </summary>
        internal readonly List<LiteNetLibBehaviour> SyncBehaviours = new List<LiteNetLibBehaviour>();
        /// <summary>
        /// Array of all behaviours
        /// </summary>
        internal LiteNetLibBehaviour[] Behaviours { get; private set; }
        internal readonly Dictionary<long, LiteNetLibPlayer> Subscribers = new Dictionary<long, LiteNetLibPlayer>();

        public string AssetId { get { return assetId; } }
        public int HashAssetId
        {
            get
            {
                unchecked
                {
                    int hash1 = 5381;
                    int hash2 = hash1;

                    for (int i = 0; i < AssetId.Length && AssetId[i] != '\0'; i += 2)
                    {
                        hash1 = ((hash1 << 5) + hash1) ^ AssetId[i];
                        if (i == AssetId.Length - 1 || AssetId[i + 1] == '\0')
                            break;
                        hash2 = ((hash2 << 5) + hash2) ^ AssetId[i + 1];
                    }

                    return hash1 + (hash2 * 1566083941);
                }
            }
        }
        public uint ObjectId { get { return objectId; } }
        public long ConnectionId { get; internal set; } = -1;
        public LiteNetLibGameManager Manager { get { return LiteNetLibGameManager.Instance; } }

        private string logTag;
        public string LogTag
        {
            get
            {
                if (string.IsNullOrEmpty(logTag))
                    logTag = $"{Manager.LogTag}->{name}({GetType().Name})";
                return logTag;
            }
        }

        public LiteNetLibPlayer Player
        {
            get
            {
                LiteNetLibPlayer foundPlayer;
                if (Manager == null || !Manager.TryGetPlayer(ConnectionId, out foundPlayer))
                    return null;
                return foundPlayer;
            }
        }

        public bool IsServer
        {
            get { return Manager != null && Manager.IsServer; }
        }

        public bool IsClient
        {
            get { return Manager != null && Manager.IsClient; }
        }

        public bool IsOwnerClient
        {
            get { return IsClient && Manager.ClientConnectionId >= 0 && ConnectionId >= 0 && Manager.ClientConnectionId == ConnectionId; }
        }

        public bool IsSceneObject
        {
            get; private set;
        }

        private bool destroyed;

        internal void NetworkUpdate(float deltaTime)
        {
            if (Manager == null)
                return;

            Profiler.BeginSample("LiteNetLibIdentity - Network Update");
            int loopCounter;
            for (loopCounter = 0; loopCounter < SyncFields.Count; ++loopCounter)
            {
                SyncFields[loopCounter].NetworkUpdate(deltaTime);
            }

            for (loopCounter = 0; loopCounter < SyncBehaviours.Count; ++loopCounter)
            {
                SyncBehaviours[loopCounter].NetworkUpdate(deltaTime);
            }
            Profiler.EndSample();
        }

        #region IDs generate in Editor
#if UNITY_EDITOR
        private void OnValidate()
        {
            SetupIDs();
        }

        [ContextMenu("Reorder Scene Object Id")]
        public void ContextMenuReorderSceneObjectId()
        {
            ReorderSceneObjectId();
        }

        private void AssignAssetID(GameObject prefab)
        {
            string path = AssetDatabase.GetAssetPath(prefab);
            assetId = AssetDatabase.AssetPathToGUID(path);
        }

        private bool ThisIsAPrefab()
        {
#if UNITY_2018_3_OR_NEWER
            return PrefabUtility.IsPartOfPrefabAsset(gameObject);
#else
            PrefabType prefabType = PrefabUtility.GetPrefabType(gameObject);
            if (prefabType == PrefabType.Prefab)
                return true;
            return false;
#endif
        }

        private bool ThisIsASceneObjectWithThatReferencesPrefabAsset(out GameObject prefab)
        {
            prefab = null;
#if UNITY_2018_3_OR_NEWER
            if (!PrefabUtility.IsPartOfNonAssetPrefabInstance(gameObject))
                return false;
#else
            PrefabType prefabType = PrefabUtility.GetPrefabType(gameObject);
            if (prefabType == PrefabType.None)
                return false;
#endif
#if UNITY_2018_2_OR_NEWER
            prefab = (GameObject)PrefabUtility.GetCorrespondingObjectFromSource(gameObject);
#else
            prefab = (GameObject)PrefabUtility.GetPrefabParent(gameObject);
#endif
            if (prefab == null)
            {
                Logging.LogError(LogTag, $"Failed to find prefab parent for scene object: {gameObject.name}.");
                return false;
            }
            return true;
        }

        private void SetupIDs()
        {
            string oldAssetId = assetId;
            uint oldObjectId = objectId;
            GameObject prefab;
            if (ThisIsAPrefab())
            {
                // This is a prefab, can create prefab while playing so it will still assign asset ID and reset object ID
                AssignAssetID(gameObject);
                objectId = 0;
            }
            else if (ThisIsASceneObjectWithThatReferencesPrefabAsset(out prefab))
            {
                if (!Application.isPlaying)
                {
                    // This is a scene object with prefab link
                    AssignAssetID(prefab);
                    if (gameObject.scene == SceneManager.GetActiveScene())
                    {
                        // Assign object id if it is in scene
                        AssignSceneObjectId();
                        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                    }
                    else
                    {
                        // Difference working scene?, clear object Id
                        objectId = 0;
                    }
                }
            }
            else
            {
                if (!Application.isPlaying)
                {
                    // This is a pure scene object (Not a prefab)
                    assetId = string.Empty;
                    if (gameObject.scene == SceneManager.GetActiveScene())
                    {
                        // Assign object id if it is in scene
                        AssignSceneObjectId();
                        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                    }
                    else
                    {
                        // Difference working scene?, clear object Id
                        objectId = 0;
                    }
                }
            }
            // Do not mark dirty while playing
            if (!Application.isPlaying && (oldAssetId != assetId || oldObjectId != objectId))
                EditorUtility.SetDirty(this);
        }
#endif
        #endregion

        #region SyncField Functions
        internal LiteNetLibSyncField ProcessSyncField(LiteNetLibElementInfo info, NetDataReader reader, bool isInitial)
        {
            return ProcessSyncField(GetSyncField(info), reader, isInitial);
        }

        internal LiteNetLibSyncField ProcessSyncField(LiteNetLibSyncField syncField, NetDataReader reader, bool isInitial)
        {
            if (syncField == null)
                return null;
            syncField.Deserialize(reader, isInitial);
            return syncField;
        }

        internal LiteNetLibSyncField GetSyncField(LiteNetLibElementInfo info)
        {
            if (info.objectId != ObjectId)
                return null;
            if (info.elementId >= 0 && info.elementId < SyncFields.Count)
                return SyncFields[info.elementId];
            if (Manager.LogError)
                Logging.LogError(LogTag, $"Cannot find sync field: {info.elementId}.");
            return null;
        }
        #endregion

        #region NetFunction Functions
        internal LiteNetLibFunction ProcessNetFunction(LiteNetLibElementInfo info, NetDataReader reader, bool hookCallback)
        {
            return ProcessNetFunction(GetNetFunction(info), reader, hookCallback);
        }

        internal LiteNetLibFunction ProcessNetFunction(LiteNetLibFunction netFunction, NetDataReader reader, bool hookCallback)
        {
            if (netFunction == null)
                return null;
            netFunction.DeserializeParameters(reader);
            if (hookCallback)
                netFunction.HookCallback();
            return netFunction;
        }

        internal LiteNetLibFunction GetNetFunction(LiteNetLibElementInfo info)
        {
            if (info.objectId != ObjectId)
                return null;
            if (info.elementId >= 0 && info.elementId < NetFunctions.Count)
                return NetFunctions[info.elementId];
            if (Manager.LogError)
                Logging.LogError(LogTag, $"Cannot find net function: {info.elementId}.");
            return null;
        }
        #endregion

        #region SyncList Functions
        internal LiteNetLibSyncList ProcessSyncList(LiteNetLibElementInfo info, NetDataReader reader)
        {
            return ProcessSyncList(GetSyncList(info), reader);
        }

        internal LiteNetLibSyncList ProcessSyncList(LiteNetLibSyncList syncList, NetDataReader reader)
        {
            if (syncList == null)
                return null;
            syncList.DeserializeOperation(reader);
            return syncList;
        }

        internal LiteNetLibSyncList GetSyncList(LiteNetLibElementInfo info)
        {
            if (info.objectId != ObjectId)
                return null;
            if (info.elementId >= 0 && info.elementId < SyncLists.Count)
                return SyncLists[info.elementId];
            if (Manager.LogError)
                Logging.LogError(LogTag, $"Cannot find sync list: {info.elementId}.");
            return null;
        }
        #endregion

        internal LiteNetLibBehaviour ProcessSyncBehaviour(byte behaviourIndex, NetDataReader reader)
        {
            if (behaviourIndex >= Behaviours.Length)
                return null;
            LiteNetLibBehaviour behaviour = Behaviours[behaviourIndex];
            behaviour.Deserialize(reader);
            return behaviour;
        }

        internal bool TryGetBehaviour<T>(byte behaviourIndex, out T behaviour)
            where T : LiteNetLibBehaviour
        {
            behaviour = null;
            if (behaviourIndex >= Behaviours.Length)
                return false;
            behaviour = Behaviours[behaviourIndex] as T;
            return behaviour != null;
        }

        internal void WriteInitialSyncFields(NetDataWriter writer)
        {
            foreach (LiteNetLibSyncField field in SyncFields)
            {
                if (field.doNotSyncInitialDataImmediately)
                    continue;
                field.Serialize(writer);
            }
        }

        internal void ReadInitialSyncFields(NetDataReader reader)
        {
            foreach (LiteNetLibSyncField field in SyncFields)
            {
                if (field.doNotSyncInitialDataImmediately)
                    continue;
                field.Deserialize(reader, true);
            }
        }

        internal void SendInitSyncFields(long connectionId)
        {
            foreach (LiteNetLibSyncField field in SyncFields)
            {
                if (!field.doNotSyncInitialDataImmediately)
                    continue;
                field.SendUpdate(true, connectionId, DeliveryMethod.ReliableOrdered);
            }
        }

        internal void SendInitSyncLists(long connectionId)
        {
            foreach (LiteNetLibSyncList list in SyncLists)
            {
                for (int i = 0; i < list.Count; ++i)
                    list.SendOperation(connectionId, LiteNetLibSyncList.Operation.Insert, i);
            }
        }

        public bool IsSceneObjectExists(uint objectId)
        {
            if (Manager != null)
            {
                // If this is spawned while gameplay, find it by manager assets
                return Manager.Assets.ContainsSceneObject(objectId);
            }
            // If this is now spawned while gameplay, find objects in scene
            LiteNetLibIdentity[] netObjects = FindObjectsOfType<LiteNetLibIdentity>();
            foreach (LiteNetLibIdentity netObject in netObjects)
            {
                if (netObject.objectId == objectId && netObject != this)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Initial Identity, will be called when spawned. If object id == 0, it will generate new object id
        /// </summary>
        /// <param name="objectId"></param>
        /// <param name="connectionId"></param>
        internal void Initial(bool isSceneObject, uint objectId = 0, long connectionId = -1)
        {
            this.objectId = objectId;
            ConnectionId = connectionId;
            destroyed = false;
            if (objectId > HighestObjectId)
                HighestObjectId = objectId;
            IsSceneObject = isSceneObject;
            if (!IsSceneObject)
                AssignSceneObjectId();

            if (!IsSetupBehaviours)
            {
                // Setup behaviours index, we will use this as reference for network functions
                // NOTE: Maximum network behaviour for a identity is 255 (included children)
                Behaviours = GetComponentsInChildren<LiteNetLibBehaviour>();
                SyncBehaviours.Clear();
                byte loopCounter;
                for (loopCounter = 0; loopCounter < Behaviours.Length; ++loopCounter)
                {
                    Behaviours[loopCounter].Setup(loopCounter);
                    if (Behaviours[loopCounter].CanSyncBehaviour())
                        SyncBehaviours.Add(Behaviours[loopCounter]);
                }
                IsSetupBehaviours = true;
            }

            // If this is host, hide it then will showing when rebuild subscribers
            if (IsServer && IsClient)
                OnServerSubscribingRemoved();

            RebuildSubscribers(true);
        }

        internal void SetOwnerClient(bool isOwnerClient)
        {
            int loopCounter;
            for (loopCounter = 0; loopCounter < Behaviours.Length; ++loopCounter)
            {
                Behaviours[loopCounter].OnSetOwnerClient(isOwnerClient);
            }
        }

        internal void InitTransform(Vector3 position, Quaternion rotation)
        {
            int loopCounter;
            for (loopCounter = 0; loopCounter < Behaviours.Length; ++loopCounter)
            {
                Behaviours[loopCounter].InitTransform(position, rotation);
            }
        }

        internal void OnStartServer()
        {
            int loopCounter;
            for (loopCounter = 0; loopCounter < Behaviours.Length; ++loopCounter)
            {
                Behaviours[loopCounter].OnStartServer();
            }
        }

        internal void OnStartClient()
        {
            int loopCounter;
            for (loopCounter = 0; loopCounter < Behaviours.Length; ++loopCounter)
            {
                Behaviours[loopCounter].OnStartClient();
            }
        }

        internal void OnStartOwnerClient()
        {
            int loopCounter;
            for (loopCounter = 0; loopCounter < Behaviours.Length; ++loopCounter)
            {
                Behaviours[loopCounter].OnStartOwnerClient();
            }
        }

        internal void AssignSceneObjectId()
        {
            if (objectId == 0 || IsSceneObjectExists(objectId))
                objectId = GetNewObjectId();
        }

        internal static void ResetObjectId()
        {
            HighestObjectId = 0;
        }

        internal static uint GetNewObjectId()
        {
            LiteNetLibIdentity[] netObjects = FindObjectsOfType<LiteNetLibIdentity>();
            if (HighestObjectId == 0)
            {
                uint result = HighestObjectId;
                foreach (LiteNetLibIdentity netObject in netObjects)
                {
                    if (netObject.objectId > result)
                        result = netObject.objectId;
                }
                HighestObjectId = result;
            }
            ++HighestObjectId;
            return HighestObjectId;
        }

        private static void ReorderSceneObjectId()
        {
            ResetObjectId();
            LiteNetLibIdentity[] netObjects = FindObjectsOfType<LiteNetLibIdentity>();
            foreach (LiteNetLibIdentity netObject in netObjects)
            {
                netObject.objectId = ++HighestObjectId;
#if UNITY_EDITOR
                // Do not mark dirty while playing
                if (!Application.isPlaying)
                    EditorUtility.SetDirty(netObject);
#endif
            }
#if UNITY_EDITOR
            // Do not mark dirty while playing
            if (!Application.isPlaying)
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
#endif
        }

        public int CountSubscribers()
        {
            return Subscribers.Count;
        }

        public void ClearSubscribers()
        {
            // Only server can manage subscribers
            if (!IsServer)
                return;

            foreach (LiteNetLibPlayer subscriber in Subscribers.Values)
            {
                subscriber.RemoveSubscribing(this, false);
            }
            Subscribers.Clear();
#if UNITY_EDITOR
            subscriberIds.Clear();
#endif
        }

        public void AddSubscriber(LiteNetLibPlayer subscriber)
        {
            // Only server can manage subscribers
            if (!IsServer || subscriber == null)
                return;

            if (Subscribers.ContainsKey(subscriber.ConnectionId))
            {
                if (Manager.LogDebug)
                    Logging.Log(LogTag, $"Subscriber: {subscriber.ConnectionId} already added to {gameObject}.");
                return;
            }

            Subscribers[subscriber.ConnectionId] = subscriber;
#if UNITY_EDITOR
            if (!subscriberIds.Contains(subscriber.ConnectionId))
                subscriberIds.Add(subscriber.ConnectionId);
#endif
            subscriber.AddSubscribing(this);
        }

        public void RemoveSubscriber(LiteNetLibPlayer subscriber, bool removePlayerSubscribing)
        {
            // Only server can manage subscribers
            if (!IsServer)
                return;

            Subscribers.Remove(subscriber.ConnectionId);
#if UNITY_EDITOR
            subscriberIds.Remove(subscriber.ConnectionId);
#endif
            if (removePlayerSubscribing)
                subscriber.RemoveSubscribing(this, false);
        }

        public bool ContainsSubscriber(long connectionId)
        {
            return Subscribers.ContainsKey(connectionId);
        }

        public bool ShouldAddSubscriber(LiteNetLibPlayer subscriber)
        {
            int loopCounter;
            for (loopCounter = 0; loopCounter < Behaviours.Length; ++loopCounter)
            {
                if (!Behaviours[loopCounter].ShouldAddSubscriber(subscriber))
                    return false;
            }
            return true;
        }

        public bool IsSubscribedOrOwning(long connectionId)
        {
            return ContainsSubscriber(connectionId) || connectionId == ConnectionId;
        }

        public void RebuildSubscribers(bool initialize)
        {
            // Only server can manage subscribers
            if (!IsServer || !IsSetupBehaviours)
                return;

            LiteNetLibPlayer ownerPlayer = Player;
            if (initialize)
                AddSubscriber(ownerPlayer);

            bool hasChanges = false;
            bool shouldRebuild = false;
            HashSet<LiteNetLibPlayer> newSubscribers = new HashSet<LiteNetLibPlayer>();
            HashSet<LiteNetLibPlayer> oldSubscribers = new HashSet<LiteNetLibPlayer>(Subscribers.Values);

            int loopCounter;
            for (loopCounter = 0; loopCounter < Behaviours.Length; ++loopCounter)
            {
                shouldRebuild |= Behaviours[loopCounter].OnRebuildSubscribers(newSubscribers, initialize);
            }

            // If shouldRebuild is FALSE, it's means it does not have to rebuild subscribers
            if (!shouldRebuild)
            {
                // None of the behaviours rebuilt our subscribers, use built-in rebuild method
                if (initialize)
                {
                    foreach (LiteNetLibPlayer player in Manager.GetPlayers())
                    {
                        if (ConnectionId == player.ConnectionId || !player.IsReady)
                            continue;

                        if (ShouldAddSubscriber(player))
                            AddSubscriber(player);
                    }
                }
                return;
            }

            // Apply changes from rebuild
            foreach (LiteNetLibPlayer subscriber in newSubscribers)
            {
                if (subscriber == null)
                    continue;

                if (!subscriber.IsReady)
                {
                    if (Manager.LogWarn)
                        Logging.Log(LogTag, $"Subscriber: {subscriber.ConnectionId} is not ready.");
                    continue;
                }

                if ((ownerPlayer == null || subscriber.ConnectionId != ownerPlayer.ConnectionId) && (initialize || !oldSubscribers.Contains(subscriber)))
                {
                    subscriber.AddSubscribing(this);
                    if (Manager.LogDebug)
                        Logging.Log(LogTag, $"Add subscriber: {subscriber.ConnectionId} to {gameObject}.");
                    hasChanges = true;
                }
            }

            // Remove subscribers that is not in new subscribers list
            foreach (LiteNetLibPlayer subscriber in oldSubscribers)
            {
                if (!newSubscribers.Contains(subscriber))
                {
                    subscriber.RemoveSubscribing(this, true);
                    if (Manager.LogDebug)
                        Logging.Log(LogTag, $"Remove subscriber: {subscriber.ConnectionId} from {gameObject}.");
                    hasChanges = true;
                }
            }

            if (!hasChanges)
                return;

            // Rebuild subscribers
            Subscribers.Clear();
            foreach (LiteNetLibPlayer subscriber in newSubscribers)
                Subscribers.Add(subscriber.ConnectionId, subscriber);

#if UNITY_EDITOR
            subscriberIds.Clear();
            foreach (LiteNetLibPlayer subscriber in newSubscribers)
                subscriberIds.Add(subscriber.ConnectionId);
#endif
        }

        public void OnServerSubscribingAdded()
        {
            int loopCounter;
            for (loopCounter = 0; loopCounter < Behaviours.Length; ++loopCounter)
            {
                Behaviours[loopCounter].OnServerSubscribingAdded();
            }
        }

        public void OnServerSubscribingRemoved()
        {
            int loopCounter;
            for (loopCounter = 0; loopCounter < Behaviours.Length; ++loopCounter)
            {
                Behaviours[loopCounter].OnServerSubscribingRemoved();
            }
        }

        public void SetOwnerClient(long connectionId)
        {
            if (!IsServer)
                return;

            Manager.Assets.SetObjectOwner(ObjectId, connectionId);
        }

        public void NetworkDestroy()
        {
            if (!IsServer)
                return;

            NetworkDestroyFunction();
        }

        public void NetworkDestroy(float delay)
        {
            if (!IsServer)
                return;

            Invoke(nameof(NetworkDestroyFunction), delay);
        }

        private void NetworkDestroyFunction()
        {
            if (!destroyed)
            {
                Manager.Assets.NetworkDestroy(ObjectId, DestroyObjectReasons.RequestedToDestroy);
                Subscribers.Clear();
#if UNITY_EDITOR
                subscriberIds.Clear();
#endif
                destroyed = true;
            }
        }

        public void OnNetworkDestroy(byte reasons)
        {
            int loopCounter;
            for (loopCounter = 0; loopCounter < Behaviours.Length; ++loopCounter)
            {
                Behaviours[loopCounter].OnNetworkDestroy(reasons);
            }
        }
    }
}
