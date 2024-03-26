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
        private int hashAssetId;

        public int HashAssetId
        {
            get { return hashAssetId; }
        }

#if UNITY_EDITOR
        public AssetReferenceLiteNetLibIdentity(LiteNetLibIdentity identity) : base(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(identity)))
        {
            if (identity != null)
            {
                hashAssetId = identity.HashAssetId;
                Debug.Log($"[AssetReferenceLiteNetLibIdentity] Set `hashAssetId` to `{hashAssetId}`, name: {identity.name}");
            }
            else
            {
                hashAssetId = 0;
                Debug.LogWarning($"[AssetReferenceLiteNetLibIdentity] Cannot find identity, so set `hashAssetId` to `0`");
            }
        }

        public override bool ValidateAsset(string path)
        {
            return ValidateAsset(AssetDatabase.LoadAssetAtPath<LiteNetLibIdentity>(path));
        }

        public override bool ValidateAsset(Object obj)
        {
            return (obj != null) && (obj is LiteNetLibIdentity);
        }

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
#endif
    }
}