#if !EXCLUDE_PREFAB_REFS
using UnityEngine;

namespace LiteNetLibManager
{
    [DisallowMultipleComponent]
    public class LiteNetLibObjectPlaceholder : MonoBehaviour
    {
        [Tooltip("Set unique asset Id if it will use this game object as a prefab")]
        public string uniqueAssetId;
        [Tooltip("Turn it on to use this name as `uniqueAssetId` if `uniqueAssetId` is empty")]
        public bool useNameAsAssetIdIfEmpty = true;
        [Tooltip("Set this to `TRUE` to destroy this game object after the prefab is instantiated")]
        public bool destroyThisOnSpawned = true;

        private LiteNetLibIdentity _objectPrefab;
        private bool _thisIsAPrefab = false;

        private void Start()
        {
            PreparePrefab();
            ProceedInstantiating();
        }

        public void PreparePrefab()
        {
            // It must have LiteNetLibIdentity attached
            LiteNetLibIdentity identity = GetComponent<LiteNetLibIdentity>();
            if (identity == null)
            {
                Logging.LogError($"[LiteNetLibObjectPlaceholder] Cannot set {this} as a prefab, it must has `LiteNetLibIdentity` attached");
                return;
            }
            // Assign proper asset ID, if this object is also a prefab then don't do anything, it will use this object as a prefab automatically
            if (!string.IsNullOrEmpty(identity.AssetId))
                uniqueAssetId = identity.AssetId;
            if (string.IsNullOrEmpty(uniqueAssetId))
            {
                if (useNameAsAssetIdIfEmpty)
                {
                    uniqueAssetId = name;
                }
                else
                {
                    Logging.LogError($"[LiteNetLibObjectPlaceholder] Cannot set {this} as a prefab, its unique asset ID is empty");
                    return;
                }
            }
            // Find for a game manager instance
            LiteNetLibGameManager gameManager = FindFirstObjectByType<LiteNetLibGameManager>();
            if (gameManager == null)
            {
                Logging.LogError($"[LiteNetLibObjectPlaceholder] Cannot set {uniqueAssetId} as a prefab, there is no instance of `LiteNetLibGameManager` in the scene.");
                return;
            }
            _objectPrefab = identity;
            _objectPrefab.AssetId = uniqueAssetId;
            if (gameManager.Assets.GuidToPrefabs.TryGetValue(_objectPrefab.HashAssetId, out LiteNetLibIdentity registerdPrefab))
            {
                _objectPrefab = registerdPrefab;
                return;
            }
            gameManager.Assets.RegisterPrefab(_objectPrefab);
            _thisIsAPrefab = true;
        }

        public void ProceedInstantiating()
        {
            gameObject.SetActive(false);
            if (_objectPrefab == null)
            {
                Logging.LogError("[LiteNetLibObjectPlaceholder] Cannot instantiates, `objectPrefab` is empty.");
                return;
            }
            LiteNetLibGameManager gameManager = FindFirstObjectByType<LiteNetLibGameManager>();
            if (gameManager == null)
            {
                Logging.LogError("[LiteNetLibObjectPlaceholder] Cannot instantiates, there is no instance of `LiteNetLibGameManager` in the scene.");
                return;
            }
            LiteNetLibObjectPlaceholder placedHolderComp = _objectPrefab.GetComponent<LiteNetLibObjectPlaceholder>();
            placedHolderComp.enabled = false;
            gameManager.Assets.RegisterPrefab(_objectPrefab);
            if (gameManager.IsServer)
            {
                gameManager.Assets.NetworkSpawn(_objectPrefab.HashAssetId, transform.position, transform.rotation);
            }
            if (!_thisIsAPrefab && destroyThisOnSpawned)
                Destroy(gameObject);
        }
    }
}
#endif