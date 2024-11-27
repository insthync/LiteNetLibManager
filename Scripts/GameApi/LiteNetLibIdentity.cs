using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using LiteNetLib.Utils;
using UnityEngine.Events;
using UnityEngine.Rendering;
using Cysharp.Threading.Tasks;

namespace LiteNetLibManager
{
    [DisallowMultipleComponent]
    public sealed class LiteNetLibIdentity : MonoBehaviour
    {
        public static uint HighestObjectId { get; private set; }
        /// <summary>
        /// If any of these function's result is true, it will force hide the object from another object
        /// </summary>
        public static readonly List<ForceHideDelegate> ForceHideFunctions = new List<ForceHideDelegate>();
        /// <summary>
        /// If any of these function's result is true, it will not hide the object from another object
        /// </summary>
        public static readonly List<HideExceptionDelegate> HideExceptionFunctions = new List<HideExceptionDelegate>();
        [Tooltip("Turn this on to assign asset ID automatically, if it is empty (should turn this off if you want to set custom ID)"), SerializeField]
        private bool autoAssignAssetIdIfEmpty = true;
        [Tooltip("Asset ID will be hashed to uses as prefab instantiating reference, leave it empty to auto generate asset ID by asset path"), SerializeField]
        private string assetId = string.Empty;
        [SerializeField]
        private uint objectId = 0;
        [Tooltip("If this is <= 0f, it will uses interest manager's `defaultVisibleRange` setting"), SerializeField]
        private float visibleRange = 0f;
        [Tooltip("If this is `TRUE` it will always visible no matter how far from player's objects"), SerializeField]
        private bool alwaysVisible = false;
        [Tooltip("If this is `TRUE` it will not destroy this network object when player disconnect the game"), SerializeField]
        private bool doNotDestroyWhenDisconnect = false;
        [Tooltip("If this is > 0, it will get instance from pooling system"), SerializeField]
        private int poolingSize = 0;

        [Header("Events")]
        public UnityEvent onGetInstance = new UnityEvent();
        public LiteNetLibConnectionIdEvent onSubscriberAdded = new LiteNetLibConnectionIdEvent();
        public LiteNetLibConnectionIdEvent onSubscriberRemoved = new LiteNetLibConnectionIdEvent();

        /// <summary>
        /// This will be true when identity was spawned by manager
        /// </summary>
        public bool IsSpawned { get; private set; }
        /// <summary>
        /// This will be true when identity was requested to destroy
        /// </summary>
        public bool IsDestroyed { get; private set; }
        /// <summary>
        /// This will be true when identity setup
        /// </summary>
        public bool IsSetupBehaviours { get; private set; }
        /// <summary>
        /// Array of all behaviours
        /// </summary>
        public LiteNetLibBehaviour[] Behaviours { get; private set; }
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
        /// List of networked objects which subscribed by this identity
        /// </summary>
        internal readonly HashSet<uint> Subscribings = new HashSet<uint>();
        /// <summary>
        /// List of players which subscribe this identity
        /// </summary>
        internal readonly HashSet<long> Subscribers = new HashSet<long>();

