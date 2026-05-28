#if !DISABLE_ADDRESSABLES
using Insthync.AddressableAssetTools;
using UnityEngine;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace LiteNetLibManager
{
    [System.Serializable]
    public class AssetReferenceLiteNetLibIdentity : AssetReferenceComponent<LiteNetLibIdentity>
    {
        [SerializeField]
        protected int hashAssetId;

        public int HashAssetId
        {
            get { return hashAssetId; }
            set { hashAssetId = value; }
        }

        public AssetReferenceLiteNetLibIdentity(string guid) : base(guid)
        {
        }

#if UNITY_EDITOR
        public AssetReferenceLiteNetLibIdentity(LiteNetLibIdentity identity) : base(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(identity)))
        {
            if (identity == null)
            {
                Debug.LogWarning($"[AssetReferenceLiteNetLibIdentity] Cannot find identity, so set `hashAssetId` to 0");
                hashAssetId = 0;
                return;
            }
            Debug.Log($"[AssetReferenceLiteNetLibIdentity] Set `hashAssetId` to {hashAssetId}, name: {identity.name}");
            hashAssetId = identity.HashAssetId;
        }

        public override bool SetEditorAsset(Object value)
        {
            if (!base.SetEditorAsset(value))
            {
                return false;
            }

            if (value is GameObject gameObject && gameObject.TryGetComponent(out LiteNetLibIdentity identity))
            {
                ValidateHashAssetID(identity);
                return true;
            }
            else
            {
                Debug.LogWarning($"[AssetReferenceLiteNetLibIdentity] Cannot find identity or not proper object's type, so set `hashAssetId` to 0");
                hashAssetId = 0;
                return false;
            }
        }

        public bool ValidateHashAssetID()
        {
            GameObject editorAsset = this.editorAsset as GameObject;
            return ValidateHashAssetID(editorAsset);
        }

        public virtual bool ValidateHashAssetID(GameObject editorAsset)
        {
            if (!editorAsset)
            {
                if (hashAssetId == 0)
                {
                    return false;
                }
                Debug.Log("[AssetReferenceLiteNetLibIdentity] Hash asset ID validated, hash asset ID changed to 0, prefab: NULL");
                hashAssetId = 0;
                return true;
            }
            return ValidateHashAssetID(editorAsset.GetComponent<LiteNetLibIdentity>());
        }

        public virtual bool ValidateHashAssetID(LiteNetLibIdentity identity)
        {
            int newHashAssetId = 0;
            string identityName = "NULL";
            if (identity)
            {
                newHashAssetId = identity.HashAssetId;
                identityName = identity.name;
            }
            if (hashAssetId == newHashAssetId)
            {
                return false;
            }
            Debug.Log($"[AssetReferenceLiteNetLibIdentity] Hash asset ID validated, hash asset ID changed to {newHashAssetId} (from {hashAssetId}), prefab: {identityName}");
            hashAssetId = newHashAssetId;
            return true;
        }

        public static bool ValidateHashAssetID(AssetReferenceLiteNetLibIdentity addressablePrefab)
        {
            bool hasChanges = false;
            AssetReferenceLiteNetLibIdentity tempRef = addressablePrefab;
            if (tempRef != null && tempRef.ValidateHashAssetID())
            {
                hasChanges |= true;
            }
            return hasChanges;
        }

        public static bool ValidateHashAssetIDs(IList<AssetReferenceLiteNetLibIdentity> addressablePrefabs)
        {
            bool hasChanges = false;
            AssetReferenceLiteNetLibIdentity tempRef;
            for (int i = 0; i < addressablePrefabs.Count; ++i)
            {
                tempRef = addressablePrefabs[i];
                if (tempRef != null && tempRef.ValidateHashAssetID())
                {
                    addressablePrefabs[i] = tempRef;
                    hasChanges |= true;
                }
            }

            return hasChanges;
        }
#endif
    }
}
#endif