using Cysharp.Text;
using Cysharp.Threading.Tasks;
using Insthync.AddressableAssetTools;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

namespace LiteNetLibManager
{
    public class LiteNetLibAssets : MonoBehaviour
    {
        public const string TAG_NULL = "<NULL_A>";
        private static int s_spawnPositionCounter = 0;

        public bool playerSpawnRandomly;
#if !EXCLUDE_PREFAB_REFS
        #region Prefab Refs
        public LiteNetLibIdentity playerPrefab;
        public LiteNetLibIdentity[] spawnablePrefabs = new LiteNetLibIdentity[0];
        public LiteNetLibIdentity PlayerPrefab { get; protected set; }
        public SceneField offlineScene;
        public SceneField onlineScene;
        #endregion
#endif

        #region Addressable Assets Refs
        public AssetReferenceLiteNetLibIdentity addressablePlayerPrefab;
        public AssetReferenceLiteNetLibIdentity[] addressableSpawnablePrefabs = new AssetReferenceLiteNetLibIdentity[0];
        public AssetReferenceLiteNetLibIdentity AddressablePlayerPrefab { get; protected set; }
        [Tooltip("If this is not empty, it will load offline scene by this instead of `offlineScene`")]
        public AssetReferenceScene addressableOfflineScene;
        [Tooltip("If this is not empty, it will load online scene by this instead of `onlineScene`")]
        public AssetReferenceScene addressableOnlineScene;
        #endregion

        public UnityEvent onInitialize = new UnityEvent();
        public LiteNetLibLoadSceneEvent onLoadSceneStart = new LiteNetLibLoadSceneEvent();
        public LiteNetLibLoadSceneEvent onLoadSceneProgress = new LiteNetLibLoadSceneEvent();
        public LiteNetLibLoadSceneEvent onLoadSceneFinish = new LiteNetLibLoadSceneEvent();
        public UnityEvent onSceneFileSizeRetrieving = new UnityEvent();
        public AddressableAssetFileSizeEvent onSceneFileSizeRetrieved = new AddressableAssetFileSizeEvent();
        public UnityEvent onSceneDepsDownloading = new UnityEvent();
        public AddressableAssetDownloadProgressEvent onSceneDepsFileDownloading = new AddressableAssetDownloadProgressEvent();
        public UnityEvent onSceneDepsDownloaded = new UnityEvent();
        public LiteNetLibLoadAdditiveSceneEvent onLoadAdditiveSceneStart = new LiteNetLibLoadAdditiveSceneEvent();
        public LiteNetLibLoadAdditiveSceneEvent onLoadAdditiveSceneProgress = new LiteNetLibLoadAdditiveSceneEvent();
        public LiteNetLibLoadAdditiveSceneEvent onLoadAdditiveSceneFinish = new LiteNetLibLoadAdditiveSceneEvent();
        public LiteNetLibIdentityEvent onObjectSpawn = new LiteNetLibIdentityEvent();
        public LiteNetLibIdentityEvent onObjectDestroy = new LiteNetLibIdentityEvent();
        public bool disablePooling = false;

        internal readonly List<LiteNetLibSpawnPoint> SpawnPoints = new List<LiteNetLibSpawnPoint>();
        internal readonly Dictionary<int, LiteNetLibIdentity> GuidToPrefabs = new Dictionary<int, LiteNetLibIdentity>();
        internal readonly Dictionary<int, Queue<LiteNetLibIdentity>> PooledObjects = new Dictionary<int, Queue<LiteNetLibIdentity>>();
        internal readonly Dictionary<uint, LiteNetLibIdentity> SpawnedObjects = new Dictionary<uint, LiteNetLibIdentity>();
        internal readonly Dictionary<string, int> SpawningScenePlaceholderCounts = new Dictionary<string, int>();
        internal readonly List<LiteNetLibIdentity> SpawningSceneObjects = new List<LiteNetLibIdentity>();

        public LiteNetLibGameManager Manager { get; private set; }

        private string _logTag;
        public string LogTag
        {
            get
            {
                if (string.IsNullOrEmpty(_logTag))
                {
                    string managerTag = Manager != null ? Manager.LogTag : LiteNetLibManager.TAG_NULL;
                    string behaviourTag = this != null ? $"{name}<A_{GetType().Name}>" : TAG_NULL;
                    _logTag = $"{managerTag}.{behaviourTag}";
                }
                return _logTag;
            }
        }

        private void Awake()
        {
            Manager = GetComponent<LiteNetLibGameManager>();
        }

        public async UniTask Initialize()
        {
            if (onInitialize != null)
                onInitialize.Invoke();
            await RegisterPrefabs();
            RegisterSpawnPoints();
            RegisterSceneObjects();
        }

