#if !DISABLE_ADDRESSABLES
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
                Debug.LogWarning($"[AssetReferenceLiteNetLibBehaviour] Cannot find behaviour, so set `hashAssetId` to 0");
                hashAssetId = 0;
                return;
            }
            LiteNetLibIdentity identity = behaviour.GetComponentInParent<LiteNetLibIdentity>();
            if (identity == null)
            {
                Debug.LogWarning($"[AssetReferenceLiteNetLibBehaviour] Cannot find identity, so set `hashAssetId` to 0");
                hashAssetId = 0;
                return;
            }
            Debug.Log($"[AssetReferenceLiteNetLibBehaviour] Set `hashAssetId` to {hashAssetId}, name: {behaviour.name}");
            hashAssetId = identity.HashAssetId;
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
#endif