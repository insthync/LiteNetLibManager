using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class LiteNetLibAssets : MonoBehaviour
{
    public LiteNetLibIdentity[] registeringPrefabs;
    protected readonly Dictionary<string, GameObject> guidToPrefabs = new Dictionary<string, GameObject>();
    protected readonly Dictionary<long, GameObject> spawnedObjects = new Dictionary<long, GameObject>();
    protected long objectIdCounter = 0;

    private LiteNetLibManager manager;
    public LiteNetLibManager Manager
    {
        get
        {
            if (manager == null)
                manager = GetComponent<LiteNetLibManager>();
            return manager;
        }
    }

    public void RegisterPrefabs()
    {
        foreach (var registeringPrefab in registeringPrefabs)
        {
            RegisterPrefab(registeringPrefab);
        }
    }

    public void ClearRegisterPrefabs()
    {
        guidToPrefabs.Clear();
    }

    public void RegisterPrefab(LiteNetLibIdentity prefab)
    {
        guidToPrefabs.Add(prefab.assetId, prefab.gameObject);
    }

    public bool UnregisterPrefab(LiteNetLibIdentity prefab)
    {
        return guidToPrefabs.Remove(prefab.assetId);
    }

    public GameObject NetworkSpawn(GameObject gameObject)
    {
        if (gameObject == null)
        {
            if (Manager.LogWarn) Debug.LogWarning("[" + name + "] LiteNetLibAssets::NetworkSpawn - GameObject is null.");
            return null;
        }
        var obj = gameObject.GetComponent<LiteNetLibIdentity>();
        return NetworkSpawn(obj);
    }

    public GameObject NetworkSpawn(LiteNetLibIdentity obj)
    {
        if (obj == null)
        {
            if (Manager.LogWarn) Debug.LogWarning("[" + name + "] LiteNetLibAssets::NetworkSpawn - LiteNetLibIdentity is null.");
            return null;
        }
        return NetworkSpawn(obj.assetId);
    }

    public GameObject NetworkSpawn(string assetId)
    {
        GameObject spawningObject = null;
        if (guidToPrefabs.TryGetValue(assetId, out spawningObject))
            spawnedObjects.Add(++objectIdCounter, spawningObject);
        else if (Manager.LogWarn)
            Debug.LogWarning("[" + name + "] LiteNetLibAssets::NetworkSpawn - Asset Id: " + assetId + " is not registered.");
        return spawningObject;
    }

    public bool NetworkDestroy(GameObject gameObject)
    {
        if (gameObject == null)
        {
            if (Manager.LogWarn) Debug.LogWarning("[" + name + "] LiteNetLibAssets::NetworkDestroy - GameObject is null.");
            return false;
        }
        var obj = gameObject.GetComponent<LiteNetLibIdentity>();
        return NetworkDestroy(obj);
    }

    public bool NetworkDestroy(LiteNetLibIdentity obj)
    {
        if (obj == null)
        {
            if (Manager.LogWarn) Debug.LogWarning("[" + name + "] LiteNetLibAssets::NetworkDestroy - LiteNetLibIdentity is null.");
            return false;
        }
        return NetworkDestroy(obj.objectId);
    }

    public bool NetworkDestroy(long objectId)
    {
        GameObject spawnedObject;
        if (spawnedObjects.TryGetValue(objectId, out spawnedObject) && spawnedObjects.Remove(objectId))
        {
            Destroy(spawnedObject);
            return true;
        }
        else if (Manager.LogWarn)
            Debug.LogWarning("[" + name + "] LiteNetLibAssets::NetworkDestroy - Object Id: " + objectId + " is not spawned.");
        return false;
    }
}