        public void Clear(bool doNotResetObjectId = false)
        {
            ClearSpawnedObjects();
            ClearPooledObjects();
            SpawnPoints.Clear();
            ResetSpawnPositionCounter();
            if (!doNotResetObjectId)
                LiteNetLibIdentity.ResetObjectId();
            AddressableAssetsManager.ReleaseAll();
        }

        public void RegisterSpawnPoints()
        {
            SpawnPoints.Clear();
            SpawnPoints.AddRange(FindObjectsOfType<LiteNetLibSpawnPoint>());
        }

        public async UniTask RegisterPrefabs()
        {
#if !EXCLUDE_PREFAB_REFS
            for (int i = 0; i < spawnablePrefabs.Length; ++i)
            {
                RegisterPrefab(spawnablePrefabs[i]);
            }
            if (playerPrefab != null)
            {
                PlayerPrefab = playerPrefab;
                RegisterPrefab(playerPrefab);
            }
#endif
            List<Task<LiteNetLibIdentity>> ops = new List<Task<LiteNetLibIdentity>>();
            for (int i = 0; i < addressableSpawnablePrefabs.Length; ++i)
            {
                if (addressableSpawnablePrefabs[i].IsDataValid())
                {
                    ops.Add(RegisterAddressablePrefabAsync(addressableSpawnablePrefabs[i]));
                }
            }
            if (addressablePlayerPrefab.IsDataValid())
            {
                ops.Add(RegisterAddressablePrefabAsync(addressablePlayerPrefab));
            }
            await Task.WhenAll(ops);
        }

        public LiteNetLibIdentity RegisterPrefab(LiteNetLibIdentity prefab)
        {
            if (prefab == null)
            {
                if (Manager.LogWarn) Logging.LogWarning(LogTag, "RegisterPrefab - prefab is null.");
                return null;
            }
            if (Manager.LogDev) Logging.Log(LogTag, $"RegisterPrefab [{prefab.HashAssetId}] name [{prefab.name}]");
            GuidToPrefabs[prefab.HashAssetId] = prefab;
            return prefab;
        }

        public bool UnregisterPrefab(LiteNetLibIdentity prefab)
        {
            if (prefab == null)
            {
                if (Manager.LogWarn) Logging.LogWarning(LogTag, "UnregisterPrefab - prefab is null.");
                return false;
            }
            if (Manager.LogDev) Logging.Log(LogTag, $"UnregisterPrefab [{prefab.HashAssetId}] name [{prefab.name}]");
            return GuidToPrefabs.Remove(prefab.HashAssetId);
        }

        public async Task<LiteNetLibIdentity> RegisterAddressablePrefabAsync(AssetReferenceLiteNetLibIdentity addressablePrefab)
        {
            if (!addressablePrefab.IsDataValid())
            {
                if (Manager.LogWarn) Logging.LogWarning(LogTag, "RegisterAddressablePrefab - prefab is null.");
                return null;
            }
            if (Manager.LogDev) Logging.Log(LogTag, $"RegisterAddressablePrefab [{addressablePrefab.HashAssetId}]");
            LiteNetLibIdentity prefab = await addressablePrefab.GetOrLoadAssetAsync<LiteNetLibIdentity>();
            GuidToPrefabs[addressablePrefab.HashAssetId] = prefab;
            return prefab;
        }

        public LiteNetLibIdentity RegisterAddressablePrefab(AssetReferenceLiteNetLibIdentity addressablePrefab)
        {
            if (!addressablePrefab.IsDataValid())
            {
                if (Manager.LogWarn) Logging.LogWarning(LogTag, "RegisterAddressablePrefab - prefab is null.");
                return null;
            }
            if (Manager.LogDev) Logging.Log(LogTag, $"RegisterAddressablePrefab [{addressablePrefab.HashAssetId}]");
            LiteNetLibIdentity prefab = addressablePrefab.GetOrLoadAsset<LiteNetLibIdentity>();
            GuidToPrefabs[addressablePrefab.HashAssetId] = prefab;
            return prefab;
        }

        public bool UnregisterAddressablePrefab(AssetReferenceLiteNetLibIdentity addressablePrefab)
        {
            if (!addressablePrefab.IsDataValid())
            {
                if (Manager.LogWarn) Logging.LogWarning(LogTag, "UnregisterAddressablePrefab - prefab is null.");
                return false;
            }
            if (Manager.LogDev) Logging.Log(LogTag, $"UnregisterAddressablePrefab [{addressablePrefab.HashAssetId}]");
            return GuidToPrefabs.Remove(addressablePrefab.HashAssetId);
        }

