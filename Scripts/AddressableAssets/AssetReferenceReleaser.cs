using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace LiteNetLibManager
{
    public class AssetReferenceReleaser : MonoBehaviour
    {
        private AsyncOperationHandle? _handler;

        public void Setup(AsyncOperationHandle handler)
        {
            _handler = handler;
        }

        private void OnDestroy()
        {
            if (!_handler.HasValue)
            {
                // Not setup properly, not sure it instance should be release?
                return;
            }
            Addressables.ReleaseInstance(gameObject);
            Addressables.Release(_handler.Value);
        }
    }
}
