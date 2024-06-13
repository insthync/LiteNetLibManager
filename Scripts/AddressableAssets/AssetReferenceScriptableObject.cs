using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace LiteNetLibManager
{
    /// <summary>
    /// Creates an AssetReference that is restricted to having a specific ScriptableObject.
    /// * This is the class that inherits from AssetReference.  It is generic and does not specify which ScriptableObjects it might care about.  A concrete child of this class is required for serialization to work.* At edit-time it validates that the asset set on it is a GameObject with the required ScriptableObject.
    /// * At edit-time it validates that the asset set on it is a GameObject with the required ScriptableObject.
    /// * API matches base class (LoadAssetAsync & InstantiateAsync).
    /// </summary>
    /// <typeparam name="TScriptableObject"> The scriptable object type.</typeparam>
    public class AssetReferenceScriptableObject<TScriptableObject> : AssetReference
        where TScriptableObject : ScriptableObject
    {
        public AssetReferenceScriptableObject(string guid) : base(guid) { }

        public AsyncOperationHandle<TScriptableObject> LoadAssetAsync()
        {
            return Addressables.LoadAssetAsync<TScriptableObject>(this);
        }

        public new AsyncOperationHandle<GameObject> InstantiateAsync(Vector3 position, Quaternion rotation, Transform parent = null)
        {
            Debug.LogError($"InstantiateAsync is not supported for {typeof(TScriptableObject)} references.");
            return default;
        }

        public override bool ValidateAsset(Object obj)
        {
            if (obj is TScriptableObject)
                return true;

            Debug.LogError($"{GetType()} only supports {typeof(TScriptableObject)}.");
            return false;
        }
    }
}
