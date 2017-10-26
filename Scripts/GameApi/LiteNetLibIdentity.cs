using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using LiteNetLib.Utils;

namespace LiteNetLibHighLevel
{
    [DisallowMultipleComponent]
    public sealed class LiteNetLibIdentity : MonoBehaviour
    {
        public static uint HighestObjectId { get; private set; }
        [ShowOnly, SerializeField]
        private string assetId;
        [ShowOnly, SerializeField]
        private uint objectId;
        [ShowOnly, SerializeField]
        private long connectId;
        [ReadOnly, SerializeField]
        private LiteNetLibGameManager manager;
        private readonly List<LiteNetLibBehaviour> behaviours = new List<LiteNetLibBehaviour>();
#if UNITY_EDITOR
        [Header("Helpers")]
        public bool reorderSceneObjectId;
#endif
        public string AssetId { get { return assetId; } }
        public uint ObjectId { get { return objectId; } }
        public long ConnectId { get { return connectId; } }
        public LiteNetLibGameManager Manager { get { return manager; } }
        public bool IsServer
        {
            get {
                if (Manager == null)
                    Debug.LogError("Manager is empty");
                return Manager != null && Manager.IsServer; }
        }
        public bool IsClient
        {
            get { return Manager != null && Manager.IsClient; }
        }
        public bool IsLocalClient
        {
            get { return Manager != null && ConnectId == Manager.Client.Peer.ConnectId; }
        }

#if UNITY_EDITOR
        private void OnValidate()
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
                ValidateObjectId();
            }
            else
            {
                // This is a pure scene object (Not a prefab)
                assetId = string.Empty;
                ValidateObjectId();
            }
        }
#endif

        public void ProcessSyncField(SyncFieldInfo info, NetDataReader reader)
        {
            if (info.objectId != ObjectId)
                return;
            if (info.behaviourIndex < 0 || info.behaviourIndex >= behaviours.Count)
                return;
            behaviours[info.behaviourIndex].ProcessSyncField(info, reader);
        }

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

        public void Initial(LiteNetLibGameManager manager, uint objectId = 0, long connectId = 0)
        {
            this.objectId = objectId;
            this.connectId = connectId;
            this.manager = manager;
            if (objectId > HighestObjectId)
                HighestObjectId = objectId;
            ValidateObjectId();
            // Setup behaviours index, we will use this as reference for network functions
            behaviours.Clear();
            var behaviourComponents = GetComponents<LiteNetLibBehaviour>();
            foreach (var behaviour in behaviourComponents)
            {
                behaviour.OnValidateNetworkFunctions(behaviours.Count);
                behaviours.Add(behaviour);
            }
        }

        private void ValidateObjectId()
        {
            if (objectId == 0 || IsSceneObjectExists(objectId))
                objectId = GetNewObjectId();
        }

        public static void ResetObjectId()
        {
            HighestObjectId = 0;
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

        private static void ReorderSceneObjectId()
        {
            ResetObjectId();
            LiteNetLibIdentity[] netObjects = FindObjectsOfType<LiteNetLibIdentity>();
            foreach (LiteNetLibIdentity netObject in netObjects)
            {
                netObject.objectId = ++HighestObjectId;
            }
        }
    }
}