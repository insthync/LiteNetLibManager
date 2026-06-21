using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace LiteNetLibManager
{
    public class LiteNetLibManagerAssetTools
    {
        [MenuItem("Tools/LiteNetLibManager/Select Prefabs With LiteNetLibIdentity")]
        public static void SelectPrefabsWithLiteNetLibIdentity()
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab");
            List<GameObject> results = new List<GameObject>();
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;
                if (prefab.GetComponentInChildren<LiteNetLibIdentity>(true) != null)
                {
                    results.Add(prefab);
                }
            }
            Selection.objects = results.ToArray();
            Debug.Log($"Found {results.Count} prefabs with {typeof(LiteNetLibIdentity).Name}");
        }

        [MenuItem("Tools/LiteNetLibManager/Write Asset IDs To Console")]
        public static void WriteAssetIdsToConsole()
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;
                LiteNetLibIdentity identity = prefab.GetComponentInChildren<LiteNetLibIdentity>(true);
                if (identity != null)
                {
                    identity.WriteSelectedAssetIDsToConsole();
                }
            }
        }

        [MenuItem("Tools/LiteNetLibManager/Assign Asset IDs")]
        public static void AssignAssetIDs()
        {
            Dictionary<string, GameObject> hashedAssetIDs = new Dictionary<string, GameObject>();
            string[] guids = AssetDatabase.FindAssets("t:Prefab");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;
                LiteNetLibIdentity identity = prefab.GetComponent<LiteNetLibIdentity>();
                if (identity != null)
                {
                    string assetId = identity.AssetId;
                    identity.AssignAssetID();
                    string newAssetId = identity.AssetId;
                    if (!string.Equals(assetId, newAssetId))
                    {
                        Debug.Log($"Assigned Asset ID {newAssetId} (from {assetId}) to prefab {prefab.name} at path {path}", prefab);
                    }
                    if (hashedAssetIDs.ContainsKey(newAssetId))
                    {
                        Debug.LogError($"Key collision is occurs {newAssetId}, please fix it manually", prefab);
                    }
                    else
                    {
                        hashedAssetIDs.Add(newAssetId, prefab);
                    }
                }
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Assigned Asset IDs to prefabs with LiteNetLibIdentity.");
        }

        [MenuItem("Tools/LiteNetLibManager/Assign All Scene Object IDs (If Empty Or Duplicated)")]
        public static void AssignSceneObjectIDs()
        {
            LiteNetLibIdentity.s_AssignSceneObjectIDs();
        }
    }
}