        public string AssetId
        {
            get { return assetId; }
            internal set
            {
                assetId = value;
                _hashAssetId = null;
            }
        }
        private int? _hashAssetId;
        public int HashAssetId
        {
            get
            {
                if (!Application.isPlaying)
                {
                    // Not playing yet, maybe in editor, so force reset hash asset ID
                    _hashAssetId = null;
                }
                if (!_hashAssetId.HasValue)
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

                        _hashAssetId = hash1 + (hash2 * 1566083941);
                    }
                }
                return _hashAssetId.Value;
            }
        }
        public uint ObjectId { get { return objectId; } internal set { objectId = value; } }
        public float VisibleRange { get { return visibleRange; } set { visibleRange = value; } }
        public bool AlwaysVisible { get { return alwaysVisible; } set { alwaysVisible = value; } }
        public bool DoNotDestroyWhenDisconnect { get { return doNotDestroyWhenDisconnect; } set { doNotDestroyWhenDisconnect = value; } }
        public int PoolingSize { get { return poolingSize; } set { poolingSize = value; } }
        public byte DataChannel { get; set; } = 0;
        public string SubChannelId { get; set; } = string.Empty;
        /// <summary>
        /// If this is `TRUE` it will disallow other connections to subscribe this networked object
        /// </summary>
        public bool IsHide { get; set; }
        /// <summary>
        /// This will be used while `IsHide` is `TRUE` to allow some connections to subscribe this networked object
        /// </summary>
        public HashSet<long> HideExceptions { get; } = new HashSet<long>();
        public long ConnectionId { get; internal set; } = -1;
        public bool IsPooledInstance { get; internal set; } = false;
        public LiteNetLibGameManager Manager { get; internal set; }

        private string _logTag;
        public string LogTag
        {
            get
            {
                if (string.IsNullOrWhiteSpace(_logTag))
                    _logTag = $"{Manager.LogTag}->{name}({GetType().Name})";
                return _logTag;
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

        public bool IsHost
        {
            get { return IsServer && IsOwnerClient; }
        }

        public bool IsOwnedByServer
        {
            get { return IsServer && ConnectionId < 0; }
        }

        public bool IsOwnerClientOrOwnedByServer
        {
            get { return IsOwnerClient || IsOwnedByServer; }
        }

        public bool IsSceneObject
        {
            get; internal set;
        }

        public bool IsPlaceHolder
        {
            get; internal set;
        }

        #region IDs generate in Editor
#if UNITY_EDITOR
        private void OnValidate()
        {
            Event evt = Event.current;
            if (evt != null && evt.commandName == "Duplicate")
            {
                // Reset asset ID to regenerate it
                assetId = string.Empty;
            }
            if (string.IsNullOrWhiteSpace(assetId))
            {
                if (autoAssignAssetIdIfEmpty)
                {
                    SetupIDs();
                    return;
                }
                if (ThisIsAPrefab())
                {
                    Debug.LogWarning($"[LiteNetLibIdentity] prefab named {name} has no assigned ID, the ID must be assigned, you can use \"Assign Asset ID If Empty\" context menu to assign ID or set yours.", gameObject);
                }
                else if (ThisIsASceneObjectWithThatReferencesPrefabAsset(out GameObject prefab))
                {
                    Debug.LogWarning($"[LiteNetLibIdentity] prefab named {prefab.name} has no assigned ID, the ID must be assigned, you can use \"Assign Asset ID If Empty\" context menu to assign ID or set yours.", gameObject);
                }
            }
        }

        internal void AssignAssetID(GameObject prefab)
        {
            if (!string.IsNullOrWhiteSpace(assetId))
                return;
            assetId = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(prefab));
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
            prefab = PrefabUtility.GetCorrespondingObjectFromSource(gameObject);
#else
            prefab = (GameObject)PrefabUtility.GetPrefabParent(gameObject);
#endif
            if (prefab == null)
            {
                Logging.LogError(LogTag, $"Failed to find prefab parent for scene object: {gameObject.name}.", gameObject);
                return false;
            }
            return true;
        }

        [ContextMenu("Setup IDs")]
        internal void SetupIDs()
        {
            string oldAssetId = assetId;
            GameObject prefab;
            if (ThisIsAPrefab())
            {
                // This is a prefab, can create prefab while playing so it will still assign asset ID and reset object ID
                AssignAssetID(gameObject);
            }
            else if (ThisIsASceneObjectWithThatReferencesPrefabAsset(out prefab))
            {
                if (Application.isPlaying)
                {
                    Debug.LogWarning($"[LiteNetLibIdentity] Cannot setup IDs while playing", gameObject);
                    return;
                }
                // This is a scene object with prefab link
                AssignAssetID(prefab);
                if (objectId == 0)
                    Debug.LogWarning($"[LiteNetLibIdentity] No object ID set for {name}", gameObject);
            }
            else
            {
                if (Application.isPlaying)
                {
                    Debug.LogWarning($"[LiteNetLibIdentity] Cannot setup IDs while playing", gameObject);
                    return;
                }
                // This is a pure scene object (Not a prefab)
                assetId = string.Empty;
            }
            // Do not mark dirty while playing
            if (!Application.isPlaying && !string.Equals(oldAssetId, assetId))
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
            syncList.ProcessOperations(reader);
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

        /// <summary>
        /// This function will be called when send networked object spawning message, to write sync field data
        /// </summary>
        /// <param name="writer"></param>
        internal void WriteInitSyncFields(NetDataWriter writer)
        {
            foreach (LiteNetLibSyncField field in SyncFields)
            {
                if (field.HasSyncBehaviourFlag(LiteNetLibSyncField.SyncBehaviour.DoNotSyncInitialDataImmediately))
                    continue;
                field.Serialize(writer);
            }
        }

        /// <summary>
        /// This function will be called when receive networked object spawning message, to read sync field data
        /// </summary>
        /// <param name="reader"></param>
        internal void ReadInitSyncFields(NetDataReader reader)
        {
            foreach (LiteNetLibSyncField field in SyncFields)
            {
                if (field.HasSyncBehaviourFlag(LiteNetLibSyncField.SyncBehaviour.DoNotSyncInitialDataImmediately))
                    continue;
                field.Deserialize(reader, true);
            }
        }

        /// <summary>
        /// This function will be called after networked object spawning message was sent
        /// </summary>
        /// <param name="connectionId"></param>
        internal void SendInitSyncFields(long connectionId)
        {
            foreach (LiteNetLibSyncField field in SyncFields)
            {
                if (!field.HasSyncBehaviourFlag(LiteNetLibSyncField.SyncBehaviour.DoNotSyncInitialDataImmediately))
                    continue;
                field.SendUpdate(true, connectionId);
            }
        }

        internal void SendInitSyncLists(long connectionId)
        {
            foreach (LiteNetLibSyncList list in SyncLists)
            {
                list.SendInitialList(connectionId);
            }
        }

        /// <summary>
        /// Initial Identity, will be called when spawned. If object id == 0, it will generate new object id
        /// </summary>
        /// <param name="manager"></param>
        /// <param name="objectId"></param>
        /// <param name="connectionId"></param>
        internal void Initial(LiteNetLibGameManager manager, uint objectId = 0, long connectionId = -1)
        {
            Manager = manager;
            ObjectId = objectId;
            ConnectionId = connectionId;
            UpdateHighestObjectId(objectId);
            IsDestroyed = false;
            IsSpawned = true;
            AssignObjectId();

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

            // If this is host, hide it then will be showned when initialize subscribings
            if (IsServer && IsClient)
                OnServerSubscribingRemoved();

            Manager.Assets.SpawnedObjects.Add(ObjectId, this);
            if (IsServer && ConnectionId >= 0)
            {
                Player.SpawnedObjects.Add(ObjectId, this);
                Player.Subscribe(ObjectId);
            }

            Manager.InterestManager.NotifyNewObject(this);
        }

        internal void OnSetOwnerClient(bool isOwnerClient)
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
        public bool IsObjectExists(uint objectId)
        {
            // If this is now spawned while gameplay, find objects in scene
            LiteNetLibIdentity[] netObjects = FindObjectsOfType<LiteNetLibIdentity>();
            foreach (LiteNetLibIdentity netObject in netObjects)
            {
                if (netObject.objectId == objectId && netObject != this)
                    return true;
            }
            return false;
        }

        internal void AssignObjectId()
        {
            if (objectId == 0 || IsObjectExists(objectId))
                objectId = GetNewObjectId();
        }

        internal static void ResetObjectId()
        {
            HighestObjectId = 0;
        }

        internal static uint GetNewObjectId()
        {
            if (HighestObjectId == 0)
            {
                uint result = HighestObjectId;
                LiteNetLibIdentity[] netObjects = FindObjectsOfType<LiteNetLibIdentity>();
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

        internal static void UpdateHighestObjectId(uint objectId)
        {
            if (objectId > HighestObjectId)
                HighestObjectId = objectId;
        }

        public bool AddSubscriber(long connectionId)
        {
            if (Subscribers.Add(connectionId))
            {
                onSubscriberAdded.Invoke(connectionId);
                return true;
            }
            return false;
        }

        public bool RemoveSubscriber(long connectionId)
        {
            if (Subscribers.Remove(connectionId))
            {
                onSubscriberRemoved.Invoke(connectionId);
                return true;
            }
            return false;
        }

        public bool HasSubscriber(long connectionId)
        {
            return Subscribers.Contains(connectionId);
        }

        public int CountSubscribers()
        {
            return Subscribers.Count;
        }

        public bool HasSubscriberOrIsOwning(long connectionId)
        {
            return connectionId == ConnectionId || HasSubscriber(connectionId);
        }

        public void AddSubscribing(uint subscribing)
        {
            Subscribings.Add(subscribing);
            Player.Subscribe(subscribing);
        }

        public void RemoveSubscribing(uint subscribing)
        {
            Subscribings.Remove(subscribing);
            Player.Unsubscribe(subscribing);
        }

        public void UpdateSubscribings(HashSet<uint> newSubscribings)
        {
            if (!IsServer || ConnectionId < 0 || !Player.IsReady)
            {
                // This is not player's networked object
                return;
            }
            // Always add controlled network object to subscribe it
            LiteNetLibIdentity tempIdentity;
            newSubscribings.Add(ObjectId);
            foreach (uint oldSubscribing in Subscribings)
            {
                if (oldSubscribing == ObjectId)
                    continue;
                if (!newSubscribings.Contains(oldSubscribing))
                {
                    Player.Unsubscribe(oldSubscribing);
                    if (Manager.LogDebug)
                        Logging.Log(LogTag, $"Player: {ConnectionId} unsubscribe object ID: {oldSubscribing}.");
                }
            }
            Subscribings.Clear();
            foreach (uint newSubscribing in newSubscribings)
            {
                if (!Manager.Assets.TryGetSpawnedObject(newSubscribing, out tempIdentity) ||
                    tempIdentity.IsDestroyed)
                    continue;
                Subscribings.Add(newSubscribing);
                Player.Subscribe(newSubscribing);
                if (Manager.LogDebug)
                    Logging.Log(LogTag, $"Player: {ConnectionId} subscribe object ID: {newSubscribing}.");
            }
        }

        public bool IsHideFrom(LiteNetLibIdentity identity)
        {
            if (identity == null)
            {
                // WTF?
                return true;
            }
            if (ConnectionId == identity.ConnectionId)
            {
                // Don't hide, player own this one
                return false;
            }
            if (!string.Equals(SubChannelId, identity.SubChannelId))
            {
                // Hide because sub-channelIDs are different
                return true;
            }
            foreach (ForceHideDelegate func in ForceHideFunctions)
            {
                if (func.Invoke(this, identity))
                {
                    // In force hide conditions, so hide
                    return true;
                }
            }
            if (!IsHide)
            {
                // Not hide, so not hide
                return false;
            }
            if (HideExceptions.Contains(ConnectionId))
            {
                // In hide exception conditions, so not hide
                return false;
            }
            foreach (HideExceptionDelegate func in HideExceptionFunctions)
            {
                if (func.Invoke(this, identity))
                {
                    // In hide exception conditions, so not hide
                    return false;
                }
            }
            // Hide
            return true;
        }

        public void OnServerSubscribingAdded()
        {
            int loopCounter;
            for (loopCounter = 0; loopCounter < Behaviours.Length; ++loopCounter)
            {
                Behaviours[loopCounter].OnServerSubscribingAdded();
            }
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                return;
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; ++i)
            {
                renderers[i].forceRenderingOff = false;
            }
        }

        public void OnServerSubscribingRemoved()
        {
            int loopCounter;
            for (loopCounter = 0; loopCounter < Behaviours.Length; ++loopCounter)
            {
                Behaviours[loopCounter].OnServerSubscribingRemoved();
            }
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                return;
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; ++i)
            {
                renderers[i].forceRenderingOff = true;
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

            DestroyFromAssets();
        }

        public void NetworkDestroy(float delay)
        {
            if (!IsServer)
                return;
            InternalNetworkDestroy(delay).Forget();
        }

        private async UniTaskVoid InternalNetworkDestroy(float delay)
        {
            if (delay < 0)
                return;
            await UniTask.Delay((int)(1000 * delay));
            DestroyFromAssets();
        }

        private void DestroyFromAssets()
        {
            if (!IsDestroyed && Manager.Assets.NetworkDestroy(ObjectId, DestroyObjectReasons.RequestedToDestroy))
                IsDestroyed = true;
        }

        internal void OnNetworkDestroy(byte reasons)
        {
            int loopCounter;
            for (loopCounter = 0; loopCounter < Behaviours.Length; ++loopCounter)
            {
                Behaviours[loopCounter].OnNetworkDestroy(reasons);
            }
            if (Manager.IsServer)
            {
                // If this is server, send message to clients to destroy object
                LiteNetLibPlayer player;
                foreach (long subscriber in Subscribers)
                {
                    if (Manager.TryGetPlayer(subscriber, out player))
                    {
                        player.Subscribings.Remove(objectId);
                        Manager.SendServerDestroyObject(subscriber, objectId, reasons);
                    }
                }
                // Delete object from owner player's spawned objects collection
                if (ConnectionId >= 0)
                    Player.SpawnedObjects.Remove(ObjectId);
            }
            // Delete object from assets component
            Manager.Assets.SpawnedObjects.Remove(ObjectId);
            // Clear data
            Subscribings.Clear();
            Subscribers.Clear();
            IsSpawned = false;
        }

        internal void OnGetInstance()
        {
            ResetSyncData();
            onGetInstance.Invoke();
        }

        internal void ResetSyncData()
        {
            // Clear/reset syncing data
            foreach (LiteNetLibSyncField field in SyncFields)
            {
                field.Reset();
            }
            foreach (LiteNetLibSyncList list in SyncLists)
            {
                list.Reset();
            }
        }

        private void OnDestroy()
        {
            foreach (LiteNetLibSyncField field in SyncFields)
            {
                field.UnregisterUpdating();
            }
            foreach (LiteNetLibSyncList list in SyncLists)
            {
                list.UnregisterUpdating();
            }
            onGetInstance.RemoveAllListeners();
            onGetInstance = null;
            onSubscriberAdded.RemoveAllListeners();
            onSubscriberAdded = null;
            onSubscriberRemoved.RemoveAllListeners();
            onSubscriberRemoved = null;
            SyncFields.Clear();
            NetFunctions.Clear();
            SyncLists.Clear();
            SyncBehaviours.Clear();
            Subscribings.Clear();
            Subscribers.Clear();
            HideExceptions.Clear();
        }
    }
}
