using Cysharp.Text;
using Cysharp.Threading.Tasks;
using LiteNetLib.Utils;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Events;
using UnityEngine.Rendering;
using UnityEngine.Serialization;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace LiteNetLibManager
{
    [DisallowMultipleComponent]
    public sealed class LiteNetLibIdentity : MonoBehaviour
    {
        public const string TAG_NULL = "<NULL_I>";

        public static uint HighestObjectId { get; private set; }
        /// <summary>
        /// If any of these function's result is true, it will force hide the object from another object
        /// </summary>
        public static readonly List<ForceHideDelegate> ForceHideFunctions = new List<ForceHideDelegate>();
        /// <summary>
        /// If any of these function's result is true, it will not hide the object from another object
        /// </summary>
        public static readonly List<HideExceptionDelegate> HideExceptionFunctions = new List<HideExceptionDelegate>();
#if UNITY_EDITOR
        [SerializeField, Tooltip("Turn this on to assign asset ID automatically, if it is empty (should turn this off if you want to set custom ID, so when you delete/clear it won't assign)")]
        private bool autoAssignAssetIdIfEmpty = true;
        [SerializeField, Tooltip("Turn this on then it will always use GUID as its asset ID")]
        private bool forceUseAssetGUIDAsAssetId = false;
#endif
        [SerializeField, Tooltip("Asset ID will be hashed to uses as prefab instantiating reference, leave it empty to auto generate asset ID by asset path")]
        private string assetId = string.Empty;
#if UNITY_EDITOR
        [SerializeField, Tooltip("Turn this on to assign scene object ID automatically, if it is empty (should turn this off if you want to set custom ID, so when you delete/clear it won't assign)")]
        private bool autoAssignSceneObjectIdIfEmpty = true;
#endif
        [SerializeField, FormerlySerializedAs("objectId"), Tooltip("Scene object ID will be hashed to uses as reference for networked object spawning message")]
        private string sceneObjectId = string.Empty;
        [SerializeField, Tooltip("If this is `TRUE` this object will never be scene object")]
        private bool forceNotSceneObject = false;
        [SerializeField, ReadOnly]
        private uint networkedObjectId = 0;
        [SerializeField, Tooltip("Sync field/list channel ID")]
        private byte syncChannelId = 0;
        [SerializeField, Tooltip("Default RPCs channel ID")]
        private byte defaultRpcChannelId = 0;
        [SerializeField, Tooltip("If objects have differences `Sub Channel ID` it will not being subscribed")]
        private string subChannelId = string.Empty;
        [SerializeField, Tooltip("If this is <= 0f, it will uses interest manager's `defaultVisibleRange` setting")]
        private float visibleRange = 0f;
        [SerializeField, Tooltip("If this is `TRUE` it will always visible no matter how far from player's objects")]
        private bool alwaysVisible = false;
        [SerializeField, Tooltip("If this is `TRUE` it will not destroy this network object when player disconnect the game")]
        private bool doNotDestroyWhenDisconnect = false;
        [SerializeField, Tooltip("If this is > 0, it will get instance from pooling system")]
        private int poolingSize = 0;
#if !UNITY_SERVER
        [SerializeField]
        private bool forceRenderingOffWhileHidding = true;
        [SerializeField]
        private bool muteAudioSourceWhileHidding = true;
#endif

        [Header("Events")]
        public UnityEvent onGetInstance = new UnityEvent();
        public LiteNetLibConnectionIdEvent onSubscriberAdded = new LiteNetLibConnectionIdEvent();
        public LiteNetLibConnectionIdEvent onSubscriberRemoved = new LiteNetLibConnectionIdEvent();
        public UnityEvent onServerSubscribingAdded = new UnityEvent();
        public UnityEvent onServerSubscribingRemoved = new UnityEvent();

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
        /// List of sync elements from all behaviours (include children behaviours)
        /// </summary>
        internal readonly Dictionary<int, LiteNetLibSyncElement> SyncElements = new Dictionary<int, LiteNetLibSyncElement>();
        /// <summary>
        /// List of net functions from all behaviours (include children behaviours)
        /// </summary>
        internal readonly Dictionary<int, LiteNetLibRPC> RPCs = new Dictionary<int, LiteNetLibRPC>();
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
                    // Not playing yet, maybe in editor, so force reset hashed ID
                    _hashAssetId = null;
                }
                if (!_hashAssetId.HasValue)
                {
                    _hashAssetId = GetHashedId(AssetId);
                }
                return _hashAssetId.Value;
            }
        }
        public string SceneObjectId
        {
            get { return sceneObjectId; }
            internal set
            {
                sceneObjectId = value;
                _hashSceneObjectId = null;
            }
        }
        private int? _hashSceneObjectId;
        public int HashSceneObjectId
        {
            get
            {
                if (!Application.isPlaying)
                {
                    // Not playing yet, maybe in editor, so force reset hashed ID
                    _hashSceneObjectId = null;
                }
                if (!_hashSceneObjectId.HasValue)
                {
                    _hashSceneObjectId = GetHashedId(SceneObjectId);
                }
                return _hashSceneObjectId.Value;
            }
        }
        public bool ForceNotSceneObject { get { return forceNotSceneObject; } set { forceNotSceneObject = value; } }
        public uint ObjectId { get { return networkedObjectId; } internal set { networkedObjectId = value; } }
        public byte SyncChannelId { get { return syncChannelId; } }
        public byte DefaultRpcChannelId { get { return defaultRpcChannelId; } }
        public string SubChannelId { get { return subChannelId; } set { subChannelId = value; } }
        public float VisibleRange { get { return visibleRange; } set { visibleRange = value; } }
        public bool AlwaysVisible { get { return alwaysVisible; } set { alwaysVisible = value; } }
        public bool DoNotDestroyWhenDisconnect { get { return doNotDestroyWhenDisconnect; } set { doNotDestroyWhenDisconnect = value; } }
        public int PoolingSize { get { return poolingSize; } set { poolingSize = value; } }
        /// <summary>
        /// If this is `TRUE` it will disallow other connections to subscribe this networked object
        /// </summary>
        public bool IsHide { get; set; }
        /// <summary>
        /// This will be used while `IsHide` is `TRUE` to allow some connections to subscribe this networked object
        /// </summary>
        public HashSet<long> HideExceptions { get; } = new HashSet<long>();
