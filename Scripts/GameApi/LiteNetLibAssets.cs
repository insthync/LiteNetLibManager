using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
        internal readonly Dictionary<string, LiteNetLibIdentity> GuidToPrefabs = new Dictionary<string, LiteNetLibIdentity>();
        internal readonly Dictionary<uint, LiteNetLibIdentity> SceneObjects = new Dictionary<uint, LiteNetLibIdentity>();
        internal readonly Dictionary<uint, LiteNetLibIdentity> SpawnedObjects = new Dictionary<uint, LiteNetLibIdentity>();

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
            GuidToPrefabs[prefab.AssetId] = prefab;
        }

        public bool UnregisterPrefab(LiteNetLibIdentity prefab)
        {
            if (prefab == null)
            {
                if (Manager.LogWarn) Debug.LogWarning("[" + name + "] LiteNetLibAssets::UnregisterPrefab - prefab is null.");
                return false;
            }
            return GuidToPrefabs.Remove(prefab.AssetId);
        }

        public void ClearSpawnedObjects()
        {
            var objectIds = SpawnedObjects.Keys.ToArray();
            for (var i = objectIds.Length - 1; i >= 0; --i)
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
                NetworkSpawnScene(sceneObject.ObjectId, sceneObject.transform.position);
            }
        }

        public LiteNetLibIdentity NetworkSpawnScene(uint objectId, Vector3 position)
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
                sceneObject.Initial(Manager, true, objectId);
                SpawnedObjects[sceneObject.ObjectId] = sceneObject;
                return sceneObject;
            }
            else if (Manager.LogWarn)
                Debug.LogWarning("[" + name + "] LiteNetLibAssets::NetworkSpawnScene - Object Id: " + objectId + " is not registered.");
            return null;
        }

        public LiteNetLibIdentity NetworkSpawn(GameObject gameObject, uint objectId = 0, long connectId = 0)
        {
            return NetworkSpawn(gameObject, Vector3.zero, objectId, connectId);
        }

        public LiteNetLibIdentity NetworkSpawn(GameObject gameObject, Vector3 position, uint objectId = 0, long connectId = 0)
        {
            if (gameObject == null)
            {
                if (Manager.LogWarn) Debug.LogWarning("[" + name + "] LiteNetLibAssets::NetworkSpawn - gameObject is null.");
                return null;
            }
            var identity = gameObject.GetComponent<LiteNetLibIdentity>();
            return NetworkSpawn(identity, position, objectId, connectId);
        }

        public LiteNetLibIdentity NetworkSpawn(LiteNetLibIdentity identity, Vector3 position, uint objectId = 0, long connectId = 0)
        {
            if (identity == null)
            {
                if (Manager.LogWarn) Debug.LogWarning("[" + name + "] LiteNetLibAssets::NetworkSpawn - identity is null.");
                return null;
            }
            return NetworkSpawn(identity.AssetId, position, objectId, connectId);
        }

        public LiteNetLibIdentity NetworkSpawn(string assetId, Vector3 position, uint objectId = 0, long connectId = 0)
        {
            if (!Manager.IsNetworkActive)
            {
                Debug.LogWarning("[" + name + "] LiteNetLibAssets::NetworkSpawn - Network is not active cannot spawn");
                return null;
            }
            
            // If it's scene object use network spawn scene function to spawn it
            if (SceneObjects.ContainsKey(objectId))
                return NetworkSpawnScene(objectId, position);
            
            // Spawned objects cannot spawning again
            if (SpawnedObjects.ContainsKey(objectId))
                return null;
            
            LiteNetLibIdentity spawningObject = null;
            if (GuidToPrefabs.TryGetValue(assetId, out spawningObject))
            {
                var spawnedObject = Instantiate(spawningObject);
                spawnedObject.gameObject.SetActive(true);
                spawnedObject.transform.position = position;
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

        public bool NetworkDestroy(GameObject gameObject)
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
            return NetworkDestroy(identity.ObjectId);
        }

        public bool NetworkDestroy(uint objectId)
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
                // If the object is scene object, don't destroy just hide it, else destroy
                if (SceneObjects.ContainsKey(objectId))
                    spawnedObject.gameObject.SetActive(false);
                else
                    Destroy(spawnedObject.gameObject);
                // If this is server, send message to clients to destroy object
                if (Manager.IsServer)
                    Manager.SendServerDestroyObject(objectId);
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

        public static void ResetSpawnPositionCounter()
        {
            spawnPositionCounter = 0;
        }
    }
}
