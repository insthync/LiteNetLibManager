using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace LiteNetLibManager
{
    [System.Serializable]
    public class AssetReferenceLiteNetLibBehaviour<T> : AssetReferenceComponent<T>
        where T : LiteNetLibBehaviour
    {
        [SerializeField]
        private int hashAssetId;

        public int HashAssetId
        {
            get { return hashAssetId; }
        }

#if UNITY_EDITOR
        public AssetReferenceLiteNetLibBehaviour(LiteNetLibBehaviour behaviour) : base(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(behaviour)))
        {
            if (behaviour != null && behaviour.TryGetComponent(out LiteNetLibIdentity identity))
            {
                hashAssetId = identity.HashAssetId;
                Debug.Log($"[AssetReferenceLiteNetLibBehaviour] Set `hashAssetId` to `{hashAssetId}`, name: {behaviour.name}");
            }
            else
            {
                hashAssetId = 0;
                Debug.LogWarning($"[AssetReferenceLiteNetLibBehaviour] Cannot find behaviour, so set `hashAssetId` to `0`");
            }
        }

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
    }


    [System.Serializable]
    public class AssetReferenceLiteNetLibBehaviour : AssetReferenceLiteNetLibBehaviour<LiteNetLibBehaviour>
    {
        public AssetReferenceLiteNetLibBehaviour(LiteNetLibBehaviour behaviour) : base(behaviour)
        {
        }
    }
}