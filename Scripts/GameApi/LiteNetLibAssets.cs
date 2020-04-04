using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace LiteNetLibManager
{
    public class LiteNetLibAssets : MonoBehaviour
    {
        private static int spawnPositionCounter = 0;
        public bool playerSpawnRandomly;
        public LiteNetLibIdentity playerPrefab;
        public LiteNetLibIdentity[] spawnablePrefabs;
        public LiteNetLibIdentity PlayerPrefab { get; protected set; }
        public LiteNetLibScene offlineScene;
        public LiteNetLibScene onlineScene;
        public LiteNetLibLoadSceneEvent onLoadSceneStart;
        public LiteNetLibLoadSceneEvent onLoadSceneProgress;
        public LiteNetLibLoadSceneEvent onLoadSceneFinish;

        internal readonly List<LiteNetLibSpawnPoint> CacheSpawnPoints = new List<LiteNetLibSpawnPoint>();
        internal readonly Dictionary<int, LiteNetLibIdentity> GuidToPrefabs = new Dictionary<int, LiteNetLibIdentity>();
        internal readonly Dictionary<uint, LiteNetLibIdentity> SceneObjects = new Dictionary<uint, LiteNetLibIdentity>();
        internal readonly Dictionary<uint, LiteNetLibIdentity> SpawnedObjects = new Dictionary<uint, LiteNetLibIdentity>();
        
        public LiteNetLibGameManager Manager { get; private set; }
        public string LogTag { get { return Manager.LogTag + "::LiteNetLibAssets"; } }

        private void Awake()
        {
            Manager = GetComponent<LiteNetLibGameManager>();
        }

        public void Initialize()
        {
            RegisterPrefabs();
            RegisterSpawnPoints();
            RegisterSceneObjects();
        }

        public void Clear(bool doNotResetObjectId = false)
        {
            ClearSpawnedObjects();
            CacheSpawnPoints.Clear();
            GuidToPrefabs.Clear();
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
            GuidToPrefabs.Clear();
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
            if (Manager.LogDev) Logging.Log(LogTag, "RegisterPrefab [" + prefab.HashAssetId + "] name [" + prefab.name + "]");
            GuidToPrefabs[prefab.HashAssetId] = prefab;
        }

        public bool UnregisterPrefab(LiteNetLibIdentity prefab)
        {
            if (prefab == null)
            {
                if (Manager.LogWarn) Logging.LogWarning(LogTag, "UnregisterPrefab - prefab is null.");
                return false;
            }
            if (Manager.LogDev) Logging.Log(LogTag, "UnregisterPrefab [" + prefab.HashAssetId + "] name [" + prefab.name + "]");
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

            LiteNetLibIdentity sceneObject = null;
            if (SceneObjects.TryGetValue(objectId, out sceneObject))
            {
                sceneObject.gameObject.SetActive(true);
                sceneObject.Initial(Manager, true, objectId, connectionId);
                sceneObject.InitTransform(position, rotation);
                sceneObject.SetOwnerClient(connectionId >= 0 && connectionId == Manager.ClientConnectionId);
                if (Manager.IsServer)
                    sceneObject.OnStartServer();
                if (Manager.IsClient)
                    sceneObject.OnStartClient();
                if (connectionId >= 0 && connectionId == Manager.ClientConnectionId)
                    sceneObject.OnStartOwnerClient();
                SpawnedObjects[sceneObject.ObjectId] = sceneObject;
                return sceneObject;
            }
            else if (Manager.LogWarn)
                Logging.LogWarning(LogTag, "NetworkSpawnScene - Object Id: " + objectId + " is not registered.");
            return null;
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
            identity.SetOwnerClient(connectionId >= 0 && connectionId == Manager.ClientConnectionId);
            if (Manager.IsServer)
                identity.OnStartServer();
            if (Manager.IsClient)
                identity.OnStartClient();
            if (connectionId >= 0 && connectionId == Manager.ClientConnectionId)
                identity.OnStartOwnerClient();
            SpawnedObjects[identity.ObjectId] = identity;

            // Add to player spawned objects dictionary
            LiteNetLibPlayer player;
            if (Manager.TryGetPlayer(connectionId, out player))
                player.SpawnedObjects[identity.ObjectId] = identity;

            return identity;
        }

        public LiteNetLibIdentity NetworkSpawn(int hashAssetId, Vector3 position, Quaternion rotation, uint objectId = 0, long connectionId = -1)
        {
            LiteNetLibIdentity spawningObject = null;
            if (GuidToPrefabs.TryGetValue(hashAssetId, out spawningObject))
                return NetworkSpawn(Instantiate(spawningObject.gameObject, position, rotation), objectId, connectionId);
            // If object with hash asset id not exists
            if (Manager.LogWarn)
                Logging.LogWarning(LogTag, "NetworkSpawn - Asset Id: " + hashAssetId + " is not registered.");
            return null;
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
                // Remove from player spawned objects dictionary
                LiteNetLibPlayer player;
                if (Manager.TryGetPlayer(spawnedObject.ConnectionId, out player))
                    player.SpawnedObjects.Remove(objectId);
                // Remove from asset spawned objects dictionary
                SpawnedObjects.Remove(objectId);
                spawnedObject.OnNetworkDestroy(reasons);
                // If the object is scene object, don't destroy just hide it, else destroy
                if (SceneObjects.ContainsKey(objectId))
                    spawnedObject.gameObject.SetActive(false);
                else
                    Destroy(spawnedObject.gameObject);
                // If this is server, send message to clients to destroy object
                if (Manager.IsServer)
                    Manager.SendServerDestroyObject(objectId, reasons);
                return true;
            }
            else if (Manager.LogWarn)
                Logging.LogWarning(LogTag, "NetworkDestroy - Object Id: " + objectId + " is not spawned.");
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
                // Remove from player spawned objects dictionary and add to target connection id
                LiteNetLibPlayer playerA;
                LiteNetLibPlayer playerB;
                if (Manager.TryGetPlayer(spawnedObject.ConnectionId, out playerA) &&
                    Manager.TryGetPlayer(connectionId, out playerB))
                {
                    playerA.SpawnedObjects.Remove(objectId);
                    playerB.SpawnedObjects[spawnedObject.ObjectId] = spawnedObject;
                }
                // Set connection id
                spawnedObject.ConnectionId = connectionId;
                // Call set owner client event
                spawnedObject.SetOwnerClient(connectionId >= 0 && connectionId == Manager.ClientConnectionId);
                // If this is server, send message to clients to set object owner
                if (Manager.IsServer)
                    Manager.SendServerSetObjectOwner(objectId, connectionId);
                return true;
            }
            else if (Manager.LogWarn)
                Logging.LogWarning(LogTag, "NetworkDestroy - Object Id: " + objectId + " is not spawned.");

            return false;
        }

        public Vector3 GetPlayerSpawnPosition()
        {
            if (CacheSpawnPoints.Count == 0)
                return Vector3.zero;
            if (playerSpawnRandomly)
                return CacheSpawnPoints[Random.Range(0, CacheSpawnPoints.Count)].Position;
            else
            {
                if (spawnPositionCounter >= CacheSpawnPoints.Count)
                    spawnPositionCounter = 0;
                return CacheSpawnPoints[spawnPositionCounter++].Position;
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

        public Dictionary<uint, LiteNetLibIdentity>.ValueCollection GetSceneObjects()
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

        public Dictionary<uint, LiteNetLibIdentity>.ValueCollection GetSpawnedObjects()
        {
            return SpawnedObjects.Values;
        }

        public static void ResetSpawnPositionCounter()
        {
            spawnPositionCounter = 0;
        }
    }
}
