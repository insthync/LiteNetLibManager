using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace LiteNetLibManager
{
    public static class AddressableAssetsManager
    {
        private static Dictionary<object, LiteNetLibBehaviour> s_loadedAssets = new Dictionary<object, LiteNetLibBehaviour>();
        private static Dictionary<object, AsyncOperationHandle> s_assetRefs = new Dictionary<object, AsyncOperationHandle>();

        public static async Task<TType> GetOrLoadAssetAsync<TAssetRef, TType>(this TAssetRef assetRef, System.Action<AsyncOperationHandle<TType>> handlerCallback = null)
            where TAssetRef : AssetReferenceLiteNetLibBehaviour<TType>
            where TType : LiteNetLibBehaviour
        {
            if (s_loadedAssets.TryGetValue(assetRef.RuntimeKey, out LiteNetLibBehaviour result))
                return result as TType;
            AsyncOperationHandle<TType> handler = assetRef.LoadAssetAsync();
            handlerCallback?.Invoke(handler);
            await handler.Task;
            TType castedResult = handler.Result;
            s_loadedAssets[assetRef.RuntimeKey] = castedResult;
            s_assetRefs[assetRef.RuntimeKey] = handler;
            return castedResult;
        }

        public static TType GetOrLoadAsset<TAssetRef, TType>(this TAssetRef assetRef, System.Action<AsyncOperationHandle<TType>> handlerCallback = null)
            where TAssetRef : AssetReferenceLiteNetLibBehaviour<TType>
            where TType : LiteNetLibBehaviour
        {
            if (s_loadedAssets.TryGetValue(assetRef.RuntimeKey, out LiteNetLibBehaviour result))
                return result as TType;
            AsyncOperationHandle<TType> handler = assetRef.LoadAssetAsync();
            handlerCallback?.Invoke(handler);
            TType castedResult = handler.WaitForCompletion();
            s_loadedAssets[assetRef.RuntimeKey] = castedResult;
            s_assetRefs[assetRef.RuntimeKey] = handler;
            return castedResult;
        }

        public static void Release<TAssetRef>(this TAssetRef assetRef)
            where TAssetRef : AssetReference
        {
            if (s_assetRefs.TryGetValue(assetRef.RuntimeKey, out AsyncOperationHandle handler))
                Addressables.Release(handler);
            s_assetRefs.Remove(assetRef.RuntimeKey);
            s_loadedAssets.Remove(assetRef.RuntimeKey);
        }
    }
}