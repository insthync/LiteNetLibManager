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
        protected readonly Dictionary<string, LiteNetLibIdentity> guidToPrefabs = new Dictionary<string, LiteNetLibIdentity>();
        protected readonly Dictionary<uint, LiteNetLibIdentity> spawnedObjects = new Dictionary<uint, LiteNetLibIdentity>();

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
            guidToPrefabs.Clear();
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
            guidToPrefabs[prefab.AssetId] = prefab;
        }

        public bool UnregisterPrefab(LiteNetLibIdentity prefab)
        {
            if (prefab == null)
            {
                if (Manager.LogWarn) Debug.LogWarning("[" + name + "] LiteNetLibAssets::UnregisterPrefab - prefab is null.");
                return false;
            }
            return guidToPrefabs.Remove(prefab.AssetId);
        }

        public void ClearSpawnedObjects()
        {
            foreach (var objectId in spawnedObjects.Keys)
            {
                NetworkDestroy(objectId);
            }
        }

        public void RegisterSceneObjects()
        {
            if (spawnedObjects.Count > 0)
            {
                if (Manager.LogWarn) Debug.LogWarning("[" + name + "] LiteNetLibAssets::RegisterSceneObjects - Cannot register scene objects, they're already spawned or this is called after spawn any objects.");
                return;
            }
            var netObjects = FindObjectsOfType<LiteNetLibIdentity>();
            foreach (var netObject in netObjects)
            {
                if (netObject.ObjectId > 0)
                    spawnedObjects[netObject.ObjectId] = netObject;
            }
        }

        public LiteNetLibIdentity NetworkSpawn(GameObject gameObject)
        {
            if (gameObject == null)
            {
                if (Manager.LogWarn) Debug.LogWarning("[" + name + "] LiteNetLibAssets::NetworkSpawn - gameObject is null.");
                return null;
            }
            var obj = gameObject.GetComponent<LiteNetLibIdentity>();
            return NetworkSpawn(obj);
        }

        public LiteNetLibIdentity NetworkSpawn(LiteNetLibIdentity netObject)
        {
            if (netObject == null)
            {
                if (Manager.LogWarn) Debug.LogWarning("[" + name + "] LiteNetLibAssets::NetworkSpawn - netObject is null.");
                return null;
            }
            return NetworkSpawn(netObject.AssetId);
        }

        public LiteNetLibIdentity NetworkSpawn(string assetId)
        {
            LiteNetLibIdentity spawningObject = null;
            if (guidToPrefabs.TryGetValue(assetId, out spawningObject))
            {
                var spawnedObject = Instantiate(spawningObject);
                spawnedObject.InitialObjectId();
                spawnedObjects[spawnedObject.ObjectId] = spawnedObject;
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
            var obj = gameObject.GetComponent<LiteNetLibIdentity>();
            return NetworkDestroy(obj);
        }

        public bool NetworkDestroy(LiteNetLibIdentity netObject)
        {
            if (netObject == null)
            {
                if (Manager.LogWarn) Debug.LogWarning("[" + name + "] LiteNetLibAssets::NetworkDestroy - netObject is null.");
                return false;
            }
            return NetworkDestroy(netObject.ObjectId);
        }

        public bool NetworkDestroy(uint objectId)
        {
            LiteNetLibIdentity spawnedObject;
            if (spawnedObjects.TryGetValue(objectId, out spawnedObject) && spawnedObjects.Remove(objectId))
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
