using Cysharp.Threading.Tasks;
using Insthync.AddressableAssetTools;
using UnityEngine;
#if !DISABLE_ADDRESSABLES
using UnityEngine.AddressableAssets;
#endif
using UnityEngine.SceneManagement;

namespace LiteNetLibManager
{
    public class LiteNetLibAdditiveSceneLoader : MonoBehaviour
    {
        public SceneField[] scenes = new SceneField[0];
#if !DISABLE_ADDRESSABLES
        public AssetReferenceScene[] addressableScenes = new AssetReferenceScene[0];
#endif
#if UNITY_SERVER || UNITY_EDITOR
        public SceneField[] serverOnlyScenes = new SceneField[0];
#if !DISABLE_ADDRESSABLES
        public AssetReferenceScene[] serverOnlyAddressableScenes = new AssetReferenceScene[0];
#endif
#endif
#if !UNITY_SERVER || UNITY_EDITOR
        public SceneField[] clientOnlyScenes = new SceneField[0];
#if !DISABLE_ADDRESSABLES
        public AssetReferenceScene[] clientOnlyAddressableScenes = new AssetReferenceScene[0];
#endif
#endif
#if UNITY_EDITOR
        public SceneField[] editorOnlyScenes = new SceneField[0];
#if !DISABLE_ADDRESSABLES
        public AssetReferenceScene[] editorOnlyAddressableScenes = new AssetReferenceScene[0];
#endif
#endif

        public int GetTotalLoadCount()
        {
            int total = 0;
            total += scenes.Length;
#if !DISABLE_ADDRESSABLES
            total += addressableScenes.Length;
#endif

#if UNITY_SERVER
            total += serverOnlyScenes.Length;
#if !DISABLE_ADDRESSABLES
            total += serverOnlyAddressableScenes.Length;
#endif
#endif

#if !UNITY_SERVER
            total += clientOnlyScenes.Length;
#if !DISABLE_ADDRESSABLES
            total += clientOnlyAddressableScenes.Length;
#endif
#endif

#if UNITY_EDITOR
            total += editorOnlyScenes.Length;
#if !DISABLE_ADDRESSABLES
            total += editorOnlyAddressableScenes.Length;
#endif
#endif
            return total;
        }

        public async UniTask LoadAll(LiteNetLibGameManager manager, string sceneName, bool isOnline)
        {
            int loadCount = 0;
            loadCount = await LoadAll(manager, sceneName, isOnline,
                scenes,
#if !DISABLE_ADDRESSABLES
                addressableScenes,
#endif
                loadCount);
#if UNITY_SERVER
            loadCount = await LoadAll(manager, sceneName, isOnline,
                serverOnlyScenes,
#if !DISABLE_ADDRESSABLES
                serverOnlyAddressableScenes,
#endif
                loadCount);
#endif
#if !UNITY_SERVER
            loadCount = await LoadAll(manager, sceneName, isOnline,
                clientOnlyScenes,
#if !DISABLE_ADDRESSABLES
                clientOnlyAddressableScenes,
#endif
                loadCount);
#endif
#if UNITY_EDITOR
            loadCount = await LoadAll(manager, sceneName, isOnline,
                editorOnlyScenes,
#if !DISABLE_ADDRESSABLES
                editorOnlyAddressableScenes,
#endif
                loadCount);
#endif
        }

        public async UniTask<int> LoadAll(LiteNetLibGameManager manager, string sceneName, bool isOnline, 
            SceneField[] scenes,
#if !DISABLE_ADDRESSABLES
            AssetReferenceScene[] addressableScenes, 
#endif
            int loadCount)
        {
            // Load scene
            for (int i = 0; i < scenes.Length; ++i)
            {
                string loadingName = $"{sceneName}_{loadCount++}";
                // Load the scene
                await UniTask.Yield();
                manager.Assets.onLoadSceneStart.Invoke(loadingName, true, isOnline, 0f);
                var op = SceneManager.LoadSceneAsync(
                    scenes[i].SceneName,
                    new LoadSceneParameters(LoadSceneMode.Additive));
                while (!op.isDone)
                {
                    await UniTask.Yield();
                    manager.Assets.onLoadSceneProgress.Invoke(loadingName, true, isOnline, op.progress);
                }
                await UniTask.Yield();
                manager.Assets.onLoadSceneFinish.Invoke(loadingName, true, isOnline, 1f);
                manager.LoadedAdditiveScenesCount++;
                manager.Assets.onLoadAdditiveSceneProgress.Invoke(manager.LoadedAdditiveScenesCount, manager.TotalAdditiveScensCount);
            }
#if !DISABLE_ADDRESSABLES
            // Load from addressable
            for (int i = 0; i < addressableScenes.Length; ++i)
            {
                string loadingName = $"{sceneName}_{loadCount++}";
                // Download the scene
                await AddressableAssetDownloadManager.Download(
                    addressableScenes[i].RuntimeKey,
                    manager.Assets.onSceneFileSizeRetrieving.Invoke,
                    manager.Assets.onSceneFileSizeRetrieved.Invoke,
                    manager.Assets.onSceneDepsDownloading.Invoke,
                    manager.Assets.onSceneDepsFileDownloading.Invoke,
                    manager.Assets.onSceneDepsDownloaded.Invoke,
                    null);
                // Load the scene
                manager.Assets.onLoadSceneStart.Invoke(loadingName, true, isOnline, 0f);
                var op = Addressables.LoadSceneAsync(
                    addressableScenes[i].RuntimeKey,
                    new LoadSceneParameters(LoadSceneMode.Additive));
                AddressableAssetsManager.AddAddressableSceneHandle(op);
                while (!op.IsDone)
                {
                    await UniTask.Yield();
                    float percentageComplete = op.GetDownloadStatus().Percent;
                    manager.Assets.onLoadSceneProgress.Invoke(loadingName, true, isOnline, percentageComplete);
                }
                await UniTask.Yield();
                manager.Assets.onLoadSceneFinish.Invoke(loadingName, true, isOnline, 1f);
                manager.LoadedAdditiveScenesCount++;
                manager.Assets.onLoadAdditiveSceneProgress.Invoke(manager.LoadedAdditiveScenesCount, manager.TotalAdditiveScensCount);
            }
#endif
            return loadCount;
        }
    }
}