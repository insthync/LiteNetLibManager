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
        public LiteNetLibIdentity[] registeringPrefabs;
        public readonly Dictionary<string, LiteNetLibIdentity> GuidToPrefabs = new Dictionary<string, LiteNetLibIdentity>();
        public readonly Dictionary<uint, LiteNetLibIdentity> SceneObjects = new Dictionary<uint, LiteNetLibIdentity>();
        public readonly Dictionary<uint, LiteNetLibIdentity> SpawnedObjects = new Dictionary<uint, LiteNetLibIdentity>();

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

        public void ClearRegisterPrefabs()
        {
            GuidToPrefabs.Clear();
        }

        public void RegisterPrefabs()
        {
            foreach (var registeringPrefab in registeringPrefabs)
            {
                RegisterPrefab(registeringPrefab);
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
            foreach (var objectId in SpawnedObjects.Keys)
            {
                NetworkDestroy(objectId);
            }
        }

        public void RegisterSceneObjects()
        {
            if (SpawnedObjects.Count > 0)
            {
                if (Manager.LogWarn) Debug.LogWarning("[" + name + "] LiteNetLibAssets::RegisterSceneObjects - Cannot register scene objects, they're already spawned or this is called after spawn any objects.");
                return;
            }
            var sceneObjects = FindObjectsOfType<LiteNetLibIdentity>();
            foreach (var sceneObject in sceneObjects)
            {
                if (sceneObject.ObjectId > 0)
                {
                    sceneObject.Initial(Manager);
                    SceneObjects[sceneObject.ObjectId] = sceneObject;
                    SpawnedObjects[sceneObject.ObjectId] = sceneObject;
                }
            }
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
            if (identity == null)
            {
                if (Manager.LogWarn) Debug.LogWarning("[" + name + "] LiteNetLibAssets::NetworkSpawn - identity is null.");
                return null;
            }
            return NetworkSpawn(identity.AssetId, position, objectId, connectId);
        }

        public LiteNetLibIdentity NetworkSpawn(string assetId, Vector3 position, uint objectId = 0, long connectId = 0)
        {
            // Scene objects cannot be spawned
            if (SceneObjects.ContainsKey(objectId))
                return null;
            LiteNetLibIdentity spawningObject = null;
            if (GuidToPrefabs.TryGetValue(assetId, out spawningObject))
            {
                var spawnedObject = Instantiate(spawningObject);
                spawnedObject.transform.position = position;
                spawnedObject.Initial(Manager, objectId, connectId);
                SpawnedObjects[spawnedObject.ObjectId] = spawnedObject;
                if (Manager.IsServer)
                    Manager.SendServerSpawnObject(spawnedObject);
            }
            else if (Manager.LogWarn)
                Debug.LogWarning("[" + name + "] LiteNetLibAssets::NetworkSpawn - Asset Id: " + assetId + " is not registered.");
            return spawningObject;
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
            // Scene objects cannot be destroyed
            if (SceneObjects.ContainsKey(objectId))
                return false;
            LiteNetLibIdentity spawnedObject;
            if (SpawnedObjects.TryGetValue(objectId, out spawnedObject) && SpawnedObjects.Remove(objectId))
            {
                Destroy(spawnedObject.gameObject);
                return true;
            }
            else if (Manager.LogWarn)
                Debug.LogWarning("[" + name + "] LiteNetLibAssets::NetworkDestroy - Object Id: " + objectId + " is not spawned.");
            return false;
        }
    }
}
