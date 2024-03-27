using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace LiteNetLibManager
{
    /// <summary>
    /// Creates an AssetReference that is restricted to having a specific Component.
    /// * This is the class that inherits from AssetReference.  It is generic and does not specify which Components it might care about.  A concrete child of this class is required for serialization to work.* At edit-time it validates that the asset set on it is a GameObject with the required Component.
    /// * At edit-time it validates that the asset set on it is a GameObject with the required Component.
    /// * At runtime it can load/instantiate the GameObject, then return the desired component.  API matches base class (LoadAssetAsync & InstantiateAsync).
    /// </summary>
    /// <typeparam name="TComponent"> The component type.</typeparam>
    public class AssetReferenceComponent<TComponent> : AssetReference
        where TComponent : Component
    {
        public AssetReferenceComponent(string guid) : base(guid)
        {
        }

        public new AsyncOperationHandle<TComponent> InstantiateAsync(Vector3 position, Quaternion rotation, Transform parent = null)
        {
            return Addressables.ResourceManager.CreateChainOperation(Addressables.InstantiateAsync(RuntimeKey, position, rotation, parent, false), GameObjectReady);
        }

        public new AsyncOperationHandle<TComponent> InstantiateAsync(Transform parent = null, bool instantiateInWorldSpace = false)
        {
            return Addressables.ResourceManager.CreateChainOperation(Addressables.InstantiateAsync(RuntimeKey, parent, instantiateInWorldSpace, false), GameObjectReady);
        }

        public AsyncOperationHandle<TComponent> LoadAssetAsync()
        {
            return Addressables.ResourceManager.CreateChainOperation(base.LoadAssetAsync<GameObject>(), GameObjectReady);
        }

        static AsyncOperationHandle<TComponent> GameObjectReady(AsyncOperationHandle<GameObject> arg)
        {
            var comp = arg.Result.GetComponent<TComponent>();
            return Addressables.ResourceManager.CreateCompletedOperation(comp, string.Empty);
        }

        public override bool ValidateAsset(Object obj)
        {
            return ValidateAsset<TComponent>(obj);
        }

        public override bool ValidateAsset(string path)
        {
            return ValidateAsset<TComponent>(path);
        }

        protected bool ValidateAsset<T>(Object obj)
            where T : Component
        {
            GameObject go = obj as GameObject;
            return go != null && go.GetComponent<T>() != null;
        }

        protected bool ValidateAsset<T>(string path)
            where T : Component
        {
#if UNITY_EDITOR
            return ValidateAsset<T>(AssetDatabase.LoadAssetAtPath<GameObject>(path));
#else
            return false;
#endif
        }

        public static void ReleaseInstance(AsyncOperationHandle<TComponent> op)
        {
            // Release the instance
            var component = op.Result as Component;
            if (component != null)
            {
                Addressables.ReleaseInstance(component.gameObject);
            }

            // Release the handle
            Addressables.Release(op);
        }
    }
}