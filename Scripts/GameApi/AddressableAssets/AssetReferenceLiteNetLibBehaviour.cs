using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace LiteNetLibManager
{
    [System.Serializable]
    public class AssetReferenceLiteNetLibBehaviour<TBehaviour> : AssetReferenceLiteNetLibIdentity
        where TBehaviour : LiteNetLibBehaviour
    {
        public AssetReferenceLiteNetLibBehaviour(string guid) : base(guid)
        {
        }

#if UNITY_EDITOR
        public AssetReferenceLiteNetLibBehaviour(LiteNetLibBehaviour behaviour) : base(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(behaviour)))
        {
            if (behaviour == null)
            {
                hashAssetId = 0;
                Debug.LogWarning($"[AssetReferenceLiteNetLibBehaviour] Cannot find behaviour, so set `hashAssetId` to `0`");
                return;
            }
            LiteNetLibIdentity identity = behaviour.GetComponentInParent<LiteNetLibIdentity>();
            if (identity == null)
            {
                hashAssetId = 0;
                Debug.LogWarning($"[AssetReferenceLiteNetLibBehaviour] Cannot find identity, so set `hashAssetId` to `0`");
                return;
            }
            hashAssetId = identity.HashAssetId;
            Debug.Log($"[AssetReferenceLiteNetLibBehaviour] Set `hashAssetId` to `{hashAssetId}`, name: {behaviour.name}");
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
                hashAssetId = identity.GetComponent<LiteNetLibIdentity>().HashAssetId;
                Debug.Log($"[AssetReferenceLiteNetLibBehaviour] Set `hashAssetId` to `{hashAssetId}` when set editor asset: `{value.name}`");
                return true;
            }
            else
            {
                hashAssetId = 0;
                Debug.LogWarning($"[AssetReferenceLiteNetLibBehaviour] Cannot find behaviour or not proper object's type, so set `hashAssetId` to `0`");
                return false;
            }
        }
#endif

        public override bool ValidateAsset(Object obj)
        {
            return ValidateAsset<TBehaviour>(obj);
        }

        public override bool ValidateAsset(string path)
        {
            return ValidateAsset<TBehaviour>(path);
        }
    }


    [System.Serializable]
    public class AssetReferenceLiteNetLibBehaviour : AssetReferenceLiteNetLibBehaviour<LiteNetLibBehaviour>
    {
        public AssetReferenceLiteNetLibBehaviour(string guid) : base(guid)
        {
        }

#if UNITY_EDITOR
        public AssetReferenceLiteNetLibBehaviour(LiteNetLibBehaviour behaviour) : base(behaviour)
        {
        }
#endif
    }
}