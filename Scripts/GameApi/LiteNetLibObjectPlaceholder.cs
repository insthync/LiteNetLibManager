using UnityEngine;

namespace LiteNetLibManager
{
    [DisallowMultipleComponent]
    public class LiteNetLibObjectPlaceholder : MonoBehaviour
    {
        public LiteNetLibIdentity objectPrefab;
        [Tooltip("Turn it on to set this game object as a prefab to spawn")]
        public bool setItselfAsPrefab = true;
        [Tooltip("Set unique asset Id if it will use this game object as a prefab")]
        public string uniqueAssetId;
        [Tooltip("Turn it on to use this name as `uniqueAssetId` if `uniqueAssetId` is empty")]
        public bool useNameAsAssetIdIfEmpty = true;
        [Tooltip("Set this to `TRUE` to destroy this game object after the prefab is instantiated")]
        public bool destroyThisOnSpawned = true;

        private void Start()
        {
            PreparePrefab();
            ProceedInstantiating();
        }

        public void PreparePrefab()
        {
            if (!setItselfAsPrefab)
                return;
            // It must have LiteNetLibIdentity attached
            LiteNetLibIdentity identity = GetComponent<LiteNetLibIdentity>();
            if (identity == null)
            {
                Logging.LogError($"[LiteNetLibObjectPlaceholder] Cannot set {this} as a prefab, it must has `LiteNetLibIdentity` attached");
                setItselfAsPrefab = false;
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
                    setItselfAsPrefab = false;
                    return;
                }
            }
            // Find for a game manager instance
            LiteNetLibGameManager gameManager = FindObjectOfType<LiteNetLibGameManager>();
            if (gameManager == null)
            {
                Logging.LogError($"[LiteNetLibObjectPlaceholder] Cannot set {uniqueAssetId} as a prefab, there is no instance of `LiteNetLibGameManager` in the scene.");
                setItselfAsPrefab = false;
                return;
            }
            objectPrefab = identity;
            objectPrefab.AssetId = uniqueAssetId;
            if (gameManager.Assets.GuidToPrefabs.TryGetValue(objectPrefab.HashAssetId, out LiteNetLibIdentity registerdPrefab))
            {
                objectPrefab = registerdPrefab;
                setItselfAsPrefab = false;
                return;
            }
            gameManager.Assets.RegisterPrefab(objectPrefab);
        }

        public void ProceedInstantiating()
        {
            gameObject.SetActive(false);
            if (objectPrefab == null)
            {
                Logging.LogError("[LiteNetLibObjectPlaceholder] Cannot instantiates, `objectPrefab` is empty.");
                return;
            }
            LiteNetLibGameManager gameManager = FindObjectOfType<LiteNetLibGameManager>();
            if (gameManager == null)
            {
                Logging.LogError("[LiteNetLibObjectPlaceholder] Cannot instantiates, there is no instance of `LiteNetLibGameManager` in the scene.");
                return;
            }
            LiteNetLibObjectPlaceholder placedHolderComp = objectPrefab.GetComponent<LiteNetLibObjectPlaceholder>();
            placedHolderComp.enabled = false;
            if (gameManager.IsServer)
            {
                gameManager.Assets.RegisterPrefab(objectPrefab);
                gameManager.Assets.NetworkSpawn(objectPrefab.HashAssetId, transform.position, transform.rotation);
            }
            if (!setItselfAsPrefab && destroyThisOnSpawned)
                Destroy(gameObject);
        }
    }
}
