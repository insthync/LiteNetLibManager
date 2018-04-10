using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace LiteNetLibHighLevel
{
    public class LiteNetLibAssets : MonoBehaviour
    {
        private static int spawnPositionCounter = 0;
        public bool playerSpawnRandomly;
        public Transform[] playerSpawnPositions;
        public LiteNetLibIdentity playerPrefab;
        public LiteNetLibIdentity[] spawnablePrefabs;
        public LiteNetLibIdentity PlayerPrefab { get; protected set; }
        private readonly Dictionary<string, LiteNetLibIdentity> GuidToPrefabs = new Dictionary<string, LiteNetLibIdentity>();
        private readonly Dictionary<uint, LiteNetLibIdentity> SceneObjects = new Dictionary<uint, LiteNetLibIdentity>();
        private readonly Dictionary<uint, LiteNetLibIdentity> SpawnedObjects = new Dictionary<uint, LiteNetLibIdentity>();

        private LiteNetLibGameManager manager;
        public LiteNetLibGameManager Manager
        {
            get
            {
                if (manager == null)
                    manager = GetComponent<LiteNetLibGameManager>();
                return manager;
            }
        }

        public void ClearRegisteredPrefabs()
        {
            GuidToPrefabs.Clear();
        }

        public void RegisterPrefabs()
        {
            for (var i = 0; i < spawnablePrefabs.Length; ++i)
            {
                var registeringPrefab = spawnablePrefabs[i];
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
                if (Manager.LogWarn) Debug.LogWarning("[" + name + "] LiteNetLibAssets::RegisterPrefab - prefab is null.");
                return;
            }
            if (Manager.LogDev) Debug.Log("[" + name + "] LiteNetLibAssets::RegisterPrefab [" + prefab.AssetId + "]");
            GuidToPrefabs[prefab.AssetId] = prefab;
        }

        public bool UnregisterPrefab(LiteNetLibIdentity prefab)
        {
            if (prefab == null)
            {
                if (Manager.LogWarn) Debug.LogWarning("[" + name + "] LiteNetLibAssets::UnregisterPrefab - prefab is null.");
                return false;
            }
            if (Manager.LogDev) Debug.Log("[" + name + "] LiteNetLibAssets::UnregisterPrefab [" + prefab.AssetId + "]");
            return GuidToPrefabs.Remove(prefab.AssetId);
        }

        public void ClearSpawnedObjects()
        {
            var objectIds = new List<uint>(SpawnedObjects.Keys);
            for (var i = objectIds.Count - 1; i >= 0; --i)
            {
                var objectId = objectIds[i];
                LiteNetLibIdentity spawnedObject;
                if (SpawnedObjects.TryGetValue(objectId, out spawnedObject))
                {
                    // Remove from asset spawned objects dictionary
                    SpawnedObjects.Remove(objectId);
                    // If the object is scene object, don't destroy just hide it, else destroy
                    if (SceneObjects.ContainsKey(objectId))
                        spawnedObject.gameObject.SetActive(false);
                    else
                        Destroy(spawnedObject.gameObject);
                }
            }
        }

        public void RegisterSceneObjects()
        {
            var sceneObjects = FindObjectsOfType<LiteNetLibIdentity>();
            for (var i = 0; i < sceneObjects.Length; ++i)
            {
                var sceneObject = sceneObjects[i];
                if (sceneObject.ObjectId > 0)
                {
                    sceneObject.gameObject.SetActive(false);
                    SceneObjects[sceneObject.ObjectId] = sceneObject;
                }
            }
        }

        public void SpawnSceneObjects()
        {
            var sceneObjects = new List<LiteNetLibIdentity>(SceneObjects.Values);
            for (var i = 0; i < sceneObjects.Count; ++i)
            {
                var sceneObject = sceneObjects[i];
                NetworkSpawnScene(sceneObject.ObjectId, sceneObject.transform.position, sceneObject.transform.rotation);
            }
        }

        public LiteNetLibIdentity NetworkSpawnScene(uint objectId, Vector3 position, Quaternion rotation)
        {
            if (!Manager.IsNetworkActive)
            {
                Debug.LogWarning("[" + name + "] LiteNetLibAssets::NetworkSpawnScene - Network is not active cannot spawn");
                return null;
            }

            LiteNetLibIdentity sceneObject = null;
            if (SceneObjects.TryGetValue(objectId, out sceneObject))
            {
                sceneObject.gameObject.SetActive(true);
                sceneObject.transform.position = position;
                sceneObject.transform.rotation = rotation;
                sceneObject.Initial(Manager, true, objectId);
                SpawnedObjects[sceneObject.ObjectId] = sceneObject;
                return sceneObject;
            }
            else if (Manager.LogWarn)
                Debug.LogWarning("[" + name + "] LiteNetLibAssets::NetworkSpawnScene - Object Id: " + objectId + " is not registered.");
            return null;
        }

        public LiteNetLibIdentity NetworkSpawn(GameObject gameObject, Vector3 position, Quaternion rotation, uint objectId = 0, long connectId = 0)
        {
            if (gameObject == null)
            {
                if (Manager.LogWarn) Debug.LogWarning("[" + name + "] LiteNetLibAssets::NetworkSpawn - gameObject is null.");
                return null;
            }
            var identity = gameObject.GetComponent<LiteNetLibIdentity>();
            return NetworkSpawn(identity, position, rotation, objectId, connectId);
        }

        public LiteNetLibIdentity NetworkSpawn(LiteNetLibIdentity identity, Vector3 position, Quaternion rotation, uint objectId = 0, long connectId = 0)
        {
            if (identity == null)
            {
                if (Manager.LogWarn) Debug.LogWarning("[" + name + "] LiteNetLibAssets::NetworkSpawn - identity is null.");
                return null;
            }
            return NetworkSpawn(identity.AssetId, position, rotation, objectId, connectId);
        }

        public LiteNetLibIdentity NetworkSpawn(string assetId, Vector3 position, Quaternion rotation, uint objectId = 0, long connectId = 0)
        {
            if (!Manager.IsNetworkActive)
            {
                Debug.LogWarning("[" + name + "] LiteNetLibAssets::NetworkSpawn - Network is not active cannot spawn");
                return null;
            }
            
            // If it's scene object use network spawn scene function to spawn it
            if (SceneObjects.ContainsKey(objectId))
                return NetworkSpawnScene(objectId, position, rotation);
            
            // Spawned objects cannot spawning again
            if (SpawnedObjects.ContainsKey(objectId))
                return null;
            
            LiteNetLibIdentity spawningObject = null;
            if (GuidToPrefabs.TryGetValue(assetId, out spawningObject))
            {
                var spawnedObject = Instantiate(spawningObject);
                spawnedObject.gameObject.SetActive(true);
                spawnedObject.transform.position = position;
                spawnedObject.transform.rotation = rotation;
                spawnedObject.Initial(Manager, false, objectId, connectId);
                SpawnedObjects[spawnedObject.ObjectId] = spawnedObject;
                // Add to player spawned objects dictionary
                LiteNetLibPlayer player;
                if (Manager.Players.TryGetValue(connectId, out player))
                    player.SpawnedObjects[spawnedObject.ObjectId] = spawnedObject;
                return spawnedObject;
            }
            else if (Manager.LogWarn)
                Debug.LogWarning("[" + name + "] LiteNetLibAssets::NetworkSpawn - Asset Id: " + assetId + " is not registered.");
            return null;
        }

        public bool NetworkDestroy(GameObject gameObject, DestroyObjectReasons reasons)
        {
            if (gameObject == null)
            {
                if (Manager.LogWarn) Debug.LogWarning("[" + name + "] LiteNetLibAssets::NetworkDestroy - gameObject is null.");
                return false;
            }
            var identity = gameObject.GetComponent<LiteNetLibIdentity>();
            if (identity == null)
            {
                if (Manager.LogWarn) Debug.LogWarning("[" + name + "] LiteNetLibAssets::NetworkSpawn - identity is null.");
                return false;
            }
            return NetworkDestroy(identity.ObjectId, reasons);
        }

        public bool NetworkDestroy(uint objectId, DestroyObjectReasons reasons)
        {
            if (!Manager.IsNetworkActive)
            {
                Debug.LogWarning("[" + name + "] LiteNetLibAssets::NetworkDestroy - Network is not active cannot destroy");
                return false;
            }

            LiteNetLibIdentity spawnedObject;
            if (SpawnedObjects.TryGetValue(objectId, out spawnedObject))
            {
                // Remove from player spawned objects dictionary
                LiteNetLibPlayer player;
                if (Manager.Players.TryGetValue(spawnedObject.ConnectId, out player))
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
                Debug.LogWarning("[" + name + "] LiteNetLibAssets::NetworkDestroy - Object Id: " + objectId + " is not spawned.");
            return false;
        }

        public Vector3 GetPlayerSpawnPosition()
        {
            if (playerSpawnPositions == null || playerSpawnPositions.Length == 0)
                return Vector3.zero;
            if (playerSpawnRandomly)
                return playerSpawnPositions[Random.Range(0, playerSpawnPositions.Length)].position;
            else
            {
                if (spawnPositionCounter >= playerSpawnPositions.Length)
                    spawnPositionCounter = 0;
                return playerSpawnPositions[spawnPositionCounter++].position;
            }
        }

        public List<LiteNetLibIdentity> GetSceneObjects()
        {
            return new List<LiteNetLibIdentity>(SceneObjects.Values);
        }

        public List<LiteNetLibIdentity> GetSpawnedObjects()
        {
            return new List<LiteNetLibIdentity>(SpawnedObjects.Values);
        }

        public bool ContainsSceneObject(uint objectId)
        {
            return SceneObjects.ContainsKey(objectId);
        }

        public bool ContainsSpawnedObject(uint objectId)
        {
            return SpawnedObjects.ContainsKey(objectId);
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

        public static void ResetSpawnPositionCounter()
        {
            spawnPositionCounter = 0;
        }
    }
}
