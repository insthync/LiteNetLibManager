using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace LiteNetLibManager
{
    public class LiteNetLibAssets : MonoBehaviour
    {
        private static int spawnPositionCounter = 0;
        public bool playerSpawnRandomly;
        public LiteNetLibIdentity playerPrefab;
        public LiteNetLibIdentity[] spawnablePrefabs;
        public LiteNetLibIdentity PlayerPrefab { get; protected set; }
        public SceneField offlineScene;
        public SceneField onlineScene;
        public UnityEvent onInitialize;
        public LiteNetLibLoadSceneEvent onLoadSceneStart;
        public LiteNetLibLoadSceneEvent onLoadSceneProgress;
        public LiteNetLibLoadSceneEvent onLoadSceneFinish;
        public LiteNetLibIdentityEvent onObjectSpawn;
        public LiteNetLibIdentityEvent onObjectDestroy;

        internal readonly List<LiteNetLibSpawnPoint> CacheSpawnPoints = new List<LiteNetLibSpawnPoint>();
        internal readonly Dictionary<int, LiteNetLibIdentity> GuidToPrefabs = new Dictionary<int, LiteNetLibIdentity>();
        internal readonly Dictionary<uint, LiteNetLibIdentity> SceneObjects = new Dictionary<uint, LiteNetLibIdentity>();
        internal readonly Dictionary<uint, LiteNetLibIdentity> SpawnedObjects = new Dictionary<uint, LiteNetLibIdentity>();
        internal readonly Dictionary<int, Queue<LiteNetLibIdentity>> PooledObjects = new Dictionary<int, Queue<LiteNetLibIdentity>>();

        public LiteNetLibGameManager Manager { get; private set; }
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

        private void Awake()
        {
            Manager = GetComponent<LiteNetLibGameManager>();
        }

        public void Initialize()
        {
            if (onInitialize != null)
                onInitialize.Invoke();
            RegisterPrefabs();
            RegisterSpawnPoints();
            RegisterSceneObjects();
        }

        public void Clear(bool doNotResetObjectId = false)
        {
            ClearSpawnedObjects();
            ClearPooledObjects();
            CacheSpawnPoints.Clear();
            SceneObjects.Clear();
            ResetSpawnPositionCounter();
            if (!doNotResetObjectId)
                LiteNetLibIdentity.ResetObjectId();
        }

        public void RegisterSpawnPoints()
        {
            CacheSpawnPoints.Clear();
            CacheSpawnPoints.AddRange(FindObjectsOfType<LiteNetLibSpawnPoint>());
        }

        public void RegisterPrefabs()
        {
            for (int i = 0; i < spawnablePrefabs.Length; ++i)
            {
                LiteNetLibIdentity registeringPrefab = spawnablePrefabs[i];
                RegisterPrefab(registeringPrefab);
            }
            if (playerPrefab != null)
            {
                PlayerPrefab = playerPrefab;
                RegisterPrefab(playerPrefab);
            }
        }

        public void RegisterPrefab(LiteNetLibIdentity prefab)
        {
            if (prefab == null)
            {
                if (Manager.LogWarn) Logging.LogWarning(LogTag, "RegisterPrefab - prefab is null.");
                return;
            }
            if (Manager.LogDev) Logging.Log(LogTag, $"RegisterPrefab [{prefab.HashAssetId}] name [{prefab.name}]");
            GuidToPrefabs[prefab.HashAssetId] = prefab;
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

        public void ClearSpawnedObjects()
        {
            List<uint> objectIds = new List<uint>(SpawnedObjects.Keys);
            for (int i = objectIds.Count - 1; i >= 0; --i)
            {
                uint objectId = objectIds[i];
                LiteNetLibIdentity spawnedObject;
                if (SpawnedObjects.TryGetValue(objectId, out spawnedObject))
                {
                    // Destroy only non scene object
                    if (!SceneObjects.ContainsKey(objectId) && spawnedObject != null)
                        Destroy(spawnedObject.gameObject);
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
            foreach (int hashAssetId in GuidToPrefabs.Keys)
            {
                InitPoolingObject(hashAssetId);
            }
        }

        public void InitPoolingObject(int hashAssetId)
        {
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

        public LiteNetLibIdentity GetInstance(int hashAssetId)
        {
            return GetInstance(hashAssetId, Vector3.zero, Quaternion.identity);
        }

        public LiteNetLibIdentity GetInstance(int hashAssetId, Vector3 position, Quaternion rotation)
        {
            LiteNetLibIdentity instance = null;

            if (PooledObjects.ContainsKey(hashAssetId) && PooledObjects.Count > 0)
            {
                // Get pooled instance
                instance = PooledObjects[hashAssetId].Dequeue();
            }

            if (GuidToPrefabs.ContainsKey(hashAssetId))
            {
                // Create a new instance
                instance = Instantiate(GuidToPrefabs[hashAssetId]);
            }

            if (instance != null)
            {
                instance.transform.position = position;
                instance.transform.rotation = rotation;
                instance.gameObject.SetActive(true);
            }

            return instance;
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
            instance.gameObject.SetActive(false);
            queue.Enqueue(instance);
        }

        public void RegisterSceneObjects()
        {
            SceneObjects.Clear();
            LiteNetLibIdentity[] sceneObjects = FindObjectsOfType<LiteNetLibIdentity>();
            for (int i = 0; i < sceneObjects.Length; ++i)
            {
                LiteNetLibIdentity sceneObject = sceneObjects[i];
                if (sceneObject.ObjectId > 0)
                {
                    sceneObject.gameObject.SetActive(false);
                    SceneObjects[sceneObject.ObjectId] = sceneObject;
                    LiteNetLibIdentity.UpdateHighestObjectId(sceneObject.ObjectId);
                }
            }
        }

        /// <summary>
        /// This function will be called on start server and when network scene loaded to spawn scene objects
        /// So each scene objects connection id will = -1 (No owning client)
        /// </summary>
        public void SpawnSceneObjects()
        {
            List<LiteNetLibIdentity> sceneObjects = new List<LiteNetLibIdentity>(SceneObjects.Values);
            for (int i = 0; i < sceneObjects.Count; ++i)
            {
                LiteNetLibIdentity sceneObject = sceneObjects[i];
                NetworkSpawnScene(sceneObject.ObjectId, sceneObject.transform.position, sceneObject.transform.rotation);
            }
        }

        public LiteNetLibIdentity NetworkSpawnScene(uint objectId, Vector3 position, Quaternion rotation, long connectionId = -1)
        {
            if (!Manager.IsNetworkActive)
            {
                Logging.LogWarning(LogTag, "NetworkSpawnScene - Network is not active cannot spawn");
                return null;
            }

            LiteNetLibIdentity identity;
            if (!SceneObjects.TryGetValue(objectId, out identity))
            {
                Logging.LogWarning(LogTag, $"NetworkSpawnScene - Object Id: {objectId} is not registered.");
                return null;
            }

            identity.gameObject.SetActive(true);
            identity.Initial(Manager, true, objectId, connectionId);
            identity.InitTransform(position, rotation);
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

        public LiteNetLibIdentity NetworkSpawn(GameObject gameObject, uint objectId = 0, long connectionId = -1)
        {
            if (gameObject == null)
            {
                if (Manager.LogWarn) Logging.LogWarning(LogTag, "NetworkSpawn - gameObject is null.");
                return null;
            }

            LiteNetLibIdentity identity = gameObject.GetComponent<LiteNetLibIdentity>();
            if (identity == null)
            {
                if (Manager.LogWarn) Logging.LogWarning(LogTag, "NetworkSpawn - identity is null.");
                return null;
            }

            identity.gameObject.SetActive(true);
            identity.Initial(Manager, false, objectId, connectionId);
            identity.InitTransform(gameObject.transform.position, gameObject.transform.rotation);
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
            return NetworkSpawn(GetInstance(hashAssetId, position, rotation).gameObject, objectId, connectionId);
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
                // If the object is scene object, don't destroy just hide it, else destroy
                if (SceneObjects.ContainsKey(objectId))
                {
                    spawnedObject.gameObject.SetActive(false);
                }
                else
                {
                    if (spawnedObject.IsPooledInstance)
                        PushInstanceBack(spawnedObject);
                    else
                        Destroy(spawnedObject.gameObject);
                }
                return true;
            }
            else if (Manager.LogWarn)
            {
                Logging.LogWarning(LogTag, $"NetworkDestroy - Object Id: {objectId} is not spawned.");
            }
            return false;
        }

        public bool SetObjectOwner(uint objectId, long connectionId)
        {
            if (!Manager.IsNetworkActive)
            {
                Logging.LogWarning(LogTag, "NetworkDestroy - Network is not active cannot set object owner");
                return false;
            }
            LiteNetLibIdentity spawnedObject;
            if (SpawnedObjects.TryGetValue(objectId, out spawnedObject))
            {
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
                Logging.LogWarning(LogTag, $"NetworkDestroy - Object Id: {objectId} is not spawned.");

            return false;
        }

        public Vector3 GetPlayerSpawnPosition()
        {
            if (CacheSpawnPoints.Count == 0)
                return Vector3.zero;
            if (playerSpawnRandomly)
                return CacheSpawnPoints[Random.Range(0, CacheSpawnPoints.Count)].GetRandomPosition();
            else
            {
                if (spawnPositionCounter >= CacheSpawnPoints.Count)
                    spawnPositionCounter = 0;
                return CacheSpawnPoints[spawnPositionCounter++].GetRandomPosition();
            }
        }

        public bool ContainsSceneObject(uint objectId)
        {
            return SceneObjects.ContainsKey(objectId);
        }

        public bool TryGetSceneObject(uint objectId, out LiteNetLibIdentity identity)
        {
            return SceneObjects.TryGetValue(objectId, out identity);
        }

        public bool TryGetSceneObject<T>(uint objectId, out T result) where T : LiteNetLibBehaviour
        {
            result = null;
            LiteNetLibIdentity identity;
            if (SceneObjects.TryGetValue(objectId, out identity))
            {
                result = identity.GetComponent<T>();
                return result != null;
            }
            return false;
        }

        public IEnumerable<LiteNetLibIdentity> GetSceneObjects()
        {
            return SceneObjects.Values;
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
            spawnPositionCounter = 0;
        }
    }
}
