using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace LiteNetLibManager
{
    public class InstantiatedAssetReference<TComponent>
    {
        public AssetReference Reference { get; set; }
        public AsyncOperationHandle<TComponent> Handler { get; set; }
        public TComponent Instance => Handler.Result;

        public void Release()
        {
            // Release the instance
            var component = Handler.Result as Component;
            if (component != null)
            {
                Addressables.ReleaseInstance(component.gameObject);
            }

            // Release the handle
            Addressables.Release(Handler);
        }
    }
}