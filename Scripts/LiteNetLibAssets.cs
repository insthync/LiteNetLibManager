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

    public void RegisterPrefabs()
    {
        foreach (var registeringPrefab in registeringPrefabs)
        {
            RegisterPrefab(registeringPrefab);
        }
    }

    public void RegisterPrefab(LiteNetLibIdentity prefab)
    {
        guidToPrefabs.Add(prefab.assetId, prefab.gameObject);
    }

    public bool UnregisterPrefab(LiteNetLibIdentity prefab)
    {
        return guidToPrefabs.Remove(prefab.assetId);
    }

    public void NetworkSpawn(LiteNetLibIdentity obj)
    {
        spawnedObjects.Add(++objectIdCounter, obj.gameObject);
    }

    public bool NetworkDestroy(LiteNetLibIdentity obj)
    {
        if (obj == null)
            return false;
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
        return false;
    }
}