        public void ClearSpawnedObjects()
        {
            List<uint> objectIds = new List<uint>(SpawnedObjects.Keys);
            for (int i = objectIds.Count - 1; i >= 0; --i)
            {
                uint objectId = objectIds[i];
                LiteNetLibIdentity spawnedObject;
                if (SpawnedObjects.TryGetValue(objectId, out spawnedObject) && spawnedObject != null)
                {
                    // Remove from asset spawned objects dictionary
                    SpawnedObjects.Remove(objectId);
                }
            }
            SpawnedObjects.Clear();
        }

        public void ClearPooledObjects()
        {
            foreach (Queue<LiteNetLibIdentity> queue in PooledObjects.Values)
            {
                while (queue.Count > 0)
                {
                    LiteNetLibIdentity instance = queue.Dequeue();
                    try
                    {
                        // I tried to avoid null exception but it still ocurring
                        if (instance != null)
                            Destroy(instance.gameObject);
                    }
                    catch { }
                }
            }
            PooledObjects.Clear();
        }

        public void InitPoolingObjects()
        {
            // No pooling
            if (disablePooling)
                return;

            foreach (int hashAssetId in GuidToPrefabs.Keys)
            {
                InitPoolingObject(hashAssetId);
            }
        }

        public void InitPoolingObject(int hashAssetId)
        {
            // No pooling
            if (disablePooling)
                return;

            if (!GuidToPrefabs.ContainsKey(hashAssetId))
            {
                Debug.LogWarning($"Cannot init prefab: {hashAssetId}, can't find the registered prefab.");
                return;
            }

            // Already init pool for the prefab
            if (PooledObjects.ContainsKey(hashAssetId))
                return;

            LiteNetLibIdentity prefab = GuidToPrefabs[hashAssetId];
            // Don't create an instance for this prefab, if pooling size < 0
            if (prefab.PoolingSize <= 0)
                return;

            Queue<LiteNetLibIdentity> queue = new Queue<LiteNetLibIdentity>();
            LiteNetLibIdentity tempInstance;
            for (int i = 0; i < prefab.PoolingSize; ++i)
            {
                tempInstance = Instantiate(prefab);
                tempInstance.IsPooledInstance = true;
                tempInstance.gameObject.SetActive(false);
                queue.Enqueue(tempInstance);
            }
            PooledObjects[hashAssetId] = queue;
        }

        public LiteNetLibIdentity GetObjectInstance(int hashAssetId)
        {
            return GetObjectInstance(hashAssetId, Vector3.zero, Quaternion.identity);
        }

        public LiteNetLibIdentity GetObjectInstance(int hashAssetId, Vector3 position, Quaternion rotation)
        {
            if (PooledObjects.ContainsKey(hashAssetId) && PooledObjects[hashAssetId].Count > 0)
            {
                // Get pooled instance
                LiteNetLibIdentity instance = PooledObjects[hashAssetId].Dequeue();
                instance.OnGetInstance();
                instance.transform.position = position;
                instance.transform.rotation = rotation;
                return instance;
            }

            if (GuidToPrefabs.ContainsKey(hashAssetId))
            {
                // Create a new instance
                LiteNetLibIdentity instance = Instantiate(GuidToPrefabs[hashAssetId], position, rotation);
                instance.gameObject.SetActive(false);
                return instance;
            }

            return null;
        }

        public void PushInstanceBack(LiteNetLibIdentity instance)
        {
            if (instance == null)
            {
                Debug.LogWarning($"[PoolSystem] Cannot push back ({instance.gameObject}). The instance's is empty.");
                return;
            }
            if (!instance.IsPooledInstance)
            {
                Debug.LogWarning($"[PoolSystem] Cannot push back ({instance.gameObject}). The instance is not pooled instance.");
                return;
            }
            Queue<LiteNetLibIdentity> queue;
            if (!PooledObjects.TryGetValue(instance.HashAssetId, out queue))
            {
                Debug.LogWarning($"[PoolSystem] Cannot push back ({instance.gameObject}). The instance's prefab does not initailized yet.");
                return;
            }
            if (queue.Count >= instance.PoolingSize)
            {
                Destroy(instance.gameObject);
            }
            else
            {
                instance.gameObject.SetActive(false);
                queue.Enqueue(instance);
            }
        }

