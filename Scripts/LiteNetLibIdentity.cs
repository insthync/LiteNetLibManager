using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif


public class LiteNetLibIdentity : MonoBehaviour
{
    public static uint HighestObjectId { get; private set; }
    [ShowOnly]
    public string assetId;
    [ShowOnly]
    public uint objectId;
#if UNITY_EDITOR
    [Header("Helpers")]
    public bool reorderSceneObjectId;
#endif

#if UNITY_EDITOR
    protected virtual void OnValidate()
    {
        SetupIDs();
        if (reorderSceneObjectId)
        {
            reorderSceneObjectId = false;
            ReorderSceneObjectId();
        }
    }

    private void AssignAssetID(GameObject prefab)
    {
        string path = AssetDatabase.GetAssetPath(prefab);
        assetId = AssetDatabase.AssetPathToGUID(path);
    }

    private bool ThisIsAPrefab()
    {
        PrefabType prefabType = PrefabUtility.GetPrefabType(gameObject);
        if (prefabType == PrefabType.Prefab)
            return true;
        return false;
    }

    private bool ThisIsASceneObjectWithPrefabParent(out GameObject prefab)
    {
        prefab = null;
        PrefabType prefabType = PrefabUtility.GetPrefabType(gameObject);
        if (prefabType == PrefabType.None)
            return false;
        prefab = (GameObject)PrefabUtility.GetPrefabParent(gameObject);
        if (prefab == null)
        {
            Debug.LogError("Failed to find prefab parent for scene object [name:" + gameObject.name + "]");
            return false;
        }
        return true;
    }

    private void SetupIDs()
    {
        GameObject prefab;
        if (ThisIsAPrefab())
        {
            // This is a prefab
            AssignAssetID(gameObject);
            objectId = 0;
        }
        else if (ThisIsASceneObjectWithPrefabParent(out prefab))
        {
            // This is a scene object with prefab link
            AssignAssetID(prefab);
            if (objectId == 0 || IsSceneObjectExists(objectId))
                objectId = GetNewObjectId();
        }
        else
        {
            // This is a pure scene object (Not a prefab)
            assetId = string.Empty;
            if (objectId == 0 || IsSceneObjectExists(objectId))
                objectId = GetNewObjectId();
        }
    }
#endif

    public bool IsSceneObjectExists(uint objectId)
    {
        LiteNetLibIdentity[] netObjects = FindObjectsOfType<LiteNetLibIdentity>();
        foreach (LiteNetLibIdentity netObject in netObjects)
        {
            if (netObject == this)
                continue;
            if (netObject.objectId == objectId)
                return true;
        }
        return false;
    }

    public static void ResetObjectId()
    {
        HighestObjectId = 0;
    }

    public static void ReorderSceneObjectId()
    {
        ResetObjectId();
        LiteNetLibIdentity[] netObjects = FindObjectsOfType<LiteNetLibIdentity>();
        foreach (LiteNetLibIdentity netObject in netObjects)
        {
            netObject.objectId = ++HighestObjectId;
        }
    }

    public static uint GetNewObjectId()
    {
        LiteNetLibIdentity[] netObjects = FindObjectsOfType<LiteNetLibIdentity>();
        if (HighestObjectId == 0)
        {
            uint result = HighestObjectId;
            foreach (LiteNetLibIdentity netObject in netObjects)
            {
                if (netObject.objectId > result)
                    result = netObject.objectId;
            }
            HighestObjectId = result;
        }
        ++HighestObjectId;
        return HighestObjectId;
    }
}
