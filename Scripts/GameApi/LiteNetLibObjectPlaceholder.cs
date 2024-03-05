using UnityEngine;

namespace LiteNetLibManager
{
    public class LiteNetLibObjectPlaceholder : MonoBehaviour
    {
        public LiteNetLibIdentity objectPrefab;
        [Tooltip("Set this to `TRUE` to destroy this game object after the prefab is instantiated")]
        public bool destroyThisOnSpawned = true;

        private void Start()
        {
            ProceedInstantiating();
        }

        public void ProceedInstantiating()
        {
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
            if (gameManager.IsServer)
            {
                gameManager.Assets.RegisterPrefab(objectPrefab);
                gameManager.Assets.NetworkSpawn(objectPrefab.HashAssetId, transform.position, transform.rotation);
            }
            gameObject.SetActive(false);
            if (destroyThisOnSpawned)
                Destroy(gameObject);
        }
    }
}