        public void RegisterSceneObjects()
        {
            SpawningScenePlaceholderCounts.Clear();
            SpawningSceneObjects.Clear();
            for (int i = 0; i < SceneManager.sceneCount; ++i)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded)
                    continue;
                GameObject[] rootObjects = scene.GetRootGameObjects();
                for (int j = 0; j < rootObjects.Length; ++j)
                {
                    LiteNetLibIdentity[] sceneObjects = rootObjects[j].GetComponentsInChildren<LiteNetLibIdentity>(true);
                    for (int k = 0; k < sceneObjects.Length; ++k)
                    {
                        LiteNetLibIdentity sceneObject = sceneObjects[k];
                        if (string.IsNullOrWhiteSpace(sceneObject.AssetId))
                        {
                            string key = sceneObject.name;
                            if (!SpawningScenePlaceholderCounts.ContainsKey(key))
                                SpawningScenePlaceholderCounts.Add(key, 0);
                            ++SpawningScenePlaceholderCounts[key];
                            int count = SpawningScenePlaceholderCounts[key];
                            sceneObject.AssetId = ZString.Concat(key, '_', count);
                            sceneObject.IsPlaceHolder = true;
                            RegisterPrefab(sceneObject);
                        }
                        else if (!GuidToPrefabs.ContainsKey(sceneObject.HashAssetId))
                        {
                            // Not registered as a prefab yet?
                            sceneObject.IsPlaceHolder = true;
                            RegisterPrefab(sceneObject);
                        }
                        sceneObject.ObjectId = 0;
                        sceneObject.IsSceneObject = true;
                        // Hide the place holder, if place holder is prefab, it will be destroyed later
                        sceneObject.gameObject.SetActive(false);
                        SpawningSceneObjects.Add(sceneObject);
                    }
                }
            }
        }

        /// <summary>
        /// This function will be called on start server and when network scene loaded to spawn scene objects
        /// So each scene objects connection id will = -1 (No owning client)
        /// </summary>
        public void PrepareServerSceneObjects()
        {
            for (int i = 0; i < SpawningSceneObjects.Count; ++i)
            {
                LiteNetLibIdentity sceneObject = SpawningSceneObjects[i];
                NetworkSpawn(sceneObject);
            }
            SpawningScenePlaceholderCounts.Clear();
            SpawningSceneObjects.Clear();
        }

        public void PrepareClientSceneObjects()
        {
            for (int i = 0; i < SpawningSceneObjects.Count; ++i)
            {
                LiteNetLibIdentity sceneObject = SpawningSceneObjects[i];
                if (!sceneObject.IsPlaceHolder)
                {
                    // It is a prefab, so it can be destroyed from scene
                    Destroy(sceneObject.gameObject);
                }
            }
            SpawningScenePlaceholderCounts.Clear();
            SpawningSceneObjects.Clear();
        }

        public LiteNetLibIdentity NetworkSpawn(GameObject gameObject, uint objectId = 0, long connectionId = -1)
        {
            if (gameObject == null)
            {
                if (Manager.LogWarn) Logging.LogWarning(LogTag, "NetworkSpawn - gameObject is null.");
                return null;
            }
            return NetworkSpawn(gameObject.GetComponent<LiteNetLibIdentity>(), objectId, connectionId);
        }

        public LiteNetLibIdentity NetworkSpawn(LiteNetLibIdentity identity, uint objectId = 0, long connectionId = -1)
        {
            if (identity == null)
            {
                if (Manager.LogWarn) Logging.LogWarning(LogTag, "NetworkSpawn - identity is null.");
                return null;
            }

            identity.gameObject.SetActive(true);
            identity.Initial(Manager, objectId, connectionId);
            identity.InitTransform(identity.transform.position, identity.transform.rotation);
            identity.OnSetOwnerClient(connectionId >= 0 && connectionId == Manager.ClientConnectionId);
            if (Manager.IsServer)
                identity.OnStartServer();
            if (Manager.IsClient)
                identity.OnStartClient();
            if (connectionId >= 0 && connectionId == Manager.ClientConnectionId)
                identity.OnStartOwnerClient();
            if (onObjectSpawn != null)
                onObjectSpawn.Invoke(identity);

            return identity;
        }

        public LiteNetLibIdentity NetworkSpawn(int hashAssetId, Vector3 position, Quaternion rotation, uint objectId = 0, long connectionId = -1)
        {
            if (!GuidToPrefabs.ContainsKey(hashAssetId))
            {
                if (Manager.LogWarn)
                    Logging.LogWarning(LogTag, $"NetworkSpawn - Asset Id: {hashAssetId} is not registered.");
                return null;
            }
            return NetworkSpawn(GetObjectInstance(hashAssetId, position, rotation), objectId, connectionId);
        }

        public bool NetworkDestroy(GameObject gameObject, byte reasons)
        {
            if (gameObject == null)
            {
                if (Manager.LogWarn) Logging.LogWarning(LogTag, "NetworkDestroy - gameObject is null.");
                return false;
            }
            LiteNetLibIdentity identity = gameObject.GetComponent<LiteNetLibIdentity>();
            if (identity == null)
            {
                if (Manager.LogWarn) Logging.LogWarning(LogTag, "NetworkSpawn - identity is null.");
                return false;
            }
            return NetworkDestroy(identity.ObjectId, reasons);
        }

        public bool NetworkDestroy(uint objectId, byte reasons)
        {
            if (!Manager.IsNetworkActive)
            {
                Logging.LogWarning(LogTag, "NetworkDestroy - Network is not active cannot destroy");
                return false;
            }

            LiteNetLibIdentity spawnedObject;
            if (SpawnedObjects.TryGetValue(objectId, out spawnedObject))
            {
                // Call this function to tell behaviour that the identity is being destroyed
                spawnedObject.OnNetworkDestroy(reasons);
                if (onObjectDestroy != null)
                    onObjectDestroy.Invoke(spawnedObject);
                DestroyObjectInstance(spawnedObject);
                return true;
            }
            else if (Manager.LogWarn)
            {
                Logging.LogWarning(LogTag, $"NetworkDestroy - Object Id: {objectId} is not spawned.");
            }
            return false;
        }

        public void DestroyObjectInstance(LiteNetLibIdentity instance)
        {
            if (instance == null)
                return;

            if (instance.IsPooledInstance)
                PushInstanceBack(instance);
            else
                Destroy(instance.gameObject);
        }

        public bool SetObjectOwner(uint objectId, long connectionId)
        {
            if (!Manager.IsNetworkActive)
            {
                Logging.LogWarning(LogTag, "SetObjectOwner - Network is not active cannot set object owner");
                return false;
            }
            LiteNetLibIdentity spawnedObject;
            if (SpawnedObjects.TryGetValue(objectId, out spawnedObject))
            {
                if (spawnedObject.ConnectionId == connectionId)
                    return false;

                // If this is server, send message to clients to set object owner
                if (Manager.IsServer)
                {
                    foreach (long subscriber in spawnedObject.Subscribers)
                    {
                        if (subscriber == connectionId)
                            continue;
                        Manager.SendServerSetObjectOwner(subscriber, objectId, connectionId);
                    }
                    Manager.SendServerSetObjectOwner(connectionId, objectId, connectionId);
                }
                // Remove from player spawned objects dictionary and add to target connection id
                LiteNetLibPlayer playerA;
                LiteNetLibPlayer playerB;
                if (Manager.TryGetPlayer(spawnedObject.ConnectionId, out playerA))
                    playerA.SpawnedObjects.Remove(objectId);
                if (Manager.TryGetPlayer(connectionId, out playerB))
                    playerB.SpawnedObjects[spawnedObject.ObjectId] = spawnedObject;
                // Set connection id
                spawnedObject.ConnectionId = connectionId;
                // Call set owner client event
                spawnedObject.OnSetOwnerClient(connectionId >= 0 && connectionId == Manager.ClientConnectionId);
                return true;
            }
            else if (Manager.LogWarn)
                Logging.LogWarning(LogTag, $"SetObjectOwner - Object Id: {objectId} is not spawned.");

            return false;
        }

        public Vector3 GetPlayerSpawnPosition()
        {
            if (SpawnPoints.Count == 0)
                return Vector3.zero;
            if (playerSpawnRandomly)
                return SpawnPoints[Random.Range(0, SpawnPoints.Count)].GetRandomPosition();
            else
            {
                if (s_spawnPositionCounter >= SpawnPoints.Count)
                    s_spawnPositionCounter = 0;
                return SpawnPoints[s_spawnPositionCounter++].GetRandomPosition();
            }
        }

        public bool ContainsSpawnedObject(uint objectId)
        {
            return SpawnedObjects.ContainsKey(objectId);
        }

        public bool TryGetSpawnedObject(uint objectId, out LiteNetLibIdentity identity)
        {
            return SpawnedObjects.TryGetValue(objectId, out identity);
        }

        public bool TryGetSpawnedObject<T>(uint objectId, out T result) where T : LiteNetLibBehaviour
        {
            result = null;
            LiteNetLibIdentity identity;
            if (SpawnedObjects.TryGetValue(objectId, out identity))
            {
                result = identity.GetComponent<T>();
                return result != null;
            }
            return false;
        }

        public IEnumerable<LiteNetLibIdentity> GetSpawnedObjects()
        {
            return SpawnedObjects.Values;
        }

        public static void ResetSpawnPositionCounter()
        {
            s_spawnPositionCounter = 0;
        }
    }
}