#if !UNITY_SERVER
        private HashSet<Renderer> _hiddingRenderers = new HashSet<Renderer>();
        private HashSet<AudioSource> _hiddingAudioSources = new HashSet<AudioSource>();
#endif
        public long ConnectionId { get; internal set; } = -1;
        public bool IsPooledInstance { get; internal set; } = false;
        public LiteNetLibGameManager Manager { get; internal set; }

        private string _logTag;
        public string LogTag
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
                        stringBuilder.Append('I');
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

        public bool IsOwnerHost
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
            get; private set;
        }

        internal static int GetHashedId(string id)
        {
            unchecked
            {
                int hash1 = 5381;
                int hash2 = hash1;

                for (int i = 0; i < id.Length && id[i] != '\0'; i += 2)
                {
                    hash1 = ((hash1 << 5) + hash1) ^ id[i];
                    if (i == id.Length - 1 || id[i + 1] == '\0')
                        break;
                    hash2 = ((hash2 << 5) + hash2) ^ id[i + 1];
                }

                return hash1 + (hash2 * 1566083941);
            }
        }

        #region IDs generate in Editor
#if UNITY_EDITOR
        private void OnValidate()
        {
            PrefabStage prefabStage = PrefabStageUtility.GetPrefabStage(gameObject);
            if (prefabStage != null && !prefabStage.scene.isLoaded)
            {
                // In prefab mode, and it is not loaded yet. No data to validate yet, skip it.
                return;
            }

            Event evt = Event.current;
            if (evt != null && (string.Equals(evt.commandName, "Duplicate") || string.Equals(evt.commandName, "Paste")))
            {
                // Reset asset ID to regenerate it
                assetId = string.Empty;
                sceneObjectId = string.Empty;
            }
            if (string.IsNullOrWhiteSpace(assetId) || assetId.Equals("0") || forceUseAssetGUIDAsAssetId)
            {
                if (autoAssignAssetIdIfEmpty || forceUseAssetGUIDAsAssetId)
                {
                    AssignAssetID();
                }
                else if (ThisIsAPrefab())
                {
                    Debug.LogWarning($"[LiteNetLibIdentity] prefab named {name} has no assigned ID, the ID must be assigned, you can use \"Assign Asset ID\" context menu to assign ID or set yours.", gameObject);
                }
                else if (ThisIsASceneObjectWithThatReferencesPrefabAsset(out GameObject prefab))
                {
                    Debug.LogWarning($"[LiteNetLibIdentity] prefab named {prefab.name} has no assigned ID, the ID must be assigned, you can use \"Assign Asset ID\" context menu to assign ID or set yours.", gameObject);
                }
            }
            if (string.IsNullOrWhiteSpace(sceneObjectId) || sceneObjectId.Equals("0"))
            {
                if (!gameObject.scene.IsValid())
                {
                    return;
                }
                if (autoAssignSceneObjectIdIfEmpty)
                {
                    AssignSceneObjectID();
                    return;
                }
                Debug.LogWarning($"[LiteNetLibIdentity] scene object named {name} has no assigned ID, the ID must be assigned, you can use \"Assign Scene Object ID\" context menu to assign ID or set yours.", gameObject);
            }
        }

        internal void AssignAssetID(GameObject prefab)
        {
            if (!string.IsNullOrWhiteSpace(assetId) && !forceUseAssetGUIDAsAssetId)
                return;
            assetId = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(prefab));
        }

        private bool ThisIsAPrefab()
        {
            return PrefabUtility.IsPartOfPrefabAsset(gameObject);
        }

        private bool ThisIsASceneObjectWithThatReferencesPrefabAsset(out GameObject prefab)
        {
            prefab = null;
            if (!PrefabUtility.IsPartOfNonAssetPrefabInstance(gameObject))
                return false;
            prefab = PrefabUtility.GetCorrespondingObjectFromSource(gameObject);
            if (prefab == null)
            {
                Logging.LogError(LogTag, $"Failed to find prefab parent for scene object: {gameObject.name}.", gameObject);
                return false;
            }
            return true;
        }

        [ContextMenu("Assign Asset ID")]
        internal void AssignAssetID()
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
                if (string.IsNullOrWhiteSpace(sceneObjectId))
                    Debug.LogWarning($"[LiteNetLibIdentity] No object ID set for {name}", gameObject);
            }
            // Do not mark dirty while playing
            if (!Application.isPlaying && !string.Equals(oldAssetId, assetId))
                EditorUtility.SetDirty(this);
        }

        [ContextMenu("Assign Scene Object ID")]
        internal void AssignSceneObjectID()
        {
            Scene objectScene = gameObject.scene;
            if (!objectScene.IsValid())
            {
                Debug.LogWarning($"[LiteNetLibIdentity] Only object in valid scene can be assigned", gameObject);
                return;
            }
            if (string.IsNullOrWhiteSpace(sceneObjectId) || sceneObjectId.Equals("0") || FoundAObjectWithSceneObjectID(objectScene, sceneObjectId, this))
            {
                int countSuffix = 0;
                // Keep find a proper ID
                do
                {
                    sceneObjectId = GenerateSceneObjectId(this, ++countSuffix);
                } while (string.IsNullOrWhiteSpace(sceneObjectId) || sceneObjectId.Equals("0") || FoundAObjectWithSceneObjectID(objectScene, sceneObjectId, this));
                Debug.Log($"[LiteNetLibIdentity] Scene object ID assigned: {sceneObjectId}", gameObject);
            }
            EditorUtility.SetDirty(gameObject);
        }

        [ContextMenu("Assign All Scene Object IDs (If Empty Or Duplicated)")]
        public void AssignSceneObjectIDs()
        {
            s_AssignSceneObjectIDs();
        }

        public static void s_AssignSceneObjectIDs()
        {
            for (int i = 0; i < SceneManager.loadedSceneCount; ++i)
            {
                Scene loadedScene = SceneManager.GetSceneAt(i);
                GameObject[] rootObjects = loadedScene.GetRootGameObjects();
                for (int j = 0; j < rootObjects.Length; ++j)
                {
                    LiteNetLibIdentity[] identities = rootObjects[j].GetComponentsInChildren<LiteNetLibIdentity>(true);
                    for (int k = 0; k < identities.Length; ++k)
                    {
                        identities[k].AssignSceneObjectID();
                    }
                }
                rootObjects = null;
            }
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        internal static string GenerateSceneObjectId(LiteNetLibIdentity identity, int countSuffix)
        {
            return $"{identity.name}_{countSuffix}";
        }

        internal static bool FoundAObjectWithSceneObjectID(Scene loadedScene, string id, LiteNetLibIdentity ignoreIdentity)
        {
            if (!loadedScene.isLoaded)
                return false;
            GameObject[] rootObjects = loadedScene.GetRootGameObjects();
            for (int i = 0; i < rootObjects.Length; ++i)
            {
                LiteNetLibIdentity[] identities = rootObjects[i].GetComponentsInChildren<LiteNetLibIdentity>(true);
                for (int j = 0; j < identities.Length; ++j)
                {
                    LiteNetLibIdentity identity = identities[j];
                    if (identity == ignoreIdentity)
                        continue;
                    if (string.Equals(id, identity.sceneObjectId))
                        return true;
                }
            }
            return false;
        }
#endif
        #endregion

        #region Sync Element Functions
        internal bool TryGetSyncElement(int elementId, out LiteNetLibSyncElement syncElement)
        {
            return SyncElements.TryGetValue(elementId, out syncElement);
        }
        #endregion

        #region RPC Functions
        internal LiteNetLibRPC ProcessRPC(LiteNetLibElementInfo info, NetDataReader reader, bool hookCallback)
        {
            return ProcessRPC(GetRPC(info), reader, hookCallback);
        }

        internal LiteNetLibRPC ProcessRPC(LiteNetLibRPC rpc, NetDataReader reader, bool hookCallback)
        {
            if (rpc == null)
                return null;
            rpc.DeserializeParameters(reader);
            if (hookCallback)
                rpc.HookCallback();
            return rpc;
        }

        internal LiteNetLibRPC GetRPC(LiteNetLibElementInfo info)
        {
            if (info.objectId != ObjectId)
                return null;
            if (!RPCs.TryGetValue(info.elementId, out LiteNetLibRPC rpc))
            {
                if (Manager.LogError)
                    Logging.LogError(LogTag, $"Cannot find net function: {info.elementId}.");
                return null;
            }
            return rpc;
        }
        #endregion

        internal bool TryGetBehaviour<T>(byte behaviourIndex, out T behaviour)
            where T : LiteNetLibBehaviour
        {
            behaviour = null;
            if (behaviourIndex >= Behaviours.Length)
                return false;
            behaviour = Behaviours[behaviourIndex] as T;
            return behaviour != null;
        }

        public bool IsSceneObjectExists(int sceneObjectId)
        {
            if (Manager != null)
            {
                // If this is spawned while running in play mode, find it by manager assets
                return Manager.Assets.ContainsSceneObject(sceneObjectId);
            }
            // If this is not spawned while running in play mode, find objects in scene
            LiteNetLibIdentity[] netObjects = FindObjectsByType<LiteNetLibIdentity>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (LiteNetLibIdentity netObject in netObjects)
            {
                if (netObject.HashSceneObjectId == sceneObjectId && netObject != this)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Initial Identity, will be called when spawned. If object id == 0, it will generate new object id
        /// </summary>
        /// <param name="manager"></param>
        /// <param name="isSceneObject"></param>
        /// <param name="objectId"></param>
        /// <param name="connectionId"></param>
        internal void Initial(LiteNetLibGameManager manager, bool isSceneObject, uint objectId = 0, long connectionId = -1)
        {
            Manager = manager;
            ObjectId = objectId;
            ConnectionId = connectionId;
            UpdateHighestObjectId(objectId);
            IsDestroyed = false;
            IsSpawned = true;
            IsSceneObject = isSceneObject;
            AssignObjectId();

            if (!IsSetupBehaviours)
            {
                // Setup behaviours index, we will use this as reference for network functions
                // NOTE: Maximum network behaviour for a identity is 255 (included children)
                Behaviours = GetComponentsInChildren<LiteNetLibBehaviour>();
                byte loopCounter;
                for (loopCounter = 0; loopCounter < Behaviours.Length; ++loopCounter)
                {
                    Behaviours[loopCounter].Setup(loopCounter);
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

            // Initialized, tell behaviours
            for (int i = 0; i < Behaviours.Length; ++i)
            {
                Behaviours[i].OnIdentityInitialize();
            }
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

        internal void AssignObjectId()
        {
            if (ObjectId == 0)
                ObjectId = GetNewObjectId();
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
                LiteNetLibIdentity[] netObjects = FindObjectsByType<LiteNetLibIdentity>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (LiteNetLibIdentity netObject in netObjects)
                {
                    if (netObject.ObjectId > result)
                        result = netObject.ObjectId;
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
            if (Manager.LogDebug)
                Logging.Log(LogTag, $"Player: {ConnectionId} subscribe object ID: {subscribing}.");
        }

        public void RemoveSubscribing(uint subscribing)
        {
            Subscribings.Remove(subscribing);
            Player.Unsubscribe(subscribing);
            if (Manager.LogDebug)
                Logging.Log(LogTag, $"Player: {ConnectionId} unsubscribe object ID: {subscribing}.");
        }

        public void ClearSubscribings()
        {
            if (!IsServer || ConnectionId < 0 || !Player.IsReady)
            {
                // This is not player's networked object
                return;
            }
            // Always add controlled network object to subscribe it
            foreach (uint oldSubscribing in Subscribings)
            {
                if (oldSubscribing == ObjectId)
                    continue;
                Player.Unsubscribe(oldSubscribing);
                if (Manager.LogDebug)
                    Logging.Log(LogTag, $"Player: {ConnectionId} unsubscribe object ID: {oldSubscribing}.");
            }
            Subscribings.Clear();
            if (IsDestroyed)
                return;
            AddSubscribing(ObjectId);
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
                if (newSubscribings.Contains(oldSubscribing))
                    continue;
                Player.Unsubscribe(oldSubscribing);
                if (Manager.LogDebug)
                    Logging.Log(LogTag, $"Player: {ConnectionId} unsubscribe object ID: {oldSubscribing}.");
            }
            Subscribings.Clear();
            foreach (uint newSubscribing in newSubscribings)
            {
                if (!Manager.Assets.TryGetSpawnedObject(newSubscribing, out tempIdentity) || tempIdentity.IsDestroyed)
                    continue;
                AddSubscribing(newSubscribing);
            }
        }

        public bool IsDifferSubChannelId(LiteNetLibIdentity identity)
        {
            if (identity == null)
            {
                // WTF?
                return false;
            }
            if (string.IsNullOrEmpty(SubChannelId) && string.IsNullOrEmpty(identity.SubChannelId))
                return false;
            return !string.Equals(SubChannelId, identity.SubChannelId);
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
            if (IsDifferSubChannelId(identity))
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
            onServerSubscribingAdded.Invoke();
#if !UNITY_SERVER
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                return;
            // Reset renderers/audio sources
            foreach (Renderer renderer in _hiddingRenderers)
            {
                renderer.forceRenderingOff = false;
            }
            foreach (AudioSource audioSource in _hiddingAudioSources)
            {
                audioSource.mute = false;
            }
            _hiddingRenderers.Clear();
            _hiddingAudioSources.Clear();
#endif
        }

        public void OnServerSubscribingRemoved()
        {
            int loopCounter;
            for (loopCounter = 0; loopCounter < Behaviours.Length; ++loopCounter)
            {
                Behaviours[loopCounter].OnServerSubscribingRemoved();
            }
            onServerSubscribingRemoved.Invoke();
#if !UNITY_SERVER
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                return;
            // Reset renderers/audio sources
            foreach (Renderer renderer in _hiddingRenderers)
            {
                renderer.forceRenderingOff = false;
            }
            foreach (AudioSource audioSource in _hiddingAudioSources)
            {
                audioSource.mute = false;
            }
            _hiddingRenderers.Clear();
            _hiddingAudioSources.Clear();
            // Set renderers
            if (forceRenderingOffWhileHidding)
            {
                Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
                foreach (Renderer renderer in renderers)
                {
                    if (renderer.forceRenderingOff)
                        continue;
                    renderer.forceRenderingOff = true;
                    _hiddingRenderers.Add(renderer);
                }
            }
            // Set audio sources
            if (muteAudioSourceWhileHidding)
            {
                AudioSource[] audioSources = GetComponentsInChildren<AudioSource>(true);
                foreach (AudioSource audioSource in audioSources)
                {
                    if (audioSource.mute)
                        continue;
                    audioSource.mute = true;
                    _hiddingAudioSources.Add(audioSource);
                }
            }
#endif
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
                    if (!Manager.TryGetPlayer(subscriber, out player))
                        continue;
                    player.Subscribings.Remove(ObjectId);
                    player.SyncingStates.AppendDestroySyncState(this, reasons);
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
            foreach (LiteNetLibSyncElement field in SyncElements.Values)
            {
                field.Reset();
            }
        }

        private void OnDestroy()
        {
            foreach (LiteNetLibSyncElement syncElement in SyncElements.Values)
            {
                syncElement.UnregisterUpdating();
            }
            onGetInstance.RemoveAllListeners();
            onGetInstance = null;
            onSubscriberAdded.RemoveAllListeners();
            onSubscriberAdded = null;
            onSubscriberRemoved.RemoveAllListeners();
            onSubscriberRemoved = null;
            SyncElements.Clear();
            RPCs.Clear();
            Subscribings.Clear();
            Subscribers.Clear();
            HideExceptions.Clear();
            if (Behaviours != null)
            {
                // `Behaviours` can be null because it is not setup yet.
                for (int i = 0; i < Behaviours.Length; ++i)
                {
                    Behaviours[i].OnIdentityDestroy();
                }
            }
#if !UNITY_SERVER
            _hiddingRenderers.Clear();
            _hiddingAudioSources.Clear();
#endif
        }
    }
}
