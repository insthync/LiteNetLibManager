using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class LiteNetLibIdentity : MonoBehaviour
{
    public string assetId;
    public long objectId;

#if UNITY_EDITOR
    protected virtual void OnValidate()
    {
        SetupIDs();
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
        }
        else if (ThisIsASceneObjectWithPrefabParent(out prefab))
        {
            // This is a scene object with prefab link
            AssignAssetID(prefab);
        }
        else
        {
            // This is a pure scene object (Not a prefab)
            assetId = string.Empty;
        }
    }
#endif
}
