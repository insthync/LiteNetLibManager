using Insthync.AddressableAssetTools;
using UnityEngine;
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
        }

        public AssetReferenceLiteNetLibIdentity(string guid) : base(guid)
        {
        }

#if UNITY_EDITOR
        public AssetReferenceLiteNetLibIdentity(LiteNetLibIdentity identity) : base(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(identity)))
        {
            if (identity == null)
            {
                hashAssetId = 0;
                Debug.LogWarning($"[AssetReferenceLiteNetLibIdentity] Cannot find identity, so set `hashAssetId` to `0`");
                return;
            }
            hashAssetId = identity.HashAssetId;
            Debug.Log($"[AssetReferenceLiteNetLibBehaviour] Set `hashAssetId` to `{hashAssetId}`, name: {identity.name}");
        }
#endif

#if UNITY_EDITOR
        public override bool SetEditorAsset(Object value)
        {
            if (!base.SetEditorAsset(value))
            {
                return false;
            }

            if ((value is GameObject gameObject) && gameObject.TryGetComponent(out LiteNetLibIdentity identity))
            {
                hashAssetId = identity.HashAssetId;
                Debug.Log($"[AssetReferenceLiteNetLibIdentity] Set `hashAssetId` to `{hashAssetId}` when set editor asset: `{value.name}`");
                return true;
            }
            else
            {
                hashAssetId = 0;
                Debug.LogWarning($"[AssetReferenceLiteNetLibIdentity] Cannot find identity or not proper object's type, so set `hashAssetId` to `0`");
                return false;
            }
        }

        public virtual bool ValidateHashAssetID()
        {
            GameObject editorAsset = this.editorAsset as GameObject;
            if (!editorAsset)
            {
                return false;
            }
            int newHashAssetId;
            if (editorAsset.TryGetComponent(out LiteNetLibIdentity identity))
            {
                newHashAssetId = identity.HashAssetId;
            }
            else
            {
                return false;
            }
            if (hashAssetId == newHashAssetId)
            {
                return false;
            }
            hashAssetId = newHashAssetId;
            Debug.Log($"Hash asset ID validated, hash asset ID changed to {hashAssetId}");
            return true;
        }
#endif
    }
}